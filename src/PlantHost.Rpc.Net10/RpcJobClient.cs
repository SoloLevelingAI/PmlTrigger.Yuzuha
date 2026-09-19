using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Diagnostics;

namespace PlantHost.Rpc;

/// <summary>Native AOT-compatible client-side API for durable background jobs.</summary>
public sealed class RpcJobClient
{
    private readonly RpcClient _client;

    internal RpcJobClient(RpcClient client)
    {
        _client = client;
    }

    public Task<JobSubmission?> SubmitAsync<TCommand>(
        string jobType,
        TCommand command,
        JsonTypeInfo<TCommand> commandTypeInfo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            throw new ArgumentException("A job type is required.", nameof(jobType));
        ArgumentNullException.ThrowIfNull(commandTypeInfo);

        var request = new JobSubmitRequest
        {
            JobType = jobType,
            Serializer = RpcClient.CompatibleSerializerName,
            CommandPayload = JsonSerializer.SerializeToUtf8Bytes(
                command,
                commandTypeInfo)
        };

        return _client.InvokeSystemAsync(
            SystemRpcNames.JobsService,
            SystemRpcNames.SubmitJob,
            request,
            PlantHostRpcJsonContext.Default.JobSubmitRequest,
            PlantHostRpcJsonContext.Default.JobSubmission,
            cancellationToken);
    }

    public async Task<JobStatus?> GetStatusAsync(
        JobId jobId,
        CancellationToken cancellationToken = default)
    {
        JobStatusResponse? reply = await _client.InvokeSystemAsync(
                SystemRpcNames.JobsService,
                SystemRpcNames.GetJobStatus,
                new JobStatusRequest { JobId = jobId },
                PlantHostRpcJsonContext.Default.JobStatusRequest,
                PlantHostRpcJsonContext.Default.JobStatusResponse,
                cancellationToken)
            .ConfigureAwait(false);
        return reply?.Status;
    }

    public async Task<bool> CancelAsync(
        JobId jobId,
        CancellationToken cancellationToken = default)
    {
        JobCancelResponse? reply = await _client.InvokeSystemAsync(
                SystemRpcNames.JobsService,
                SystemRpcNames.CancelJob,
                new JobStatusRequest { JobId = jobId },
                PlantHostRpcJsonContext.Default.JobStatusRequest,
                PlantHostRpcJsonContext.Default.JobCancelResponse,
                cancellationToken)
            .ConfigureAwait(false);
        return reply?.Accepted == true;
    }

    public async Task<JobResult<TResult>> GetResultAsync<TResult>(
        JobId jobId,
        JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resultTypeInfo);
        JobResultResponse? reply = await _client.InvokeSystemAsync(
                SystemRpcNames.JobsService,
                SystemRpcNames.GetJobResult,
                new JobStatusRequest { JobId = jobId },
                PlantHostRpcJsonContext.Default.JobStatusRequest,
                PlantHostRpcJsonContext.Default.JobResultResponse,
                cancellationToken)
            .ConfigureAwait(false);

        JobStatus status = reply?.Status ??
            throw new RemoteRpcException("job_not_found", "The job was not found.");
        if (status.State != JobState.Succeeded)
        {
            throw new InvalidOperationException(
                "The job has no successful result. Current state: " + status.State);
        }
        if (!string.Equals(
                status.ResultSerializer,
                RpcClient.CompatibleSerializerName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The job result uses serializer '" + status.ResultSerializer +
                "', but this client uses '" +
                RpcClient.CompatibleSerializerName + "'.");
        }
        if (status.ResultPayload == null)
            throw new InvalidDataException("The successful job has no result payload.");

        return new JobResult<TResult>
        {
            Status = status,
            Result = JsonSerializer.Deserialize(status.ResultPayload, resultTypeInfo)
        };
    }

    public async Task<JobStatus> WaitForCompletionAsync(
        JobId jobId,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default)
    {
        if (pollingInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollingInterval));

        while (true)
        {
            JobStatus? status = await GetStatusAsync(jobId, cancellationToken)
                .ConfigureAwait(false);
            if (status == null)
                throw new RemoteRpcException("job_not_found", "The job was not found.");
            if (status.IsTerminal)
                return status;
            await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Background process-heartbeat lease owned by an RpcClient.</summary>
public sealed class RpcHeartbeatSession : IDisposable
{
    private readonly RpcHeartbeatOptions _options;
    private readonly Func<
        RpcHeartbeatOptions?,
        CancellationToken,
        Task<ProcessHeartbeatResponse?>> _send;
    private readonly Action<RpcHeartbeatSession> _onDisposed;
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private bool _disposed;

    internal RpcHeartbeatSession(
        RpcHeartbeatOptions options,
        Func<RpcHeartbeatOptions?, CancellationToken, Task<ProcessHeartbeatResponse?>> send,
        Action<RpcHeartbeatSession> onDisposed)
    {
        if (string.IsNullOrWhiteSpace(options.InstanceId))
            throw new ArgumentException("Heartbeat InstanceId is required.", nameof(options));
        if (options.Interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.AttemptTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.FailureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));

        _options = options;
        _send = send;
        _onDisposed = onDisposed;
    }

    public string InstanceId => _options.InstanceId;

    public RpcConnectionState State { get; private set; } =
        RpcConnectionState.Connecting;

    public bool IsConnected => State == RpcConnectionState.Connected;

    public DateTime? LastAttemptUtc { get; private set; }

    public DateTime? LastSuccessUtc { get; private set; }

    public TimeSpan? LastLatency { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public Exception? LastError { get; private set; }

    public bool IsRunning => _loop is { IsCompleted: false };

    public Exception? LoopError => _loop?.Exception?.GetBaseException();

    public event EventHandler<RpcConnectionStateChangedEventArgs>? StateChanged;

    internal void Start()
    {
        _loop = Task.Run(LoopAsync);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stop.Cancel();
        try
        {
            _loop?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        TransitionTo(RpcConnectionState.Disposed);
        _stop.Dispose();
        _onDisposed(this);
    }

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            LastAttemptUtc = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(
                _stop.Token);
            attempt.CancelAfter(_options.AttemptTimeout);

            try
            {
                await _send(_options, attempt.Token).ConfigureAwait(false);
                stopwatch.Stop();
                LastSuccessUtc = DateTime.UtcNow;
                LastLatency = stopwatch.Elapsed;
                ConsecutiveFailures = 0;
                LastError = null;
                TransitionTo(RpcConnectionState.Connected);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                RecordFailure(
                    new TimeoutException(
                        "The PlantHost.Rpc heartbeat timed out after " +
                        _options.AttemptTimeout + "."),
                    stopwatch.Elapsed);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                RecordFailure(exception, stopwatch.Elapsed);
            }

            try
            {
                await Task.Delay(_options.Interval, _stop.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void RecordFailure(Exception exception, TimeSpan latency)
    {
        LastLatency = latency;
        ConsecutiveFailures++;
        LastError = exception;
        TransitionTo(
            ConsecutiveFailures >= _options.FailureThreshold
                ? RpcConnectionState.Disconnected
                : RpcConnectionState.Degraded);
    }

    private void TransitionTo(RpcConnectionState newState)
    {
        RpcConnectionState previous = State;
        if (previous == newState)
            return;

        State = newState;
        EventHandler<RpcConnectionStateChangedEventArgs>? handlers = StateChanged;
        if (handlers == null)
            return;

        var eventArgs = new RpcConnectionStateChangedEventArgs(previous, newState);
        foreach (EventHandler<RpcConnectionStateChangedEventArgs> handler in
                 handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch
            {
                // Observers must never stop the transport heartbeat loop.
            }
        }
    }
}
