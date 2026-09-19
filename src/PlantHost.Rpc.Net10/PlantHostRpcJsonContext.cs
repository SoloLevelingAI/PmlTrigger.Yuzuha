using System.Text.Json.Serialization;

namespace PlantHost.Rpc;

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
