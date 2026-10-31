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
    internal int? ProcessId => target?.ProcessId;
    internal async Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token = default)
    {
        var result = Read<AvevaSessionListResponse>(await Call("aveva.sessions.list", new { }, new(), token).ConfigureAwait(false));
        discovered = result.Sessions.ToArray(); return result;
    }
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
    internal void Bind(YuzuhaToolkit.Workspace.DevelopmentTarget selected)
    {
        if (selected.ProcessId <= 0 || selected.StartTicks <= 0 || string.IsNullOrWhiteSpace(selected.Model)) throw new ArgumentException("开发目标身份不完整。");
        target = new(selected.ProcessId, selected.Model, ExpectedProcessStartTimeUtcTicks: selected.StartTicks);
    }
    internal Task<CommunicationResponse> InvokeAsync(string operation, object input, CancellationToken token) => Call(operation, input, Target() with { AllowExecution = true }, token);
    internal static T Read<T>(CommunicationResponse response)
    {
        if (!response.Result.Success) throw new IOException(response.Result.Error?.Code + ": " + response.Result.Error?.Message);
        if (response.Result.Data is not { } data) throw new IOException("通信回执缺少所需数据。");
        var value = data.Deserialize<T>(Json); return value is null ? throw new IOException("通信回执为空。") : value;
    }
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
