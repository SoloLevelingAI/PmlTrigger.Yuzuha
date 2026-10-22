using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Mcp;

/// <summary>公开共享操作目录、契约与明确授权调用的 MCP 适配器。</summary>
[McpServerToolType]
public sealed class OperationTools(OperationExecutor executor)
{
    /// <summary>返回共享操作的稳定标识、输入契约与执行要求，不执行 PML。</summary>
    [McpServerTool]
    [Description("List the shared Phayton operations, stable IDs, input contracts and execution requirements. Does not run PML.")]
    public string OpList() => JsonSerializer.Serialize(executor.Registry.Descriptors.ToArray(),
        OperationJsonContext.Default.OperationDescriptorArray);

    /// <summary>返回一个已注册操作的契约；未知标识产生明确错误。</summary>
    [McpServerTool]
    [Description("Describe one shared Phayton operation by stable ID. Does not run PML.")]
    public string OpDescribe(string operationId) => executor.Registry.TryGet(operationId, out var entry)
        ? JsonSerializer.Serialize(entry!.Descriptor, OperationJsonContext.Default.OperationDescriptor)
        : JsonSerializer.Serialize(OperationResult.Fail(operationId, 3, "UNKNOWN_OPERATION", "Unknown operation ID."),
            OperationJsonContext.Default.OperationResult);

    /// <summary>按显式目标和执行授权调用共享操作内核，不自动重试。</summary>
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
