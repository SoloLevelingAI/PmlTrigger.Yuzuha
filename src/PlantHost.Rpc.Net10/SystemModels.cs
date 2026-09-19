namespace PlantHost.Rpc;

internal static class SystemRpcNames
{
    public const string HealthService = "$planthost-rpc.health";
    public const string JobsService = "$planthost-rpc.jobs";
    public const string Ping = "Ping";
    public const string GetProcessStatus = "GetProcessStatus";
    public const string SubmitJob = "Submit";
    public const string GetJobStatus = "GetStatus";
    public const string CancelJob = "Cancel";
    public const string GetJobResult = "GetResult";
}

internal sealed class ProcessHeartbeatRequest
{
    public string? InstanceId { get; set; }
    public string? Runtime { get; set; }
    public int ProcessId { get; set; }
}

internal sealed class ProcessHeartbeatResponse
{
    public DateTime ServerTimeUtc { get; set; }
    public bool Healthy { get; set; }
}

internal sealed class ProcessStatusRequest
{
    public string? InstanceId { get; set; }
}

internal sealed class ProcessStatusResponse
{
    public ProcessHeartbeatStatus? Status { get; set; }
}

internal sealed class JobSubmitRequest
{
    public string? JobType { get; set; }
    public string? Serializer { get; set; }
    public byte[]? CommandPayload { get; set; }
}

internal sealed class JobStatusRequest
{
    public JobId JobId { get; set; }
}

internal sealed class JobStatusResponse
{
    public JobStatus? Status { get; set; }
}

internal sealed class JobCancelResponse
{
    public bool Accepted { get; set; }
    public JobStatus? Status { get; set; }
}

internal sealed class JobResultResponse
{
    public JobStatus? Status { get; set; }
}
