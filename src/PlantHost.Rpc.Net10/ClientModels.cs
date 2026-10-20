namespace PlantHost.Rpc;

/// <summary>Stable identifier for a background RPC job.</summary>
public readonly struct JobId : IEquatable<JobId>
{
    /// <summary>校验非空作业编号并保存其序号值。</summary>
    /// <param name="value">待校验并按声明类型格式化的值。</param>
    public JobId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A job ID is required.", nameof(value));
        Value = value;
    }

    /// <summary>校验后的稳定作业编号字符串。</summary>
    public string Value { get; init; }

    /// <summary>生成新的后台作业稳定编号。</summary>
    public static JobId NewId() => new(Guid.NewGuid().ToString("N"));

    /// <summary>校验并包装已有作业编号；不提交新作业。</summary>
    /// <param name="value">待校验并按声明类型格式化的值。</param>
    public static JobId Parse(string value) => new(value);

    /// <summary>按序号规则比较稳定作业编号。</summary>
    /// <param name="other">参与相等比较的另一个稳定作业编号。</param>
    public bool Equals(JobId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>按序号规则比较稳定作业编号。</summary>
    /// <param name="obj">需要进行类型核验与相等比较的对象。</param>
    public override bool Equals(object? obj) => obj is JobId other && Equals(other);

    /// <summary>计算与序号相等规则一致的编号哈希值。</summary>
    public override int GetHashCode() =>
        Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>返回作业编号的字符串；默认结构值返回空字符串。</summary>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>比较两个作业编号是否相等。</summary>
    /// <param name="left">相等比较的左操作数。</param>
    /// <param name="right">相等比较的右操作数。</param>
    public static bool operator ==(JobId left, JobId right) => left.Equals(right);

    /// <summary>比较两个作业编号是否不相等。</summary>
    /// <param name="left">相等比较的左操作数。</param>
    /// <param name="right">相等比较的右操作数。</param>
    public static bool operator !=(JobId left, JobId right) => !left.Equals(right);
}

/// <summary>后台 RPC 作业的排队、执行、取消、完成与失联状态。</summary>
public enum JobState
{
    /// <summary>请求已登记，等待工作实例领取。</summary>
    Queued = 0,
    /// <summary>工作实例正在执行。</summary>
    Running = 1,
    /// <summary>取消请求已登记，等待工作实例响应。</summary>
    CancelRequested = 2,
    /// <summary>作业已成功完成。</summary>
    Succeeded = 3,
    /// <summary>作业执行失败，原因见错误字段。</summary>
    Failed = 4,
    /// <summary>工作实例已确认取消。</summary>
    Canceled = 5,
    /// <summary>作业的工作实例心跳失效，无法确认正常完成。</summary>
    Orphaned = 6
}

/// <summary>后台作业状态快照，包含进度、心跳、终态、错误和结果载荷。</summary>
public sealed class JobStatus
{
    /// <summary>稳定的后台作业编号。</summary>
    public JobId JobId { get; set; }
    /// <summary>由服务器识别的后台作业类型名称。</summary>
    public string? JobType { get; set; }
    /// <summary>当前协议状态；枚举或字符串取值由所属契约限定。</summary>
    public JobState State { get; set; }
    /// <summary>后台作业最近报告的进度数值。</summary>
    public int Progress { get; set; }
    /// <summary>当前状态的可读说明。</summary>
    public string? Message { get; set; }
    /// <summary>处理该作业的工作实例编号。</summary>
    public string? WorkerId { get; set; }
    /// <summary>服务器记录的创建 UTC 时间。</summary>
    public DateTime CreatedUtc { get; set; }
    /// <summary>服务器最近更新状态的 UTC 时间。</summary>
    public DateTime UpdatedUtc { get; set; }
    /// <summary>工作实例最近心跳的 UTC 时间。</summary>
    public DateTime LastHeartbeatUtc { get; set; }
    /// <summary>最近一次进度报告的 UTC 时间，未报告时为空。</summary>
    public DateTime? LastProgressUtc { get; set; }
    /// <summary>进入终态的 UTC 时间，尚未结束时为空。</summary>
    public DateTime? CompletedUtc { get; set; }
    /// <summary>机器可判定的协议或执行错误代码。</summary>
    public string? ErrorCode { get; set; }
    /// <summary>执行失败的可读原因，成功时通常为空。</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>结果载荷使用的序列化格式名称。</summary>
    public string? ResultSerializer { get; set; }
    /// <summary>服务器保存的结果字节载荷。</summary>
    public byte[]? ResultPayload { get; set; }
    /// <summary>协议或状态快照版本，用于判断兼容性或更新次序。</summary>
    public long Version { get; set; }

    /// <summary>状态是否已成功、失败、取消或被判定为失联。</summary>
    public bool IsTerminal =>
        State is JobState.Succeeded or
            JobState.Failed or
            JobState.Canceled or
            JobState.Orphaned;
}

/// <summary>提交作业后返回的稳定作业编号及初始状态。</summary>
public sealed class JobSubmission
{
    /// <summary>稳定的后台作业编号。</summary>
    public JobId JobId { get; set; }
    /// <summary>当前协议状态；枚举或字符串取值由所属契约限定。</summary>
    public JobState State { get; set; }
}

/// <summary>已读取的作业状态及按调用方类型反序列化的结果。</summary>
/// <typeparam name="TResult">结果反序列化使用的静态类型。</typeparam>
public sealed class JobResult<TResult>
{
    /// <summary>服务器返回的状态快照；没有对应记录时可为空。</summary>
    public required JobStatus Status { get; init; }
    /// <summary>查询或异步作业返回的结果数据。</summary>
    public TResult? Result { get; init; }
}

/// <summary>一个客户端实例的最近心跳与存活判定。</summary>
public sealed class ProcessHeartbeatStatus
{
    /// <summary>用于心跳登记与查询的客户端实例稳定编号。</summary>
    public string? InstanceId { get; set; }
    /// <summary>发送方的运行时标识。</summary>
    public string? Runtime { get; set; }
    /// <summary>目标或发送方操作系统进程编号；不能代替启动时间核验。</summary>
    public int ProcessId { get; set; }
    /// <summary>服务器最近看到该实例的 UTC 时间。</summary>
    public DateTime LastSeenUtc { get; set; }
    /// <summary>实例是否仍在服务器配置的心跳有效期内。</summary>
    public bool IsAlive { get; set; }
}

/// <summary>客户端心跳会话从连接到释放的状态。</summary>
public enum RpcConnectionState
{
    /// <summary>正在等待首次可用的心跳响应。</summary>
    Connecting = 0,
    /// <summary>最近心跳成功。</summary>
    Connected = 1,
    /// <summary>已出现失败，但尚未达到连续失败阈值。</summary>
    Degraded = 2,
    /// <summary>连续失败达到阈值。</summary>
    Disconnected = 3,
    /// <summary>会话已被调用方释放。</summary>
    Disposed = 4
}

/// <summary>一次连接状态变化的旧状态与新状态。</summary>
public sealed class RpcConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>保存连接状态变化的前后快照。</summary>
    /// <param name="previousState">状态变更前的连接状态。</param>
    /// <param name="currentState">状态变更后的连接状态。</param>
    internal RpcConnectionStateChangedEventArgs(
        RpcConnectionState previousState,
        RpcConnectionState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    /// <summary>此次变化发生前的连接状态。</summary>
    public RpcConnectionState PreviousState { get; }

    /// <summary>此次变化后的连接状态。</summary>
    public RpcConnectionState CurrentState { get; }
}

/// <summary>心跳实例编号、间隔、单次超时与连续失败阈值。</summary>
public sealed class RpcHeartbeatOptions
{
    /// <summary>设置新的实例编号及默认心跳间隔、超时和失败阈值。</summary>
    public RpcHeartbeatOptions()
    {
        InstanceId = Guid.NewGuid().ToString("N");
        Interval = TimeSpan.FromSeconds(5);
        AttemptTimeout = TimeSpan.FromSeconds(1);
        FailureThreshold = 3;
    }

    /// <summary>用于心跳登记与查询的客户端实例稳定编号。</summary>
    public string InstanceId { get; set; }
    /// <summary>相邻心跳尝试的计划间隔。</summary>
    public TimeSpan Interval { get; set; }
    /// <summary>单次心跳尝试的超时边界。</summary>
    public TimeSpan AttemptTimeout { get; set; }
    /// <summary>触发断连状态的连续心跳失败次数。</summary>
    public int FailureThreshold { get; set; }
}
