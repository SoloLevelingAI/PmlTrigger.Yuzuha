using System.Text.Json.Serialization.Metadata;

namespace PlantHost.Rpc;

/// <summary>Represents an error returned by the remote endpoint.</summary>
public sealed class RemoteRpcException : Exception
{
    /// <summary>保存服务端返回的错误代码与可读说明。</summary>
    /// <param name="errorCode">可供程序判定的稳定错误代码。</param>
    /// <param name="message">向调用方解释失败或状态的可读信息。</param>
    public RemoteRpcException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>机器可判定的协议或执行错误代码。</summary>
    public string ErrorCode { get; }
}

/// <summary>
/// Describes one AOT-safe RPC operation. JSON metadata is supplied explicitly so
/// the client never needs reflection or runtime code generation.
/// </summary>
/// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
/// <typeparam name="TResponse">响应数据的静态类型。</typeparam>
public sealed class RpcMethod<TRequest, TResponse>
{
    /// <summary>声明一次有响应 RPC 操作的稳定路由及请求/响应 JSON 元数据。</summary>
    /// <param name="service">协议定义的稳定服务路由。</param>
    /// <param name="operation">协议定义的稳定操作路由。</param>
    /// <param name="requestTypeInfo">请求类型的编译期 JSON 元数据，避免 Native AOT 反射序列化。</param>
    /// <param name="responseTypeInfo">响应类型的编译期 JSON 元数据，避免 Native AOT 反射反序列化。</param>
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

    /// <summary>RPC 服务的稳定路由名称。</summary>
    public string Service { get; }

    /// <summary>RPC 操作的稳定路由名称。</summary>
    public string Operation { get; }

    /// <summary>请求类型的编译期 JSON 序列化元数据。</summary>
    public JsonTypeInfo<TRequest> RequestTypeInfo { get; }

    /// <summary>响应类型的编译期 JSON 反序列化元数据。</summary>
    public JsonTypeInfo<TResponse> ResponseTypeInfo { get; }
}

/// <summary>Describes one AOT-safe RPC operation that has no response payload.</summary>
/// <typeparam name="TRequest">请求数据的静态类型。</typeparam>
public sealed class RpcCommand<TRequest>
{
    /// <summary>声明一次无结果 RPC 命令的稳定路由及请求 JSON 元数据。</summary>
    /// <param name="service">协议定义的稳定服务路由。</param>
    /// <param name="operation">协议定义的稳定操作路由。</param>
    /// <param name="requestTypeInfo">请求类型的编译期 JSON 元数据，避免 Native AOT 反射序列化。</param>
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

    /// <summary>RPC 服务的稳定路由名称。</summary>
    public string Service { get; }

    /// <summary>RPC 操作的稳定路由名称。</summary>
    public string Operation { get; }

    /// <summary>请求类型的编译期 JSON 序列化元数据。</summary>
    public JsonTypeInfo<TRequest> RequestTypeInfo { get; }
}
