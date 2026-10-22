using Microsoft.Extensions.Logging;
using PlantHost.Rpc;

namespace YuzuhaToolkit.Mcp;

/// <summary>协调会话选择、PID/启动时间/模块核验与串行化 RPC 连接；不自动重试命令。</summary>
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

    /// <summary>当前已选择的目标 PID，用于本机运行日志；执行前仍重新核验完整身份。</summary>
    public int? SelectedProcessId => _selectedSession?.ProcessId;

    /// <summary>配置只读会话发现与诊断依赖，尚未选择任何 AVEVA 目标。</summary>
    /// <param name="logger">仅写诊断流的日志记录器。</param>
    /// <param name="discovery">用于重新核验候选的发现器或本机发现结果。</param>
    public YuzuhaRpcBridge(
        ILogger<YuzuhaRpcBridge> logger,
        AvevaSessionDiscovery discovery)
    {
        _logger = logger;
        _discovery = discovery;
    }

    /// <summary>停止持有的心跳或连接资源；重复释放不会再次发起操作。</summary>
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

    /// <summary>只读发现候选工程会话，不自动选择或执行 PML。</summary>
    public AvevaSessionListResponse DiscoverSessions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _discovery.Discover();
    }

    /// <summary>锁定显式 PID 的专用管道，并核验进程启动时间与 Host 模块身份。</summary>
    /// <param name="processId">从会话发现结果取得的准确目标 PID，不能猜测。</param>
    /// <param name="expectedModel">可选的预期 AVEVA 模块；提供时必须与 Host 身份匹配。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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
                var newService = new AotPmlCommandService(newClient);
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

    /// <summary>核对已选择会话的连接与身份；未选择目标时返回明确状态。</summary>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>在核验后的目标 Host 主线程执行一次已授权命令；取消或超时不自动重试。</summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>核验目标后执行一次返回全局数组的 PML 请求，保持调用串行与清理选项。</summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    /// <param name="globalVar">用于承载结果的 PML 全局数组变量名。</param>
    /// <param name="deleteGlobalVar">读取结束时是否清理该结果全局变量。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>核验身份与 Host 能力后，按预算只读已有数组，不重放生成表达式。</summary>
    public async Task<RunPmlCommandResponse> ReadPmlArrayAsync(string globalVar,int startIndex,int maxItems,int maxCharacters,CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RequireSelectedService();
            var identity=await ReadAndValidateIdentityCoreAsync(cancellationToken).ConfigureAwait(false);
            return await BoundedArrayRequest.SendAsync(_service!,identity,globalVar,startIndex,maxItems,maxCharacters,cancellationToken).ConfigureAwait(false);
        }
        finally { _connectionGate.Release(); }
    }

    /// <summary>在目标身份锁内检查查询能力并发送只读 Form/Gadget 请求，不自动重试。</summary>
    public async Task<RunPmlCommandResponse> QueryPmlFormsAsync(YuzuhaToolkit.Pml.PmlFormQueryRequest query, CancellationToken cancellationToken)
    {
        YuzuhaToolkit.Pml.PmlFormQuery.Validate(query);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RequireSelectedService();
            var identity = await ReadAndValidateIdentityCoreAsync(cancellationToken).ConfigureAwait(false);
            return await PmlFormQueryRequestSender.SendAsync(_service!, identity, query, cancellationToken).ConfigureAwait(false);
        }
        finally { _connectionGate.Release(); }
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

/// <summary>会话选择、连接和身份核验的独立状态及失败原因。</summary>
public sealed class YuzuhaConnectionStatus
{
    /// <summary>是否已经显式选定目标工程会话。</summary>
    public bool SessionSelected { get; set; }
    /// <summary>是否已建立目标 RPC 通信；仍需单独判断身份核验。</summary>
    public bool Connected { get; set; }
    /// <summary>PID、启动时间、专用管道和模块是否全部匹配。</summary>
    public bool TargetVerified { get; set; }
    /// <summary>本次显式选择并锁定的候选会话。</summary>
    public AvevaSessionCandidate? SelectedSession { get; set; }
    /// <summary>绑定该目标 PID 的专用 Named Pipe 名称。</summary>
    public string PipeName { get; set; } = string.Empty;
    /// <summary>已锁定的目标进程编号。</summary>
    public int ExpectedProcessId { get; set; }
    /// <summary>已锁定的目标进程启动 UTC ticks。</summary>
    public long ExpectedProcessStartTimeUtcTicks { get; set; }
    /// <summary>选定或显式要求匹配的 AVEVA 模块名称。</summary>
    public string ExpectedModel { get; set; } = string.Empty;
    /// <summary>目标 Host 返回的完整身份快照。</summary>
    public HostIdentityResponse? Host { get; set; }
    /// <summary>当前查询、连接或处理失败的可读原因。</summary>
    public string? Error { get; set; }
}
