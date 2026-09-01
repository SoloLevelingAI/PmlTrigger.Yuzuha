using Microsoft.Extensions.Logging;
using PlantHost.Rpc;

namespace YuzuhaToolkit.Mcp;

public sealed class YuzuhaRpcBridge : IDisposable
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly AvevaSessionDiscovery _discovery;
    private readonly ILogger<YuzuhaRpcBridge> _logger;
    private RpcClient? _client;
    private bool _disposed;
    private RpcHeartbeatSession? _heartbeat;
    private IPmlCommandService? _service;
    private AvevaSessionCandidate? _selectedSession;
    private YuzuhaTargetOptions? _target;

    public YuzuhaRpcBridge(
        ILogger<YuzuhaRpcBridge> logger,
        AvevaSessionDiscovery discovery)
    {
        _logger = logger;
        _discovery = discovery;
    }

    public void Dispose()
    {
        _connectionGate.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            DisconnectCore();
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public AvevaSessionListResponse DiscoverSessions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _discovery.Discover();
    }

    public async Task<YuzuhaConnectionStatus> SelectSessionAsync(
        int processId,
        string? expectedModel,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidate = _discovery.RequireCandidate(processId);
            DisconnectCore();

            if (!candidate.PipeDetected)
                throw new InvalidOperationException(
                    "AVEVA_PID_PIPE_NOT_FOUND: The selected window owns PID " +
                    candidate.ProcessId + ", but pipe '" +
                    candidate.PipeName + "' was not detected. Restart AVEVA " +
                    "with the PID-bound Yuzuha Host; the legacy shared pipe " +
                    "is never used as a fallback.");

            RpcClient? newClient = null;
            RpcHeartbeatSession? newHeartbeat = null;
            try
            {
                newClient = RpcClient.Connect(
                    candidate.PipeName,
                    connectTimeoutMilliseconds: 3000);
                var newService = newClient.CreateProxy<IPmlCommandService>();
                newHeartbeat = newClient.StartHeartbeat(
                    new RpcHeartbeatOptions
                    {
                        InstanceId = "pml-command-mcp-net10-" +
                                     Environment.ProcessId,
                        Interval = TimeSpan.FromSeconds(2)
                    });

                var identity = await newService.GetHostIdentityAsync(
                        new HostIdentityRequest(),
                        cancellationToken)
                    .ConfigureAwait(false);
                ValidateDiscoveredIdentity(candidate, identity, expectedModel);

                _client = newClient;
                _heartbeat = newHeartbeat;
                _service = newService;
                _selectedSession = candidate;
                _target = YuzuhaTargetOptions.FromCandidate(
                    candidate,
                    identity.Model!);
                newClient = null;
                newHeartbeat = null;

                _logger.LogInformation(
                    "Selected AVEVA window {WindowTitle}; PID={ProcessId}, " +
                    "start ticks={ProcessStartTimeUtcTicks}, model={Model}, " +
                    "pipe={PipeName}.",
                    candidate.WindowTitle,
                    candidate.ProcessId,
                    candidate.ProcessStartTimeUtcTicks,
                    identity.Model,
                    candidate.PipeName);

                return CreateConnectedStatus(identity);
            }
            catch
            {
                newHeartbeat?.Dispose();
                newClient?.Dispose();
                throw;
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task<YuzuhaConnectionStatus> GetConnectionStatusAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_target == null || _service == null)
                return CreateNotSelectedStatus();

            try
            {
                var identity = await ReadAndValidateIdentityCoreAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                return CreateConnectedStatus(identity);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return CreateFailedStatus(exception.Message);
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public Task<RunPmlCommandResponse> RunPmlCommandAsync(
        string pmlCommand,
        CancellationToken cancellationToken)
    {
        return RunVerifiedAsync(
            service => service.RunPmlCommandAsync(
                new RunPmlCommandRequest { PmlCommand = pmlCommand },
                cancellationToken),
            cancellationToken);
    }

    public Task<RunPmlCommandResponse> RunPmlCommandListAsync(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar,
        CancellationToken cancellationToken)
    {
        return RunVerifiedAsync(
            service => service.RunPmlCommandAsync(
                new RunPmlCommandRequest
                {
                    PmlCommand = pmlCommand,
                    ReturnList = true,
                    GlobalVar = globalVar,
                    DeleteGlobalVar = deleteGlobalVar
                },
                cancellationToken),
            cancellationToken);
    }

    private async Task<RunPmlCommandResponse> RunVerifiedAsync(
        Func<IPmlCommandService, Task<RunPmlCommandResponse>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RequireSelectedService();
            await ReadAndValidateIdentityCoreAsync(cancellationToken)
                .ConfigureAwait(false);
            return await operation(_service!).ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async Task<HostIdentityResponse> ReadAndValidateIdentityCoreAsync(
        CancellationToken cancellationToken)
    {
        RequireSelectedService();
        var identity = await _service!.GetHostIdentityAsync(
                new HostIdentityRequest(),
                cancellationToken)
            .ConfigureAwait(false);
        ValidateSelectedIdentity(identity);
        return identity;
    }

    private void RequireSelectedService()
    {
        if (_target == null || _service == null)
            throw new InvalidOperationException(
                "E3D_TARGET_NOT_SELECTED: Call list_aveva_sessions, then " +
                "select_aveva_session with one returned PID before executing PML.");
    }

    private static void ValidateDiscoveredIdentity(
        AvevaSessionCandidate candidate,
        HostIdentityResponse? identity,
        string? expectedModel)
    {
        if (identity == null)
            throw new InvalidDataException(
                "E3D_TARGET_IDENTITY_EMPTY: The host returned no identity.");
        if (identity.ProcessId != candidate.ProcessId)
            throw new InvalidOperationException(
                "E3D_TARGET_PID_MISMATCH: selected PID " +
                candidate.ProcessId + ", host reported " +
                identity.ProcessId + ".");
        if (identity.ProcessStartTimeUtcTicks !=
            candidate.ProcessStartTimeUtcTicks)
            throw new InvalidOperationException(
                "E3D_TARGET_START_MISMATCH: selected process start UTC ticks " +
                candidate.ProcessStartTimeUtcTicks + ", host reported " +
                identity.ProcessStartTimeUtcTicks + ".");
        if (!string.Equals(identity.PipeName, candidate.PipeName,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "E3D_TARGET_PIPE_MISMATCH: selected pipe '" +
                candidate.PipeName + "', host reported '" +
                (identity.PipeName ?? "") + "'.");
        if (string.IsNullOrWhiteSpace(identity.Model))
            throw new InvalidOperationException(
                "E3D_TARGET_MODEL_EMPTY: The host reported no AVEVA model.");
        if (!string.IsNullOrWhiteSpace(expectedModel) &&
            !string.Equals(identity.Model, expectedModel.Trim(),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "E3D_TARGET_MODEL_MISMATCH: expected model '" +
                expectedModel.Trim() + "', host reported '" +
                identity.Model + "'.");
    }

    private void ValidateSelectedIdentity(HostIdentityResponse? identity)
    {
        ValidateDiscoveredIdentity(_selectedSession!, identity,
            _target!.ExpectedModel);
    }

    private YuzuhaConnectionStatus CreateConnectedStatus(
        HostIdentityResponse identity)
    {
        return new YuzuhaConnectionStatus
        {
            SessionSelected = true,
            Connected = true,
            TargetVerified = true,
            SelectedSession = _selectedSession,
            PipeName = _target!.PipeName,
            ExpectedProcessId = _target.ProcessId,
            ExpectedProcessStartTimeUtcTicks =
                _target.ProcessStartTimeUtcTicks,
            ExpectedModel = _target.ExpectedModel,
            Host = identity
        };
    }

    private static YuzuhaConnectionStatus CreateNotSelectedStatus()
    {
        return new YuzuhaConnectionStatus
        {
            Error = "E3D_TARGET_NOT_SELECTED: Call list_aveva_sessions, then " +
                    "select_aveva_session before executing PML."
        };
    }

    private YuzuhaConnectionStatus CreateFailedStatus(string error)
    {
        return new YuzuhaConnectionStatus
        {
            SessionSelected = true,
            Connected = false,
            TargetVerified = false,
            SelectedSession = _selectedSession,
            PipeName = _target!.PipeName,
            ExpectedProcessId = _target.ProcessId,
            ExpectedProcessStartTimeUtcTicks =
                _target.ProcessStartTimeUtcTicks,
            ExpectedModel = _target.ExpectedModel,
            Error = error
        };
    }

    private void DisconnectCore()
    {
        _heartbeat?.Dispose();
        _heartbeat = null;
        _client?.Dispose();
        _client = null;
        _service = null;
        _target = null;
        _selectedSession = null;
    }
}

public sealed class YuzuhaConnectionStatus
{
    public bool SessionSelected { get; set; }
    public bool Connected { get; set; }
    public bool TargetVerified { get; set; }
    public AvevaSessionCandidate? SelectedSession { get; set; }
    public string PipeName { get; set; } = string.Empty;
    public int ExpectedProcessId { get; set; }
    public long ExpectedProcessStartTimeUtcTicks { get; set; }
    public string ExpectedModel { get; set; } = string.Empty;
    public HostIdentityResponse? Host { get; set; }
    public string? Error { get; set; }
}
