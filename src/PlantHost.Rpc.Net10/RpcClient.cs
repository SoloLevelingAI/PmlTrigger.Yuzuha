using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PlantHost.Rpc;

/// <summary>
/// Native AOT-compatible PlantHost.Rpc client. Calls use generated JSON metadata
/// and stable wire route names instead of reflection-generated interface proxies.
/// </summary>
public sealed class RpcClient : IDisposable
{
    /// <summary>与旧 Framework 服务兼容的 JSON 序列化协议名称。</summary>
    internal const string CompatibleSerializerName = "newtonsoft-json";

    private readonly NamedPipeRpcClientTransport _transport;
    private readonly ConcurrentDictionary<RpcHeartbeatSession, byte> _heartbeats = new();
    private bool _disposed;

    /// <summary>配置本机管道传输与作业客户端；不在构造时发送业务命令。</summary>
    /// <param name="pipeName">目标本机 Named Pipe 名称。</param>
    /// <param name="connectTimeoutMilliseconds">单次建立管道连接的超时毫秒数；-1 表示无限等待。</param>
    public RpcClient(
        string pipeName,
        int connectTimeoutMilliseconds = Timeout.Infinite)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
            throw new ArgumentException("A pipe name is required.", nameof(pipeName));
        if (connectTimeoutMilliseconds < Timeout.Infinite ||
            connectTimeoutMilliseconds == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(connectTimeoutMilliseconds));
        }

        _transport = new NamedPipeRpcClientTransport(
            pipeName,
            connectTimeoutMilliseconds);
        Jobs = new RpcJobClient(this);
    }

    /// <summary>通过当前 RPC 客户端访问后台作业的入口。</summary>
    public RpcJobClient Jobs { get; }

    /// <summary>配置指定本机管道的 RPC 客户端；实际连接由调用时的传输建立。</summary>
    /// <param name="pipeName">目标本机 Named Pipe 名称。</param>
    /// <param name="connectTimeoutMilliseconds">单次建立管道连接的超时毫秒数；-1 表示无限等待。</param>
    public static RpcClient Connect(
        string pipeName,
        int connectTimeoutMilliseconds = Timeout.Infinite)
    {
        return new RpcClient(pipeName, connectTimeoutMilliseconds);
    }

    /// <summary>使用稳定服务/操作路由和调用方 JSON 元数据发送一次请求；失败通过异常传播，不重试命令。</summary>
    /// <param name="method">包含请求与响应 JSON 元数据的 RPC 方法描述。</param>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
    /// <typeparam name="TResponse">响应数据的静态类型。</typeparam>
    public Task<TResponse?> InvokeAsync<TRequest, TResponse>(
        RpcMethod<TRequest, TResponse> method,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        return InvokeAsync(
            method.Service,
            method.Operation,
            request,
            method.RequestTypeInfo,
            method.ResponseTypeInfo,
            cancellationToken);
    }

    /// <summary>使用稳定服务/操作路由和调用方 JSON 元数据发送一次请求；失败通过异常传播，不重试命令。</summary>
    /// <param name="service">协议定义的稳定服务路由。</param>
    /// <param name="operation">协议定义的稳定操作路由。</param>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="requestTypeInfo">请求类型的编译期 JSON 元数据，避免 Native AOT 反射序列化。</param>
    /// <param name="responseTypeInfo">响应类型的编译期 JSON 元数据，避免 Native AOT 反射反序列化。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
    /// <typeparam name="TResponse">响应数据的静态类型。</typeparam>
    public async Task<TResponse?> InvokeAsync<TRequest, TResponse>(
        string service,
        string operation,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(requestTypeInfo);
        ArgumentNullException.ThrowIfNull(responseTypeInfo);

        RpcWireResponse response = await SendAsync(
                service,
                operation,
                JsonSerializer.SerializeToUtf8Bytes(request, requestTypeInfo),
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.Deserialize(response.Payload, responseTypeInfo);
    }

    /// <summary>使用稳定服务/操作路由和调用方 JSON 元数据发送一次请求；失败通过异常传播，不重试命令。</summary>
    /// <param name="command">待发送的命令值、PML 文本或已声明的 RPC 命令描述。</param>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
    /// <returns>表示异步操作完成的任务；取消或传输失败通过异常传播。</returns>
    public Task InvokeAsync<TRequest>(
        RpcCommand<TRequest> command,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return InvokeAsync(
            command.Service,
            command.Operation,
            request,
            command.RequestTypeInfo,
            cancellationToken);
    }

    /// <summary>使用稳定服务/操作路由和调用方 JSON 元数据发送一次请求；失败通过异常传播，不重试命令。</summary>
    /// <param name="service">协议定义的稳定服务路由。</param>
    /// <param name="operation">协议定义的稳定操作路由。</param>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="requestTypeInfo">请求类型的编译期 JSON 元数据，避免 Native AOT 反射序列化。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
    /// <returns>表示异步操作完成的任务；取消或传输失败通过异常传播。</returns>
    public async Task InvokeAsync<TRequest>(
        string service,
        string operation,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(requestTypeInfo);

        await SendAsync(
                service,
                operation,
                JsonSerializer.SerializeToUtf8Bytes(request, requestTypeInfo),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>轮询健康检查直至服务可用或取消；此轮询不执行 PML 命令。</summary>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <returns>表示异步操作完成的任务；取消或传输失败通过异常传播。</returns>
    public async Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        while (true)
        {
            try
            {
                await PingAsync(null, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception)
                when (!cancellationToken.IsCancellationRequested &&
                      exception is TimeoutException or IOException)
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>创建并启动独立心跳会话，跟踪连接状态和连续失败。</summary>
    /// <param name="options">心跳实例、间隔、超时与连续失败阈值。</param>
    public RpcHeartbeatSession StartHeartbeat(RpcHeartbeatOptions? options = null)
    {
        ThrowIfDisposed();
        options ??= new RpcHeartbeatOptions();
        var session = new RpcHeartbeatSession(
            options,
            PingAsync,
            heartbeat => _heartbeats.TryRemove(heartbeat, out _));
        _heartbeats.TryAdd(session, 0);
        session.Start();
        return session;
    }

    /// <summary>读取指定客户端实例最近的服务端心跳快照。</summary>
    /// <param name="instanceId">用于服务端心跳登记或查询的客户端实例编号。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    public async Task<ProcessHeartbeatStatus?> GetProcessStatusAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ProcessStatusResponse? reply = await InvokeSystemAsync(
                SystemRpcNames.HealthService,
                SystemRpcNames.GetProcessStatus,
                new ProcessStatusRequest { InstanceId = instanceId },
                PlantHostRpcJsonContext.Default.ProcessStatusRequest,
                PlantHostRpcJsonContext.Default.ProcessStatusResponse,
                cancellationToken)
            .ConfigureAwait(false);
        return reply?.Status;
    }

    /// <summary>停止持有的心跳或连接资源；重复释放不会再次发起操作。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (RpcHeartbeatSession heartbeat in _heartbeats.Keys)
            heartbeat.Dispose();
        _heartbeats.Clear();
    }

    /// <summary>发送健康或后台作业协议请求，使用系统服务的固定序列化约定。</summary>
    /// <param name="service">协议定义的稳定服务路由。</param>
    /// <param name="operation">协议定义的稳定操作路由。</param>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="requestTypeInfo">请求类型的编译期 JSON 元数据，避免 Native AOT 反射序列化。</param>
    /// <param name="responseTypeInfo">响应类型的编译期 JSON 元数据，避免 Native AOT 反射反序列化。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    /// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
    /// <typeparam name="TResponse">响应数据的静态类型。</typeparam>
    internal Task<TResponse?> InvokeSystemAsync<TRequest, TResponse>(
        string service,
        string operation,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
    {
        return InvokeAsync(
            service,
            operation,
            request,
            requestTypeInfo,
            responseTypeInfo,
            cancellationToken);
    }

    private Task<ProcessHeartbeatResponse?> PingAsync(
        RpcHeartbeatOptions? options,
        CancellationToken cancellationToken)
    {
        var request = new ProcessHeartbeatRequest
        {
            InstanceId = options?.InstanceId,
            Runtime = options == null ? null : Environment.Version.ToString(),
            ProcessId = options == null ? 0 : Environment.ProcessId
        };

        return InvokeSystemAsync(
            SystemRpcNames.HealthService,
            SystemRpcNames.Ping,
            request,
            PlantHostRpcJsonContext.Default.ProcessHeartbeatRequest,
            PlantHostRpcJsonContext.Default.ProcessHeartbeatResponse,
            cancellationToken);
    }

    private async Task<RpcWireResponse> SendAsync(
        string service,
        string operation,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(service))
            throw new ArgumentException("A service name is required.", nameof(service));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("An operation name is required.", nameof(operation));

        var requestId = Guid.NewGuid();
        var wireRequest = new RpcWireRequest
        {
            RequestId = requestId,
            Service = service,
            Operation = operation,
            Serializer = CompatibleSerializerName,
            Payload = payload
        };

        RpcWireResponse response = await _transport
            .SendAsync(wireRequest, cancellationToken)
            .ConfigureAwait(false);

        if (response.RequestId != requestId)
            throw new InvalidDataException("RPC response request ID does not match.");
        if (!response.Success)
        {
            throw new RemoteRpcException(
                response.ErrorCode,
                response.ErrorMessage);
        }

        return response;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>本机 Named Pipe 请求传输，负责连接超时、帧交换和取消。</summary>
internal sealed class NamedPipeRpcClientTransport
{
    private readonly string _pipeName;
    private readonly int _connectTimeoutMilliseconds;

    /// <summary>保存目标管道名称与连接超时，按发送请求建立传输。</summary>
    /// <param name="pipeName">目标本机 Named Pipe 名称。</param>
    /// <param name="connectTimeoutMilliseconds">单次建立管道连接的超时毫秒数；-1 表示无限等待。</param>
    public NamedPipeRpcClientTransport(
        string pipeName,
        int connectTimeoutMilliseconds)
    {
        _pipeName = pipeName;
        _connectTimeoutMilliseconds = connectTimeoutMilliseconds;
    }

    /// <summary>向目标 Named Pipe 发送一个请求帧，读取对应响应并遵守取消边界。</summary>
    /// <param name="request">当前操作的请求数据及其显式目标或参数。</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    public async Task<RpcWireResponse> SendAsync(
        RpcWireRequest request,
        CancellationToken cancellationToken)
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(
                _connectTimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        await PipeFraming.WriteAsync(
                pipe,
                WireProtocol.EncodeRequest(request),
                cancellationToken)
            .ConfigureAwait(false);

        byte[] response = await PipeFraming
            .ReadAsync(pipe, cancellationToken)
            .ConfigureAwait(false);
        return WireProtocol.DecodeResponse(response);
    }
}
