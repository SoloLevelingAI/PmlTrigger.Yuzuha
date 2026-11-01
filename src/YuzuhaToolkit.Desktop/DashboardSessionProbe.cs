using System.Text.Json;
using System.IO;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Desktop;

/// <summary>仅调用只读发现/身份核验，不共享工作台的明确目标。</summary>
internal sealed class DashboardSessionProbe : IDashboardSessionProbe
{
    public async Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token)
    {
        var reply = await CommunicationCliClient.FromEnvironment().InvokeAsync("aveva.sessions.list", JsonSerializer.SerializeToElement(new { }), new(), token).ConfigureAwait(false);
        if (reply.ProtocolVersion != 1) throw new InvalidDataException("发现回执通信协议不是 V1。");
        return DesktopCliSession.Read<AvevaSessionListResponse>(reply);
    }
    public async Task<DashboardProbeReply> ProbeAsync(AvevaSessionCandidate candidate, string? expectedModule, CancellationToken token)
    {
        var context = new OperationContext(candidate.ProcessId, expectedModule, ExpectedProcessStartTimeUtcTicks: candidate.ProcessStartTimeUtcTicks);
        var reply = await CommunicationCliClient.FromEnvironment().InvokeAsync("aveva.session.select", JsonSerializer.SerializeToElement(new { }), context, token).ConfigureAwait(false);
        return new(reply.ProtocolVersion, DesktopCliSession.Read<YuzuhaConnectionStatus>(reply));
    }
}
