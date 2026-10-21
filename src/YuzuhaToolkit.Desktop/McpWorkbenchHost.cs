using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>WPF 直接复用已有会话桥和注册调用服务；不启动 HTTP 或另建窗体管理。</summary>
internal sealed class McpWorkbenchHost : IWorkbenchHost, IDisposable
{
    private readonly YuzuhaRpcBridge bridge = new(NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery());
    private readonly RuntimeJournal journal = new("PMLTWIN 桌面会话");
    private readonly PmlFunctionTools functions;
    /// <summary>复用同一个本机函数注册表建立调用服务。</summary>
    internal McpWorkbenchHost(PmlFunctionRegistry registry) => functions = new(registry, new PmlCallTools(bridge, new PmlFunctionTrustStore(WorkbenchPaths.TrustFile(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("YUZUHA_TRUST_FILE"), Environment.GetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT")))));
    /// <summary>只读发现已有 AVEVA 窗口。</summary>
    public AvevaSessionListResponse Discover() => bridge.DiscoverSessions();
    /// <summary>显式连接并核验已有专用管道。</summary>
    public Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token) => bridge.SelectSessionAsync(pid, model, token);
    /// <summary>核验已选目标而不执行 PML。</summary>
    public Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token) => bridge.GetConnectionStatusAsync(token);
    /// <summary>复用生产桥和原生 Form/Gadget 查询策略。</summary>
    public Task<RunPmlCommandResponse> QueryAsync(PmlFormQueryRequest request, CancellationToken token) => Record("desktop_form_query", new { request.Kind, request.FormName, request.GadgetName, request.StartIndex, request.MaxItems, request.MaxCharacters }, () => bridge.QueryPmlFormsAsync(request, token));
    /// <summary>复用生产有界数组入口。</summary>
    public Task<RunPmlCommandResponse> ReadArrayAsync(string name, int start, int items, int chars, CancellationToken token) => Record("desktop_array_query", new { name, start, items, chars }, () => bridge.ReadPmlArrayAsync(name, start, items, chars, token));
    /// <summary>复用注册函数工具，包括权限、源码和人工信任提示。</summary>
    public Task<RunPmlCommandResponse> InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token) => Record("desktop_registered_function", new { name, parameterCount = parameters.Count }, async () => WorkbenchReply.ParseRegisteredCall(await functions.InvokePmlFunction(name, parameters, token)));
    private async Task<RunPmlCommandResponse> Record(string tool, object parameters, Func<Task<RunPmlCommandResponse>> action)
    {
        var id = journal.Begin(tool, JsonSerializer.Serialize(parameters), bridge.SelectedProcessId);
        try
        {
            var result = await action();
            journal.Complete(id, result.Success ? "succeeded" : "failed", $"request={result.RequestId}; code={result.Code}; {result.ErrorMessage}", bridge.SelectedProcessId);
            return result;
        }
        catch (Exception error) { journal.Complete(id, error is OperationCanceledException or IOException or TimeoutException ? "unknown" : "failed", error.Message, bridge.SelectedProcessId); throw; }
    }
    /// <summary>释放本窗口持有的连接和本机日志。</summary>
    public void Dispose() { bridge.Dispose(); journal.Dispose(); }
}
