using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>发现契约的 Native AOT JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LaunchDiscoveryRequest))]
[JsonSerializable(typeof(LaunchDiscoveryResult))]
public partial class LaunchDiscoveryJsonContext : JsonSerializerContext;

/// <summary>免交互环境发现操作，HTML 与 CLI 使用同一服务和结构化结果。</summary>
public static class LaunchDiscoveryOperations
{
    /// <summary>建立不要求写入或启动授权的发现和解析描述。</summary>
    public static IEnumerable<OperationDescriptor> Descriptors()
    {
        using var schema = JsonDocument.Parse("""
            {"type":"object","additionalProperties":false,"properties":{
              "roots":{"type":"array","minItems":1,"maxItems":32,"items":{"type":"string","minLength":1}},
              "product":{"type":"string","enum":["E3D","AM","PDMS"]},"version":{"type":"string"},
              "maximumDepth":{"type":"integer","minimum":0,"maximum":8},
              "maximumFiles":{"type":"integer","minimum":1,"maximum":300000},
              "timeoutSeconds":{"type":"integer","minimum":1,"maximum":120},
              "candidateId":{"type":"string"},"preferredExecutable":{"type":"string"},
              "overrides":{"type":"object","additionalProperties":false,"properties":{
                "project":{"type":"string"},"module":{"type":"string"},"user":{"type":"string"},"mdb":{"type":"string"},
                "workingDirectory":{"type":"string"},"initialization":{"type":"string"}}}}}
            """);
        foreach (var id in new[] { "environment.discover", "environment.resolve" })
            yield return new(id, 1, "Read-only bounded environment discovery and explicit override resolution; returns needsInput without prompting or launching.", schema.RootElement.Clone());
    }

    /// <summary>建立可注入范围来源的统一注册项，避免界面复制解析规则。</summary>
    public static IEnumerable<OperationRegistration> Registrations(LaunchDiscoveryService? service = null)
    {
        var discovery = service ?? new();
        var handler = new DelegateOperationHandler((id, input, _, token) =>
        {
            try
            {
                var request = JsonSerializer.Deserialize(input, LaunchDiscoveryJsonContext.Default.LaunchDiscoveryRequest)!;
                var result = discovery.Discover(request, token);
                var data = JsonSerializer.SerializeToElement(result, LaunchDiscoveryJsonContext.Default.LaunchDiscoveryResult);
                return Task.FromResult(result.NeedsInput.Length == 0 ? OperationResult.Ok(id, data)
                    : OperationResult.Fail(id, 2, "ENVIRONMENT_NEEDS_INPUT", "请选择未解决字段；候选和原因见 data。", data));
            }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException)
            { return Task.FromResult(OperationResult.Fail(id, 2, "ENVIRONMENT_DISCOVERY_FAILED", error.Message)); }
        });
        return Descriptors().Select(descriptor => new OperationRegistration(descriptor, handler));
    }
}
