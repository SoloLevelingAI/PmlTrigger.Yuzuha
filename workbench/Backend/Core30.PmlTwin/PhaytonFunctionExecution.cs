using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private sealed partial class PhaytonFunctions
    {
        private const string CommandOperation = "pml.command.execute";
        private const string ArrayOperation = "pml.array.read";

        private CoreData ExecuteWithActivity(CallPlan plan)
        {
            var runId = Guid.NewGuid().ToString("N");
            var log = new JsonLinesRunLogProvider().Open(context, runId);
            using var process = Process.GetCurrentProcess();
            var detail = $"{plan.Entry.Name} · AVEVA PID {plan.Target.Pid} · {plan.Target.Model}";
            var entry = new RunEvent(DateTimeOffset.UtcNow, runId, "P03", "started", detail)
            {
                Category = "runtime", Operation = "phayton-invoke", ProcessId = process.Id,
                StartTicks = process.StartTime.ToUniversalTime().Ticks, EntryPoint = "Phayton"
            };
            RecordActivity(log, entry);
            try
            {
                var result = Execute(plan);
                var state = Value(result, "state");
                var phase = state is "ok" or "partial" ? "completed" : "failed";
                if (RecordActivity(log, entry with { At = DateTimeOffset.UtcNow, Phase = phase, Detail = $"{detail} · {state}" }))
                    result.SetField("activityLog", log.RelativePath);
                return result;
            }
            catch (Exception error) when (CoreRunner.IsOperationalError(error))
            {
                RecordActivity(log, entry with { At = DateTimeOffset.UtcNow, Phase = "failed", Detail = $"{detail} · {error.GetType().Name}" });
                throw;
            }
        }

        private static bool RecordActivity(IRunLog log, RunEvent entry)
        {
            try { log.Record(entry); return true; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
        }

        private CoreData Execute(CallPlan plan) => plan.ReadOnly ? ExecuteReviewed(plan) : plan.Mode switch
        {
            "scalar" => ExecuteScalar(plan),
            "array" => ExecuteArray(plan),
            _ => Dispatch(plan)
        };

        private CoreData ExecuteReviewed(CallPlan plan)
        {
            var reply = Send("pml.function.invoke", new { plan.Input.functionName, plan.Input.parameters, requireReviewed = true },
                plan.Target, ExecuteFlag);
            if (!reply.HostSucceeded) return Failure(reply);
            var data = reply.Data;
            if (plan.Mode == "array")
            {
                var window = data.Field("ArrayWindow");
                var rows = window.Field("Rows");
                var truncated = window.Field("Truncated");
                if (rows.ValueKind != JsonValueKind.Array || truncated.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return Failure(reply, "Host 未返回有效的只读 ARRAY 窗口。");
                var value = new CoreData("value", new CoreField("type", "ARRAY"),
                    new CoreField("count", rows.GetArrayLength()), rows.GetRawText());
                return Result(truncated.GetBoolean() ? "partial" : "ok", plan.Describe(), value);
            }
            return plan.Mode == "scalar"
                ? Result("ok", plan.Describe(), new CoreData("value", new CoreField("type", plan.Entry.ReturnType), data.String("FunctionResult")))
                : Result("ok", plan.Describe());
        }

        private CoreData Dispatch(CallPlan plan)
        {
            var reply = Send("pml.function.invoke", plan.Input, plan.Target, ExecuteFlag);
            return reply.HostSucceeded ? Result("ok", plan.Describe()) : Failure(reply);
        }

        private CoreData ExecuteArray(CallPlan plan)
        {
            var reply = ReadArray(plan, plan.Command);
            if (!reply.HostSucceeded)
                return Failure(reply);
            var data = reply.Data;
            var unparsed = data.Integer("UnparsedCount");
            var items = data.Field("Items");
            if (items.ValueKind != JsonValueKind.Array)
                return Failure(reply, "Host 未返回可读取的 ARRAY 项。");
            var value = new CoreData("value", new CoreField("type", "ARRAY"), new CoreField("count", data.Integer("Count")),
                new CoreField("unparsed", unparsed), items.GetRawText());
            if (unparsed == 0)
                return Result("ok", plan.Describe(), value);

            var message = FirstAttributeFailure(data.Field("Unparsed")) ?? $"有 {unparsed} 行返回值无法解析，原文见 Host 回执。";
            return Result("partial", plan.Describe(), value, new CoreData("message", message));
        }

        private static string? FirstAttributeFailure(JsonElement rows)
        {
            const string displayPrefix = "<STRING> '";
            const string failurePrefix = "failedToGetValue ";
            foreach (var row in rows.Items())
            {
                if (row.ValueKind != JsonValueKind.String || row.GetString() is not { } text)
                    continue;
                if (text.StartsWith(displayPrefix, StringComparison.Ordinal) && text.EndsWith('\''))
                    text = text[displayPrefix.Length..^1];
                if (text.StartsWith(failurePrefix + "|", StringComparison.Ordinal))
                    return "属性读取失败 " + text[failurePrefix.Length..];
            }
            return null;
        }

        /// <summary>Evaluates a scalar once, reads the owned result, then deletes the session variable.</summary>
        private CoreData ExecuteScalar(CallPlan plan)
        {
            var temporary = "Core30PmlResult" + Guid.NewGuid().ToString("N");
            var assign = Send(CommandOperation, new { pmlCommand = $"!!{temporary} = {plan.Command}" }, plan.Target, ExecuteFlag);
            if (!assign.HostSucceeded)
                return FailedAssignment(plan, temporary, assign);

            PhaytonReceipt read;
            PhaytonReceipt cleanup;
            try { read = ReadArray(plan, $"!!YuzuhaReadGlobal(1,0,'','{temporary}')"); }
            finally { cleanup = DeleteTemporary(plan, temporary); }

            if (!read.HostSucceeded)
                return Failure(read);
            var items = read.Data.Field("Items").Items().ToArray();
            if (items.Length != 1 || read.Data.Integer("UnparsedCount") != 0 || items[0].String("Type") != plan.Entry.ReturnType)
                return Failure(read, "Host 未返回与登记签名一致的单个标量值。");
            var value = new CoreData("value", new CoreField("type", plan.Entry.ReturnType), items[0].String("Value"));
            return cleanup.HostSucceeded ? Result("ok", plan.Describe(), value)
                : Result("cleanup-failed", plan.Describe(), value, new CoreData("message", $"临时变量清理未确认：{temporary}"));
        }

        private CoreData FailedAssignment(CallPlan plan, string temporary, PhaytonReceipt assign)
        {
            if (!assign.MayHaveExecuted)
                return Failure(assign);
            var cleanup = DeleteTemporary(plan, temporary);
            return Failure(assign, cleanup.HostSucceeded ? "调用状态未知，已请求清理临时变量。请勿自动重试。"
                : $"调用状态与临时变量 {temporary} 的清理均未确认。请勿自动重试。");
        }

        private PhaytonReceipt ReadArray(CallPlan plan, string expression) => Send(ArrayOperation, new
        {
            pmlCommand = expression,
            globalVar = "Core30PmlRead" + Guid.NewGuid().ToString("N"),
            deleteGlobalVar = true,
            includeEmpty = true
        }, plan.Target, ExecuteFlag);

        private PhaytonReceipt DeleteTemporary(CallPlan plan, string name) =>
            Send(CommandOperation, new { pmlCommand = $"!!{name}.delete()" }, plan.Target, ExecuteFlag);
    }
}
