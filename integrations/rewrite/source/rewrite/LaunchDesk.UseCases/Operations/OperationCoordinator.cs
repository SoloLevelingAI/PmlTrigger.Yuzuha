using System.Collections.Concurrent;

namespace LaunchDesk.UseCases;

/// <summary>管理有限操作受理、首次执行准入、处理器作用域及取消通知。</summary>
/// <param name="journal">持久操作日志端口。</param>
/// <param name="registry">有限操作处理器注册表。</param>
/// <param name="store">配置存储端口。</param>
/// <param name="machineId"><include file="../../Comments.xml" path="doc/MachineId/param[@name='machineId']/*"/></param>
/// <param name="contextDecorator">可选的执行上下文诊断装饰器；不改执行权所有者。</param>
public sealed class OperationCoordinator(
    IRunJournal journal,
    IOperationHandlerRegistry registry,
    IWorkspaceStore store,
    Guid machineId, Func<IOperationContext, IOperationContext>? contextDecorator = null) : IOperationCommands, IAsyncDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentDictionary<OperationId, Task> tasks = new();
    private readonly ConcurrentDictionary<OperationId, CancellationTokenSource> cancellations = new();

    /// <inheritdoc />
    public async ValueTask<Result<OperationId>> SubmitAsync(OperationRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Kind) || request.Id.Value == Guid.Empty)
            return Result<OperationId>.Fail("UnsupportedOperationKind", "操作种类或标识无效。");
        var existing = await journal.ReadRequestAsync(request.Id, ct);
        if (existing.IsSuccess)
            return existing.Value.RequestDigest == request.RequestDigest
                ? Result<OperationId>.Ok(request.Id)
                : Result<OperationId>.Fail("OperationConflict", "操作标识已用于不同请求。");
        if (existing.Error!.Code != "OperationNotFound") return Result<OperationId>.Fail(existing.Error);
        var workspace = await store.ReadAsync(ct);
        if (!workspace.IsSuccess) return Result<OperationId>.Fail(workspace.Error!);
        if (workspace.Value.Revision != request.ExpectedRevision)
            return Result<OperationId>.Fail("RevisionConflict", "配置已改变，请重新生成本次请求。");
        if (request.Deployment is { } deployment && deployment.Plan.WorkspaceRevision != request.ExpectedRevision)
            return Result<OperationId>.Fail("RevisionConflict", "交付计划对应的配置已过期，请重新预览。");
        LaunchConfiguration? launch = null;
        if (request.Kind == OperationKind.Launch)
        {
            var profile = workspace.Value.Profiles.FirstOrDefault(p => p.Id == request.ProfileId);
            var app = workspace.Value.Applications.FirstOrDefault(a => a.Id == profile?.Fields.ApplicationId);
            if (profile is null || app is null) return Result<OperationId>.Fail("InvalidReference", "未找到配置或本机应用入口。");
            if (app.RequiresLocalBinding)
                return Result<OperationId>.Fail("LocalBindingRequired", "导入的入口尚未在本机确认，请到应用与版本核验并保存该应用。");
            var license = workspace.Value.LicenseEndpoints.FirstOrDefault(l => l.Id == profile.Fields.LicenseId)
                ?.Resource;
            if (license is not null && license.MachineId != machineId)
                return Result<OperationId>.Fail("MachineBindingRequired", "该连接属于其他电脑，请在本机重新保存连接配置。");
            if (app.Kind != EntryKind.Executable && license is null)
                return Result<OperationId>.Fail("ResourceRequired", "该应用需要选择许可连接。");
            launch = new LaunchConfiguration(workspace.Value.Revision, profile, app, license,
                workspace.Value.Tools.Where(t => profile.Fields.ToolIds.Contains(t.Id)).ToImmutableArray());
        }

        var planDigest = request.Kind switch
        {
            OperationKind.Launch => CanonicalDigest.Of(launch),
            OperationKind.Deploy => request.Deployment?.ConfirmedDigest,
            OperationKind.ApplyVariables => request.Variables?.ConfirmedDigest,
            OperationKind.MigrateConfiguration => request.Migration?.SourceSha256,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(planDigest)) return Result<OperationId>.Fail("MissingPlan", "缺少本次操作的冻结计划。");
        var accepted =
            await journal.AcceptAsync(
                new OperationRequestSnapshot(request, machineId, launch, planDigest, DateTimeOffset.UtcNow), ct);
        if (!accepted.IsSuccess) return Result<OperationId>.Fail(accepted.Error!);
        if (accepted.Value.Disposition == AcceptanceDisposition.Existing) return Result<OperationId>.Ok(request.Id);
        var execution = await journal.TryBeginExecutionAsync(accepted.Value, ct);
        if (!execution.IsSuccess) return Result<OperationId>.Fail(execution.Error!);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        cancellations[request.Id] = cancellation;
        // 调用方仅控制受理；已受理操作归应用生命周期。
        var task = Task.Run(() => ExecuteAsync(accepted.Value.Snapshot, execution.Value, cancellation));
        tasks[request.Id] = task;
        return Result<OperationId>.Ok(request.Id);
    }

    private async Task ExecuteAsync(OperationRequestSnapshot request, OperationExecutionLease execution,
        CancellationTokenSource cancellation)
    {
        await using var lease = execution;
        await using var context = contextDecorator?.Invoke(new OperationExecutionContext(journal, new OperationStatePolicy(), execution))
            ?? new OperationExecutionContext(journal, new OperationStatePolicy(), execution);
        try
        {
            var created = registry.Create(request.Kind);
            if (!created.IsSuccess)
            {
                await context.ReportAsync(
                    new OperationFact(RunStage.Failed, created.Error!.Code, created.Error.SafeMessage),
                    CancellationToken.None);
                return;
            }

            await using var handler = created.Value;
            var result = await handler.Handler.ExecuteAsync(request, context, cancellation.Token);
            var fact = result.IsSuccess
                ? new OperationFact(result.Value.Stage, result.Value.ReasonCode, result.Value.SafeMessage)
                : new OperationFact(FailureStage(result.Error!.Code), result.Error.Code, result.Error.SafeMessage);
            await context.ReportAsync(fact, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await context.ReportAsync(
                new OperationFact(RunStage.Unconfirmed, "ObservationStopped", "观察已停止；目标应用或已发生的写入不会被自动撤销或重放。"),
                CancellationToken.None);
        }
        catch (Exception)
        {
            await context.ReportAsync(
                new OperationFact(RunStage.NeedsVerification, "UnexpectedOperationFailure",
                    "操作结果需要核验，请查看原操作记录；不会自动重新执行。"), CancellationToken.None);
        }
        finally
        {
            cancellations.TryRemove(request.Id, out _);
            cancellation.Dispose();
        }
    }

    private static RunStage FailureStage(string code)
    {
        return code == "ElevationCancelled"
            ? RunStage.Cancelled
            : code.Contains("NeedsVerification", StringComparison.Ordinal) ||
              code is "RecoveryRequired" or "WorkerConnectionTimeout" or "AdmissionIndeterminate"
                ? RunStage.NeedsVerification
                : RunStage.Failed;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> CancelAsync(OperationId id, CancellationToken ct)
    {
        var result = await journal.RequestCancellationAsync(id, ct);
        if (!result.IsSuccess) return Result<Unit>.Fail(result.Error!);
        if (cancellations.TryGetValue(id, out var cancellation)) await cancellation.CancelAsync();
        return Result<Unit>.Ok(new Unit());
    }

    /// <summary>取消协调器生命周期并等待已登记执行任务结束；不直接终止外部目标应用。</summary>
    /// <returns>表示协调器拥有的执行任务已结束的任务。</returns>
    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        await Task.WhenAll(tasks.Values);
        lifetime.Dispose();
    }
}
