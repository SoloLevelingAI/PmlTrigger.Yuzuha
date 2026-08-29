using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlantHost.Rpc;

namespace YuzuhaToolkit.Mcp;

public sealed class YuzuhaRpcBridge : IHostedService, IDisposable
{
    public static readonly string PipeName = ResolvePipeName();

    private readonly RpcClient _client;
    private readonly RpcHeartbeatOptions _heartbeatOptions;
    private readonly ILogger<YuzuhaRpcBridge> _logger;
    private bool _disposed;
    private RpcHeartbeatSession? _heartbeat;

    private static readonly RpcMethod<
        RunPmlCommandRequest,
        RunPmlCommandResponse> RunPmlCommandMethod = new(
            "yuzuha.pml-command.v1",
            "run-pml-command",
            YuzuhaJsonContext.Default.RunPmlCommandRequest,
            YuzuhaJsonContext.Default.RunPmlCommandResponse);

    public YuzuhaRpcBridge(ILogger<YuzuhaRpcBridge> logger)
    {
        _logger = logger;
        _client = RpcClient.Connect(
            PipeName,
            connectTimeoutMilliseconds: 3000);
        _heartbeatOptions = new RpcHeartbeatOptions
        {
            InstanceId =
                "pml-command-mcp-net10-" +
                Environment.ProcessId,
            Interval = TimeSpan.FromSeconds(2),
            AttemptTimeout = TimeSpan.FromSeconds(1),
            FailureThreshold = 3
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_heartbeat != null)
        {
            _heartbeat.StateChanged -= OnConnectionStateChanged;
            _heartbeat.Dispose();
        }
        _client.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _heartbeat = _client.StartHeartbeat(
            _heartbeatOptions);
        _heartbeat.StateChanged += OnConnectionStateChanged;
        _logger.LogInformation(
            "PML RPC heartbeat started for pipe {PipeName} as {InstanceId}.",
            PipeName,
            _heartbeatOptions.InstanceId);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public async Task<RunPmlCommandResponse> RunPmlCommandAsync(
        string pmlCommand,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _client.InvokeAsync(
                RunPmlCommandMethod,
                new RunPmlCommandRequest { PmlCommand = pmlCommand },
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "The PML host returned an empty RPC response.");
    }

    public async Task<RunPmlCommandResponse> RunPmlCommandListAsync(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _client.InvokeAsync(
                RunPmlCommandMethod,
                new RunPmlCommandRequest
                {
                    PmlCommand = pmlCommand,
                    ReturnList = true,
                    GlobalVar = globalVar,
                    DeleteGlobalVar = deleteGlobalVar
                },
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "The PML host returned an empty RPC response.");
    }

    public YuzuhaConnectionStatus GetConnectionStatus()
    {
        RpcHeartbeatSession? heartbeat = _heartbeat;
        return new YuzuhaConnectionStatus
        {
            State = heartbeat?.State.ToString() ?? RpcConnectionState.Connecting.ToString(),
            IsConnected = heartbeat?.IsConnected == true,
            PipeName = PipeName,
            InstanceId = _heartbeatOptions.InstanceId,
            LastAttemptUtc = heartbeat?.LastAttemptUtc,
            LastSuccessUtc = heartbeat?.LastSuccessUtc,
            LastLatencyMilliseconds = heartbeat?.LastLatency?.TotalMilliseconds,
            ConsecutiveFailures = heartbeat?.ConsecutiveFailures ?? 0,
            LastError = heartbeat?.LastError?.Message,
            HeartbeatRunning = heartbeat?.IsRunning == true,
            HeartbeatLoopError = heartbeat?.LoopError?.Message
        };
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_heartbeat?.IsConnected == true)
            return;

        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        readiness.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await _client.WaitForReadyAsync(readiness.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            YuzuhaConnectionStatus status = GetConnectionStatus();
            throw new InvalidOperationException(
                "E3D_HOST_OFFLINE: The AVEVA PML host is not reachable on pipe '" +
                PipeName + "'. State=" + status.State +
                "; LastError=" + (status.LastError ?? "none") + ".");
        }
    }

    private void OnConnectionStateChanged(
        object? sender,
        RpcConnectionStateChangedEventArgs eventArgs)
    {
        YuzuhaConnectionStatus status = GetConnectionStatus();
        if (eventArgs.CurrentState == RpcConnectionState.Connected)
        {
            _logger.LogInformation(
                "PML RPC connection is {State}; latency {LatencyMs:F1} ms.",
                status.State,
                status.LastLatencyMilliseconds);
            return;
        }

        _logger.LogWarning(
            "PML RPC connection changed from {PreviousState} to {State}; " +
            "failures={Failures}; error={Error}.",
            eventArgs.PreviousState,
            status.State,
            status.ConsecutiveFailures,
            status.LastError);
    }

    private static string ResolvePipeName()
    {
        string? configured = Environment.GetEnvironmentVariable(
            "YUZUHA_PML_PIPE");
        return string.IsNullOrWhiteSpace(configured)
            ? "yuzuha.pml.command.v1"
            : configured.Trim();
    }
}

public sealed class YuzuhaConnectionStatus
{
    public string State { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string PipeName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public double? LastLatencyMilliseconds { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastError { get; set; }
    public bool HeartbeatRunning { get; set; }
    public string? HeartbeatLoopError { get; set; }
}
