using System;
using System.Threading;
using System.Threading.Tasks;
using PlantHost.Rpc;

namespace YuzuhaToolkit.PmlHost.Net48;

[RpcService("yuzuha.pml-command.v1")]
public interface IPmlCommandService
{
    [RpcOperation("run-pml-command")]
    Task<RunPmlCommandResponse> RunPmlCommandAsync(
        RunPmlCommandRequest request,
        CancellationToken cancellationToken);
}

public sealed class RunPmlCommandRequest
{
    public string PmlCommand { get; set; }
    public bool ReturnList { get; set; }
    public string GlobalVar { get; set; }
    public bool DeleteGlobalVar { get; set; }
}

public sealed class RunPmlCommandResponse
{
    public bool Success { get; set; }
    public string Code { get; set; }
    public string ErrorMessage { get; set; }
    public string PmlCommand { get; set; }
    public string[] ResultList { get; set; }
    public string RequestId { get; set; }
    public int ExecutionThreadId { get; set; }
    public string ServerRuntime { get; set; }
    public DateTime ServerTimeUtc { get; set; }
}
