using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>独立通信 CLI 的版本化单次入口；不加载管理操作或启动 GUI。</summary>
public static class CommunicationCliEndpoint
{
    /// <summary>读取一份请求并返回一份关联回执；读取与检查本身不执行 PML。</summary>
    public static async Task<int> RunAsync(TextReader input, TextWriter output, CancellationToken token = default)
    {
        CommunicationRequest? request = null;
        OperationResult result;
        var version = typeof(CommunicationCliEndpoint).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        try
        {
            var text = new StringBuilder(); var buffer = new char[4096]; int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
            {
                if (text.Length + count > CommunicationProtocol.MaxCharacters) throw new ArgumentException("请求超过通信预算。");
                text.Append(buffer, 0, count);
            }
            request = JsonSerializer.Deserialize(text.ToString(), CommunicationJsonContext.Default.CommunicationRequest)
                ?? throw new ArgumentException("缺少通信请求。");
            if (request.ProtocolVersion != CommunicationProtocol.Version)
                throw new ArgumentException("不支持的通信协议版本。");
            if (!Guid.TryParseExact(request.RequestId, "N", out _) || request.Context is null ||
                string.IsNullOrWhiteSpace(request.OperationId) || request.Input.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("请求标识、上下文或输入无效。");
            if (!CommunicationProtocol.IsCommunicationOperation(request.OperationId))
                throw new ArgumentException("管理操作请通过管理器 CLI 执行。");
            if (request.Context.ProcessId is not null && request.Context.ExpectedProcessStartTimeUtcTicks is not > 0)
                throw new ArgumentException("目标调用需要本次发现的进程启动时间，不能只指定 PID。");
            using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery())
            { RequiredProcessStartTimeUtcTicks = request.Context.ExpectedProcessStartTimeUtcTicks };
            using var journal = new RuntimeJournal("Yuzuha communication CLI");
            var executor = CommunicationOperationCatalog.Create(bridge, new PmlFunctionTrustStore(), bridge.CreateIndependent, journal: journal);
            var rejected = executor.Registry.TryGet(request.OperationId, out var registration)
                ? CommunicationOperationCatalog.ValidatePinnedExecutionTarget(request.OperationId,
                    registration!.Descriptor.RequiresPid && registration.Descriptor.RequiresExecution, request.Context)
                : null;
            result = rejected ?? (request.OperationId == "communication.capabilities"
                ? OperationResult.Ok(request.OperationId, JsonSerializer.SerializeToElement(new CommunicationCapabilities(
                    CommunicationProtocol.Version, version, "single-request-stdio-json",
                    executor.Registry.Descriptors.Where(d => CommunicationProtocol.IsCommunicationOperation(d.Id)).ToArray()),
                    CommunicationJsonContext.Default.CommunicationCapabilities))
                : await executor.InvokeAsync(request.OperationId, request.Input, request.Context, token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        { result = OperationResult.Fail(request?.OperationId ?? "communication.request", 8, "CANCELLED", "等待已取消；不自动重发。", mayHaveExecuted: request?.Context?.AllowExecution == true); }
        catch (Exception error) when (error is JsonException or ArgumentException)
        { result = OperationResult.Fail(request?.OperationId ?? "communication.request", 2, "INVALID_PROTOCOL_REQUEST", error.Message); }
        catch (Exception error)
        { result = OperationResult.Fail(request?.OperationId ?? "communication.request", 9, "COMMUNICATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message, mayHaveExecuted: request?.Context?.AllowExecution == true); }
        await output.WriteLineAsync(JsonSerializer.Serialize(new CommunicationResponse(CommunicationProtocol.Version,
            request?.RequestId ?? "invalid", result, version), CommunicationJsonContext.Default.CommunicationResponse)).ConfigureAwait(false);
        return result.ExitCode;
    }
}
