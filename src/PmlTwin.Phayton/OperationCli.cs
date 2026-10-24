using System.Globalization;
using System.Text.Json;

namespace PmlTwin.Phayton;

/// <summary>将 op list、describe、invoke 命令映射到共享操作执行器。</summary>
public static class OperationCli
{
    /// <summary>解析明确的 PID、模块与执行授权，输出 JSON 结果及稳定退出码。</summary>
    public static async Task<int> RunAsync(string[] args, OperationExecutor executor,
        TextWriter output, CancellationToken cancellationToken = default, TextReader? inputReader = null)
    {
        OperationResult result;
        try
        {
            if (args.Length == 2 && args[0] == "op" && args[1] == "list")
                result = OperationResult.Ok("op.list", JsonSerializer.SerializeToElement(
                    executor.Registry.Descriptors.ToArray(), OperationJsonContext.Default.OperationDescriptorArray));
            else if (args.Length == 3 && args[0] == "op" && args[1] == "describe")
                result = executor.Registry.TryGet(args[2], out var entry)
                    ? OperationResult.Ok("op.describe", JsonSerializer.SerializeToElement(
                        entry!.Descriptor, OperationJsonContext.Default.OperationDescriptor))
                    : OperationResult.Fail(args[2], 3, "UNKNOWN_OPERATION", "Unknown operation ID: " + args[2]);
            else if (args.Length >= 3 && args[0] == "op" && args[1] is "invoke" or "batch")
            {
                string json = "{}";
                int? pid = null;
                string? model = null;
                var allow = false;
                var allowWrite = false;
                var allowLaunch = false;
                var inputSelected = false;
                var batch = args[1] == "batch";
                var timeoutSeconds = 120;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = batch ? 2 : 3; i < args.Length; i++)
                {
                    var option = args[i];
                    if (!seen.Add(option)) throw new ArgumentException("Duplicate option: " + option);
                    if (option == "--allow-execution") { allow = true; continue; }
                    if (option == "--allow-write") { allowWrite = true; continue; }
                    if (option == "--allow-launch") { allowLaunch = true; continue; }
                    if (option == "--input-stdin")
                    {
                        if (inputSelected) throw new ArgumentException("Choose only one JSON input source.");
                        inputSelected = true;
                        json = await ReadBoundedAsync(inputReader ?? Console.In, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (option is not ("--input" or "--input-file" or "--pid" or "--expected-model" or "--timeout-seconds") || ++i >= args.Length)
                        throw new ArgumentException("Unknown or incomplete option: " + option);
                    switch (option)
                    {
                        case "--input":
                        case "--input-file":
                            if (inputSelected) throw new ArgumentException("Choose only one JSON input source.");
                            inputSelected = true;
                            if (option == "--input-file")
                            {
                                using var file = File.OpenText(Path.GetFullPath(args[i]));
                                json = await ReadBoundedAsync(file, cancellationToken).ConfigureAwait(false);
                            }
                            else json = args[i];
                            if (json.Length > 1048576) throw new ArgumentException("JSON input exceeds 1 MiB of characters.");
                            break;
                        case "--pid":
                            if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                                throw new ArgumentException("--pid must be a positive integer.");
                            pid = parsed;
                            break;
                        case "--expected-model":
                            model = args[i];
                            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("--expected-model must not be blank.");
                            break;
                        case "--timeout-seconds":
                            if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out timeoutSeconds) || timeoutSeconds is < 1 or > 3600)
                                throw new ArgumentException("--timeout-seconds must be between 1 and 3600.");
                            break;
                    }
                }
                using var document = JsonDocument.Parse(json);
                var context = new OperationContext(pid, model, allow, allowWrite, allowLaunch);
                if (batch)
                {
                    if (!inputSelected) throw new ArgumentException("Batch requires an explicit JSON array input.");
                    result = await RunBatchAsync(document.RootElement, executor, context, timeoutSeconds, cancellationToken).ConfigureAwait(false);
                }
                else result = await InvokeTimedAsync(args[2], document.RootElement, executor, context, timeoutSeconds, cancellationToken).ConfigureAwait(false);
            }
            else throw new ArgumentException(
                "Usage: op list | op describe <id> | op invoke <id> | op batch [--input <json> | --input-file <path> | --input-stdin] [--pid <pid>] [--expected-model <module>] [--allow-execution] [--allow-write] [--allow-launch] [--timeout-seconds <1..3600>]");
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or IOException or UnauthorizedAccessException)
        {
            result = OperationResult.Fail("cli", 2, "INVALID_INPUT", exception.Message);
        }
        await output.WriteLineAsync(JsonSerializer.Serialize(result, OperationJsonContext.Default.OperationResult))
            .ConfigureAwait(false);
        return result.ExitCode;
    }

    private static async Task<OperationResult> InvokeTimedAsync(string id, JsonElement input, OperationExecutor executor,
        OperationContext context, int seconds, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return await executor.InvokeAsync(id, input, context, deadline.Token).ConfigureAwait(false);
    }

    private static async Task<OperationResult> RunBatchAsync(JsonElement requests, OperationExecutor executor,
        OperationContext context, int seconds, CancellationToken token)
    {
        if (requests.ValueKind != JsonValueKind.Array || requests.GetArrayLength() is < 1 or > 1000)
            throw new ArgumentException("Batch must contain 1..1000 operation requests.");
        using var schema = JsonDocument.Parse("""
            {"type":"object","additionalProperties":false,"properties":{
              "operationId":{"type":"string","minLength":1},
              "input":{"type":"object","properties":{},"additionalProperties":true},
              "processId":{"type":"integer","minimum":1,"maximum":2147483647},
              "expectedModel":{"type":"string","minLength":1}},"required":["operationId","input"]}
            """);
        // Validate every envelope before the first side effect; operation/domain errors
        // remain ordered execution results and stop only after earlier results are retained.
        foreach (var request in requests.EnumerateArray())
        {
            var error = InputContract.Validate(request, schema.RootElement);
            if (error is not null) throw new ArgumentException(error);
        }
        var outcomes = new List<OperationResult>();
        foreach (var request in requests.EnumerateArray())
        {
            var target = context with
            {
                ProcessId = request.TryGetProperty("processId", out var pid) ? pid.GetInt32() : context.ProcessId,
                ExpectedModel = request.TryGetProperty("expectedModel", out var model) ? model.GetString() : context.ExpectedModel
            };
            var outcome = await InvokeTimedAsync(request.GetProperty("operationId").GetString()!, request.GetProperty("input"), executor, target, seconds, token).ConfigureAwait(false);
            outcomes.Add(outcome);
            if (!outcome.Success) break;
        }
        var failed = outcomes.LastOrDefault(r => !r.Success);
        var data = JsonSerializer.SerializeToElement(new OperationBatchResult(failed is null, failed is not null, outcomes.Count, outcomes.ToArray()), OperationJsonContext.Default.OperationBatchResult);
        return failed is null ? OperationResult.Ok("op.batch", data) : OperationResult.Fail("op.batch", failed.ExitCode,
            failed.Error?.Code ?? "BATCH_FAILED", "Batch stopped; earlier results are retained and no operation is retried. " + failed.Error?.Message,
            data, failed.Error?.MayHaveExecuted == true);
    }

    private static async Task<string> ReadBoundedAsync(TextReader reader, CancellationToken token)
    {
        var buffer = new char[8192];
        var result = new System.Text.StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            if (result.Length + read > 1048576) throw new ArgumentException("JSON input exceeds 1 MiB of characters.");
            result.Append(buffer, 0, read);
        }
        return result.ToString();
    }
}
