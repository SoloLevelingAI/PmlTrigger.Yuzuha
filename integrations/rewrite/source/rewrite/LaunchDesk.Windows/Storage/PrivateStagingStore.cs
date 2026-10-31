namespace LaunchDesk.Windows;

/// <summary>在已登记控制根内维护冻结来源及不可变计划。</summary>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
/// <param name="reader">工具包检查端口。</param>
public sealed class PrivateStagingStore(WorkspaceLocation location, IPackageReader reader) : IStagingStore
{
    private string Staging => Path.Combine(location.ControlRoot, "staging");
    private string Plans => Path.Combine(location.ControlRoot, "plans");

    /// <inheritdoc />
    public async ValueTask<Result<SourceSnapshot>> StageAsync(SourceSnapshot source, CancellationToken ct)
    {
        try
        {
            if (source.ArchiveSha256 is not null && PackageReader.Hash(source.Source.Path) != source.ArchiveSha256)
                return Result<SourceSnapshot>.Fail("SourceChanged", "压缩包在预览后已改变，请重新读取来源。");
            AtomicFiles.ProtectDirectory(Staging);
            var id = Guid.NewGuid().ToString("N");
            var root = Path.Combine(Staging, id);
            Directory.CreateDirectory(root);
            var files = ImmutableArray.CreateBuilder<SourceFile>();
            foreach (var file in source.Files)
            {
                ct.ThrowIfCancellationRequested();
                PackageReader.SafeRelative(file.RelativePath);
                var target = WindowsPaths.CombineRelative(root, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using (var input = PackageReader.SourceStream(file.SourcePath))
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output, ct);
                    output.Flush(true);
                }

                if (PackageReader.Hash(target) != file.Sha256 || new FileInfo(target).Length != file.Bytes)
                    return Result<SourceSnapshot>.Fail("SourceChanged", "来源文件已改变，当前预览不会被执行。");
                files.Add(file with { SourcePath = target });
            }

            if (PackageReader.Digest(files, source.ArchiveSha256) != source.SourceDigest)
                return Result<SourceSnapshot>.Fail("SourceChanged", "来源摘要不匹配。");
            return Result<SourceSnapshot>.Ok(source with { Files = files.ToImmutable(), StagingId = id });
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<SourceSnapshot>(ex, "StagingFailed");
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> SavePlanAsync(DeploymentPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            AtomicFiles.Save(Path.Combine(Plans, plan.Id.ToString("N") + ".json"), plan);
            return ValueTask.FromResult(Result<Unit>.Ok(new Unit()));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<Unit>(ex));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<DeploymentPlan>> ReadPlanAsync(Guid planId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(Result<DeploymentPlan>.Ok(
                AtomicFiles.Read<DeploymentPlan>(Path.Combine(Plans, planId.ToString("N") + ".json"))));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<DeploymentPlan>(ex, "PlanMissing"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> ValidateAsync(DeploymentPlan plan, CancellationToken ct)
    {
        var stored = await ReadPlanAsync(plan.Id, ct);
        if (!stored.IsSuccess) return Result<Unit>.Fail(stored.Error!);
        if (CanonicalDigest.Of(stored.Value) != CanonicalDigest.Of(plan) ||
            plan.PlanDigest != CanonicalDigest.Of(plan with { PlanDigest = "" }))
            return Result<Unit>.Fail("PlanChanged", "计划内容已改变，请重新预览。");
        if (plan.Action == DeploymentAction.Install && plan.OriginalSource is not null)
        {
            var current = await reader.InspectAsync(plan.OriginalSource, ct);
            if (!current.IsSuccess) return Result<Unit>.Fail(current.Error!);
            if (current.Value.SourceDigest != plan.SourceDigest)
                return Result<Unit>.Fail("SourceChanged", "来源在预览后发生改变，请重新生成计划。");
        }

        try
        {
            foreach (var file in plan.Files.Where(f => !f.Delete))
            {
                ct.ThrowIfCancellationRequested();
                var canonical = WindowsPaths.Full(file.SourcePath);
                if (!WindowsPaths.Within(canonical, Staging) &&
                    !WindowsPaths.Within(canonical, Path.Combine(location.ControlRoot, "receipts")))
                    return Result<Unit>.Fail("UntrustedStage", "计划来源不在受控暂存或备份范围。");
                if (PackageReader.Hash(canonical) != file.SourceSha256)
                    return Result<Unit>.Fail("StagingChanged", "暂存资料校验失败，请重新预览。");
            }

            return Result<Unit>.Ok(new Unit());
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<Unit>(ex, "StagingChanged");
        }
    }
}