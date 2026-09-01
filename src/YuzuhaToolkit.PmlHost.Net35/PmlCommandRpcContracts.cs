using System;
using PlantHost.Rpc;

namespace YuzuhaToolkit.PmlHost
{
    [RpcService("yuzuha.pml-command.v1")]
    public interface IPmlCommandService
    {
        [RpcOperation("run-pml-command")]
        RunPmlCommandResponse RunPmlCommand(
            RunPmlCommandRequest request);

        [RpcOperation("get-host-identity")]
        HostIdentityResponse GetHostIdentity(
            HostIdentityRequest request);
    }

    public sealed class HostIdentityRequest
    {
    }

    public sealed class HostIdentityResponse
    {
        public int ProcessId { get; set; }
        public long ProcessStartTimeUtcTicks { get; set; }
        public string Model { get; set; }
        public string PipeName { get; set; }
        public string HostFramework { get; set; }
        public string HostVersion { get; set; }
        public DateTime ServerTimeUtc { get; set; }
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
}
