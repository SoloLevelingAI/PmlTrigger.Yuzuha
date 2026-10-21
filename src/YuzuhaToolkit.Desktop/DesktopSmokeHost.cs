using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Desktop;

/// <summary>仅 --smoke-output 使用的离线界面夹具；不会发现或连接 AVEVA。</summary>
internal sealed class DesktopSmokeHost : IWorkbenchHost
{
    /// <summary>仅合成界面验证模拟发送后丢失回执。</summary>
    internal bool LoseInvocationReply { get; set; }
    private readonly AvevaSessionCandidate candidate = new() { ProcessId = 12345, ProcessStartTimeUtcTicks = DateTime.UtcNow.AddHours(-1).Ticks, Product = "E3D 2.1 · UI 合成夹具", Project = "非真实工程", WindowTitle = "离线界面验证，不是 AVEVA 实机", PipeDetected = true, PipeName = "synthetic-only" };
    /// <summary>返回一个明确标记为合成的窗口。</summary>
    public AvevaSessionListResponse Discover() => new() { Count = 1, Sessions = [candidate] };
    /// <summary>模拟显式核验后的既有响应。</summary>
    public Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token) => StatusAsync(token);
    /// <summary>模拟支持两项协议的 NET48 Host。</summary>
    public Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token) => Task.FromResult(new YuzuhaConnectionStatus { SessionSelected = true, TargetVerified = true, Connected = true, SelectedSession = candidate, ExpectedProcessId = candidate.ProcessId, ExpectedProcessStartTimeUtcTicks = candidate.ProcessStartTimeUtcTicks, ExpectedModel = "Design", Host = new() { ProcessId = candidate.ProcessId, ProcessStartTimeUtcTicks = candidate.ProcessStartTimeUtcTicks, Model = "Design", HostFramework = "NET48 · 合成", HostVersion = "UI fixture", FormQueryVersion = 1, BoundedArrayReadVersion = 1 } });
    /// <summary>为三栏布局提供固定元数据，不执行 PML。</summary>
    public Task<RunPmlCommandResponse> QueryAsync(PmlFormQueryRequest request, CancellationToken token)
    {
        var item = new PmlFormQueryItem { Index = 1, Name = request.Kind is "forms" or "shown-forms" ? "UiProbeForm" : "accept", Type = request.Kind is "forms" or "shown-forms" ? "FORM" : "BUTTON", Reference = "UI fixture only", Properties = request.Kind == "gadget" ? [new() { Name = "shown", Status = "available", Value = "TRUE" }, new() { Name = "active", Status = "available", Value = "TRUE" }, new() { Name = "visible", Status = "available", Value = "TRUE" }, new() { Name = "tooltip", Status = "available", Value = "" }, new() { Name = "callback", Status = "unavailable", Error = "合成：此属性不支持读取" }] : [] };
        return Task.FromResult(new RunPmlCommandResponse { Success = true, RequestId = "ui-fixture-" + request.Kind, ServerRuntime = "synthetic", FormQueryResult = new() { Kind = request.Kind, StartIndex = request.StartIndex, Scanned = 3, IndexUpperBound = 3, Items = [item] } });
    }
    /// <summary>使用共享有界策略提供数组夹具。</summary>
    public Task<RunPmlCommandResponse> ReadArrayAsync(string name, int start, int items, int chars, CancellationToken token) => Task.FromResult(new RunPmlCommandResponse { Success = true, RequestId = "ui-fixture-array", ArrayWindow = PmlArrayWindow.Read(3, start, items, chars, i => "合成行 " + i) });
    /// <summary>只返回合成调度回执，不执行函数。</summary>
    public Task<RunPmlCommandResponse> InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token) => LoseInvocationReply ? throw new System.IO.IOException("合成：发送后丢失调用回执") : Task.FromResult(new RunPmlCommandResponse { Success = true, RequestId = "ui-fixture-call", ServerRuntime = "synthetic" });
}
