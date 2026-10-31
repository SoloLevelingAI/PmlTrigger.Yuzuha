using System.Text.Json;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>开发流程保持同一 PID/启动时间/模块；每一步经独立通信 CLI 重新核验。</summary>
internal sealed class DesktopDevelopmentHost : IPmlDevelopmentHost, IDisposable
{
    private readonly DevelopmentTarget expected;
    private readonly DesktopCliSession session;
    /// <summary>保存开发目标并绑定到给定或新建的独立 CLI 会话；不在构造期间连接 Host。</summary>
    internal DesktopDevelopmentHost(DevelopmentTarget expected, DesktopCliSession? channel = null)
    { this.expected = expected; session = channel ?? new(); session.Bind(expected); }
    /// <inheritdoc />
    public async Task<DevelopmentTarget> VerifyAsync(CancellationToken token)
    {
        var status = await session.StatusAsync(token).ConfigureAwait(false);
        if (!status.TargetVerified || status.Host is not { } host || host.ProcessId != expected.ProcessId || host.ProcessStartTimeUtcTicks != expected.StartTicks ||
            !expected.Model.Equals(host.Model, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("开发会话身份已变化。");
        return new(host.ProcessId, host.ProcessStartTimeUtcTicks, host.Model!);
    }
    /// <inheritdoc />
    public async Task<DevelopmentReply> ExecuteAsync(string command, CancellationToken token) => Parse(
        await session.InvokeAsync("pml.command.execute", new { pmlCommand = command }, token).ConfigureAwait(false));
    /// <inheritdoc />
    public async Task<DevelopmentRows> ReadAsync(string global, CancellationToken token)
    {
        var reply = Parse(await session.InvokeAsync("pml.array.read", new { pmlCommand = "!!" + global, globalVar = global, deleteGlobalVar = true, includeEmpty = true }, token).ConfigureAwait(false));
        if (!reply.Success) return new(reply, 0, []);
        return new(reply, reply.Raw.GetProperty("UnparsedCount").GetInt32(), reply.Raw.GetProperty("Items").EnumerateArray()
            .Select(i => new DevelopmentValue(i.GetProperty("Path").GetString()!, i.GetProperty("Value").ValueKind == JsonValueKind.Null ? null : i.GetProperty("Value").GetString())).ToArray());
    }
    private static DevelopmentReply Parse(YuzuhaToolkit.Communication.CommunicationResponse response)
    {
        var data = response.Result.Data ?? JsonSerializer.SerializeToElement(response.Result);
        return new(response.Result.Success, data, response.Result.Error?.Message, response.Result.Error?.MayHaveExecuted == true);
    }
    /// <inheritdoc />
    public void Dispose() { }
}
