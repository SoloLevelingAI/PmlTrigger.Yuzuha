using System.IO;
using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Desktop;

/// <summary>桌面只保存明确目标；每次通信重新创建独立 CLI，不持有 RPC 连接。</summary>
internal sealed class DesktopCliSession(Func<string, JsonElement, OperationContext, CancellationToken, Task<CommunicationResponse>>? invoke = null)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private AvevaSessionCandidate[] discovered = [];
    private OperationContext? target;
    /// <summary>当前已绑定目标的 PID；尚未选择或身份核验失效后为 null。</summary>
    internal int? ProcessId => target?.ProcessId;
    /// <summary>通过独立通信调用刷新候选快照，供后续显式选择验证启动身份及管道。</summary>
    internal async Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token = default)
    {
        var result = Read<AvevaSessionListResponse>(await Call("aveva.sessions.list", new { }, new(), token).ConfigureAwait(false));
        discovered = result.Sessions.ToArray(); return result;
    }
    /// <summary>从最近发现的候选中选择 PID，核对 Host 回执的启动时间、管道和模块后才保存目标。</summary>
    internal async Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token)
    {
        target = null;
        var candidate = discovered.SingleOrDefault(c => c.ProcessId == pid) ?? throw new InvalidOperationException("请先发现并明确选择目标。");
        if (candidate.ProcessStartTimeUtcTicks <= 0 || !candidate.PipeDetected) throw new InvalidOperationException("发现目标缺少启动身份或专用管道。");
        // Initial read-only selection pins the Host-reported module; every later process supplies it.
        var context = new OperationContext(pid, model, ExpectedProcessStartTimeUtcTicks: candidate.ProcessStartTimeUtcTicks);
        var status = Read<YuzuhaConnectionStatus>(await Call("aveva.session.select", new { }, context, token).ConfigureAwait(false));
        if (!status.TargetVerified || !status.Connected || status.Host is not { } host || host.ProcessId != pid ||
            host.ProcessStartTimeUtcTicks != candidate.ProcessStartTimeUtcTicks || host.PipeName != candidate.PipeName || string.IsNullOrWhiteSpace(host.Model) ||
            model is not null && !model.Equals(host.Model, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("选择回执的目标身份不一致。");
        target = context with { ExpectedModel = host.Model }; return status;
    }
    /// <summary>重新核验绑定目标的连接、PID、启动时间及模块；失败时清除目标并要求重新选择。</summary>
    internal async Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token)
    {
        var expected = Target();
        try
        {
            var status = Read<YuzuhaConnectionStatus>(await Call("aveva.session.status", new { }, expected, token).ConfigureAwait(false));
            if (!status.TargetVerified || !status.Connected || status.Host is not { } host || host.ProcessId != expected.ProcessId || host.ProcessStartTimeUtcTicks != expected.ExpectedProcessStartTimeUtcTicks ||
                !string.Equals(host.Model, expected.ExpectedModel, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前目标身份已变化，请重新发现并选择。");
            return status;
        }
        catch { target = null; throw; }
    }
    /// <summary>绑定开发流程提供的完整 PID、启动时间和模块，供后续通信重新核验。</summary>
    internal void Bind(YuzuhaToolkit.Workspace.DevelopmentTarget selected)
    {
        if (selected.ProcessId <= 0 || selected.StartTicks <= 0 || string.IsNullOrWhiteSpace(selected.Model)) throw new ArgumentException("开发目标身份不完整。");
        target = new(selected.ProcessId, selected.Model, ExpectedProcessStartTimeUtcTicks: selected.StartTicks);
    }
    /// <summary>向已绑定目标发出明确允许执行的独立通信请求；没有目标时拒绝调用。</summary>
    internal Task<CommunicationResponse> InvokeAsync(string operation, object input, CancellationToken token) => Call(operation, input, Target() with { AllowExecution = true }, token);
    /// <summary>检查成功回执及非空数据后按不区分属性大小写的 JSON 契约反序列化，否则抛出通信异常。</summary>
    internal static T Read<T>(CommunicationResponse response)
    {
        if (!response.Result.Success) throw new IOException(response.Result.Error?.Code + ": " + response.Result.Error?.Message);
        if (response.Result.Data is not { } data) throw new IOException("通信回执缺少所需数据。");
        var value = data.Deserialize<T>(Json); return value is null ? throw new IOException("通信回执为空。") : value;
    }
    /// <summary>读取命令回执并保留明确失败信息；执行结果未知或成功回执缺少 Success 字段时拒绝自动重发。</summary>
    internal static RunPmlCommandResponse Receipt(CommunicationResponse response)
    {
        if (response.Result.Error?.MayHaveExecuted == true && response.Result.Data is null)
            throw new IOException(response.Result.Error.Code + ": " + response.Result.Error.Message + "；结果未知，不自动重发。");
        if (response.Result.Success && (response.Result.Data is not { ValueKind: JsonValueKind.Object } data ||
            !data.EnumerateObject().Any(p => p.Name.Equals("Success", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)))
            throw new IOException("通信缺少明确的命令回执，结果未知。");
        var receipt = response.Result.Data?.Deserialize<RunPmlCommandResponse>(Json) ?? new();
        if (!response.Result.Success) { receipt.Success = false; receipt.Code = response.Result.Error?.Code; receipt.ErrorMessage = response.Result.Error?.Message; }
        return receipt;
    }
    private OperationContext Target() => target ?? throw new InvalidOperationException("请先选择并核验目标。");
    private Task<CommunicationResponse> Call(string operation, object input, OperationContext context, CancellationToken token)
    {
        var body = JsonSerializer.SerializeToElement(input);
        return invoke is null ? CommunicationCliClient.FromEnvironment().InvokeAsync(operation, body, context, token) : invoke(operation, body, context, token);
    }
}
