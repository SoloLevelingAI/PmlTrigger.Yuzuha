namespace PlantHost.Rpc;

/// <summary>PlantHost 系统健康检查与后台作业的稳定路由名称。</summary>
internal static class SystemRpcNames
{
    /// <summary>系统健康检查服务的稳定协议名称。</summary>
    public const string HealthService = "$planthost-rpc.health";
    /// <summary>后台作业服务的稳定协议名称。</summary>
    public const string JobsService = "$planthost-rpc.jobs";
    /// <summary>心跳 Ping 操作的稳定名称。</summary>
    public const string Ping = "Ping";
    /// <summary>实例心跳状态查询的稳定操作名称。</summary>
    public const string GetProcessStatus = "GetProcessStatus";
    /// <summary>后台作业提交的稳定操作名称。</summary>
    public const string SubmitJob = "Submit";
    /// <summary>后台作业状态查询的稳定操作名称。</summary>
    public const string GetJobStatus = "GetStatus";
    /// <summary>后台作业取消请求的稳定操作名称。</summary>
    public const string CancelJob = "Cancel";
    /// <summary>后台作业结果读取的稳定操作名称。</summary>
    public const string GetJobResult = "GetResult";
}

/// <summary>登记客户端实例、运行时和进程编号的心跳请求。</summary>
internal sealed class ProcessHeartbeatRequest
{
    /// <summary>用于心跳登记与查询的客户端实例稳定编号。</summary>
    public string? InstanceId { get; set; }
    /// <summary>发送方的运行时标识。</summary>
    public string? Runtime { get; set; }
    /// <summary>目标或发送方操作系统进程编号；不能代替启动时间核验。</summary>
    public int ProcessId { get; set; }
}

/// <summary>服务器收到心跳后的时间与健康状态。</summary>
internal sealed class ProcessHeartbeatResponse
{
    /// <summary>Host 生成响应时的 UTC 时间。</summary>
    public DateTime ServerTimeUtc { get; set; }
    /// <summary>服务器是否接受并确认本次健康检查。</summary>
    public bool Healthy { get; set; }
}

/// <summary>读取指定客户端实例心跳状态的请求。</summary>
internal sealed class ProcessStatusRequest
{
    /// <summary>用于心跳登记与查询的客户端实例稳定编号。</summary>
    public string? InstanceId { get; set; }
}

/// <summary>服务器保存的客户端实例心跳快照。</summary>
internal sealed class ProcessStatusResponse
{
    /// <summary>服务器返回的状态快照；没有对应记录时可为空。</summary>
    public ProcessHeartbeatStatus? Status { get; set; }
}

/// <summary>提交后台作业的类型名称、序列化格式和命令载荷。</summary>
internal sealed class JobSubmitRequest
{
    /// <summary>由服务器识别的后台作业类型名称。</summary>
    public string? JobType { get; set; }
    /// <summary>载荷采用的协议序列化格式名称。</summary>
    public string? Serializer { get; set; }
    /// <summary>已序列化的后台作业命令字节。</summary>
    public byte[]? CommandPayload { get; set; }
}

/// <summary>按稳定作业编号读取或取消作业的请求。</summary>
internal sealed class JobStatusRequest
{
    /// <summary>稳定的后台作业编号。</summary>
    public JobId JobId { get; set; }
}

/// <summary>作业状态查询的协议响应。</summary>
internal sealed class JobStatusResponse
{
    /// <summary>服务器返回的状态快照；没有对应记录时可为空。</summary>
    public JobStatus? Status { get; set; }
}

/// <summary>取消请求是否被接收及当前作业状态；接收不等于作业已经停止。</summary>
internal sealed class JobCancelResponse
{
    /// <summary>服务器是否接收该请求；不等同于操作已完成。</summary>
    public bool Accepted { get; set; }
    /// <summary>服务器返回的状态快照；没有对应记录时可为空。</summary>
    public JobStatus? Status { get; set; }
}

/// <summary>携带作业状态及其结果载荷的协议响应。</summary>
internal sealed class JobResultResponse
{
    /// <summary>服务器返回的状态快照；没有对应记录时可为空。</summary>
    public JobStatus? Status { get; set; }
}
