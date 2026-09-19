using PlantHost.Rpc;
using System.Text.Json.Serialization;
namespace YuzuhaToolkit.Mcp;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HostIdentityRequest))]
[JsonSerializable(typeof(HostIdentityResponse))]
[JsonSerializable(typeof(RunPmlCommandRequest))]
[JsonSerializable(typeof(RunPmlCommandResponse))]
internal partial class PmlRpcJsonContext : JsonSerializerContext { }

// Keep the NET35/NET48 service and operation names unchanged. No dynamic proxy.
internal sealed class AotPmlCommandService(RpcClient client) : IPmlCommandService
{
    private static readonly RpcMethod<HostIdentityRequest, HostIdentityResponse> Identity = new(
        "yuzuha.pml-command.v1", "get-host-identity",
        PmlRpcJsonContext.Default.HostIdentityRequest, PmlRpcJsonContext.Default.HostIdentityResponse);
    private static readonly RpcMethod<RunPmlCommandRequest, RunPmlCommandResponse> Command = new(
        "yuzuha.pml-command.v1", "run-pml-command",
        PmlRpcJsonContext.Default.RunPmlCommandRequest, PmlRpcJsonContext.Default.RunPmlCommandResponse);
    public async Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request, CancellationToken token) =>
        await client.InvokeAsync(Identity, request, token).ConfigureAwait(false)
        ?? throw new InvalidDataException("Empty host identity response.");
    public async Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken token) =>
        await client.InvokeAsync(Command, request, token).ConfigureAwait(false)
        ?? throw new InvalidDataException("Empty command response. Do not retry automatically.");
}
