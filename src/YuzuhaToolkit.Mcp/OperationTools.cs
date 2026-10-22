using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Mcp;

[McpServerToolType]
public sealed class OperationTools(OperationExecutor executor)
{
    [McpServerTool]
    [Description("List the shared Phayton operations, stable IDs, input contracts and execution requirements. Does not run PML.")]
    public string OpList() => JsonSerializer.Serialize(executor.Registry.Descriptors.ToArray(),
        OperationJsonContext.Default.OperationDescriptorArray);

    [McpServerTool]
    [Description("Describe one shared Phayton operation by stable ID. Does not run PML.")]
    public string OpDescribe(string operationId) => executor.Registry.TryGet(operationId, out var entry)
        ? JsonSerializer.Serialize(entry!.Descriptor, OperationJsonContext.Default.OperationDescriptor)
        : JsonSerializer.Serialize(OperationResult.Fail(operationId, 3, "UNKNOWN_OPERATION", "Unknown operation ID."),
            OperationJsonContext.Default.OperationResult);

    [McpServerTool]
    [Description("Invoke the same Phayton kernel as CLI. PML operations require an explicit discovered processId and allowExecution=true after the user requests execution. Identity is reverified; never retry automatically.")]
    public async Task<string> OpInvoke(string operationId, string inputJson = "{}", int? processId = null,
        string? expectedModel = null, bool allowExecution = false, CancellationToken cancellationToken = default)
    {
        OperationResult result;
        try
        {
            using var input = JsonDocument.Parse(inputJson);
            result = await executor.InvokeAsync(operationId, input.RootElement,
                new(processId, expectedModel, allowExecution), cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            result = OperationResult.Fail(operationId, 2, "INVALID_INPUT", exception.Message);
        }
        return JsonSerializer.Serialize(result, OperationJsonContext.Default.OperationResult);
    }
}
