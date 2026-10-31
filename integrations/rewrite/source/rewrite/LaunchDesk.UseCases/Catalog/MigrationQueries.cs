namespace LaunchDesk.UseCases;

/// <summary>核对原请求属于迁移后，按原编号只读观察迁移副本的启用身份。</summary>
/// <param name="journal">持久操作日志端口。</param>
/// <param name="migration">迁移预览或副本存储端口。</param>
public sealed class MigrationQueries(IRunJournal journal, IMigrationStore migration) : IMigrationQueries
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationFact>> InspectAsync(OperationId operationId, CancellationToken ct)
    {
        var request = await journal.ReadRequestAsync(operationId, ct);
        if (!request.IsSuccess) return Result<OperationFact>.Fail(request.Error!);
        if (request.Value.Kind != OperationKind.MigrateConfiguration)
            return Result<OperationFact>.Fail("WrongOperationKind", "请选择原迁移操作进行核验。");
        return await migration.InspectAsync(operationId, ct);
    }
}
