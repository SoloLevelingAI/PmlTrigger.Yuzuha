using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Communication;

namespace YuzuhaToolkit.Plugins;

/// <summary>可选工作进程的实际版本与声明操作；不加载管理器或 AVEVA 程序集。</summary>
public sealed record PluginCapabilities(string PluginId, string Version, int ContractVersion, OperationDescriptor[] Operations);
/// <summary>插件发行清单；文件指纹涵盖工作进程及其所有依赖，清单自身由登记独立固定。</summary>
public sealed record ManagerPluginManifest(int SchemaVersion, string Id, string Version, int ContractVersion,
    string EntryPoint, Dictionary<string, string> Files, OperationDescriptor[] Operations);

/// <summary>Native AOT 插件共享的编译期 JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(PluginCapabilities))]
[JsonSerializable(typeof(ManagerPluginManifest))]
public partial class PluginJsonContext : JsonSerializerContext;

/// <summary>复用中立操作信封的单次插件进程入口；每次都从 stdin 取得完整上下文。</summary>
public static class PluginWorkerHost
{
    /// <summary>只接受已编译进工作进程的能力，不从请求中获取插件身份或操作清单。</summary>
    public static async Task<int> RunAsync(PluginCapabilities capabilities, IEnumerable<OperationRegistration> registrations,
        TextReader input, TextWriter output, CancellationToken token = default)
    {
        CommunicationRequest? request = null; OperationResult result;
        try
        {
            var buffer = new char[4096]; var text = new StringBuilder(); int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
            { if (text.Length + count > CommunicationProtocol.MaxCharacters) throw new ArgumentException("插件请求超过预算。"); text.Append(buffer, 0, count); }
            request = JsonSerializer.Deserialize(text.ToString(), CommunicationJsonContext.Default.CommunicationRequest) ?? throw new ArgumentException("缺少插件请求。");
            if (request.ProtocolVersion != 1 || request.Context is null || !Guid.TryParseExact(request.RequestId, "N", out _) || request.Input.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("插件进程请求契约不匹配。");
            if (request.OperationId == "plugin.capabilities") result = OperationResult.Ok(request.OperationId,
                JsonSerializer.SerializeToElement(capabilities, PluginJsonContext.Default.PluginCapabilities));
            else
            {
                if (!request.OperationId.StartsWith(capabilities.PluginId + ".", StringComparison.Ordinal)) throw new ArgumentException("操作不属于此插件。");
                var executor = new OperationExecutor(new OperationRegistry(registrations));
                result = await executor.InvokeAsync(request.OperationId, request.Input, request.Context, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { result = OperationResult.Fail(request?.OperationId ?? "plugin.request", 8, "CANCELLED", "插件等待取消，不自动重发。", mayHaveExecuted: request?.Context?.AllowExecution == true || request?.Context?.AllowLaunch == true); }
        catch (Exception error) when (error is ArgumentException or JsonException) { result = OperationResult.Fail(request?.OperationId ?? "plugin.request", 2, "INVALID_PLUGIN_REQUEST", Message(error)); }
        catch (Exception error) { result = OperationResult.Fail(request?.OperationId ?? "plugin.request", 9, "PLUGIN_WORKER_FAILED", Message(error), mayHaveExecuted: request?.Context?.AllowExecution == true || request?.Context?.AllowLaunch == true); }
        await output.WriteLineAsync(JsonSerializer.Serialize(new CommunicationResponse(1, request?.RequestId ?? "invalid", result, capabilities.Version), CommunicationJsonContext.Default.CommunicationResponse)).ConfigureAwait(false);
        return result.ExitCode;
    }
    private static string Message(Exception error) => string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
}
