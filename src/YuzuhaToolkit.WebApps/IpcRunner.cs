using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.WebApps;

/// <summary>网页选择的会话身份；后续请求必须继续匹配同一 PID 和启动时间。</summary>
public sealed record SessionPin(int ProcessId,
    [property: System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString |
        System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)] long StartTicks, string Model);

/// <summary>隔离真实 IPC 与离线测试，保持既有操作协议不变。</summary>
public interface IWebIpc
{
    /// <summary>只读发现会话。</summary>
    AvevaSessionListResponse Discover();
    /// <summary>核验所选会话并返回 Host 模块和进程启动身份。</summary>
    Task<SessionPin> SelectAsync(int processId, CancellationToken token);
    /// <summary>在固定会话中执行既有操作一次，不重试。</summary>
    Task<OperationResult> InvokeAsync(string operation, JsonElement input, SessionPin pin, CancellationToken token);
}

/// <summary>复用 YuzuhaOperationCatalog 与原有 Named Pipe/Host，不增加 E3D 命令或协议。</summary>
public sealed class ExistingIpc : IWebIpc
{
    private readonly AvevaSessionDiscovery discovery = new();
    /// <inheritdoc />
    public AvevaSessionListResponse Discover() => discovery.Discover();
    /// <inheritdoc />
    public async Task<SessionPin> SelectAsync(int processId, CancellationToken token)
    {
        using var bridge = NewBridge();
        var status = await bridge.SelectSessionAsync(processId, null, token);
        if (!status.TargetVerified || status.Host?.Model is null) throw new InvalidOperationException("会话未通过核验。");
        return new(processId, status.ExpectedProcessStartTimeUtcTicks, status.Host.Model);
    }
    /// <inheritdoc />
    public async Task<OperationResult> InvokeAsync(string operation, JsonElement input, SessionPin pin, CancellationToken token)
    {
        using var bridge = NewBridge();
        var status = await bridge.SelectSessionAsync(pin.ProcessId, pin.Model, token);
        if (status.ExpectedProcessStartTimeUtcTicks != pin.StartTicks)
            return OperationResult.Fail(operation, 5, "TARGET_CHANGED", "会话身份变化，请重新连接。");
        // The selected bridge verifies its pinned identity again before dispatch.
        var backend = new YuzuhaOperationBackend(bridge, new PmlFunctionTrustStore());
        var json = operation switch
        {
            YuzuhaOperationIds.Execute => await backend.RunPmlCommand(input.GetProperty("pmlCommand").GetString()!, token),
            YuzuhaOperationIds.ReadArray => await backend.RunPmlCommandList(input.GetProperty("pmlCommand").GetString()!,
                input.GetProperty("globalVar").GetString()!, input.GetProperty("deleteGlobalVar").GetBoolean(), true, token),
            _ => throw new ArgumentException("网页桥不支持该操作。")
        };
        if (json.StartsWith("PML RPC failed:", StringComparison.Ordinal))
            return OperationResult.Fail(operation, 7, "RPC_FAILED", json, mayHaveExecuted: true);
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.Clone();
        return data.GetProperty("Success").GetBoolean() ? OperationResult.Ok(operation, data)
            : OperationResult.Fail(operation, 6, "HOST_FAILED", data.GetProperty("ErrorMessage").GetString() ?? "PML 失败。", data);
    }
    private YuzuhaRpcBridge NewBridge() => new(NullLogger<YuzuhaRpcBridge>.Instance, discovery);
}
