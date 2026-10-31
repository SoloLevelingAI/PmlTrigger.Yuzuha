namespace LaunchDesk.Windows;

/// <summary>实现稳定 CommitId 去重、版本化准备记录和可跨修订核验的配置提交。</summary>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
/// <param name="fault">用于验证写入中断边界的可选故障注入回调。</param>
public sealed class JsonWorkspaceStore(WorkspaceLocation location, Action<string>? fault = null) : IWorkspaceStore
{
    private readonly object owner = new();
    private string Gate => Path.Combine(location.ControlRoot, "workspace.lock");

    private string IntentFile(CommitId id)
    {
        return Path.Combine(location.ControlRoot, "commits", id + ".json");
    }

    private sealed record Pointer(int SchemaVersion, string Generation, string Sha256);

    internal sealed record Envelope(
        int SchemaVersion,
        WorkspaceSnapshotInternal Snapshot,
        ImmutableDictionary<string, CommitReceipt> History);

    private sealed class Capability(object owner, FileStream gate, CommitIntent intent)
    {
        internal object Owner { get; } = owner;
        internal FileStream Gate { get; } = gate;
        internal CommitIntent Intent { get; } = intent;
        internal bool PointerAttempted;
        internal bool Closed;
    }

    /// <summary>仅在配置尚缺失时初始化空工作空间，不覆盖已有配置。</summary>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/*"/>
    public async ValueTask<Result<Unit>> InitializeEmptyAsync(CancellationToken ct)
    {
        try
        {
            AtomicFiles.ProtectDirectory(location.ControlRoot);
            await using var gate = await AtomicFiles.LockAsync(Gate, ct);
            if (File.Exists(location.ConfigurationFile))
            {
                ReadEnvelope();
                return Result<Unit>.Ok(new Unit());
            }

            Switch(new Envelope(3, WorkspaceSnapshotInternal.Empty(),
                ImmutableDictionary<string, CommitReceipt>.Empty));
            return Result<Unit>.Ok(new Unit());
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<Unit>(ex);
        }
    }

    internal Envelope ReadEnvelope()
    {
        var pointer = AtomicFiles.Read<Pointer>(location.ConfigurationFile);
        if (pointer.SchemaVersion != 3 ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(pointer.Generation), "N", out _) ||
            pointer.Generation != Path.GetFileName(pointer.Generation))
            throw new InvalidDataException("Unknown workspace generation.");
        var path = Path.Combine(location.ControlRoot, "generations", pointer.Generation);
        var bytes = File.ReadAllBytes(path);
        if (CanonicalDigest.Bytes(bytes) != pointer.Sha256)
            throw new InvalidDataException("Generation checksum mismatch.");
        var envelope = AtomicFiles.Read<Envelope>(path);
        if (envelope.SchemaVersion != 3 || envelope.Snapshot.WorkspaceId == Guid.Empty)
            throw new InvalidDataException("Invalid workspace identity.");
        WorkspaceSchema.EnsureValid(envelope.Snapshot);
        return envelope;
    }

    private void Switch(Envelope envelope)
    {
        var generation = Guid.NewGuid().ToString("N") + ".json";
        var path = Path.Combine(location.ControlRoot, "generations", generation);
        AtomicFiles.Save(path, envelope);
        fault?.Invoke("generation-written");
        AtomicFiles.Save(location.ConfigurationFile,
            new Pointer(3, generation, CanonicalDigest.Bytes(File.ReadAllBytes(path))));
        fault?.Invoke("pointer-written");
    }

    /// <inheritdoc />
    public ValueTask<Result<WorkspaceSnapshotInternal>> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(location.ConfigurationFile))
            return ValueTask.FromResult(
                Result<WorkspaceSnapshotInternal>.Fail("ConfigurationMissing", "工作空间尚未创建，请创建或迁移配置。"));
        try
        {
            return ValueTask.FromResult(Result<WorkspaceSnapshotInternal>.Ok(ReadEnvelope().Snapshot));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<WorkspaceSnapshotInternal>(ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkspaceCommitSession>> BeginCommitAsync(CommitIntent intent, CancellationToken ct)
    {
        FileStream? gate = null;
        try
        {
            if (intent.Id.Value == Guid.Empty || intent.RequestDigest.Length != 64)
                return Result<WorkspaceCommitSession>.Fail("InvalidCommit", "提交标识或摘要无效。");
            gate = await AtomicFiles.LockAsync(Gate, ct);
            var current = ReadEnvelope();
            var existing = current.History.GetValueOrDefault(intent.Id.ToString());
            if (existing is null && File.Exists(IntentFile(intent.Id)))
                existing = AtomicFiles.Read<CommitReceipt>(IntentFile(intent.Id));
            if (existing is not null)
            {
                gate.Dispose();
                gate = null;
                return existing.RequestDigest != intent.RequestDigest
                    ? Result<WorkspaceCommitSession>.Fail("CommitConflict", "相同提交标识不能对应不同内容。")
                    : Result<WorkspaceCommitSession>.Ok(new WorkspaceCommitSession(intent, existing, current.Snapshot,
                        false, null));
            }

            if (current.Snapshot.Revision != intent.ExpectedRevision)
                return Result<WorkspaceCommitSession>.Fail("RevisionConflict", "配置已被其他操作更新，请重新载入。");
            var pending = new CommitReceipt(intent.Id, intent.RequestDigest, CommitState.Pending,
                current.Snapshot.Revision, intent.ProposedEntityId, null, null, "CommitPending");
            AtomicFiles.Save(IntentFile(intent.Id), pending);
            fault?.Invoke("intent-written");
            var capability = new Capability(owner, gate, intent);
            gate = null;
            return Result<WorkspaceCommitSession>.Ok(new WorkspaceCommitSession(intent, pending, current.Snapshot, true,
                capability, async () =>
                {
                    if (capability.Closed) return;
                    try
                    {
                        if (!capability.PointerAttempted)
                            AtomicFiles.Save(IntentFile(intent.Id),
                                pending with { State = CommitState.NotCommitted, ReasonCode = "CommitNotApplied" });
                    }
                    catch (Exception ex) when (AtomicFiles.Expected(ex))
                    {
                        /* 否定结果无法记录时保留Pending。 */
                    }
                    finally
                    {
                        capability.Closed = true;
                        await capability.Gate.DisposeAsync();
                    }
                }));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<WorkspaceCommitSession>(ex);
        }
        finally
        {
            gate?.Dispose();
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<CommitReceipt>> CommitAsync(WorkspaceCommitSession session, WorkspaceChange change,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (session.Capability is not Capability capability || !ReferenceEquals(capability.Owner, owner) ||
            capability.Closed || !session.IsFirst)
            return ValueTask.FromResult(Result<CommitReceipt>.Fail("ReadOnlyCommit", "重复提交会话没有写入能力。"));
        try
        {
            var envelope = ReadEnvelope();
            if (envelope.Snapshot.Revision != session.Intent.ExpectedRevision ||
                change.Snapshot.WorkspaceId != envelope.Snapshot.WorkspaceId)
                return ValueTask.FromResult(Result<CommitReceipt>.Fail("RevisionConflict", "工作空间身份或修订已改变。"));
            var next = change.Snapshot with { Revision = envelope.Snapshot.Revision.Next() };
            WorkspaceSchema.EnsureValid(next);
            var profile = change.Profile is null ? null : change.Profile with { Revision = next.Revision };
            var receipt = new CommitReceipt(session.Intent.Id, session.Intent.RequestDigest, CommitState.Committed,
                next.Revision,
                session.Intent.ProposedEntityId, profile, next.PublicProjection(), "Committed");
            capability.PointerAttempted = true;
            Switch(new Envelope(3, next, envelope.History.SetItem(session.Intent.Id.ToString(), receipt)));
            return ValueTask.FromResult(Result<CommitReceipt>.Ok(receipt));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            // 代际切换可能成功后，禁止报告NotCommitted或删除新凭据。
            return ValueTask.FromResult(Result<CommitReceipt>.Fail("CommitIndeterminate",
                "提交结果尚未确认，请使用原提交标识核验，勿自动新建重试。", true));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<CommitReceipt>> ReadCommitAsync(CommitId commitId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var envelope = ReadEnvelope();
            var receipt = envelope.History.GetValueOrDefault(commitId.ToString());
            receipt ??= File.Exists(IntentFile(commitId))
                ? AtomicFiles.Read<CommitReceipt>(IntentFile(commitId))
                : new CommitReceipt(commitId, "", CommitState.NotFound, envelope.Snapshot.Revision, Guid.Empty, null,
                    null, "NotFound");
            return ValueTask.FromResult(Result<CommitReceipt>.Ok(receipt));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<CommitReceipt>(ex, "CorruptCommitHistory"));
        }
    }

    internal async ValueTask<Result<Revision>> ActivateMigrationAsync(Envelope source, Revision expected,
        CancellationToken ct)
    {
        try
        {
            await using var gate = await AtomicFiles.LockAsync(Gate, ct);
            var target = ReadEnvelope();
            if (target.Snapshot.Revision != expected) return Result<Revision>.Fail("RevisionConflict", "目标工作空间已改变。");
            if (!target.Snapshot.Applications.IsEmpty || !target.Snapshot.Profiles.IsEmpty ||
                !target.Snapshot.Tools.IsEmpty || !target.Snapshot.LicenseEndpoints.IsEmpty)
                return Result<Revision>.Fail("MigrationTargetNotEmpty", "迁移目标已有配置，请选择新的数据目录，避免覆盖现有内容。");
            var snapshot = source.Snapshot with
            {
                Revision = new Revision(
                    Math.Max(source.Snapshot.Revision.Value, target.Snapshot.Revision.Value) + 1)
            };
            WorkspaceSchema.EnsureValid(snapshot);
            Switch(source with { Snapshot = snapshot });
            return Result<Revision>.Ok(snapshot.Revision);
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return Result<Revision>.Fail("MigrationIndeterminate", "迁移启用结果未确认；原配置及迁移副本均保留，请先核验。", true);
        }
    }
}