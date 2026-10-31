namespace LaunchDesk.UseCases;

/// <summary>拥有启动状态机、启动意图及后续只读观察。</summary>
/// <param name="builder">仅从冻结配置构造启动计划的纯规则服务。</param>
/// <param name="resources">独立资源监测端口。</param>
/// <param name="vault">本机凭据端口。</param>
/// <param name="processes">测试可替换的进程端口</param>
public sealed class LaunchOperationHandler(
    LaunchPlanBuilder builder,
    IResourceMonitor resources,
    ISecretVault vault,
    IProcessGateway processes) : IOperationHandler
{
    /// <inheritdoc />
    public async ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request,
        IOperationContext context, CancellationToken ct)
    {
        if (request.Launch is null || request.Kind != OperationKind.Launch)
            return Result<HandlerOutcome>.Fail("InvalidLaunch", "缺少冻结启动配置。");
        var environment = processes.CaptureEnvironment();
        if (!environment.IsSuccess) return Result<HandlerOutcome>.Fail(environment.Error!);
        var built = builder.Build(request.Launch, environment.Value);
        if (!built.IsSuccess) return Result<HandlerOutcome>.Fail(built.Error!);
        var plan = built.Value;
        var checking =
            await context.ReportAsync(new OperationFact(RunStage.Checking, "CheckingResource", "正在检查本次绑定的许可与启动计划。"),
                ct);
        if (!checking.IsSuccess) return Result<HandlerOutcome>.Fail(checking.Error!);
        if (plan.Resource is not null)
        {
            var ready = await resources.RequireFreshAsync(plan.Resource, ct);
            if (!ready.IsSuccess) return Result<HandlerOutcome>.Fail(ready.Error!);
        }

        SecretLease? secret = null;
        try
        {
            if (plan.Arguments.Any(a => a.SecretSlot))
            {
                if (plan.Credential is null) return Result<HandlerOutcome>.Fail("CredentialMissing", "请先在编辑配置中输入本机密码。");
                var resolved = await vault.ResolveAsync(plan.Credential, ct);
                if (!resolved.IsSuccess) return Result<HandlerOutcome>.Fail(resolved.Error!);
                secret = resolved.Value;
            }

            var intent =
                await context.ReportAsync(
                    new OperationFact(RunStage.Checking, "ProcessStartIntent", "已持久记录启动意图，准备创建一次目标进程。"), ct);
            if (!intent.IsSuccess) return Result<HandlerOutcome>.Fail(intent.Error!);
            var started = await processes.StartAsync(plan, secret, ct);
            secret?.Dispose();
            secret = null;
            if (!started.IsSuccess) return Result<HandlerOutcome>.Fail(started.Error!);
            await using var tracked = started.Value;
            var handoff =
                await context.ReportAsync(new OperationFact(RunStage.HandedOff, "ProcessCreated", "入口进程已创建，正在观察模块交接。"),
                    ct);
            if (!handoff.IsSuccess)
                return Result<HandlerOutcome>.Fail("StartNeedsVerification", "目标可能已启动，但状态保存未确认，请先核验原操作。");
            var stage = RunStage.HandedOff;
            await foreach (var evidence in processes.ObserveAsync(tracked, ct))
            {
                stage = evidence.Kind switch
                {
                    EvidenceKind.WindowObserved => RunStage.WindowObserved,
                    EvidenceKind.SessionConfirmed => RunStage.SessionConfirmed,
                    EvidenceKind.SessionExited => RunStage.Exited, _ => stage
                };
                var reported =
                    await context.ReportAsync(new OperationFact(stage, evidence.ReasonCode, evidence.SafeMessage), ct);
                if (!reported.IsSuccess) return Result<HandlerOutcome>.Fail(reported.Error!);
                if (stage == RunStage.Exited)
                    return Result<HandlerOutcome>.Ok(new HandlerOutcome(stage, evidence.ReasonCode,
                        evidence.SafeMessage));
            }

            return Result<HandlerOutcome>.Ok(new HandlerOutcome(RunStage.Unconfirmed, "SessionUnconfirmed",
                "入口或窗口已观察；尚无独立项目登录证据，不能确认登录成功。"));
        }
        finally
        {
            secret?.Dispose();
        }
    }
}