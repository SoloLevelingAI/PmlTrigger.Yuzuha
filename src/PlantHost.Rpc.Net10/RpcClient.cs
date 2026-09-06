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
    internal const string CompatibleSerializerName = "newtonsoft-json";

    private readonly NamedPipeRpcClientTransport _transport;
    private readonly ConcurrentDictionary<RpcHeartbeatSession, byte> _heartbeats = new();
    private bool _disposed;

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

    public RpcJobClient Jobs { get; }

    public static RpcClient Connect(
        string pipeName,
        int connectTimeoutMilliseconds = Timeout.Infinite)
    {
        return new RpcClient(pipeName, connectTimeoutMilliseconds);
    }

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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (RpcHeartbeatSession heartbeat in _heartbeats.Keys)
            heartbeat.Dispose();
        _heartbeats.Clear();
    }

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

internal sealed class NamedPipeRpcClientTransport
{
    private readonly string _pipeName;
    private readonly int _connectTimeoutMilliseconds;

    public NamedPipeRpcClientTransport(
        string pipeName,
        int connectTimeoutMilliseconds)
    {
        _pipeName = pipeName;
        _connectTimeoutMilliseconds = connectTimeoutMilliseconds;
    }

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
