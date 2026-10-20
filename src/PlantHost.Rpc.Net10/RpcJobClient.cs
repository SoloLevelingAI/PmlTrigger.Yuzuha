using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Diagnostics;

namespace PlantHost.Rpc;

/// <summary>Native AOT-compatible client-side API for durable background jobs.</summary>
public sealed class RpcJobClient
{
    private readonly RpcClient _client;

    /// <summary>通过已有 RPC 客户端访问后台作业服务。</summary>
    /// <param name="client">执行稳定路由请求的 RPC 客户端。</param>
    internal RpcJobClient(RpcClient client)
    {
        _client = client;
    }

    /// <summary>序列化并提交后台作业命令，返回服务器分配的编号与初始状态。</summary>
    /// <param name="jobType">由服务器识别的后台作业类型名称。</param>
    /// <param name="command">待发送的命令值、PML 文本或已声明的 RPC 命令描述。</param>
    /// <param name="commandTypeInfo">命令类型的编译期 JSON 序列化元数据。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TCommand">后台作业命令的静态类型。</typeparam>
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

    /// <summary>按稳定编号读取当前作业状态，不改变作业执行。</summary>
    /// <param name="jobId">服务器返回的稳定后台作业编号。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>向服务器请求取消作业；返回接收状态，不保证工作线程已经退出。</summary>
    /// <param name="jobId">服务器返回的稳定后台作业编号。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>读取作业终态并使用调用方 JSON 元数据反序列化结果载荷。</summary>
    /// <param name="jobId">服务器返回的稳定后台作业编号。</param>
    /// <param name="resultTypeInfo">结果类型的编译期 JSON 反序列化元数据。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TResult">结果反序列化使用的静态类型。</typeparam>
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

    /// <summary>轮询作业状态直至终态，检查取消并在失败时保留服务器错误。</summary>
    /// <param name="jobId">服务器返回的稳定后台作业编号。</param>
    /// <param name="pollingInterval">相邻状态查询的间隔。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>配置单个客户端实例的心跳发送委托和释放通知。</summary>
    /// <param name="options">心跳实例、间隔、超时与连续失败阈值。</param>
    /// <param name="send">发送一次心跳并返回服务器健康响应的委托。</param>
    /// <param name="onDisposed">释放会话时从所属客户端集合移除本实例的回调。</param>
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

    /// <summary>用于心跳登记与查询的客户端实例稳定编号。</summary>
    public string InstanceId => _options.InstanceId;

    /// <summary>当前协议状态；枚举或字符串取值由所属契约限定。</summary>
    public RpcConnectionState State { get; private set; } =
        RpcConnectionState.Connecting;

    /// <summary>最近心跳是否确认服务器可达；不代表 AVEVA 模型命令成功。</summary>
    public bool IsConnected => State == RpcConnectionState.Connected;

    /// <summary>最近一次心跳尝试的 UTC 时间。</summary>
    public DateTime? LastAttemptUtc { get; private set; }

    /// <summary>最近一次心跳成功的 UTC 时间。</summary>
    public DateTime? LastSuccessUtc { get; private set; }

    /// <summary>最近成功心跳的往返耗时。</summary>
    public TimeSpan? LastLatency { get; private set; }

    /// <summary>当前累计的连续心跳失败次数。</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>最近失败的错误说明；记录器可能包含截图错误。</summary>
    public Exception? LastError { get; private set; }

    /// <summary>当前监听服务或心跳循环是否处于运行状态。</summary>
    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>导致心跳循环意外结束的异常。</summary>
    public Exception? LoopError => _loop?.Exception?.GetBaseException();

    /// <summary>连接状态发生变化时通知订阅方。</summary>
    public event EventHandler<RpcConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>启动此实例的心跳循环；重复启动不会产生第二个循环。</summary>
    internal void Start()
    {
        _loop = Task.Run(LoopAsync);
    }

    /// <summary>停止持有的心跳或连接资源；重复释放不会再次发起操作。</summary>
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
