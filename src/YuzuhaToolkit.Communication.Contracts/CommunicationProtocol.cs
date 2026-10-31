using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Communication;

/// <summary>管理器与独立通信 CLI 的单请求进程协议。</summary>
public static class CommunicationProtocol
{
    /// <summary>当前支持的协议版本。</summary>
    public const int Version = 1;
    /// <summary>UTF-8 请求及响应的最大字符数；诊断流单独限制。</summary>
    public const int MaxCharacters = 2 * 1024 * 1024;
    /// <summary>只允许通信及通信诊断操作；管理操作使用管理器入口。</summary>
    public static bool IsCommunicationOperation(string id) =>
        id.StartsWith("aveva.", StringComparison.Ordinal) ||
        id.StartsWith("pml.", StringComparison.Ordinal) ||
        id is "builtin.usage" or "runtime.status" or "runtime.calls" or "communication.capabilities";
}

/// <summary>带关联标识、版本和明确目标的单次请求。</summary>
public sealed record CommunicationRequest(int ProtocolVersion, string RequestId,
    string OperationId, JsonElement Input, OperationContext Context);

/// <summary>保留原操作 JSON/退出码的通信回执。</summary>
public sealed record CommunicationResponse(int ProtocolVersion, string RequestId,
    OperationResult Result, string ServerVersion);

/// <summary>不接触 AVEVA 的能力握手信息。</summary>
public sealed record CommunicationCapabilities(int ProtocolVersion, string ServerVersion,
    string Transport, OperationDescriptor[] Operations);

/// <summary>协议序列化元数据，可由 Native AOT 通信进程使用。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CommunicationRequest))]
[JsonSerializable(typeof(CommunicationResponse))]
[JsonSerializable(typeof(CommunicationCapabilities))]
public partial class CommunicationJsonContext : JsonSerializerContext;
