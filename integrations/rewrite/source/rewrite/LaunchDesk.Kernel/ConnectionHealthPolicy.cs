using System.Collections.Immutable;

namespace LaunchDesk.Kernel;

/// <summary>定义按版本、端点、重试轮次和证据时效判断连接健康的纯策略参数。</summary>
/// <param name="Version">版本名称或策略版本</param>
/// <param name="AttemptsPerRound">每轮探测的最大尝试次数</param>
/// <param name="TimeoutMilliseconds">单次探测超时毫秒数</param>
/// <param name="RetryDelayMilliseconds">同轮失败重试间隔毫秒数</param>
/// <param name="FailedRoundsToNotify">确认掉线并通知前要求的连续失败轮数</param>
/// <param name="SuccessfulRoundsToRecover">恢复就绪所需的连续成功轮数</param>
/// <param name="FreshnessSeconds">就绪事实的有效秒数</param>
/// <param name="PollSeconds">后台探测轮询间隔秒数</param>
public sealed record ProbePolicy(
    int Version = 1,
    int AttemptsPerRound = 3,
    int TimeoutMilliseconds = 2000,
    int RetryDelayMilliseconds = 300,
    int FailedRoundsToNotify = 3,
    int SuccessfulRoundsToRecover = 1,
    int FreshnessSeconds = 15,
    int PollSeconds = 5);

/// <summary>不可变资源身份含资源ID、修订、机器、探测种类及端点/服务/VM选择参数；描述摘要按版本化规范编码计算，不含秘密。</summary>
/// <param name="ResourceId">许可资源的稳定标识</param>
/// <param name="ResourceRevision">资源描述的修订号</param>
/// <param name="MachineId">拥有本机绑定或执行权的机器标识</param>
/// <param name="Host">许可主机名或 IP 地址</param>
/// <param name="RmsPort">RMS TCP 端口</param>
/// <param name="RequireRules">是否要求 RULES 连通证据</param>
/// <param name="RulesPort">RULES TCP 端口</param>
/// <param name="ServiceName">可选的本机 RMS 服务名称</param>
/// <param name="VmPath">可选 VMware 虚拟机文件路径</param>
/// <param name="Enabled">是否启用该资源探测</param>
/// <param name="Policy">与当前资源身份共同参与健康判断的探测策略。</param>
public sealed record ResourceDescriptor(
    Guid ResourceId,
    Revision ResourceRevision,
    Guid MachineId,
    string Host,
    int RmsPort,
    bool RequireRules,
    int RulesPort,
    string? ServiceName,
    string? VmPath,
    bool Enabled,
    ProbePolicy Policy)
{
    /// <summary>完整资源描述摘要含端点、机器、修订及策略，不含秘密；用于拒绝异版本探测证据。</summary>
    public string DescriptorDigest => CanonicalDigest.Of(new
    {
        ResourceId, ResourceRevision, MachineId, Host, RmsPort, RequireRules, RulesPort, ServiceName, VmPath, Enabled,
        Policy
    });

    /// <summary>缓存/并发身份键由资源ID、修订、机器、描述摘要及策略版本组成；同名资源的异配置不共享探测状态。</summary>
    public string IdentityKey =>
        $"{ResourceId:N}/{ResourceRevision.Value}/{MachineId:N}/{DescriptorDigest}/{Policy.Version}";
}

/// <summary>一项服务、端口或虚拟机观察证据；未知与失败分别记录，说明不含秘密。</summary>
/// <param name="Kind">本项证据来自服务、TCP 还是虚拟机观察。</param>
/// <param name="Target">探测目标或配置目标路径</param>
/// <param name="Success">是否取得肯定证据；未知时为空</param>
/// <param name="ReasonCode">稳定且不含秘密的原因码</param>
/// <param name="SafeMessage">可展示且无秘密的说明</param>
public sealed record ProbeEvidence(
    EvidenceKind Kind,
    string Target,
    bool? Success,
    string ReasonCode,
    string SafeMessage);

/// <summary>不可变单次观测含完整资源身份键、实际探测来源、起止时间和分项事实；网络探测另含实际解析地址证据。</summary>
/// <param name="Resource"><include file="../Comments.xml" path="doc/Resource/*"/></param>
/// <param name="StartedAtUtc">开始时间，使用 UTC</param>
/// <param name="CompletedAtUtc">探测完成的 UTC 时间</param>
/// <param name="Evidence">分项探测或观察证据</param>
public sealed record ProbeObservation(
    ResourceDescriptor Resource,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    ImmutableArray<ProbeEvidence> Evidence)
{
    /// <summary>ConnectivityReady。</summary>
    public bool ConnectivityReady => Evidence.Where(e => e.Kind is EvidenceKind.Tcp or EvidenceKind.LocalService).Any()
                                     && Evidence.Where(e => e.Kind is EvidenceKind.Tcp or EvidenceKind.LocalService)
                                         .All(e => e.Success == true);
}

/// <summary>冻结同一完整资源身份键、策略版本、轮次序号和同身份观测集合。</summary>
/// <param name="Resource"><include file="../Comments.xml" path="doc/Resource/*"/></param>
/// <param name="Sequence">持久操作事件的单调序号</param>
/// <param name="Observations">同一资源本轮取得的尝试结果</param>
public sealed record ProbeRound(
    ResourceDescriptor Resource,
    long Sequence,
    ImmutableArray<ProbeObservation> Observations);

/// <summary>除状态、证据和时间外，携带资源修订、机器、描述摘要、策略版本及轮次；分项证据须同属该资源身份键。</summary>
/// <param name="Resource"><include file="../Comments.xml" path="doc/Resource/*"/></param>
/// <param name="RoundSequence">该端点探测结果的单调轮次序号</param>
/// <param name="State">当前持久回执、提交或资源状态。</param>
/// <param name="ObservedAtUtc">事实观察时的 UTC 时间</param>
/// <param name="ValidUntilUtc">就绪事实的过期 UTC 时间</param>
/// <param name="LastConfirmedReadyUtc">最后一次确认连通的 UTC 时间</param>
/// <param name="ConsecutiveFailedRounds">连续失败轮数</param>
/// <param name="ConsecutiveSuccessfulRounds">连续成功轮数</param>
/// <param name="NotifyDisconnect">是否在本轮确认持续掉线并通知</param>
/// <param name="Evidence">分项探测或观察证据</param>
public sealed record ProbeSnapshot(
    ResourceDescriptor Resource,
    long RoundSequence,
    ResourceState State,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ValidUntilUtc,
    DateTimeOffset? LastConfirmedReadyUtc,
    int ConsecutiveFailedRounds,
    int ConsecutiveSuccessfulRounds,
    bool NotifyDisconnect,
    ImmutableArray<ProbeEvidence> Evidence)
{
    /// <summary>判断快照是否匹配冻结端点身份，处于就绪状态且未过期。</summary>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <param name="now">用于纯规则归约或事实时效检查的当前 UTC 时间。</param>
    /// <returns>是否满足所述条件。</returns>
    public bool IsFreshFor(ResourceDescriptor expected, DateTimeOffset now)
    {
        return Resource.IdentityKey == expected.IdentityKey && now < ValidUntilUtc && State == ResourceState.Reachable;
    }

    /// <summary>为明确资源创建未取得肯定证据的初始状态。</summary>
    /// <param name="resource">探测/启动冻结的资源描述</param>
    /// <returns>绑定传入资源完整身份、没有成功证据和探测轮次的未知快照。</returns>
    public static ProbeSnapshot Unknown(ResourceDescriptor resource)
    {
        return new ProbeSnapshot(resource, 0, ResourceState.Unknown, DateTimeOffset.MinValue, DateTimeOffset.MinValue,
            null, 0, 0,
            false, []);
    }
}

/// <summary>根据完整资源身份、显式时间和策略版本归并分项证据。</summary>
public sealed class ConnectionHealthPolicy
{
    /// <summary>仅归并身份键、策略版本一致且序号前进的完成轮次；异身份须独立初始化，旧轮次不覆盖新状态。保留VM/服务/TCP分项，过期证据转为未知/待复查。</summary>
    /// <param name="previous">同一端点上一轮的健康快照。</param>
    /// <param name="round">同一资源本轮取得的探测尝试集合。</param>
    /// <param name="now">用于纯规则归约或事实时效检查的当前 UTC 时间。</param>
    /// <returns>归约后的资源快照；不匹配的资源身份或迟到轮次不能替代较新事实。</returns>
    public ProbeSnapshot Reduce(ProbeSnapshot previous, ProbeRound round, DateTimeOffset now)
    {
        if (round.Observations.IsDefaultOrEmpty ||
            round.Observations.Any(o => o.Resource.IdentityKey != round.Resource.IdentityKey))
            throw new ArgumentException(
                "A probe round must contain observations for exactly one immutable resource identity.", nameof(round));
        if (previous.Resource.IdentityKey != round.Resource.IdentityKey)
            previous = ProbeSnapshot.Unknown(round.Resource);
        if (round.Sequence <= previous.RoundSequence) return previous;
        var policy = round.Resource.Policy;
        if (policy.FailedRoundsToNotify < 1 || policy.SuccessfulRoundsToRecover < 1 || policy.FreshnessSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(round));
        var ready = round.Observations.Any(o => o.ConnectivityReady);
        var failures = ready ? 0 : previous.ConsecutiveFailedRounds + 1;
        var successes = ready ? previous.ConsecutiveSuccessfulRounds + 1 : 0;
        var state = !round.Resource.Enabled
            ? ResourceState.Disabled
            : ready
                ? previous.State == ResourceState.Unreachable && successes < policy.SuccessfulRoundsToRecover
                    ? ResourceState.Rechecking
                    : ResourceState.Reachable
                : failures >= policy.FailedRoundsToNotify
                    ? ResourceState.Unreachable
                    : ResourceState.Rechecking;
        return new ProbeSnapshot(round.Resource, round.Sequence, state, now, now.AddSeconds(policy.FreshnessSeconds),
            ready ? now : previous.LastConfirmedReadyUtc, failures, successes,
            state == ResourceState.Unreachable && previous.State != ResourceState.Unreachable,
            round.Observations.Last().Evidence);
    }
}
