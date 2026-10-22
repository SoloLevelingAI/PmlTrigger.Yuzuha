using System.Globalization;
using System.Text.Json;

namespace PmlTwin.Phayton;

public static class OperationCli
{
    public static async Task<int> RunAsync(string[] args, OperationExecutor executor,
        TextWriter output, CancellationToken cancellationToken = default)
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
            else if (args.Length >= 3 && args[0] == "op" && args[1] == "invoke")
            {
                string json = "{}";
                int? pid = null;
                string? model = null;
                var allow = false;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 3; i < args.Length; i++)
                {
                    var option = args[i];
                    if (!seen.Add(option)) throw new ArgumentException("Duplicate option: " + option);
                    if (option == "--allow-execution") { allow = true; continue; }
                    if (option is not ("--input" or "--pid" or "--expected-model") || ++i >= args.Length)
                        throw new ArgumentException("Unknown or incomplete option: " + option);
                    switch (option)
                    {
                        case "--input": json = args[i]; break;
                        case "--pid":
                            if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                                throw new ArgumentException("--pid must be a positive integer.");
                            pid = parsed;
                            break;
                        case "--expected-model":
                            model = args[i];
                            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("--expected-model must not be blank.");
                            break;
                    }
                }
                using var document = JsonDocument.Parse(json);
                result = await executor.InvokeAsync(args[2], document.RootElement,
                    new(pid, model, allow), cancellationToken).ConfigureAwait(false);
            }
            else throw new ArgumentException(
                "Usage: op list | op describe <id> | op invoke <id> [--input <json>] [--pid <pid>] [--expected-model <module>] [--allow-execution]");
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException)
        {
            result = OperationResult.Fail("cli", 2, "INVALID_INPUT", exception.Message);
        }
        await output.WriteLineAsync(JsonSerializer.Serialize(result, OperationJsonContext.Default.OperationResult))
            .ConfigureAwait(false);
        return result.ExitCode;
    }
}
