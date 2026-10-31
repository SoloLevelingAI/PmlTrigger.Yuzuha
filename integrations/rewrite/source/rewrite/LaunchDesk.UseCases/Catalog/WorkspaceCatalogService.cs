namespace LaunchDesk.UseCases;

/// <summary>配置 CRUD、稳定 ID、发现合并及直接提交结果核验。</summary>
/// <param name="store">配置存储端口。</param>
/// <param name="vault">本机凭据端口。</param>
/// <param name="discovery">入口发现端口。</param>
/// <param name="machineId"><include file="../../Comments.xml" path="doc/MachineId/param[@name='machineId']/*"/></param>
public sealed class WorkspaceCatalogService(
    IWorkspaceStore store,
    ISecretVault vault,
    IEntryDiscovery discovery,
    Guid machineId) : ICatalogService
{
    /// <inheritdoc />
    public async ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct)
    {
        var result = await store.ReadAsync(ct);
        return result.Map(snapshot => snapshot.PublicProjection());
    }

    /// <inheritdoc />
    public async ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(CommitId commitId, ProfileDraft draft,
        SecretUpdate change, Revision expected, CancellationToken ct)
    {
        if (!ConfigurationRules.ValidProfile(draft))
            return Result<ProfileSnapshot>.Fail("InvalidProfile", "请填写配置名称、应用和有效登录字段。");
        if (change.Kind == SecretUpdateKind.Replace && (change.Secret is null || change.InputId == Guid.Empty))
            return Result<ProfileSnapshot>.Fail("MissingSecretInput", "本次密码输入已经失效，请重新输入。");
        var intent = Intent(commitId, "SaveProfile", expected, new { draft, change.Kind, change.InputId },
            draft.ProfileId);
        var begin = await store.BeginCommitAsync(intent, ct);
        if (!begin.IsSuccess) return Result<ProfileSnapshot>.Fail(begin.Error!);
        await using var session = begin.Value;
        if (!session.IsFirst) return ProfileFrom(session.Receipt);
        var snapshot = session.Snapshot;
        if (!snapshot.Applications.Any(a => a.Id == draft.ApplicationId) ||
            (draft.LicenseId is { } license && !snapshot.LicenseEndpoints.Any(l => l.Id == license)) ||
            draft.ToolIds.Any(id => !snapshot.Tools.Any(t => t.Id == id)))
            return Result<ProfileSnapshot>.Fail("InvalidReference", "应用、许可或关联工具已经改变，请重新载入。");
        var id = draft.ProfileId ?? session.Receipt.GeneratedEntityId;
        var existing = snapshot.Profiles.FirstOrDefault(p => p.Id == id);
        if (draft.ProfileId.HasValue && existing is null)
            return Result<ProfileSnapshot>.Fail("ProfileNotFound", "没有找到原登录配置。");
        var credential = change.Kind == SecretUpdateKind.Clear ? null : existing?.Credential;
        if (change.Kind == SecretUpdateKind.Replace)
        {
            var secret = await vault.StoreNewAsync(change.Secret!, ct);
            if (!secret.IsSuccess) return Result<ProfileSnapshot>.Fail(secret.Error!);
            credential = secret.Value;
        }

        var saved = new ProfileConfiguration(id, draft with { ProfileId = id }, credential);
        var next = snapshot with
        {
            Profiles = snapshot.Profiles.Where(p => p.Id != id).Append(saved).ToImmutableArray()
        };
        var projection = next.PublicProjection().Profiles.Single(p => p.Id == id);
        // 结果未知时保留新旧凭据引用，不提前回收。
        var committed = await store.CommitAsync(session, new WorkspaceChange(next, projection), ct);
        return committed.Bind(ProfileFrom);
    }

    /// <inheritdoc />
    public async ValueTask<Result<ProfileSnapshot>> CopyAsync(CommitId commitId, Guid sourceId, Revision expected,
        CancellationToken ct)
    {
        var begin = await store.BeginCommitAsync(Intent(commitId, "CopyProfile", expected, sourceId), ct);
        if (!begin.IsSuccess) return Result<ProfileSnapshot>.Fail(begin.Error!);
        await using var session = begin.Value;
        if (!session.IsFirst) return ProfileFrom(session.Receipt);
        var source = session.Snapshot.Profiles.FirstOrDefault(p => p.Id == sourceId);
        if (source is null) return Result<ProfileSnapshot>.Fail("ProfileNotFound", "没有找到要复制的配置。");
        var id = session.Receipt.GeneratedEntityId;
        var fields = source.Fields with { ProfileId = id, Name = source.Fields.Name + " · 副本" };
        var next = session.Snapshot with
        {
            Profiles = session.Snapshot.Profiles.Add(new ProfileConfiguration(id, fields, null))
        };
        var receipt = await store.CommitAsync(session,
            new WorkspaceChange(next, next.PublicProjection().Profiles.Single(p => p.Id == id)), ct);
        return receipt.Bind(ProfileFrom);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> DeleteAsync(CommitId commitId, Guid profileId, Revision expected,
        CancellationToken ct)
    {
        var begin = await store.BeginCommitAsync(Intent(commitId, "DeleteProfile", expected, profileId, profileId), ct);
        if (!begin.IsSuccess) return Result<Unit>.Fail(begin.Error!);
        await using var session = begin.Value;
        if (!session.IsFirst) return ReceiptStatus(session.Receipt);
        if (!session.Snapshot.Profiles.Any(p => p.Id == profileId))
            return Result<Unit>.Fail("ProfileNotFound", "没有找到要删除的配置。");
        var next = session.Snapshot with
        {
            Profiles = session.Snapshot.Profiles.Where(p => p.Id != profileId).ToImmutableArray()
        };
        var committed = await store.CommitAsync(session, new WorkspaceChange(next), ct);
        return committed.Map(_ => new Unit());
    }

    /// <inheritdoc />
    public async ValueTask<Result<Revision>> SaveDefinitionAsync(CommitId commitId, DefinitionDraft draft,
        Revision expected, CancellationToken ct)
    {
        if (!ConfigurationRules.ValidDefinition(draft))
            return Result<Revision>.Fail("InvalidDefinition", "名称、入口、端点或工具定义无效。");
        var begin = await store.BeginCommitAsync(Intent(commitId, "Definition", expected, draft), ct);
        if (!begin.IsSuccess) return Result<Revision>.Fail(begin.Error!);
        await using var session = begin.Value;
        if (!session.IsFirst) return RevisionFrom(session.Receipt);
        var current = session.Snapshot;
        var assigned = session.Receipt.GeneratedEntityId;
        WorkspaceSnapshotInternal next;
        switch (draft.Kind)
        {
            case DefinitionKind.Application:
                var app = draft.Application!;
                app = app with { Id = app.Id == Guid.Empty ? assigned : app.Id };
                if (draft.Delete && current.Profiles.Any(p => p.Fields.ApplicationId == app.Id))
                    return ReferenceConflict();
                next = current with { Applications = Replace(current.Applications, app, x => x.Id, draft.Delete) };
                break;
            case DefinitionKind.License:
                var license = draft.License!;
                license = license with { Id = license.Id == Guid.Empty ? assigned : license.Id };
                if (draft.Delete && current.Profiles.Any(p => p.Fields.LicenseId == license.Id))
                    return ReferenceConflict();
                var previous = current.LicenseEndpoints.FirstOrDefault(l => l.Id == license.Id);
                license = license with
                {
                    Resource = license.Resource with
                    {
                        ResourceId = license.Id, MachineId = machineId,
                        ResourceRevision = (previous?.Resource.ResourceRevision ?? new Revision(0)).Next()
                    }
                };
                next = current with
                {
                    LicenseEndpoints = Replace(current.LicenseEndpoints, license, x => x.Id, draft.Delete)
                };
                break;
            case DefinitionKind.Tool:
                var tool = draft.Tool! with { Id = draft.Tool.Id == Guid.Empty ? assigned : draft.Tool.Id };
                if (draft.Delete && current.Profiles.Any(p => p.Fields.ToolIds.Contains(tool.Id)))
                    return ReferenceConflict();
                next = current with { Tools = Replace(current.Tools, tool, x => x.Id, draft.Delete) };
                break;
            default:
                var template = draft.Template! with
                {
                    Id = draft.Template.Id == Guid.Empty ? assigned : draft.Template.Id
                };
                next = current with { Templates = Replace(current.Templates, template, x => x.Id, draft.Delete) };
                break;
        }

        var committed = await store.CommitAsync(session, new WorkspaceChange(next), ct);
        return committed.Bind(RevisionFrom);
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkspaceSnapshot>> RefreshDiscoveryAsync(CommitId commitId, Guid applicationId,
        Revision expected, CancellationToken ct)
    {
        var begin = await store.BeginCommitAsync(Intent(commitId, "Discover", expected, applicationId, applicationId),
            ct);
        if (!begin.IsSuccess) return Result<WorkspaceSnapshot>.Fail(begin.Error!);
        await using var session = begin.Value;
        if (!session.IsFirst)
            return session.Receipt.Workspace is { } original
                ? Result<WorkspaceSnapshot>.Ok(original)
                : Result<WorkspaceSnapshot>.Fail(ReceiptError(session.Receipt));
        var app = session.Snapshot.Applications.FirstOrDefault(a => a.Id == applicationId);
        if (app is null) return Result<WorkspaceSnapshot>.Fail("ApplicationNotFound", "应用已不存在。");
        var found = await discovery.FindAsync(applicationId, ct);
        if (!found.IsSuccess) return Result<WorkspaceSnapshot>.Fail(found.Error!);
        app = app with { DiscoveredPaths = found.Value.ToImmutableArray() };
        if (!app.ManualBinding && found.Value.Count > 0)
        {
            var path = found.Value[0];
            app = app with { ExecutablePath = path, WorkingDirectory = Path.GetDirectoryName(path)! };
        }

        var next = session.Snapshot with
        {
            Applications = Replace(session.Snapshot.Applications, app, x => x.Id, false)
        };
        var committed = await store.CommitAsync(session, new WorkspaceChange(next), ct);
        return committed.Map(receipt => receipt.Workspace!);
    }

    /// <inheritdoc />
    public ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct)
    {
        return store.ReadCommitAsync(commitId, ct);
    }

    internal static CommitIntent Intent<T>(CommitId id, string kind, Revision expected, T input, Guid? entity = null)
    {
        return new CommitIntent(id, kind, expected, CanonicalDigest.Of(new { kind, expected, input }),
            entity ?? Guid.NewGuid());
    }

    internal static Error ReceiptError(CommitReceipt receipt)
    {
        return new Error(
            receipt.State switch
            {
                CommitState.NotCommitted => "CommitNotApplied", CommitState.Indeterminate => "CommitIndeterminate",
                _ => "CommitPending"
            }, "该提交尚无确定成功结果，请使用原提交标识核验。", true);
    }

    internal static Result<Unit> ReceiptStatus(CommitReceipt receipt)
    {
        return receipt.State == CommitState.Committed
            ? Result<Unit>.Ok(new Unit())
            : Result<Unit>.Fail(ReceiptError(receipt));
    }

    internal static Result<Revision> RevisionFrom(CommitReceipt receipt)
    {
        return receipt.State == CommitState.Committed
            ? Result<Revision>.Ok(receipt.CommittedRevision)
            : Result<Revision>.Fail(ReceiptError(receipt));
    }

    private static Result<ProfileSnapshot> ProfileFrom(CommitReceipt receipt)
    {
        return receipt.State == CommitState.Committed && receipt.Profile is not null
            ? Result<ProfileSnapshot>.Ok(receipt.Profile)
            : Result<ProfileSnapshot>.Fail(ReceiptError(receipt));
    }

    private static Result<Revision> ReferenceConflict()
    {
        return Result<Revision>.Fail("ReferenceConflict", "仍有登录配置关联此条目，请先解除关联。");
    }

    private static ImmutableArray<T> Replace<T>(ImmutableArray<T> source, T value, Func<T, Guid> id, bool delete)
    {
        return source.Where(x => id(x) != id(value)).Concat(delete ? [] : new[] { value }).ToImmutableArray();
    }
}

internal static class ConfigurationRules
{
    private static bool Text(string? value, int max = 300)
    {
        return value is not null && value.Length <= max && !value.Any(c => c is '\0' or '\r' or '\n');
    }

    internal static bool ValidProfile(ProfileDraft draft)
    {
        return !string.IsNullOrWhiteSpace(draft.Name) && Text(draft.Name) && draft.ApplicationId != Guid.Empty &&
               Text(draft.Project) && Text(draft.User) && Text(draft.Mdb) && Text(draft.Module) &&
               !draft.ToolIds.IsDefault && draft.ToolIds.Length <= 100;
    }

    internal static bool ValidDefinition(DefinitionDraft draft)
    {
        return Enum.IsDefined(draft.Kind) && draft.Kind switch
        {
            DefinitionKind.Application => draft.Application is { } a && !string.IsNullOrWhiteSpace(a.Name) &&
                                          Enum.IsDefined(a.Kind) && Text(a.ExecutablePath, 32760) &&
                                          Text(a.InitializationPath, 32760) && Text(a.WorkingDirectory, 32760),
            DefinitionKind.License => draft.License is { } l && l.Resource is not null &&
                                      !string.IsNullOrWhiteSpace(l.Name) &&
                                      !string.IsNullOrWhiteSpace(l.Resource.Host) &&
                                      l.Resource.RmsPort is > 0 and < 65536 &&
                                      l.Resource.RulesPort is > 0 and < 65536 &&
                                      WorkspaceSchema.ValidPolicy(l.Resource.Policy),
            DefinitionKind.Tool => draft.Tool is { } t && t.Source is not null && Enum.IsDefined(t.Source.Kind) &&
                                   !string.IsNullOrWhiteSpace(t.Name) && !t.Variables.IsDefault &&
                                   t.Variables.All(v =>
                                       ProcessEnvironmentPolicy.ValidVariableName(v.Name) &&
                                       !ProcessEnvironmentPolicy.Reserved(v.Name) && Text(v.Value, 32760)) &&
                                   t.Variables.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                                   t.Variables.Length,
            DefinitionKind.Template => draft.Template is { } template && !string.IsNullOrWhiteSpace(template.Name) &&
                                       !template.ArgumentTokens.IsDefault &&
                                       template.ArgumentTokens.All(t => Text(t, 32760)),
            _ => false
        };
    }
}