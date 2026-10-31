namespace LaunchDesk.UseCases;

/// <summary>在唯一执行权下串行持久提交单次操作事实。</summary>
/// <param name="journal">持久操作日志端口。</param>
/// <param name="policy">纯健康状态归约规则。</param>
/// <param name="execution">证明当前执行权仍有效的操作租约。</param>
public sealed class OperationExecutionContext(
    IRunJournal journal,
    OperationStatePolicy policy,
    OperationExecutionLease execution) : IOperationContext
{
    private readonly SemaphoreSlim gate = new(1);
    private bool disposed;

    /// <inheritdoc />
    public async ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (disposed) return Result<RunSnapshot>.Fail("ContextClosed", "操作上下文已经结束。");
            var current = await journal.ReadAsync(execution.Id, ct);
            if (!current.IsSuccess) return current;
            var transition = policy.Next(current.Value.Stage, fact.Stage);
            if (!transition.IsSuccess) return Result<RunSnapshot>.Fail(transition.Error!);
            return await journal.AppendAsync(execution, current.Value.Sequence,
                new RunEvent(current.Value.Sequence + 1, fact.Stage, fact.ReasonCode, fact.SafeMessage,
                    DateTimeOffset.UtcNow, fact.Evidence), ct);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>等待当前报告完成并关闭事实上下文，后续报告返回 ContextClosed。</summary>
    /// <returns>表示上下文关闭完成的任务。</returns>
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            disposed = true;
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>向 UI 暴露脱敏操作状态及版本。</summary>
/// <param name="journal">持久操作日志端口。</param>
public sealed class OperationProjectionService(IRunJournal journal) : ITaskQueries
{
    /// <inheritdoc />
    public ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)
    {
        return journal.ReadAsync(id, ct);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)
    {
        return journal.WatchAsync(ct);
    }
}