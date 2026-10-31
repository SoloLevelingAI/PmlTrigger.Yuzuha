using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LaunchDesk.UseCases;

/// <summary>按冻结资源身份和版本化策略调度探测及发布监测投影。</summary>
/// <param name="probe">最近探测快照/可替换探针端口</param>
/// <param name="policy">纯健康状态归约规则。</param>
/// <param name="store">配置存储端口。</param>
/// <param name="timeProvider">可替换的时间源；空值使用系统时钟。</param>
public sealed class ResourceSupervisor(
    IResourceProbe probe,
    ConnectionHealthPolicy policy,
    IWorkspaceStore store,
    TimeProvider? timeProvider = null) : IResourceMonitor
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, ProbeSnapshot> cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();
    private readonly ConcurrentDictionary<Guid, Channel<ProbeSnapshot>> subscribers = new();

    /// <inheritdoc />
    public async ValueTask<Result<ProbeSnapshot>> RefreshAsync(Guid resourceId, CancellationToken ct)
    {
        var read = await store.ReadAsync(ct);
        if (!read.IsSuccess) return Result<ProbeSnapshot>.Fail(read.Error!);
        var resource = read.Value.LicenseEndpoints.FirstOrDefault(l => l.Id == resourceId)?.Resource;
        return resource is null
            ? Result<ProbeSnapshot>.Fail("ResourceNotFound", "许可连接已不存在。")
            : await RefreshFrozenAsync(resource, ct);
    }

    /// <inheritdoc />
    public async ValueTask<Result<ProbeSnapshot>> RequireFreshAsync(ResourceDescriptor expected, CancellationToken ct)
    {
        if (cache.TryGetValue(expected.IdentityKey, out var snapshot) &&
            snapshot.IsFreshFor(expected, clock.GetUtcNow())) return Result<ProbeSnapshot>.Ok(snapshot);
        var refreshed = await RefreshFrozenAsync(expected, ct);
        return refreshed.IsSuccess && refreshed.Value.IsFreshFor(expected, clock.GetUtcNow())
            ? refreshed
            : Result<ProbeSnapshot>.Fail(refreshed.Error ??
                                         new Error("ResourceNotReady", "所选许可尚未取得当前端点的就绪连通证据。", true));
    }

    private async ValueTask<Result<ProbeSnapshot>> RefreshFrozenAsync(ResourceDescriptor resource, CancellationToken ct)
    {
        var gate = gates.GetOrAdd(resource.IdentityKey, _ => new SemaphoreSlim(1));
        await gate.WaitAsync(ct);
        try
        {
            var previous = cache.GetValueOrDefault(resource.IdentityKey) ?? ProbeSnapshot.Unknown(resource);
            var observations = ImmutableArray.CreateBuilder<ProbeObservation>();
            if (!resource.Enabled)
                observations.Add(new ProbeObservation(resource, clock.GetUtcNow(), clock.GetUtcNow(), []));
            for (var i = 0; resource.Enabled && i < resource.Policy.AttemptsPerRound; i++)
            {
                var started = clock.GetUtcNow();
                try
                {
                    var result = await probe.ObserveAsync(resource, ct).AsTask()
                        .WaitAsync(TimeSpan.FromMilliseconds(resource.Policy.TimeoutMilliseconds), clock, ct);
                    if (result.IsSuccess && result.Value.Resource.IdentityKey == resource.IdentityKey)
                        observations.Add(result.Value);
                    else observations.Add(Failed(resource, started, result.Error?.Code ?? "EvidenceIdentityMismatch"));
                }
                catch (TimeoutException)
                {
                    observations.Add(Failed(resource, started, "ProbeTimeout"));
                }

                if (observations.Last().ConnectivityReady) break;
                if (i + 1 < resource.Policy.AttemptsPerRound)
                    await Task.Delay(TimeSpan.FromMilliseconds(resource.Policy.RetryDelayMilliseconds), clock, ct);
            }

            var snapshot = policy.Reduce(previous,
                new ProbeRound(resource, previous.RoundSequence + 1, observations.ToImmutable()), clock.GetUtcNow());
            cache[resource.IdentityKey] = snapshot;
            foreach (var channel in subscribers.Values) channel.Writer.TryWrite(snapshot);
            return Result<ProbeSnapshot>.Ok(snapshot);
        }
        finally
        {
            gate.Release();
        }
    }

    private ProbeObservation Failed(ResourceDescriptor resource, DateTimeOffset started, string code)
    {
        return new ProbeObservation(resource, started, clock.GetUtcNow(),
            [new ProbeEvidence(EvidenceKind.Tcp, resource.Host, false, code, "本轮探测尚未确认连通，等待复查。")]);
    }

    /// <summary>按当前工作空间策略逐资源轮询，独立归约并发布分项证据；直到观察令牌取消。</summary>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var snapshot = await store.ReadAsync(ct);
            if (snapshot.IsSuccess)
                await Task.WhenAll(
                    snapshot.Value.LicenseEndpoints.Select(l => RefreshFrozenAsync(l.Resource, ct).AsTask()));
            var delay = snapshot.IsSuccess && snapshot.Value.LicenseEndpoints.Length > 0
                ? snapshot.Value.LicenseEndpoints.Min(l => l.Resource.Policy.PollSeconds)
                : 5;
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, delay)), clock, ct);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProbeSnapshot> WatchAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<ProbeSnapshot>(new BoundedChannelOptions(256)
            { FullMode = BoundedChannelFullMode.DropOldest });
        subscribers[id] = channel;
        try
        {
            foreach (var snapshot in cache.Values) yield return snapshot;
            await foreach (var snapshot in channel.Reader.ReadAllAsync(ct)) yield return snapshot;
        }
        finally
        {
            subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }
}
