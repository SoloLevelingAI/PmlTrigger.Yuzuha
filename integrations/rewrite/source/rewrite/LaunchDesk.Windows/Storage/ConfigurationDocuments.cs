namespace LaunchDesk.Windows;

/// <summary>以受验证的 JSON 结构原子读写非秘密分享文件。</summary>
public sealed class ConfigurationDocuments : IConfigurationDocuments
{
    /// <inheritdoc />
    public ValueTask<Result<ShareBundle>> ReadShareAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(Result<ShareBundle>.Ok(AtomicFiles.Read<ShareBundle>(path)));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<ShareBundle>(ex, "ImportReadFailed"));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> WriteShareAsync(string path, ShareBundle bundle, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            AtomicFiles.Save(path, bundle);
            return ValueTask.FromResult(Result<Unit>.Ok(new Unit()));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<Unit>(ex, "ExportWriteFailed"));
        }
    }
}

/// <summary>合并本工作空间的新交付回执与旧版只读记录，查询不触发执行。</summary>
/// <param name="files">已检查的文件/普通文件映射集</param>
/// <param name="workspace">工作空间存储端口。</param>
public sealed class DeploymentHistoryQueries(FileDeliveryPort files, IWorkspaceStore? workspace = null)
    : IDeploymentHistoryQueries
{
    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<DeploymentReceipt>>> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var history = files.ReadHistory().ToList();
            if (workspace is not null)
            {
                var snapshot = await workspace.ReadAsync(ct);
                if (!snapshot.IsSuccess) return Result<IReadOnlyList<DeploymentReceipt>>.Fail(snapshot.Error!);
                foreach (var tool in snapshot.Value.Tools) history.AddRange(LegacyHistoryReader.Read(tool));
            }

            return Result<IReadOnlyList<DeploymentReceipt>>.Ok(history.OrderByDescending(r => r.StartedAtUtc)
                .ToArray());
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<IReadOnlyList<DeploymentReceipt>>(ex, "HistoryUnavailable");
        }
    }
}