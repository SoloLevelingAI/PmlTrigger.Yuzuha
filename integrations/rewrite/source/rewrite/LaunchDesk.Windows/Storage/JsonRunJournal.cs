using System.Runtime.CompilerServices;

namespace LaunchDesk.Windows;

/// <summary>实现 IRunJournal 的跨进程受理、一次执行准入及版本化事实日志。</summary>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
public sealed class JsonRunJournal(WorkspaceLocation location) : IRunJournal
{
    private readonly object owner = new();
    private readonly Dictionary<OperationId, string> admissions = [];
    private readonly object sync = new();
    private string Root => Path.Combine(location.ControlRoot, "operations");

    private string FileFor(OperationId id)
    {
        return Path.Combine(Root, id + ".json");
    }

    private string GateFor(OperationId id)
    {
        return Path.Combine(Root, id + ".lock");
    }

    private sealed record ExecutionOwner(Guid MachineId, int ProcessId, long ProcessStartUtcTicks, Guid InstanceId);

    private sealed record Document(
        OperationRequestSnapshot Request,
        RunSnapshot Snapshot,
        ImmutableArray<RunEvent> Events,
        ExecutionOwner? Owner = null);

    private sealed record Admission(object Owner, string Token, int ProcessId, long ProcessStartTicks);

    private sealed record Execution(object Owner, FileStream Handle, OperationId Id, string Token);

    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptance>> AcceptAsync(OperationRequestSnapshot request,
        CancellationToken ct)
    {
        try
        {
            await using var gate = await AtomicFiles.LockAsync(GateFor(request.Id), ct);
            if (File.Exists(FileFor(request.Id)))
            {
                var existing = AtomicFiles.Read<Document>(FileFor(request.Id));
                return existing.Request.RequestDigest != request.RequestDigest
                    ? Result<OperationAcceptance>.Fail("OperationConflict", "操作标识已对应其他请求。")
                    : Result<OperationAcceptance>.Ok(new OperationAcceptance(existing.Request,
                        AcceptanceDisposition.Existing, null));
            }

            var snapshot = new RunSnapshot(request.Id, request.Kind, 0, RunStage.Accepted, "Accepted",
                "操作已受理，尚未取得执行准入。", DateTimeOffset.UtcNow, false, [],
                request.Request.ProfileId ?? request.Request.Deployment?.Plan.ToolId,
                request.Launch?.Profile.Fields.Name ?? (request.Kind == OperationKind.Deploy ? "工具交付" :
                    request.Kind == OperationKind.ApplyVariables ? "变量应用" : "配置迁移"));
            AtomicFiles.Save(FileFor(request.Id), new Document(request, snapshot, []));
            var token = Guid.NewGuid().ToString("N");
            lock (sync)
            {
                admissions[request.Id] = token;
            }

            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return Result<OperationAcceptance>.Ok(new OperationAcceptance(request, AcceptanceDisposition.Created,
                new Admission(owner, token, process.Id, process.StartTime.ToUniversalTime().Ticks)));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<OperationAcceptance>(ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<OperationExecutionLease>> TryBeginExecutionAsync(OperationAcceptance acceptance,
        CancellationToken ct)
    {
        if (acceptance.Disposition != AcceptanceDisposition.Created ||
            acceptance.Capability is not Admission admission || !ReferenceEquals(admission.Owner, owner))
            return Result<OperationExecutionLease>.Fail("AdmissionRequired", "只有首次受理的原调用可以取得执行权。");
        FileStream? execution = null;
        try
        {
            await using var gate = await AtomicFiles.LockAsync(GateFor(acceptance.Snapshot.Id), ct);
            var document = AtomicFiles.Read<Document>(FileFor(acceptance.Snapshot.Id));
            lock (sync)
            {
                if (!admissions.Remove(acceptance.Snapshot.Id, out var token) || token != admission.Token ||
                    document.Snapshot.Stage != RunStage.Accepted)
                    return Result<OperationExecutionLease>.Fail("AdmissionConsumed", "该操作的执行准入已经消费，不能再次派发。");
            }

            execution = new FileStream(Path.Combine(Root, acceptance.Snapshot.Id + ".execution"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            var state = document.Snapshot with
            {
                Sequence = 1, Stage = RunStage.Executing, ReasonCode = "Executing", SafeMessage = "已取得唯一执行准入。",
                ObservedAtUtc = DateTimeOffset.UtcNow
            };
            var holder = new ExecutionOwner(acceptance.Snapshot.MachineId, admission.ProcessId,
                admission.ProcessStartTicks, Guid.NewGuid());
            AtomicFiles.Save(FileFor(state.Id), document with { Snapshot = state, Owner = holder });
            var capability = new Execution(owner, execution, state.Id, admission.Token);
            execution = null;
            return Result<OperationExecutionLease>.Ok(new OperationExecutionLease(state.Id, capability,
                () => capability.Handle.DisposeAsync()));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<OperationExecutionLease>(ex, "AdmissionIndeterminate");
        }
        finally
        {
            execution?.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<RunSnapshot>> AppendAsync(OperationExecutionLease execution, long expectedSequence,
        RunEvent fact, CancellationToken ct)
    {
        if (execution.Capability is not Execution capability || !ReferenceEquals(capability.Owner, owner) ||
            capability.Handle.SafeFileHandle.IsClosed)
            return Result<RunSnapshot>.Fail("InvalidExecutionLease", "操作执行权已经释放。");
        try
        {
            await using var gate = await AtomicFiles.LockAsync(GateFor(execution.Id), ct);
            var document = AtomicFiles.Read<Document>(FileFor(execution.Id));
            if (document.Snapshot.Sequence != expectedSequence)
                return Result<RunSnapshot>.Fail("SequenceConflict", "操作事实序号已经改变。");
            var next = document.Snapshot with
            {
                Sequence = expectedSequence + 1, Stage = fact.Stage, ReasonCode = fact.ReasonCode,
                SafeMessage = fact.SafeMessage, ObservedAtUtc = fact.ObservedAtUtc,
                Evidence = fact.Evidence.IsDefault ? [] : fact.Evidence
            };
            AtomicFiles.Save(FileFor(execution.Id),
                document with
                {
                    Snapshot = next, Events = document.Events.Add(fact with { Sequence = next.Sequence })
                });
            return Result<RunSnapshot>.Ok(next);
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<RunSnapshot>(ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<RunSnapshot>> RequestCancellationAsync(OperationId id, CancellationToken ct)
    {
        try
        {
            await using var gate = await AtomicFiles.LockAsync(GateFor(id), ct);
            var document = AtomicFiles.Read<Document>(FileFor(id));
            var snapshot = document.Snapshot with { CancelRequested = true };
            AtomicFiles.Save(FileFor(id), document with { Snapshot = snapshot });
            return Result<RunSnapshot>.Ok(snapshot);
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<RunSnapshot>(ex);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<OperationRequestSnapshot>> ReadRequestAsync(OperationId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(FileFor(id)))
            return ValueTask.FromResult(Result<OperationRequestSnapshot>.Fail("OperationNotFound", "未找到该操作。"));
        try
        {
            return ValueTask.FromResult(
                Result<OperationRequestSnapshot>.Ok(AtomicFiles.Read<Document>(FileFor(id)).Request));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<OperationRequestSnapshot>(ex));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(Result<RunSnapshot>.Ok(Project(AtomicFiles.Read<Document>(FileFor(id)))));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<RunSnapshot>(ex));
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RunSnapshot> WatchAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var seen = new Dictionary<OperationId, string>();
        while (!ct.IsCancellationRequested)
        {
            Directory.CreateDirectory(Root);
            foreach (var file in Directory.EnumerateFiles(Root, "*.json"))
            {
                Document? document;
                try
                {
                    document = AtomicFiles.Read<Document>(file);
                }
                catch (Exception ex) when (AtomicFiles.Expected(ex))
                {
                    continue;
                }

                var projected = Project(document);
                var fingerprint = CanonicalDigest.Of(projected);
                if (seen.GetValueOrDefault(document.Snapshot.Id) == fingerprint) continue;
                seen[document.Snapshot.Id] = fingerprint;
                yield return projected;
            }

            await Task.Delay(200, ct);
        }
    }

    private RunSnapshot Project(Document document)
    {
        if (document.Owner is null || document.Snapshot.Stage is RunStage.Completed or RunStage.Failed
                or RunStage.Cancelled or RunStage.Exited or RunStage.Unconfirmed
                or RunStage.NeedsVerification) return document.Snapshot;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(document.Owner.ProcessId);
            if (!process.HasExited && process.StartTime.ToUniversalTime().Ticks == document.Owner.ProcessStartUtcTicks)
                try
                {
                    using var probe = new FileStream(Path.Combine(Root, document.Snapshot.Id + ".execution"),
                        FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException error) when ((error.HResult & 0xffff) == 32)
                {
                    return document.Snapshot;
                }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return document.Snapshot;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
            return document.Snapshot;
        }

        return document.Snapshot with
        {
            Stage = RunStage.NeedsVerification, ReasonCode = "ExecutionOwnerUnavailable",
            SafeMessage = "原执行进程已不可用，请核验原操作；不会自动重新执行。"
        };
    }

    /// <summary>检查当前执行租约是否仍属于指定操作身份。</summary>
    /// <include file="../../Comments.xml" path="doc/Id/*"/>
    /// <param name="parentId">握手时要求匹配的父进程标识。</param>
    /// <param name="parentStartTicks">握手时要求匹配的父进程 UTC 启动时间。</param>
    /// <returns>是否满足所述条件。</returns>
    public bool IsExecutionOwnedBy(OperationId id, int parentId, long parentStartTicks)
    {
        try
        {
            var document = AtomicFiles.Read<Document>(FileFor(id));
            if (document.Owner?.ProcessId != parentId || document.Owner.ProcessStartUtcTicks != parentStartTicks
                                                      || document.Snapshot.CancelRequested ||
                                                      document.Snapshot.Stage is RunStage.Completed or RunStage.Failed
                                                          or RunStage.Cancelled or RunStage.Unconfirmed
                                                          or RunStage.Exited
                                                          or RunStage.NeedsVerification) return false;
            using var parent = System.Diagnostics.Process.GetProcessById(parentId);
            if (parent.HasExited || parent.StartTime.ToUniversalTime().Ticks != parentStartTicks) return false;
            try
            {
                using var handle = new FileStream(Path.Combine(Root, id + ".execution"), FileMode.Open,
                    FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) == 32)
            {
                return true;
            }
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is InvalidOperationException)
        {
            return false;
        }
    }
}