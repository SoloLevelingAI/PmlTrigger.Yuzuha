using PlantHost.Rpc;
using System.Text.Json.Serialization;
namespace YuzuhaToolkit.Mcp;

/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HostIdentityRequest))]
[JsonSerializable(typeof(HostIdentityResponse))]
[JsonSerializable(typeof(RunPmlCommandRequest))]
[JsonSerializable(typeof(RunPmlCommandResponse))]
internal partial class PmlRpcJsonContext : JsonSerializerContext { }

// Keep the NET35/NET48 service and operation names unchanged. No dynamic proxy.
/// <summary>使用稳定路由和生成的 JSON 元数据调用 Host，不生成反射代理。</summary>
/// <param name="client">执行稳定路由请求的 RPC 客户端。</param>
internal sealed class AotPmlCommandService(RpcClient client) : IPmlCommandService
{
    private static readonly RpcMethod<HostIdentityRequest, HostIdentityResponse> Identity = new(
        "yuzuha.pml-command.v1", "get-host-identity",
        PmlRpcJsonContext.Default.HostIdentityRequest, PmlRpcJsonContext.Default.HostIdentityResponse);
    private static readonly RpcMethod<RunPmlCommandRequest, RunPmlCommandResponse> Command = new(
        "yuzuha.pml-command.v1", "run-pml-command",
        PmlRpcJsonContext.Default.RunPmlCommandRequest, PmlRpcJsonContext.Default.RunPmlCommandResponse);
    /// <summary>只读查询专用管道 Host 的进程、模块与启动身份，用于目标核验。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="token">取消此操作的令牌，不表示已经撤销远端副作用。</param>
    public async Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request, CancellationToken token) =>
        await client.InvokeAsync(Identity, request, token).ConfigureAwait(false)
        ?? throw new InvalidDataException("Empty host identity response.");
    /// <summary>在核验后的目标 Host 主线程执行一次已授权命令；取消或超时不自动重试。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="token">取消此操作的令牌，不表示已经撤销远端副作用。</param>
    public async Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken token) =>
        await client.InvokeAsync(Command, request, token).ConfigureAwait(false)
        ?? throw new InvalidDataException("Empty command response. Do not retry automatically.");
}
