namespace LaunchDesk.UseCases;

/// <summary>白名单导出、导入预览和稳定配置提交。</summary>
/// <param name="store">配置存储端口。</param>
/// <param name="machineId"><include file="../../Comments.xml" path="doc/MachineId/param[@name='machineId']/*"/></param>
public sealed class ConfigurationShareService(IWorkspaceStore store, Guid machineId) : IConfigurationShareService
{
    /// <inheritdoc />
    public async ValueTask<Result<ShareBundle>> ExportAsync(ShareSelection selection, CancellationToken ct)
    {
        var read = await store.ReadAsync(ct);
        if (!read.IsSuccess) return Result<ShareBundle>.Fail(read.Error!);
        var snapshot = read.Value;
        var profiles = snapshot.Profiles.Where(p => selection.ProfileIds.Contains(p.Id)).ToArray();
        var appIds = profiles.Select(p => p.Fields.ApplicationId).ToHashSet();
        var licenseIds = profiles.Select(p => p.Fields.LicenseId).ToHashSet();
        var toolIds = selection.IncludeTools ? profiles.SelectMany(p => p.Fields.ToolIds).ToHashSet() : [];
        if (selection.IncludeTools) toolIds.UnionWith(selection.ToolIds);
        var bundle = new ShareBundle(3, snapshot.Applications.Where(a => appIds.Contains(a.Id)).ToImmutableArray(),
            snapshot.LicenseEndpoints.Where(l => licenseIds.Contains(l.Id)).Select(l =>
                l with { Resource = l.Resource with { MachineId = Guid.Empty, VmPath = null } }).ToImmutableArray(),
            profiles.Select(p =>
                    new SharedProfile(p.Id, p.Fields with { ToolIds = selection.IncludeTools ? p.Fields.ToolIds : [] }))
                .ToImmutableArray(),
            snapshot.Tools.Where(t => toolIds.Contains(t.Id)).Select(t => t with { LegacyRecordsDirectory = null })
                .ToImmutableArray(), snapshot.Templates);
        return Result<ShareBundle>.Ok(bundle);
    }

    /// <inheritdoc />
    public ValueTask<Result<ImportProposal>> PreviewImportAsync(ShareBundle bundle, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (bundle.SchemaVersion != 3 || bundle.Applications.IsDefault || bundle.Profiles.IsDefault ||
            bundle.LicenseEndpoints.IsDefault || bundle.Tools.IsDefault || bundle.Templates.IsDefault ||
            bundle.Profiles.Length > 10000)
            return ValueTask.FromResult(Result<ImportProposal>.Fail("InvalidFormat", "分享文件格式或条目数量无效。"));
        var valid = bundle.Applications.All(a =>
                        ConfigurationRules.ValidDefinition(new DefinitionDraft(DefinitionKind.Application, a)))
                    && bundle.LicenseEndpoints.All(l =>
                        ConfigurationRules.ValidDefinition(new DefinitionDraft(DefinitionKind.License, License: l)))
                    && bundle.Tools.All(t =>
                        ConfigurationRules.ValidDefinition(new DefinitionDraft(DefinitionKind.Tool, Tool: t)))
                    && bundle.Profiles.All(p => ConfigurationRules.ValidProfile(p.Fields)
                                                && bundle.Applications.Any(a => a.Id == p.Fields.ApplicationId)
                                                && (!p.Fields.LicenseId.HasValue ||
                                                    bundle.LicenseEndpoints.Any(l => l.Id == p.Fields.LicenseId))
                                                && p.Fields.ToolIds.All(id => bundle.Tools.Any(t => t.Id == id)));
        var ids = bundle.Applications.Select(a => a.Id).Concat(bundle.LicenseEndpoints.Select(l => l.Id))
            .Concat(bundle.Tools.Select(t => t.Id)).Concat(bundle.Profiles.Select(p => p.Id))
            .Concat(bundle.Templates.Select(t => t.Id)).ToArray();
        if (!valid || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            return ValueTask.FromResult(Result<ImportProposal>.Fail("InvalidReference", "分享数据存在无效字段、重复标识或缺失关联。"));
        return ValueTask.FromResult(Result<ImportProposal>.Ok(new ImportProposal(Guid.NewGuid(),
            CanonicalDigest.Of(bundle), bundle,
            ["登录字段将自动填入；本机路径需重新核验，密码需在接收机器补充。", "导入仅保存配置，不会启动程序、部署工具或写入环境变量。"])));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Revision>> CommitAsync(CommitId commitId, ImportProposal proposal, Revision expected,
        CancellationToken ct)
    {
        if (!proposal.LocalBindingsConfirmed || CanonicalDigest.Of(proposal.Bundle) != proposal.BundleDigest)
            return Result<Revision>.Fail("ImportConfirmationRequired", "请确认本机绑定及当前导入预览。");
        var verified = await PreviewImportAsync(proposal.Bundle, ct);
        if (!verified.IsSuccess) return Result<Revision>.Fail(verified.Error!);
        var begin = await store.BeginCommitAsync(
            WorkspaceCatalogService.Intent(commitId, "Import", expected,
                new { proposal.BundleDigest, proposal.LocalBindingsConfirmed }), ct);
        if (!begin.IsSuccess) return Result<Revision>.Fail(begin.Error!);
        await using var session = begin.Value;
        if (!session.IsFirst) return WorkspaceCatalogService.RevisionFrom(session.Receipt);
        var seed = session.Receipt.GeneratedEntityId;

        Guid Map(Guid id)
        {
            return new Guid(Convert.FromHexString(CanonicalDigest.Text($"{seed:N}/{id:N}")[..32]));
        }

        var bundle = proposal.Bundle;
        var next = session.Snapshot with
        {
            Applications = session.Snapshot.Applications.AddRange(bundle.Applications.Select(a =>
                a with { Id = Map(a.Id), ManualBinding = true, RequiresLocalBinding = true, DiscoveredPaths = [] })),
            LicenseEndpoints = session.Snapshot.LicenseEndpoints.AddRange(bundle.LicenseEndpoints.Select(l =>
                l with
                {
                    Id = Map(l.Id),
                    Resource = l.Resource with
                    {
                        ResourceId = Map(l.Id), MachineId = machineId, ResourceRevision = new Revision(1),
                        VmPath = null
                    }
                })),
            Tools = session.Snapshot.Tools.AddRange(bundle.Tools.Select(t =>
                t with { Id = Map(t.Id), LegacyRecordsDirectory = null })),
            Profiles = session.Snapshot.Profiles.AddRange(bundle.Profiles.Select(p =>
                new ProfileConfiguration(Map(p.Id),
                    p.Fields with
                    {
                        ProfileId = Map(p.Id), ApplicationId = Map(p.Fields.ApplicationId),
                        LicenseId = p.Fields.LicenseId is { } id ? Map(id) : null,
                        ToolIds = p.Fields.ToolIds.Select(Map).ToImmutableArray()
                    }, null))),
            Templates = session.Snapshot.Templates.AddRange(bundle.Templates.Select(t => t with { Id = Map(t.Id) }))
        };
        var committed = await store.CommitAsync(session, new WorkspaceChange(next), ct);
        return committed.Bind(WorkspaceCatalogService.RevisionFrom);
    }

    /// <inheritdoc />
    public ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct)
    {
        return store.ReadCommitAsync(commitId, ct);
    }
}
