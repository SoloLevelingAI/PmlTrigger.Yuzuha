namespace PlantHost.Rpc;

/// <summary>Stable identifier for a background RPC job.</summary>
public readonly struct JobId : IEquatable<JobId>
{
    public JobId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A job ID is required.", nameof(value));
        Value = value;
    }

    public string Value { get; init; }

    public static JobId NewId() => new(Guid.NewGuid().ToString("N"));

    public static JobId Parse(string value) => new(value);

    public bool Equals(JobId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is JobId other && Equals(other);

    public override int GetHashCode() =>
        Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(JobId left, JobId right) => left.Equals(right);

    public static bool operator !=(JobId left, JobId right) => !left.Equals(right);
}

public enum JobState
{
    Queued = 0,
    Running = 1,
    CancelRequested = 2,
    Succeeded = 3,
    Failed = 4,
    Canceled = 5,
    Orphaned = 6
}

public sealed class JobStatus
{
    public JobId JobId { get; set; }
    public string? JobType { get; set; }
    public JobState State { get; set; }
    public int Progress { get; set; }
    public string? Message { get; set; }
    public string? WorkerId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }
    public DateTime? LastProgressUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultSerializer { get; set; }
    public byte[]? ResultPayload { get; set; }
    public long Version { get; set; }

    public bool IsTerminal =>
        State is JobState.Succeeded or
            JobState.Failed or
            JobState.Canceled or
            JobState.Orphaned;
}

public sealed class JobSubmission
{
    public JobId JobId { get; set; }
    public JobState State { get; set; }
}

public sealed class JobResult<TResult>
{
    public required JobStatus Status { get; init; }
    public TResult? Result { get; init; }
}

public sealed class ProcessHeartbeatStatus
{
    public string? InstanceId { get; set; }
    public string? Runtime { get; set; }
    public int ProcessId { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public bool IsAlive { get; set; }
}

public enum RpcConnectionState
{
    Connecting = 0,
    Connected = 1,
    Degraded = 2,
    Disconnected = 3,
    Disposed = 4
}

public sealed class RpcConnectionStateChangedEventArgs : EventArgs
{
    internal RpcConnectionStateChangedEventArgs(
        RpcConnectionState previousState,
        RpcConnectionState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public RpcConnectionState PreviousState { get; }

    public RpcConnectionState CurrentState { get; }
}

public sealed class RpcHeartbeatOptions
{
    public RpcHeartbeatOptions()
    {
        InstanceId = Guid.NewGuid().ToString("N");
        Interval = TimeSpan.FromSeconds(5);
        AttemptTimeout = TimeSpan.FromSeconds(1);
        FailureThreshold = 3;
    }

    public string InstanceId { get; set; }
    public TimeSpan Interval { get; set; }
    public TimeSpan AttemptTimeout { get; set; }
    public int FailureThreshold { get; set; }
}
