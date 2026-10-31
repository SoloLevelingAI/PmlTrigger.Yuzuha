using System.Security.Cryptography;

namespace LaunchDesk.Windows;

/// <summary>执行经控制范围保护、逐项可核验的文件变更。</summary>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
/// <param name="staging">私有暂存及计划存储端口。</param>
/// <param name="guard">控制资料保护端口。</param>
/// <param name="fault">用于验证写入中断边界的可选故障注入回调。</param>
/// <param name="mayMutate">在持久写入前复核调用范围是否允许改变。</param>
public sealed class FileDeliveryPort(
    WorkspaceLocation location,
    IStagingStore staging,
    IControlDataGuard guard,
    Action<string>? fault = null,
    Func<bool>? mayMutate = null) : IFileDeliveryPort
{
    private string ReceiptFile(OperationId id)
    {
        return Path.Combine(location.ControlRoot, "receipts", id.ToString(), "receipt.json");
    }

    /// <inheritdoc />
    public async ValueTask<Result<TargetSnapshot>> InspectTargetAsync(TargetSpec target, CancellationToken ct)
    {
        try
        {
            using var pin = WindowsPaths.PinPath(target.Directory);
            if (pin.Path.Exists && !pin.Path.IsDirectory)
                return Result<TargetSnapshot>.Fail("InvalidTarget", "部署目标必须是目录。");
            var safe = await guard.ValidateAsync([pin.Path], ct);
            if (!safe.IsSuccess) return Result<TargetSnapshot>.Fail(safe.Error!);
            var files = ImmutableArray.CreateBuilder<TargetFile>();
            var pending = new Stack<string>();
            if (pin.Path.Exists) pending.Push(pin.Path.Path);
            while (pending.Count > 0)
                foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                {
                    ct.ThrowIfCancellationRequested();
                    using var item = WindowsPaths.PinPath(entry);
                    if (item.Path.IsDirectory)
                    {
                        pending.Push(item.Path.Path);
                        continue;
                    }

                    using var input = WindowsPaths.OpenStableFile(item.Path.Path, false, false);
                    files.Add(new TargetFile(Path.GetRelativePath(pin.Path.Path, item.Path.Path),
                        Convert.ToHexStringLower(SHA256.HashData(input)), input.Length));
                    if (files.Count > 100000)
                        return Result<TargetSnapshot>.Fail("TargetTooLarge", "目标条目过多，请选择更具体的工具目录。");
                }

            var ordered = files.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
            return Result<TargetSnapshot>.Ok(new TargetSnapshot(pin.Path, ordered, CanonicalDigest.Of(ordered)));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<TargetSnapshot>(ex, "TargetUnavailable");
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<DeploymentReceipt>> ApplyAsync(OperationId operationId, ConfirmedPlan confirmed,
        TargetLease lease, CancellationToken ct)
    {
        if (lease.IsDisposed || lease.Capability is not CanonicalTargetLeaseProvider.Capability capability ||
            confirmed.ConfirmedDigest != confirmed.Plan.PlanDigest)
            return Result<DeploymentReceipt>.Fail("InvalidTargetLease", "缺少有效的目标写入权或当前确认计划。");
        var plan = confirmed.Plan;
        if (!capability.Original.Any(t =>
                t.Path.Equals(plan.TargetDirectory, StringComparison.OrdinalIgnoreCase) &&
                t.Identity == plan.TargetIdentity))
            return Result<DeploymentReceipt>.Fail("IdentityChanged", "目标租约与预览计划不一致。");
        var valid = await staging.ValidateAsync(plan, ct);
        if (!valid.IsSuccess) return Result<DeploymentReceipt>.Fail(valid.Error!);
        var pins = new List<WindowsPaths.Pin>();
        var receiptPath = ReceiptFile(operationId);
        var receipt = new DeploymentReceipt(operationId, plan.ToolId, plan.PlanDigest, plan.TargetDirectory,
            plan.TargetIdentity,
            "Prepared", [], DateTimeOffset.UtcNow, null, "Prepared", plan.Original, plan.Id);
        try
        {
            using var currentRoot = WindowsPaths.PinPath(plan.TargetDirectory);
            var protectedCheck = await guard.ValidateAsync([currentRoot.Path], ct);
            if (!protectedCheck.IsSuccess) return Result<DeploymentReceipt>.Fail(protectedCheck.Error!);
            if (File.Exists(receiptPath))
                return Result<DeploymentReceipt>.Fail("ReceiptExists", "该操作已有回执，不能再次执行；请读取原回执并重新预览恢复。");
            AtomicFiles.ProtectDirectory(Path.GetDirectoryName(receiptPath)!);
            // 首次写入前检查全部现有基线；持有写句柄时再核验文件。
            foreach (var change in plan.Files)
            {
                var target = WindowsPaths.CombineRelative(plan.TargetDirectory, change.RelativePath);
                if (File.Exists(target))
                {
                    using var identity = WindowsPaths.PinPath(target);
                    using var input = WindowsPaths.OpenStableFile(target, false, false);
                    if (Convert.ToHexStringLower(SHA256.HashData(input)) != change.BeforeSha256)
                        return Result<DeploymentReceipt>.Fail("TargetChanged", "目标在预览后已改变，请重新预览。");
                }
                else if (change.BeforeSha256 is not null)
                {
                    return Result<DeploymentReceipt>.Fail("TargetChanged", "原目标文件已被移除，请重新预览。");
                }
            }

            AtomicFiles.Save(receiptPath, receipt);
            fault?.Invoke("receipt-prepared");
            foreach (var change in plan.Files)
            {
                ct.ThrowIfCancellationRequested();
                if (mayMutate is not null && !mayMutate())
                    throw new OperationCanceledException("Execution admission ended.");
                var target = WindowsPaths.CombineRelative(plan.TargetDirectory, change.RelativePath);
                var backup = change.BeforeSha256 is null
                    ? null
                    : Path.Combine(Path.GetDirectoryName(receiptPath)!, "backup", change.RelativePath);
                if (backup is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    using var input = WindowsPaths.OpenStableFile(target, false, false);
                    using (var output = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await input.CopyToAsync(output, ct);
                        output.Flush(true);
                    }

                    if (PackageReader.Hash(backup) != change.BeforeSha256)
                        throw new InvalidDataException("Target changed before backup.");
                }

                var entry = new DeploymentFileReceipt(change.RelativePath, change.BeforeSha256,
                    change.Delete ? "" : change.SourceSha256, backup, true, false);
                receipt = receipt with { State = "Applying", Files = receipt.Files.Add(entry) };
                AtomicFiles.Save(receiptPath, receipt);
                fault?.Invoke("intent-written");
                ct.ThrowIfCancellationRequested();
                if (mayMutate is not null && !mayMutate())
                    throw new OperationCanceledException("Execution admission ended.");
                WindowsPaths.CreatePinnedDirectory(Path.GetDirectoryName(target)!, pins);
                if (change.Delete)
                {
                    if (change.BeforeSha256 is not null) WindowsPaths.DeleteStable(target, change.BeforeSha256);
                }
                else
                {
                    using var source = WindowsPaths.OpenStableFile(change.SourcePath, false, false);
                    if (Convert.ToHexStringLower(SHA256.HashData(source)) != change.SourceSha256)
                        throw new InvalidDataException("Staging changed.");
                    source.Position = 0;
                    using var output = WindowsPaths.OpenStableFile(target, true, change.BeforeSha256 is null);
                    if (change.BeforeSha256 is not null &&
                        Convert.ToHexStringLower(SHA256.HashData(output)) != change.BeforeSha256)
                        throw new InvalidDataException("Target changed before write.");
                    output.Position = 0;
                    output.SetLength(0);
                    await source.CopyToAsync(output, ct);
                    output.Flush(true);
                }

                fault?.Invoke("target-written");
                receipt = receipt with
                {
                    Files = receipt.Files.SetItem(receipt.Files.Length - 1, entry with { Applied = true })
                };
                AtomicFiles.Save(receiptPath, receipt);
                fault?.Invoke("fact-written");
            }

            receipt = receipt with
            {
                State = "Completed", FinishedAtUtc = DateTimeOffset.UtcNow, ReasonCode = "Completed"
            };
            AtomicFiles.Save(receiptPath, receipt);
            if (plan.Original is { } original)
            {
                var merged = await MergeResolutionAsync(original, plan, receipt, ct);
                if (!merged.IsSuccess) return Result<DeploymentReceipt>.Fail(merged.Error!);
            }

            return Result<DeploymentReceipt>.Ok(receipt);
        }
        catch (OperationCanceledException)
        {
            TryMarkRecovery(receiptPath, receipt, "CancelledNeedsVerification");
            throw;
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            TryMarkRecovery(receiptPath, receipt, "WriteNeedsVerification");
            return Result<DeploymentReceipt>.Fail("RecoveryRequired", "写入未完整确认；备份和原操作回执已保留，请预览恢复或回滚。", true);
        }
        finally
        {
            foreach (var pin in pins) pin.Dispose();
        }
    }

    private async ValueTask<Result<Unit>> MergeResolutionAsync(OperationId original, DeploymentPlan resolution,
        DeploymentReceipt result, CancellationToken ct)
    {
        var originalReceipt = AtomicFiles.Read<DeploymentReceipt>(ReceiptFile(original));
        var originalPlan = await staging.ReadPlanAsync(originalReceipt.PlanId, ct);
        if (!originalPlan.IsSuccess) return Result<Unit>.Fail(originalPlan.Error!);
        var entries = ImmutableArray.CreateBuilder<DeploymentFileReceipt>();
        foreach (var file in originalPlan.Value.Files)
        {
            var target = WindowsPaths.CombineRelative(originalPlan.Value.TargetDirectory, file.RelativePath);
            var expected = resolution.Action == DeploymentAction.Rollback ? file.BeforeSha256 : file.SourceSha256;
            var actual = File.Exists(target) ? PackageReader.Hash(target) : null;
            if (actual != expected) return Result<Unit>.Fail("RecoveryNeedsVerification", "本次变更已保存，但原操作整体结果尚需核验。");
            var old = originalReceipt.Files.FirstOrDefault(f => f.RelativePath == file.RelativePath);
            var changed = result.Files.FirstOrDefault(f => f.RelativePath == file.RelativePath);
            if (old is not null) entries.Add(old with { Applied = true });
            else if (changed is not null)
                entries.Add(changed with
                {
                    BeforeSha256 = file.BeforeSha256, AfterSha256 = file.SourceSha256, Applied = true
                });
        }

        AtomicFiles.Save(ReceiptFile(original),
            originalReceipt with
            {
                State = resolution.Action == DeploymentAction.Rollback ? "RolledBack" : "Completed",
                Files = entries.ToImmutable(), ResolvedBy = result.Id, ReasonCode = "ResolvedByExplicitOperation"
            });
        return Result<Unit>.Ok(new Unit());
    }

    private static void TryMarkRecovery(string path, DeploymentReceipt receipt, string code)
    {
        try
        {
            if (File.Exists(path))
                AtomicFiles.Save(path, receipt with { State = "NeedsVerification", ReasonCode = code });
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            /* 最终标记写入失败时保留最后持久化意图。 */
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<DeploymentReceipt>> ReadReceiptAsync(OperationId original, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(
                Result<DeploymentReceipt>.Ok(AtomicFiles.Read<DeploymentReceipt>(ReceiptFile(original))));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<DeploymentReceipt>(ex, "ReceiptMissing"));
        }
    }

    /// <summary>读取本机交付回执供更新记录显示，不触发恢复或回滚。</summary>
    /// <returns>当前工作空间已记录的交付回执集合；回执目录缺失时为空集合。</returns>
    public IReadOnlyList<DeploymentReceipt> ReadHistory()
    {
        var root = Path.Combine(location.ControlRoot, "receipts");
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "receipt.json", SearchOption.AllDirectories)
            .Select(AtomicFiles.Read<DeploymentReceipt>).OrderByDescending(r => r.StartedAtUtc).ToArray();
    }
}