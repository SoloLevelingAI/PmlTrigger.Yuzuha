using System.Collections.Concurrent;
using LaunchDesk.OperationContracts;
using LaunchDesk.Windows;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LaunchDesk.DevHost;

/// <summary>提供本机开发宿主的健康与脱敏操作事实查询，不提供写入或执行能力。</summary>
public static class DiagnosticEndpoints
{
    /// <summary>查询开发诊断宿主状态。</summary>
    /// <remarks>ready 只表示 HTTP 宿主已启动，不代表许可连通、VMware 运行或 AVEVA 项目登录成功。</remarks>
    /// <param name="state">由依赖注入提供的本机诊断投影，不是 HTTP 查询参数。</param>
    /// <returns>宿主模式、机器身份及桌面是否为必需组件。</returns>
    /// <response code="200">宿主已就绪；未执行许可或项目检查。</response>
    public static Ok<HostHealthResponse> ReadHealth(DiagnosticState state) =>
        TypedResults.Ok(new HostHealthResponse("ready", "Aspire-like", false, state.MachineId));

    /// <summary>列出当前工作空间已持久记录的操作事实。</summary>
    /// <remarks>按最近观察时间降序；空列表表示无操作。查询不重新受理/执行请求，不含密码、凭据引用、命令行或部署文件内容。</remarks>
    /// <param name="state">由依赖注入提供的只读操作投影，不是 HTTP 查询参数。</param>
    /// <returns>每个原操作最新阶段的脱敏列表。</returns>
    /// <response code="200">已返回当前原操作投影；没有记录时返回空数组。</response>
    public static Ok<OperationSummary[]> ReadOperations(DiagnosticState state) => TypedResults.Ok(state.ReadOperations());
}

/// <summary>开发诊断 HTTP 宿主的就绪说明；不表示目标应用或许可已就绪。</summary>
/// <param name="Status">HTTP 宿主状态；当前为 ready。</param>
/// <param name="Mode">开发编排模式；普通 .NET 宿主返回 Aspire-like。</param>
/// <param name="DesktopRequired">访问只读诊断接口是否要求桌面程序正在运行；当前为 false。</param>
/// <param name="Machine">当前工作空间绑定的机器标识，不含本机凭据。</param>
public sealed record HostHealthResponse(string Status, string Mode, bool DesktopRequired, Guid Machine);

/// <summary>用于 HTTP 返回的原操作事实，字段限于脱敏状态，不暴露内部执行能力。</summary>
/// <param name="Id">原操作稳定号；查询和核验时须沿用此编号。</param>
/// <param name="Kind">有限操作类别的枚举名称。</param>
/// <param name="Stage">最新持久阶段；WindowObserved 不代表项目登录成功。</param>
/// <param name="Sequence">该操作的最新单调事件序号。</param>
/// <param name="SafeMessage">处理器持久记录的脱敏说明。</param>
/// <param name="ObservedAtUtc">事实观察时的 UTC 时间。</param>
public sealed record OperationSummary(string Id, string Kind, string Stage, long Sequence, string SafeMessage,
    DateTimeOffset ObservedAtUtc);

/// <summary>保存同一工作空间按原操作号索引的只读诊断投影。</summary>
/// <param name="machineId">工作空间位置解析器确定的机器身份。</param>
public sealed class DiagnosticState(Guid machineId)
{
    private readonly ConcurrentDictionary<string, OperationSummary> operations = new();

    /// <summary>当前工作空间的机器标识。</summary>
    public Guid MachineId { get; } = machineId;

    /// <summary>返回按最近观察时间排序的不可变脱敏投影数组。</summary>
    /// <returns>每个原操作最新事件的独立数组；无记录时为空数组。</returns>
    public OperationSummary[] ReadOperations() => operations.Values.OrderByDescending(run => run.ObservedAtUtc)
        .ThenBy(run => run.Id, StringComparer.Ordinal).ToArray();

    internal void Observe(RunSnapshot run)
    {
        var summary = new OperationSummary(run.Id.ToString(), run.Kind.ToString(), run.Stage.ToString(), run.Sequence,
            run.SafeMessage, run.ObservedAtUtc);
        operations.AddOrUpdate(summary.Id, summary, (_, prior) => summary.Sequence >= prior.Sequence ? summary : prior);
    }
}

internal sealed class OperationJournalObserver(JsonRunJournal journal, DiagnosticState state) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var run in journal.WatchAsync(stoppingToken)) state.Observe(run);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
