using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>协调会话选择、PID/启动时间/模块核验与串行化 RPC 连接；不自动重试命令。</summary>
public sealed class YuzuhaRpcBridge : IDisposable
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly IAvevaSessionDiscovery _discovery;
    private readonly IPmlRpcConnectionFactory _connections;
    private readonly ILogger<YuzuhaRpcBridge> _logger;
    private IPmlRpcConnection? _connection;
    private bool _disposed;
    private readonly ExecutionAuthorizationStore _authorization;
    private readonly PmlFunctionRegistry _registry;
    private readonly string? _auditDirectory;
    private readonly RuntimeJournal _executionJournal;
    private bool _executionUnknown;
    private IPmlCommandService? _service;
    private AvevaSessionCandidate? _selectedSession;
    private YuzuhaTargetOptions? _target;

    /// <summary>当前已选择的目标 PID，用于本机运行日志；执行前仍重新核验完整身份。</summary>
    public int? SelectedProcessId => _selectedSession?.ProcessId;

    /// <summary>为注册调用的无 RPC 预检读取实际策略。</summary>
    internal ExecutionAuthorization ReadAuthorization() => _authorization.Read();

    /// <summary>配置只读会话发现与诊断依赖，尚未选择任何 AVEVA 目标。</summary>
    /// <param name="logger">仅写诊断流的日志记录器。</param>
    /// <param name="discovery">用于重新核验候选的发现器或本机发现结果。</param>
    public YuzuhaRpcBridge(
        ILogger<YuzuhaRpcBridge> logger,
        AvevaSessionDiscovery discovery)
        : this(logger, discovery, new PmlRpcConnectionFactory())
    {
    }

    /// <summary>注入发现器与连接工厂，尚不选择目标；供生产及隔离测试使用。</summary>
    public YuzuhaRpcBridge(ILogger<YuzuhaRpcBridge> logger,
        IAvevaSessionDiscovery discovery, IPmlRpcConnectionFactory connections, ExecutionAuthorizationStore? authorization = null, PmlFunctionRegistry? registry = null, string? auditDirectory = null)
    {
        _logger = logger;
        _discovery = discovery;
        _connections = connections;
        _authorization = authorization ?? new();
        _registry = registry ?? new();
        _auditDirectory = auditDirectory;
        _executionJournal = new RuntimeJournal("PML execution", auditDirectory);
    }

    /// <summary>创建共享发现器但连接状态独立的会话桥，避免并发调用切换目标。</summary>
    public YuzuhaRpcBridge CreateIndependent() => new(_logger, _discovery, _connections, _authorization, _registry, _auditDirectory);

    /// <summary>停止持有的心跳或连接资源；重复释放不会再次发起操作。</summary>
    public void Dispose()
    {
        _connectionGate.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            _executionJournal.Dispose();
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
            DisconnectCore();
            var candidate = _discovery.RequireCandidate(processId);

            if (!candidate.PipeDetected)
                throw new InvalidOperationException(
                    "AVEVA_PID_PIPE_NOT_FOUND: The selected window owns PID " +
                    candidate.ProcessId + ", but pipe '" +
                    candidate.PipeName + "' was not detected. Restart AVEVA " +
                    "with the PID-bound Yuzuha Host; the legacy shared pipe " +
                    "is never used as a fallback.");

            IPmlRpcConnection? newConnection = null;
            try
            {
                newConnection = _connections.Connect(candidate.PipeName);
                var newService = newConnection.Service;

                var identity = await newService.GetHostIdentityAsync(
                        new HostIdentityRequest(),
                        cancellationToken)
                    .ConfigureAwait(false);
                ValidateDiscoveredIdentity(candidate, identity, expectedModel);

                _connection = newConnection;
                _service = newService;
                _selectedSession = candidate;
                _executionUnknown = false;
                _target = YuzuhaTargetOptions.FromCandidate(
                    candidate,
                    identity.Model!);
                newConnection = null;

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
                newConnection?.Dispose();
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
        return RunRequestVerifiedAsync(new RunPmlCommandRequest { PmlCommand = pmlCommand }, cancellationToken);
    }

    /// <summary>执行表达式并返回数组；赋值及清理属于写操作，必须自动 MARKDB。</summary>
    public Task<RunPmlCommandResponse> RunPmlCommandListAsync(string pmlCommand, string globalVar, bool deleteGlobalVar, CancellationToken cancellationToken)
        => RunRequestVerifiedAsync(new RunPmlCommandRequest { PmlCommand = pmlCommand, ReturnList = true, GlobalVar = globalVar, DeleteGlobalVar = deleteGlobalVar }, cancellationToken);

    /// <summary>在同一会话锁中核验注册版本、签名、授权与只读证据，再派发一次。</summary>
    public Task<RunPmlCommandResponse> RunRegisteredFunctionAsync(PmlFunctionRegistry registry, string name, List<DynamicParameterInput> parameters, CancellationToken token)
        => RunRequestVerifiedAsync(new RunPmlCommandRequest(), token, registry, name, parameters);

    /// <summary>运行开发目录内的准确宏快照；授权目标、源码哈希和 MARKDB 始终核验。</summary>
    public Task<RunPmlCommandResponse> RunDevelopmentMacroAsync(string sourceFile,string sha256,CancellationToken token, string testOutcomeGlobal = "")
        => RunRequestVerifiedAsync(new RunPmlCommandRequest { DevelopmentMacroPath=sourceFile, DevelopmentMacroSha256=sha256, TestOutcomeGlobal=testOutcomeGlobal }, token);

    /// <summary>核验身份与 Host 能力后，按预算只读已有数组，不重放生成表达式。</summary>
    public Task<RunPmlCommandResponse> ReadPmlArrayAsync(string globalVar,int startIndex,int maxItems,int maxCharacters,CancellationToken cancellationToken)
    {
        YuzuhaToolkit.Pml.PmlArrayWindow.Validate(startIndex, maxItems, maxCharacters);
        if (string.IsNullOrEmpty(globalVar) || !System.Text.RegularExpressions.Regex.IsMatch(globalVar, @"\A[A-Za-z][A-Za-z0-9_]{0,127}\z")) throw new ArgumentException("Use a plain PML array name.", nameof(globalVar));
        return RunRequestVerifiedAsync(new RunPmlCommandRequest { ReadArray = true, GlobalVar = globalVar, StartIndex = startIndex, MaxItems = maxItems, MaxCharacters = maxCharacters }, cancellationToken);
    }

    /// <summary>类型化窗体查询保留只读豁免；保守模式仍禁止派发。</summary>
    public Task<RunPmlCommandResponse> QueryPmlFormsAsync(YuzuhaToolkit.Pml.PmlFormQueryRequest query, CancellationToken cancellationToken)
    {
        YuzuhaToolkit.Pml.PmlFormQuery.Validate(query);
        return RunRequestVerifiedAsync(new RunPmlCommandRequest { FormQuery = query }, cancellationToken);
    }

    private async Task<RunPmlCommandResponse> RunRequestVerifiedAsync(RunPmlCommandRequest request,
        CancellationToken token, PmlFunctionRegistry? registry = null, string? name = null, List<DynamicParameterInput>? parameters = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(token).ConfigureAwait(false);
        string? callId = null;
        var dispatched = false;
        try
        {
            var args = new JsonObject { ["command"] = request.PmlCommand, ["function"] = name, ["readArray"] = request.ReadArray, ["formQuery"] = request.FormQuery != null };
            callId = _executionJournal.Begin("pml.dispatch", JsonSerializer.Serialize(args, YuzuhaToolJsonContext.Default.JsonObject), SelectedProcessId, _target?.ProcessStartTimeUtcTicks);
            if (_executionJournal.Snapshot().LoggingError != null) throw new IOException("PML_AUDIT_UNAVAILABLE: dispatch blocked.");
            RequireSelectedService();
            if (_executionUnknown) throw new InvalidOperationException("PML_PREVIOUS_RESULT_UNKNOWN: inspect the Host, then explicitly reconnect; no automatic replay.");
            var identity = await ReadAndValidateIdentityCoreAsync(token).ConfigureAwait(false);
            var typedReadOnly = request.ReadArray || request.FormQuery != null;
            if (registry == null && request.DevelopmentMacroPath == null && request.PmlCommand != null)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(request.PmlCommand, @"\A\s*(?:MARKDB|ENDMARKDB)(?:\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new InvalidOperationException("PML_MARKDB_MANAGED: automatic groups own MARKDB/ENDMARKDB boundaries.");
                var parsed = _registry.ParseRegisteredCall(request.PmlCommand);
                if (parsed != null) { registry = _registry; name = parsed.Value.Name; parameters = parsed.Value.Parameters; }
            }
            var sourceRevision = request.DevelopmentMacroSha256 ?? "";
            var policy = _authorization.Require(identity, registry != null || request.DevelopmentMacroPath != null, request.DevelopmentMacroPath, typedReadOnly);
            if (request.DevelopmentMacroPath != null)
            {
                if (policy.Mode != "Development") throw new UnauthorizedAccessException("PML_ACTIVE_DEVELOPMENT_GRANT_REQUIRED");
                YuzuhaToolkit.Pml.PmlSourceIdentity.Verify(request.DevelopmentMacroPath, request.DevelopmentMacroSha256!);
                request.PmlCommand = YuzuhaToolkit.Pml.PmlSourceIdentity.MacroCommand(request.DevelopmentMacroPath);
            }
            string evidence = typedReadOnly ? request.ReadArray ? "typed-existing-array-v1" : "typed-form-query-v1" : "";
            if (registry != null)
            {
                var prepared = registry.PrepareInvocation(name!, parameters!, policy.Mode == "Development" && policy.BypassRules.Contains("function-permission"));
                _authorization.Require(identity, true, prepared.SourceFile);
                request.PmlCommand = prepared.Command;
                sourceRevision = prepared.SourceSha256;
                if (prepared.ReadOnly && !request.ReturnList && !request.DeleteGlobalVar)
                {
                    evidence = "reviewed-source-sha256:" + prepared.SourceSha256;
                    request.ReadOnlySourcePath=prepared.SourceFile; request.ReadOnlySourceSha256=prepared.SourceSha256;
                }
            }
            if (typedReadOnly)
            {
                if (request.ReadArray && identity.BoundedArrayReadVersion < 1) throw new NotSupportedException("PML_BOUNDED_ARRAY_HOST_UNSUPPORTED");
                if (request.FormQuery != null && identity.FormQueryVersion < 1) throw new NotSupportedException("PML_FORM_QUERY_HOST_UNSUPPORTED");
            }
            request.AutomaticMarkDb = evidence.Length == 0;
            if ((request.AutomaticMarkDb || request.ReadOnlySourcePath != null) && identity.AutomaticMarkDbVersion < 1)
                throw new NotSupportedException("PML_AUTOMATIC_MARKDB_HOST_UNSUPPORTED: update the PID-bound Host before mutation; no user code dispatched.");
            token.ThrowIfCancellationRequested();
            dispatched = true;
            var response = await _service!.RunPmlCommandAsync(request, token).ConfigureAwait(false);
            response.AuthorizationMode = policy.Mode; response.ReadOnlyEvidence = evidence;
            if (request.AutomaticMarkDb && (!response.MarkDbStarted || !response.MarkDbEnded))
            {
                if (response.Success || response.MarkDbStarted) response.Code = response.MarkDbStarted ? "MARKDB_GROUP_INCOMPLETE" : "MARKDB_RECEIPT_MISSING";
                response.Success = false;
                if (response.MarkDbStarted || response.ExecutionStarted) _executionUnknown = true;
            }
            var summary = $"mode={policy.Mode}; sourceSha256={sourceRevision}; bypass={string.Join(',', policy.BypassRules)}; reason={policy.Reason}; readonly={evidence}; markdb={response.MarkDbId}/{response.MarkDbStarted}/{response.MarkDbEnded}; request={response.RequestId}; code={response.Code}; {response.ErrorMessage}";
            _executionJournal.Complete(callId, response.Success ? "succeeded" : "failed", summary, SelectedProcessId, _target?.ProcessStartTimeUtcTicks);
            return response;
        }
        catch (Exception error)
        {
            error.Data["YuzuhaDispatched"] = dispatched;
            var unknown = dispatched && error is OperationCanceledException or IOException or TimeoutException;
            if (unknown) _executionUnknown = true;
            if (callId != null) _executionJournal.Complete(callId, unknown ? "cancelled-unknown" : "failed", error.Message, SelectedProcessId, _target?.ProcessStartTimeUtcTicks);
            throw;
        }
        finally { _connectionGate.Release(); }
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
        _connection?.Dispose();
        _connection = null;
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
