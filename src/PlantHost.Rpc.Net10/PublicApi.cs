using System.Text.Json.Serialization.Metadata;

namespace PlantHost.Rpc;

/// <summary>Represents an error returned by the remote endpoint.</summary>
public sealed class RemoteRpcException : Exception
{
    public RemoteRpcException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

/// <summary>
/// Describes one AOT-safe RPC operation. JSON metadata is supplied explicitly so
/// the client never needs reflection or runtime code generation.
/// </summary>
public sealed class RpcMethod<TRequest, TResponse>
{
    public RpcMethod(
        string service,
        string operation,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo)
    {
        if (string.IsNullOrWhiteSpace(service))
            throw new ArgumentException("A service name is required.", nameof(service));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("An operation name is required.", nameof(operation));

        Service = service;
        Operation = operation;
        RequestTypeInfo = requestTypeInfo ??
            throw new ArgumentNullException(nameof(requestTypeInfo));
        ResponseTypeInfo = responseTypeInfo ??
            throw new ArgumentNullException(nameof(responseTypeInfo));
    }

    public string Service { get; }

    public string Operation { get; }

    public JsonTypeInfo<TRequest> RequestTypeInfo { get; }

    public JsonTypeInfo<TResponse> ResponseTypeInfo { get; }
}

/// <summary>Describes one AOT-safe RPC operation that has no response payload.</summary>
public sealed class RpcCommand<TRequest>
{
    public RpcCommand(
        string service,
        string operation,
        JsonTypeInfo<TRequest> requestTypeInfo)
    {
        if (string.IsNullOrWhiteSpace(service))
            throw new ArgumentException("A service name is required.", nameof(service));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("An operation name is required.", nameof(operation));

        Service = service;
        Operation = operation;
        RequestTypeInfo = requestTypeInfo ??
            throw new ArgumentNullException(nameof(requestTypeInfo));
    }

    public string Service { get; }

    public string Operation { get; }

    public JsonTypeInfo<TRequest> RequestTypeInfo { get; }
}
