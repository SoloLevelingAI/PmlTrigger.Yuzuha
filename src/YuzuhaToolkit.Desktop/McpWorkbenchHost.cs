using System.IO;
using System.Text.Json;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>旧桌面接口转接独立通信 CLI；本地注册表保留，界面不持有 RPC 连接。</summary>
internal sealed class McpWorkbenchHost(PmlFunctionRegistry registry, DesktopCliSession? channel = null) : IWorkbenchHost, IDisposable
{
    private readonly DesktopCliSession session = channel ?? new();
    private readonly RuntimeJournal journal = new("PMLTWIN 桌面会话");
    /// <inheritdoc />
    public AvevaSessionListResponse Discover() => session.DiscoverAsync().GetAwaiter().GetResult();
    /// <inheritdoc />
    public Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token = default) => session.DiscoverAsync(token);
    /// <inheritdoc />
    public Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token) => session.SelectAsync(pid, model, token);
    /// <inheritdoc />
    public Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token) => session.StatusAsync(token);
    /// <inheritdoc />
    public Task<RunPmlCommandResponse> QueryAsync(PmlFormQueryRequest request, CancellationToken token)
    {
        var operation = request.Kind switch { "forms" or "shown-forms" => "pml.forms.list", "gadgets" => "pml.gadgets.list", "gadget" => "pml.gadget.read", _ => throw new ArgumentException("未知窗体查询。") };
        object input = request.Kind switch
        {
            "forms" or "shown-forms" => new { shownOnly = request.Kind == "shown-forms", startIndex = request.StartIndex, maxItems = request.MaxItems, maxCharacters = request.MaxCharacters },
            "gadgets" => new { formName = request.FormName, startIndex = request.StartIndex, maxItems = request.MaxItems, maxCharacters = request.MaxCharacters },
            _ => new { formName = request.FormName, gadgetName = request.GadgetName, maxCharacters = request.MaxCharacters }
        };
        return Record(operation, input, async () => DesktopCliSession.Receipt(await session.InvokeAsync(operation, input, token).ConfigureAwait(false)));
    }
    /// <inheritdoc />
    public Task<RunPmlCommandResponse> ReadArrayAsync(string name, int start, int items, int chars, CancellationToken token)
    {
        var input = new { globalVar = name, startIndex = start, maxItems = items, maxCharacters = chars };
        return Record("pml.array.window", input, async () => DesktopCliSession.Receipt(await session.InvokeAsync("pml.array.window", input, token).ConfigureAwait(false)));
    }
    /// <inheritdoc />
    public async Task<DesktopObjectReadReply> ReadObjectAsync(DesktopObjectQuery query, CancellationToken token)
    {
        var input = new { pmlCommand = query.Expression, globalVar = "YuzuhaDesktopRead" + Guid.NewGuid().ToString("N"), deleteGlobalVar = true, includeEmpty = true };
        DesktopObjectReadReply? reply = null;
        await Record("pml.array.read", new { query.Kind, query.Expression }, async () =>
        {
            var response = await session.InvokeAsync("pml.array.read", input, token).ConfigureAwait(false);
            var receipt = DesktopCliSession.Receipt(response);
            reply = DesktopObjectReadReply.Parse(response.Result.Success ? response.Result.Data?.GetRawText() ?? JsonSerializer.Serialize(receipt) : JsonSerializer.Serialize(receipt)); return receipt;
        }).ConfigureAwait(false);
        return reply!;
    }
    /// <inheritdoc />
    public Task<RunPmlCommandResponse> InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token)
    {
        _ = registry.PrepareCall(name, parameters);
        return Record("pml.function.invoke", new { name, parameterCount = parameters.Count }, async () => DesktopCliSession.Receipt(
            await session.InvokeAsync("pml.function.invoke", new { functionName = name, parameters }, token).ConfigureAwait(false)));
    }
    private async Task<RunPmlCommandResponse> Record(string operation, object parameters, Func<Task<RunPmlCommandResponse>> action)
    {
        var id = journal.Begin(operation, JsonSerializer.Serialize(parameters), session.ProcessId);
        try
        {
            var result = await action().ConfigureAwait(false);
            journal.Complete(id, result.Success ? "succeeded" : "failed", $"request={result.RequestId}; code={result.Code}; {result.ErrorMessage}", session.ProcessId); return result;
        }
        catch (Exception error) { journal.Complete(id, error is OperationCanceledException or IOException or TimeoutException ? "unknown" : "failed", error.Message, session.ProcessId); throw; }
    }
    /// <inheritdoc />
    public void Dispose() => journal.Dispose();
}
