namespace LaunchDesk.OperationContracts;

/// <summary>集中验证工作空间结构、稳定标识和关联完整性。</summary>
public static class WorkspaceSchema
{
    /// <summary>判断探测次数、超时、轮询与通知阈值是否位于支持范围。</summary>
    /// <param name="policy">纯健康状态归约规则。</param>
    /// <returns>是否满足所述条件。</returns>
    public static bool ValidPolicy(ProbePolicy? policy)
    {
        return policy is not null && policy.Version > 0
                                  && policy.AttemptsPerRound is >= 1 and <= 10 &&
                                  policy.TimeoutMilliseconds is >= 50 and <= 30000
                                  && policy.RetryDelayMilliseconds is >= 0 and <= 30000 &&
                                  policy.FailedRoundsToNotify is >= 1 and <= 100
                                  && policy.SuccessfulRoundsToRecover is >= 1 and <= 100 &&
                                  policy.FreshnessSeconds is >= 1 and <= 3600 && policy.PollSeconds is >= 1 and <= 3600;
    }

    /// <summary>验证工作空间结构、标识唯一性及所有关联；非法结构抛出数据格式错误。</summary>
    /// <param name="snapshot">不可变配置、操作或探测快照。</param>
    public static void EnsureValid(WorkspaceSnapshotInternal snapshot)
    {
        if (snapshot.WorkspaceId == Guid.Empty || snapshot.Revision.Value < 0 || snapshot.Applications.IsDefault ||
            snapshot.LicenseEndpoints.IsDefault || snapshot.Profiles.IsDefault || snapshot.Tools.IsDefault ||
            snapshot.Templates.IsDefault
            || snapshot.Profiles.Length > 10000 || snapshot.Applications.Length > 1000 ||
            snapshot.Tools.Length > 1000 || snapshot.LicenseEndpoints.Length > 1000)
            throw new InvalidDataException("Invalid workspace schema.");
        var ids = snapshot.Applications.Select(a => a.Id).Concat(snapshot.LicenseEndpoints.Select(l => l.Id))
            .Concat(snapshot.Profiles.Select(p => p.Id)).Concat(snapshot.Tools.Select(t => t.Id))
            .Concat(snapshot.Templates.Select(t => t.Id)).ToArray();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new InvalidDataException("Duplicate or missing entity identity.");
        if (snapshot.LicenseEndpoints.Any(l => l.Resource is null || l.Id != l.Resource.ResourceId ||
                                               !ValidPolicy(l.Resource.Policy)
                                               || string.IsNullOrWhiteSpace(l.Resource.Host) ||
                                               l.Resource.Host.Length > 255 || l.Resource.RmsPort is < 1 or > 65535 ||
                                               l.Resource.RulesPort is < 1 or > 65535))
            throw new InvalidDataException("Invalid resource descriptor.");
        if (snapshot.Profiles.Any(p => p.Fields is null || p.Id != p.Fields.ProfileId || p.Fields.ToolIds.IsDefault
                                       || !snapshot.Applications.Any(a => a.Id == p.Fields.ApplicationId)
                                       || (p.Fields.LicenseId.HasValue &&
                                           !snapshot.LicenseEndpoints.Any(l => l.Id == p.Fields.LicenseId))
                                       || p.Fields.ToolIds.Any(id => !snapshot.Tools.Any(t => t.Id == id))))
            throw new InvalidDataException("Broken configuration reference.");
        if (snapshot.Tools.Any(t =>
                t.Source is null || !Enum.IsDefined(t.Source.Kind) || t.Variables.IsDefault || t.Variables.Any(v =>
                    !ProcessEnvironmentPolicy.ValidVariableName(v.Name) || ProcessEnvironmentPolicy.Reserved(v.Name))))
            throw new InvalidDataException("Invalid tool configuration.");
    }
}