using System.Collections.Concurrent;
using System.IO;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Desktop;

internal enum DashboardSessionState { None, Checking, Detected, Available, Failed, Expired }
internal sealed record DashboardSessionHealth(int ProcessId, long StartTicks, string Product, string Project,
    string PipeName, string? Module, DashboardSessionState State, string Reason, DateTimeOffset ObservedAt,
    DateTimeOffset? VerifiedAt, bool Discovered, int FormQueryVersion = 0, int BoundedArrayReadVersion = 0)
{
    public bool IsAvailable => State == DashboardSessionState.Available;
    public string StateText => State switch { DashboardSessionState.Available => "Host 可用 · 身份已核验", DashboardSessionState.Checking => "正在只读核验", DashboardSessionState.Detected => "发现会话 · Host 未就绪", DashboardSessionState.Expired => "核验已过期", DashboardSessionState.Failed => "Host 不可用", _ => "尚未核验" };
    public string ObservedText => ObservedAt.ToLocalTime().ToString("HH:mm:ss");
}
internal sealed record DashboardHealthSnapshot(long Generation, DateTimeOffset ObservedAt,
    DashboardSessionHealth[] Sessions, string? Error = null, bool IsRefreshing = false)
{
    public int AvailableCount => Sessions.Count(row => row.IsAvailable);
    public DashboardSessionState State => AvailableCount > 0 ? DashboardSessionState.Available : Sessions.Any(row => row.State is DashboardSessionState.Failed or DashboardSessionState.Expired) ? DashboardSessionState.Failed : Error is not null || Sessions.Length > 0 ? DashboardSessionState.Detected : DashboardSessionState.None;
    public string Summary => AvailableCount > 0 ? $"{AvailableCount} 个 Host 可用 · 身份已核验" : Error ?? (Sessions.Length == 0 ? "未发现运行中的工程会话" : "已发现会话，尚无可用 Host");
    public static DashboardHealthSnapshot Empty { get; } = new(0, DateTimeOffset.MinValue, []);
}
internal sealed record DashboardProbeReply(int ProtocolVersion, YuzuhaConnectionStatus Status);
internal interface IDashboardSessionProbe
{
    Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token);
    Task<DashboardProbeReply> ProbeAsync(AvevaSessionCandidate candidate, string? expectedModule, CancellationToken token);
}

/// <summary>独立只读可用性证据，绝不选择工作台操作目标。</summary>
internal sealed class DashboardSessionMonitor(IDashboardSessionProbe probe, Func<DateTimeOffset>? clock = null,
    TimeSpan? timeout = null, TimeSpan? ttl = null) : IDisposable
{
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly TimeSpan deadline = timeout ?? TimeSpan.FromSeconds(2);
    private readonly TimeSpan freshness = ttl ?? TimeSpan.FromSeconds(10);
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentDictionary<string, (string Pipe, string Module, DateTimeOffset? Verified)> pinned = new();
    private DashboardHealthSnapshot current = DashboardHealthSnapshot.Empty;
    private int busy, disposed;
    private long generation;
    internal event Action<DashboardHealthSnapshot>? Updated;
    internal bool IsPolling => Volatile.Read(ref busy) != 0;
    internal DashboardHealthSnapshot Snapshot => Fresh(Volatile.Read(ref current));
    private static string Key(int pid, long ticks) => pid + ":" + ticks;
    private DashboardHealthSnapshot Fresh(DashboardHealthSnapshot snapshot)
    {
        var time = now();
        return snapshot with { Sessions = snapshot.Sessions.Select(row => row.IsAvailable && (row.VerifiedAt is null || time < row.VerifiedAt || time - row.VerifiedAt > freshness)
            ? row with { State = DashboardSessionState.Expired, Reason = "超过新鲜期或系统时间已变化，等待重新核验。" } : row).ToArray() };
    }
    private void Publish(DashboardHealthSnapshot snapshot)
    {
        if (Volatile.Read(ref disposed) != 0 || snapshot.Generation != Volatile.Read(ref generation)) return;
        Volatile.Write(ref current, snapshot); Updated?.Invoke(Fresh(snapshot));
    }
    internal async Task<bool> PollAsync(CancellationToken token = default)
    {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return false;
        var epoch = Interlocked.Increment(ref generation); var prior = Snapshot;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            AvevaSessionListResponse found;
            using (var discovery = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
            {
                discovery.CancelAfter(deadline);
                found = await probe.DiscoverAsync(discovery.Token).WaitAsync(deadline, cancellation.Token).ConfigureAwait(false);
            }
            if (found.Error is not null) throw new IOException(found.Error);
            if (found.Sessions.Count > 32) throw new InvalidDataException("会话数量超过只读监测预算，请在会话页明确选择。");
            var candidates = found.Sessions.GroupBy(c => Key(c.ProcessId, c.ProcessStartTimeUtcTicks)).Select(group => group.First()).ToArray();
            var ids = candidates.Select(c => Key(c.ProcessId, c.ProcessStartTimeUtcTicks)).ToHashSet();
            var missing = prior.Sessions.Where(row => row.VerifiedAt is not null && !ids.Contains(Key(row.ProcessId, row.StartTicks)))
                .Select(row => row with { State = DashboardSessionState.Failed, Reason = "此前核验的进程已消失；旧 PID/启动身份不再可用。", ObservedAt = now(), Discovered = false }).ToArray();
            var pending = candidates.Select(c => prior.Sessions.FirstOrDefault(row => row.IsAvailable && row.ProcessId == c.ProcessId && row.StartTicks == c.ProcessStartTimeUtcTicks && c.PipeDetected && row.PipeName == c.PipeName)
                ?? Row(c, DashboardSessionState.Checking, "仅核验身份与协议，不执行 PML。", null)).Concat(missing).ToArray();
            Publish(new(epoch, now(), pending, IsRefreshing: true));
            using var parallel = new SemaphoreSlim(4);
            var tasks = candidates.Select(async candidate =>
            {
                await parallel.WaitAsync(cancellation.Token).ConfigureAwait(false);
                try { return await Inspect(candidate, cancellation.Token).ConfigureAwait(false); }
                finally { parallel.Release(); }
            });
            var rows = await Task.WhenAll(tasks).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation.Token);
            Publish(new(epoch, now(), rows.Concat(missing).ToArray())); return true;
        }
        catch (Exception error)
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                var message = error is OperationCanceledException or TimeoutException ? "只读监测超时或已取消；旧核验证据已失效。" : "只读监测失败：" + error.Message;
                Publish(new(epoch, now(), prior.Sessions.Select(row => row with { State = row.VerifiedAt is null ? DashboardSessionState.Detected : DashboardSessionState.Failed, Reason = message, ObservedAt = now() }).ToArray(), message));
            }
            return false;
        }
        finally { Volatile.Write(ref busy, 0); }
    }
    private DashboardSessionHealth Row(AvevaSessionCandidate c, DashboardSessionState state, string reason, string? module,
        DateTimeOffset? verified = null, int form = 0, int array = 0) => new(c.ProcessId, c.ProcessStartTimeUtcTicks, c.Product ?? "AVEVA", c.Project ?? "", c.PipeName ?? "", module, state, reason, now(), verified ?? (pinned.TryGetValue(Key(c.ProcessId, c.ProcessStartTimeUtcTicks), out var pin) ? pin.Verified : null), true, form, array);
    private async Task<DashboardSessionHealth> Inspect(AvevaSessionCandidate candidate, CancellationToken token)
    {
        var key = Key(candidate.ProcessId, candidate.ProcessStartTimeUtcTicks); pinned.TryGetValue(key, out var pin);
        if (candidate.ProcessId <= 0 || candidate.ProcessStartTimeUtcTicks <= 0 || string.IsNullOrWhiteSpace(candidate.PipeName)) return Row(candidate, DashboardSessionState.Failed, "候选缺少 PID、启动时间或专用管道身份。", pin.Module);
        if (!candidate.PipeDetected) return Row(candidate, pin.Module is null ? DashboardSessionState.Detected : DashboardSessionState.Failed, "发现工程进程，但未发现该进程的 Host 专用管道。", pin.Module);
        if (pin.Pipe is not null && pin.Pipe != candidate.PipeName) return Row(candidate, DashboardSessionState.Failed, "同一进程启动身份的管道发生变化，旧证据已撤销。", pin.Module);
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token); cancellation.CancelAfter(deadline);
            var reply = await probe.ProbeAsync(candidate, pin.Module, cancellation.Token).WaitAsync(deadline, token).ConfigureAwait(false);
            var status = reply.Status; var host = status.Host;
            if (reply.ProtocolVersion != 1 || !status.Connected || !status.TargetVerified || host is null || host.ProcessId != candidate.ProcessId || host.ProcessStartTimeUtcTicks != candidate.ProcessStartTimeUtcTicks || host.PipeName != candidate.PipeName || string.IsNullOrWhiteSpace(host.Model) || status.ExpectedProcessId != candidate.ProcessId || status.ExpectedProcessStartTimeUtcTicks != candidate.ProcessStartTimeUtcTicks || status.PipeName != candidate.PipeName || !string.Equals(status.ExpectedModel, host.Model, StringComparison.OrdinalIgnoreCase))
                return Row(candidate, DashboardSessionState.Failed, status.Error ?? "Host 回执的 PID、启动身份、管道或通信协议不匹配。", pin.Module);
            if (pin.Module is not null && !string.Equals(pin.Module, host.Model, StringComparison.OrdinalIgnoreCase)) return Row(candidate, DashboardSessionState.Failed, "同一启动身份的 Host 模块已改变；不会悄悄接受新模块。", pin.Module);
            if (token.IsCancellationRequested || Volatile.Read(ref disposed) != 0) throw new OperationCanceledException(token);
            pinned.TryAdd(key, (candidate.PipeName, host.Model, null));
            if (host.FormQueryVersion != 1 || host.BoundedArrayReadVersion != 1) return Row(candidate, DashboardSessionState.Detected, "Host 身份已返回，但所需 FormQuery/BoundedArrayRead V1 协议未满足。", host.Model, form: host.FormQueryVersion, array: host.BoundedArrayReadVersion);
            pinned[key] = (candidate.PipeName, host.Model, now());
            return Row(candidate, DashboardSessionState.Available, "本轮只读回执核对 PID、启动时间、管道、模块及 V1 协议；不代表全部业务通过。", host.Model, now(), host.FormQueryVersion, host.BoundedArrayReadVersion);
        }
        catch (Exception error) { return Row(candidate, pin.Module is null ? DashboardSessionState.Detected : DashboardSessionState.Failed, error is OperationCanceledException or TimeoutException ? "Host 只读核验超时/取消，未保持旧绿。" : "Host 只读核验失败：" + error.Message, pin.Module); }
    }
    public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) != 0) return; Interlocked.Increment(ref generation); lifetime.Cancel(); }
}
