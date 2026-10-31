namespace LaunchDesk.UseCases;

/// <summary>来源刷新和选择策略；扫描/预览不执行内容。</summary>
/// <param name="reader">工具包检查端口。</param>
public sealed class ToolSourceCatalog(IPackageReader reader) : IToolSourceCatalog
{
    /// <inheritdoc />
    public async ValueTask<Result<SourceCatalog>> RefreshAsync(SourceQuery query, SourceSelection current,
        RefreshIntent intent, CancellationToken ct)
    {
        var discovered = await reader.DiscoverAsync(query, ct);
        if (!discovered.IsSuccess) return discovered;
        var selected = intent == RefreshIntent.SelectLatest
            ? discovered.Value.Candidates.FirstOrDefault()
            : discovered.Value.Candidates.FirstOrDefault(c =>
                c.Path.Equals(current.Path, StringComparison.OrdinalIgnoreCase));
        return Result<SourceCatalog>.Ok(discovered.Value with { Selection = selected });
    }

    /// <inheritdoc />
    public ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)
    {
        return reader.InspectAsync(source, ct);
    }
}

/// <summary>形成绑定实际内容、完整变更范围及恢复资料位置的不可变部署预览。</summary>
/// <param name="reader">工具包检查端口。</param>
/// <param name="staging">私有暂存及计划存储端口。</param>
/// <param name="files">已检查的文件/普通文件映射集</param>
/// <param name="guard">控制资料保护端口。</param>
public sealed class DeploymentPlanner(
    IPackageReader reader,
    IStagingStore staging,
    IFileDeliveryPort files,
    IControlDataGuard guard) : IDeploymentPlanningService
{
    /// <inheritdoc />
    public async ValueTask<Result<DeploymentPlan>> PreviewAsync(DeployRequest request, CancellationToken ct)
    {
        var inspected = await reader.InspectAsync(request.Source.Source, ct);
        if (!inspected.IsSuccess) return Result<DeploymentPlan>.Fail(inspected.Error!);
        if (inspected.Value.SourceDigest != request.Source.SourceDigest)
            return Result<DeploymentPlan>.Fail("SourceChanged", "来源已改变，请刷新并重新选择。");
        var target = await files.InspectTargetAsync(request.Target, ct);
        if (!target.IsSuccess) return Result<DeploymentPlan>.Fail(target.Error!);
        var allowed = await guard.ValidateAsync([target.Value.Root], ct);
        if (!allowed.IsSuccess) return Result<DeploymentPlan>.Fail(allowed.Error!);
        var source = await staging.StageAsync(inspected.Value, ct);
        if (!source.IsSuccess) return Result<DeploymentPlan>.Fail(source.Error!);
        var first = source.Value.Files[0].RelativePath.Split(['/', '\\'])[0];
        var flatten = request.FlattenRoot && request.Source.Source.Kind is PackageKind.Zip or PackageKind.SevenZip
                                          && source.Value.Files.All(f =>
                                              f.RelativePath.Contains('\\') && f.RelativePath.StartsWith(first + "\\",
                                                  StringComparison.OrdinalIgnoreCase));
        var existing = target.Value.Files.ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
        var changes = source.Value.Files.Select(file =>
        {
            var relative = flatten ? file.RelativePath[(first.Length + 1)..] : file.RelativePath;
            existing.TryGetValue(relative, out var before);
            return new DeploymentFile(relative, file.SourcePath, file.Sha256, file.Bytes, before?.Sha256,
                before?.Bytes ?? 0);
        }).Where(file => file.BeforeSha256 != file.SourceSha256).ToImmutableArray();
        var plan = new DeploymentPlan(Guid.NewGuid(), DeploymentAction.Install, request.WorkspaceRevision,
            request.ToolId,
            source.Value.SourceDigest, target.Value.Root.Path, target.Value.Root.Identity, changes, request.KeepBackup,
            "", OriginalSource: request.Source.Source);
        plan = plan with { PlanDigest = CanonicalDigest.Of(plan) };
        var saved = await staging.SavePlanAsync(plan, ct);
        return saved.Map(_ => plan);
    }

    /// <inheritdoc />
    public async ValueTask<Result<DeploymentPlan>> PreviewRecoveryAsync(RecoveryRequest request, CancellationToken ct)
    {
        if (request.Action == DeploymentAction.Install)
            return Result<DeploymentPlan>.Fail("InvalidRecovery", "请选择恢复或回滚。");
        var receipt = await files.ReadReceiptAsync(request.Original, ct);
        if (!receipt.IsSuccess) return Result<DeploymentPlan>.Fail(receipt.Error!);
        var original = await staging.ReadPlanAsync(receipt.Value.PlanId, ct);
        if (!original.IsSuccess) return Result<DeploymentPlan>.Fail(original.Error!);
        var target = await files.InspectTargetAsync(new TargetSpec(receipt.Value.TargetDirectory), ct);
        if (!target.IsSuccess) return Result<DeploymentPlan>.Fail(target.Error!);
        var current = target.Value.Files.ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
        var changes = ImmutableArray.CreateBuilder<DeploymentFile>();
        foreach (var file in original.Value.Files)
        {
            var saved = receipt.Value.Files.FirstOrDefault(f =>
                f.RelativePath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase));
            current.TryGetValue(file.RelativePath, out var now);
            if (saved is null && now?.Sha256 != file.BeforeSha256)
                return Result<DeploymentPlan>.Fail("ExternalTargetChanged", "原操作尚未写入的文件发生外部变化，请先人工核验。");
            if (saved?.Applied == true && now?.Sha256 != saved.AfterSha256)
                return Result<DeploymentPlan>.Fail("ExternalTargetChanged", "已写入的目标被外部修改，不能直接恢复或回滚。");
            if (request.Action == DeploymentAction.Recover)
            {
                if (now?.Sha256 == file.SourceSha256) continue;
                changes.Add(file with { BeforeSha256 = now?.Sha256, BeforeBytes = now?.Bytes ?? 0 });
            }
            else if (saved?.IntentWritten == true)
            {
                if (saved.BeforeSha256 is null)
                    changes.Add(new DeploymentFile(file.RelativePath, "", "", 0, now?.Sha256, now?.Bytes ?? 0, true));
                else if (saved.BackupPath is { } backup)
                    changes.Add(new DeploymentFile(file.RelativePath, backup, saved.BeforeSha256, file.BeforeBytes,
                        now?.Sha256, now?.Bytes ?? 0));
                else return Result<DeploymentPlan>.Fail("BackupMissing", "缺少原操作备份，不能推断回滚内容。");
            }
        }

        var plan = new DeploymentPlan(Guid.NewGuid(), request.Action, request.WorkspaceRevision, receipt.Value.ToolId,
            original.Value.SourceDigest, target.Value.Root.Path, target.Value.Root.Identity, changes.ToImmutable(),
            true, "", request.Original);
        plan = plan with { PlanDigest = CanonicalDigest.Of(plan) };
        var save = await staging.SavePlanAsync(plan, ct);
        return save.Map(_ => plan);
    }
}

/// <summary>只读生成持久变量变更计划。</summary>
/// <param name="store">配置存储端口。</param>
public sealed class VariablePlanner(IEnvironmentStore store) : IVariablePlanningService
{
    /// <inheritdoc />
    public async ValueTask<Result<VariableChangePlan>> PreviewAsync(VariableRenameRequest request, CancellationToken ct)
    {
        if (!ProcessEnvironmentPolicy.ValidVariableName(request.OldName) || !ProcessEnvironmentPolicy.ValidVariableName(
                                                                             request.NewName)
                                                                         || ProcessEnvironmentPolicy.Reserved(
                                                                             request.OldName) ||
                                                                         ProcessEnvironmentPolicy.Reserved(
                                                                             request.NewName)
                                                                         || request.NewValue?.IndexOfAny([
                                                                             '\0', '\r', '\n'
                                                                         ]) >= 0 || !Enum.IsDefined(request.Scope))
            return Result<VariableChangePlan>.Fail("InvalidVariable", "变量名、作用域或值无效；许可专用变量不能由工具修改。");
        var before = await store.ReadAsync(new VariableAddress(request.OldName, request.Scope), ct);
        var target = await store.ReadAsync(new VariableAddress(request.NewName, request.Scope), ct);
        if (!before.IsSuccess || !target.IsSuccess)
            return Result<VariableChangePlan>.Fail(before.Error ?? target.Error!);
        var plan = new VariableChangePlan(Guid.NewGuid(), before.Value, target.Value, request.NewName, request.NewValue,
            "");
        return Result<VariableChangePlan>.Ok(plan with { PlanDigest = CanonicalDigest.Of(plan) });
    }
}