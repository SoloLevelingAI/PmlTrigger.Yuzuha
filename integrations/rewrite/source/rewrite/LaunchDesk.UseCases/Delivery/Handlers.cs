namespace LaunchDesk.UseCases;

/// <summary>拥有安装、恢复和回滚的部署状态机。</summary>
/// <param name="staging">私有暂存及计划存储端口。</param>
/// <param name="files">已检查的文件/普通文件映射集</param>
/// <param name="leases">目标互斥租约端口。</param>
/// <param name="privilege">可替换的有限管理员执行端口</param>
/// <param name="controlRoot">日志、暂存、回执和执行权的保护根</param>
public sealed class DeliveryOperationHandler(
    IStagingStore staging,
    IFileDeliveryPort files,
    ITargetLeaseProvider leases,
    IPrivilegeBroker privilege,
    string controlRoot) : IOperationHandler
{
    /// <inheritdoc />
    public async ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request,
        IOperationContext context, CancellationToken ct)
    {
        if (request.Kind != OperationKind.Deploy || request.Request.Deployment is not { } confirmed ||
            confirmed.ConfirmedDigest != confirmed.Plan.PlanDigest)
            return Result<HandlerOutcome>.Fail("PlanConfirmationRequired", "需要确认当前部署计划。");
        var valid = await staging.ValidateAsync(confirmed.Plan, ct);
        if (!valid.IsSuccess) return Result<HandlerOutcome>.Fail(valid.Error!);
        var intent =
            await context.ReportAsync(
                new OperationFact(RunStage.Checking, "DeploymentIntent", "开始核验已确认计划，目标写入前将持有互斥和保护租约。"), ct);
        if (!intent.IsSuccess) return Result<HandlerOutcome>.Fail(intent.Error!);
        if (request.Request.ElevationRequested)
        {
            var elevated = await privilege.ExecuteAsync(new PrivilegedRequest(request, controlRoot, context), ct);
            return elevated.IsSuccess && elevated.Value.Completed
                ? Result<HandlerOutcome>.Ok(new HandlerOutcome(RunStage.Completed, "DeploymentCompleted",
                    "管理员执行完成，回执已保存。"))
                : Result<HandlerOutcome>.Fail(
                    elevated.Error ?? new Error(elevated.Value.ReasonCode, "管理员执行未完成，请核验原操作。"));
        }

        var acquired = await leases.AcquireAsync(
        [
            new CanonicalPath(confirmed.Plan.TargetDirectory, confirmed.Plan.TargetIdentity,
                !confirmed.Plan.TargetIdentity.StartsWith("missing:", StringComparison.Ordinal), true)
        ], ct);
        if (!acquired.IsSuccess) return Result<HandlerOutcome>.Fail(acquired.Error!);
        await using var lease = acquired.Value;
        var result = await files.ApplyAsync(request.Id, confirmed, lease, ct);
        return result.IsSuccess && result.Value.State == "Completed"
            ? Result<HandlerOutcome>.Ok(new HandlerOutcome(RunStage.Completed, "DeploymentCompleted",
                "部署完成，原文件备份与回执已保存。"))
            : Result<HandlerOutcome>.Fail(result.Error ?? new Error("RecoveryRequired", "部署需要核验；请通过预览恢复或回滚，不要直接重放。"));
    }
}

/// <summary>持久变量应用、改名检查点和部分完成核验。</summary>
/// <param name="store">配置存储端口。</param>
/// <param name="privilege">可替换的有限管理员执行端口</param>
/// <param name="controlRoot">日志、暂存、回执和执行权的保护根</param>
public sealed class VariableOperationHandler(IEnvironmentStore store, IPrivilegeBroker privilege, string controlRoot)
    : IOperationHandler
{
    /// <inheritdoc />
    public async ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request,
        IOperationContext context, CancellationToken ct)
    {
        if (request.Kind != OperationKind.ApplyVariables || request.Request.Variables is null)
            return Result<HandlerOutcome>.Fail("MissingVariablePlan", "缺少已确认的变量计划。");
        if (request.Request.ElevationRequested)
        {
            var result = await privilege.ExecuteAsync(new PrivilegedRequest(request, controlRoot, context), ct);
            return result.IsSuccess && result.Value.Completed
                ? Result<HandlerOutcome>.Ok(new HandlerOutcome(RunStage.Completed, "VariablesApplied",
                    "变量已由管理员执行端核验应用。"))
                : Result<HandlerOutcome>.Fail(result.Error ?? new Error("VariablesUnconfirmed", "变量应用结果需要核验。"));
        }

        var applied = await store.ApplyVerifiedAsync(request.Request.Variables, context, ct);
        return applied.Map(_ => new HandlerOutcome(RunStage.Completed, "VariablesApplied", "持久变量已应用并复查。"));
    }
}

/// <summary>持久化/迁移模块的配置迁移状态机及操作处理器。</summary>
/// <param name="migration">迁移预览或副本存储端口。</param>
public sealed class WorkspaceMigrationService(IMigrationStore migration) : IOperationHandler
{
    /// <inheritdoc />
    public async ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request,
        IOperationContext context, CancellationToken ct)
    {
        var intent =
            await context.ReportAsync(new OperationFact(RunStage.Checking, "MigrationIntent", "先准备独立迁移副本，原文件保持原样。"),
                ct);
        if (!intent.IsSuccess) return Result<HandlerOutcome>.Fail(intent.Error!);
        var prepared = await migration.PrepareAsync(request, ct);
        if (!prepared.IsSuccess) return Result<HandlerOutcome>.Fail(prepared.Error!);
        var valid = await migration.ValidateAsync(request.Id, ct);
        if (!valid.IsSuccess) return Result<HandlerOutcome>.Fail(valid.Error!);
        var activated = await migration.ActivateAsync(request.Id, request.Request.ExpectedRevision, ct);
        return activated.Map(_ => new HandlerOutcome(RunStage.Completed, "MigrationCompleted",
            "迁移副本已启用，原配置与旧凭据引用保留。"));
    }
}