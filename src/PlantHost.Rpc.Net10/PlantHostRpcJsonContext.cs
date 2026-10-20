using System.Text.Json.Serialization;

namespace PlantHost.Rpc;

/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ProcessHeartbeatRequest))]
[JsonSerializable(typeof(ProcessHeartbeatResponse))]
[JsonSerializable(typeof(ProcessStatusRequest))]
[JsonSerializable(typeof(ProcessStatusResponse))]
[JsonSerializable(typeof(ProcessHeartbeatStatus))]
[JsonSerializable(typeof(JobSubmitRequest))]
[JsonSerializable(typeof(JobStatusRequest))]
[JsonSerializable(typeof(JobStatusResponse))]
[JsonSerializable(typeof(JobCancelResponse))]
[JsonSerializable(typeof(JobResultResponse))]
[JsonSerializable(typeof(JobSubmission))]
[JsonSerializable(typeof(JobStatus))]
[JsonSerializable(typeof(JobId))]
internal sealed partial class PlantHostRpcJsonContext : JsonSerializerContext;
