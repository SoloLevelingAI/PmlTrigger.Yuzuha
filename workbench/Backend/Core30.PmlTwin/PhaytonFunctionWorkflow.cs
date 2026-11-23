using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    /// <summary>Uses independent Phayton registration, authorization, preview, and execution operations.</summary>
    private static CoreData RunPhaytonFunctions(FeatureContext context, CoreData request, IPhaytonFunctionClient client) =>
        new PhaytonFunctions(context, client).Run(request.Children().Single());

    private sealed partial class PhaytonFunctions(FeatureContext context, IPhaytonFunctionClient client)
    {
        private const string ChangedDigestMessage = "函数源码或登记摘要已变更。";
        private const string ListOperation = "pml.functions.list";
        private const string StatusOperation = "aveva.session.status";
        private const string ExecuteFlag = "--allow-execution";
        private const string WriteFlag = "--allow-write";
        private const string GenerateOperation = "pml.call.generate";
        private readonly List<PhaytonReceipt> receipts = [];
        private string action = "";

        internal CoreData Run(CoreData request)
        {
            action = request.Name.LocalName;
            if (action == "sessions")
                return ReadSessions();
            using var gate = client.LockCatalog();
            return action switch
            {
                "functions" => List(),
                "register" => Register(request),
                "permission" => SetPermission(request),
                "preview" or "invoke" => RunCall(request),
                _ => throw new InvalidDataException("未知的 Phayton 函数操作。")
            };
        }

        private CoreData List()
        {
            var reply = Send(ListOperation, new { });
            return reply.Success ? Result("ok", reply.Data.Items().Select(ReadEntry).Select(DescribeFunction)) : Failure(reply);
        }

        private CoreData Register(CoreData request)
        {
            var name = Value(request, "name");
            var source = RequirePmlSource(LocalSource.Resolve(context, Value(request, "sourceFile")));
            using var held = OpenSource(source);
            var reply = Send("pml.function.register", new { sourceFile = source, functionName = name }, permission: WriteFlag);
            return reply.Success ? Result("ok", DescribeFunction(ReadEntry(reply.Data))) : Failure(reply);
        }

        private CoreData SetPermission(CoreData request)
        {
            var entry = FindEntry(Value(request, "name"));
            RequireMatchingSha256(entry.Sha256, Value(request, "sha256"), ChangedDigestMessage);
            var allow = (bool)request.Field("allow")!;
            using var source = allow ? OpenRevision(entry) : null;
            var review = (bool?)request.Field("reviewedReadOnly");
            var reply = review is { } reviewed
                ? Send("pml.function.permission", new { functionName = entry.Name, allowInvoke = allow, reviewedReadOnly = reviewed,
                    expectedSha256 = entry.Sha256 }, permission: WriteFlag)
                : Send("pml.function.permission", new { functionName = entry.Name, allowInvoke = allow }, permission: WriteFlag);
            return reply.Success ? Result("ok", DescribeFunction(ReadEntry(reply.Data))) : Failure(reply);
        }

        private CoreData ReadSessions()
        {
            var list = Send("aveva.sessions.list", new { });
            if (!list.Success)
                return Failure(list);
            var rows = list.Data.Field("Sessions").Items().Where(row => row.Boolean("PipeDetected"))
                .Where(row => row.Integer("ProcessId") is > 0 and <= int.MaxValue && row.Integer("ProcessStartTimeUtcTicks") > 0)
                .ToArray();
            return Result("ok", rows.Select(InspectSession).ToArray());
        }

        private CoreData InspectSession(JsonElement row)
        {
            var target = new PhaytonTarget((int)row.Integer("ProcessId"), row.Integer("ProcessStartTimeUtcTicks"), "");
            var status = Send(StatusOperation, new { }, target, timeout: 4);
            var verified = status.Success && target.Matches(status.Data);
            var host = status.Data.Field("Host");
            return new CoreData("session", new CoreField("pid", target.Pid), new CoreField("startTicks", target.StartTicks),
                new CoreField("model", verified ? host.String("Model") : ""), new CoreField("verified", verified),
                new CoreField("product", row.String("Product")), new CoreField("project", row.String("Project")),
                new CoreField("title", row.String("WindowTitle")), new CoreField("hostVersion", host.String("HostVersion")));
        }

        private CoreData RunCall(CoreData request)
        {
            var entry = FindEntry(Value(request, "name"));
            if (!entry.AllowInvoke)
                throw new InvalidDataException("先为当前函数源码授权，再预览调用。");
            using var source = OpenRevision(entry);
            var target = new PhaytonTarget((int)request.Field("pid")!, (long)request.Field("startTicks")!,
                Value(request, "model").Trim().ToUpperInvariant());
            var parameters = ReadArguments(request, entry.Types);
            var mode = ReadMode(entry.ReturnType);
            var status = Send(StatusOperation, new { }, target);
            if (!status.Success || !target.Matches(status.Data))
                return Failure(status, "所选 AVEVA Host 的进程身份、模块或协议与请求不符。");
            var generated = Send(GenerateOperation, new { methodName = entry.Name, parameters });
            if (!generated.Success || generated.Data.ValueKind != JsonValueKind.String)
                return Failure(generated, "Phayton 未能生成调用文本。");
            var host = status.Data.Field("Host");
            var readOnly = PmlChannelMode.Read() == PmlChannelMode.ReadOnly || host.String("PmlChannelMode") == PmlChannelMode.ReadOnly;
            if (readOnly && (!entry.ReviewedReadOnly || host.Integer("PmlChannelModeVersion") < 2 || !host.Boolean("ReviewedFunctionLibraryReady")))
                return Failure(status, "只读调用需要确认源码、更新 Host，并在 EVARS 中注册只读函数目录后重开 AVEVA。");
            var plan = CreatePlan(entry, target, parameters, generated.Data.GetString()!, mode, readOnly);
            if (action == "preview")
                return Result("ok", StorePreview(plan).Describe());
            return ExecuteWithActivity(ConsumePreview(plan, Value(request, "plan")));
        }

        private string PreviewPath => context.FilePath(PhaytonFunctionClient.StatePath + "/pending-preview.txt");

        private CallPlan StorePreview(CallPlan plan)
        {
            var nonce = Guid.NewGuid().ToString("N");
            var token = Sha256(Encoding.ASCII.GetBytes(nonce + plan.Token));
            var temporary = context.FilePath($"{PhaytonFunctionClient.StatePath}/preview-{nonce}.tmp");
            try
            {
                File.WriteAllText(temporary, token + plan.Token, Encoding.ASCII);
                File.Move(temporary, PreviewPath, overwrite: true);
            }
            finally { File.Delete(temporary); }
            return plan with { Token = token };
        }

        private CallPlan ConsumePreview(CallPlan plan, string token)
        {
            if (!File.Exists(PreviewPath))
                throw new InvalidDataException("当前没有待执行预览。请重新预览调用。");
            var expected = Encoding.ASCII.GetString(ReadBounded(PreviewPath, 128));
            if (!string.Equals(token + plan.Token, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("预览与当前调用不符。请重新预览调用。");
            File.Delete(PreviewPath);
            return plan with { Token = token };
        }

        private FunctionEntry FindEntry(string name)
        {
            var list = Send(ListOperation, new { });
            if (!list.Success)
                throw new InvalidDataException("无法读取 Phayton 函数登记。" + list.Message);
            return list.Data.Items().Select(ReadEntry).SingleOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("当前工作区未登记此 Phayton 函数。");
        }

        private static FunctionEntry ReadEntry(JsonElement row) => new(row.String("Name"), row.String("SourceFile"),
            row.String("SourceSha256"), row.String("ReturnType"), row.Boolean("AllowInvoke"),
            row.Field("ParameterTypes").Items().Select(type => type.GetString() ?? "").ToArray(),
            row.String("ReviewedReadOnlySha256") is { Length: > 0 } digest && digest == row.String("SourceSha256"));

        private CoreData DescribeFunction(FunctionEntry entry)
        {
            var declaration = ReadMatchingDeclaration(entry);
            var parameters = declaration?.Children("parameter").ToArray() ?? entry.Types.Select((type, index) =>
                new CoreData("parameter", new CoreField("type", type), new CoreField("name", $"arg{index + 1}"))).ToArray();
            return new CoreData("function", new CoreField("name", entry.Name), new CoreField("source", entry.Source),
                new CoreField("sha256", entry.Sha256), new CoreField("returnType", entry.ReturnType),
                new CoreField("allowInvoke", entry.AllowInvoke), new CoreField("sourceCurrent", declaration is not null),
                new CoreField("reviewedReadOnly", entry.ReviewedReadOnly), parameters);
        }

        private CoreData? ReadMatchingDeclaration(FunctionEntry entry)
        {
            try
            {
                var source = LocalSource.Resolve(context, entry.Source);
                var bytes = ReadBounded(source, FunctionFileLimit);
                if (!Sha256(bytes).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    return null;
                var declaration = ParseRegistration(bytes, source, entry.Name);
                var types = declaration.Children("parameter").Select(parameter => Value(parameter, "type"));
                return types.SequenceEqual(entry.Types, StringComparer.Ordinal) ? declaration : null;
            }
            catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return null; }
        }

        private FileStream OpenRevision(FunctionEntry entry)
        {
            var stream = OpenSource(RequirePmlSource(LocalSource.Resolve(context, entry.Source)));
            try
            {
                var bytes = new byte[(int)stream.Length];
                stream.ReadExactly(bytes);
                RequireMatchingSha256(Sha256(bytes), entry.Sha256, ChangedDigestMessage);
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        private static FileStream OpenSource(string source)
        {
            var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= FunctionFileLimit)
                return stream;
            stream.Dispose();
            throw new InvalidDataException("PML 函数源码超过 1 MiB。");
        }

        private static string ReadMode(string returnType) => returnType switch
        {
            "" or "UNDEFINED" or "VOID" => "dispatch",
            "ARRAY" => "array",
            "STRING" or "REAL" or "BOOLEAN" => "scalar",
            _ => throw new InvalidDataException("当前可读取 STRING、REAL、BOOLEAN 或 ARRAY 返回值。")
        };

        private static CallArgument[] ReadArguments(CoreData request, string[] signature)
        {
            var fields = request.Children().ToArray();
            if (!fields.Select(field => Value(field, "type")).SequenceEqual(signature, StringComparer.Ordinal))
                throw new InvalidDataException("参数数量或类型不符合登记签名。");
            return fields.Select(field => ConvertArgument(Value(field, "type"), field.Value)).ToArray();
        }

        private static CallArgument ConvertArgument(string type, string text) => type switch
        {
            "STRING" when !text.Contains('\r') && !text.Contains('\n') => new("string", text),
            "BOOLEAN" when bool.TryParse(text, out var boolean) => new("boolean", boolean),
            "REAL" when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number) => new("real", number),
            _ => throw new InvalidDataException($"参数值不符合 {type} 格式。")
        };

        private static CallPlan CreatePlan(FunctionEntry entry, PhaytonTarget target, CallArgument[] parameters, string command, string mode, bool readOnly)
        {
            var input = new CallInput(entry.Name, parameters);
            var hash = Sha256(JsonSerializer.SerializeToUtf8Bytes(new { entry, target, input, command, mode, readOnly }));
            return new(entry, target, input, command, mode, readOnly, hash);
        }

        private PhaytonReceipt Send(string operation, object input, PhaytonTarget? target = null,
            string? permission = null, int timeout = 12)
        {
            var receipt = client.Run(operation, input, target, permission, timeout);
            receipts.Add(receipt);
            return receipt;
        }

        private CoreData Result(string state, params object?[] content) => new("result", new CoreField("action", action),
            new CoreField("state", state), new CoreField("registry", PhaytonFunctionClient.StatePath + "/functions.json"),
            content, receipts.Select(receipt => receipt.Describe()));

        private CoreData Failure(PhaytonReceipt receipt, string? message = null) => Result("failed",
            new CoreData("message", message ?? (receipt.Message.Length == 0 ? "Phayton 操作失败。" : receipt.Message)));

        private sealed record FunctionEntry(string Name, string Source, string Sha256, string ReturnType, bool AllowInvoke, string[] Types, bool ReviewedReadOnly);
        private sealed record CallArgument(string type, object value);
        private sealed record CallInput(string functionName, CallArgument[] parameters);
        private sealed record CallPlan(FunctionEntry Entry, PhaytonTarget Target, CallInput Input, string Command, string Mode, bool ReadOnly, string Token)
        {
            internal CoreData Describe() => new("plan", new CoreField("token", Token), new CoreField("mode", Mode),
                new CoreField("name", Entry.Name), new CoreField("sha256", Entry.Sha256), new CoreField("returnType", Entry.ReturnType),
                new CoreField("pid", Target.Pid), new CoreField("startTicks", Target.StartTicks), new CoreField("model", Target.Model),
                new CoreData("command", Command), new CoreData("json", JsonSerializer.Serialize(Input, new JsonSerializerOptions { WriteIndented = true })));
        }
    }
}
