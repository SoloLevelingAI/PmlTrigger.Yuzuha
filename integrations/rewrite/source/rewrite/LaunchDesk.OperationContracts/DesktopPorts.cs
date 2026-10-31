namespace LaunchDesk.OperationContracts;

/// <summary>迁移计划、非秘密预览快照与需核对的说明。</summary>
/// <param name="Plan">冻结的执行计划</param>
/// <param name="Preview">只读预览或脱敏说明</param>
/// <param name="Notes">预览中需用户核对的说明</param>
public sealed record MigrationPreview(MigrationPlan Plan, WorkspaceSnapshot Preview, string[] Notes);

/// <summary>读取明确来源并生成不改当前工作空间的迁移预览。</summary>
public interface IMigrationPlanningService
{
    /// <summary>读取明确来源并冻结文件摘要及目标预期修订；生成预览不启用迁移副本。</summary>
    /// <param name="sourcePath">明确选择的迁移来源文件路径；调用之间不共享选择状态。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<MigrationPreview>> PreviewAsync(string sourcePath, Revision expected, CancellationToken ct);
}

/// <summary>读写经过验证的分享文件，页面不直接访问文件系统。</summary>
public interface IConfigurationDocuments
{
    /// <summary>读取并验证指定 JSON 分享结构，失败时返回脱敏错误，不改工作空间。</summary>
    /// <param name="path">明确选择的文件或目录路径。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ShareBundle>> ReadShareAsync(string path, CancellationToken ct);
    /// <summary>原子写入不含秘密及执行权的分享结构到用户明确选择的位置。</summary>
    /// <param name="path">明确选择的文件或目录路径。</param>
    /// <param name="bundle">不含秘密或执行权的分享结构。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> WriteShareAsync(string path, ShareBundle bundle, CancellationToken ct);
}

/// <summary>只读查询当前工作空间的部署回执与旧版兼容记录。</summary>
public interface IDeploymentHistoryQueries
{
    /// <summary>返回按开始时间排序的新回执及旧版只读兼容记录，不执行恢复或回滚。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<IReadOnlyList<DeploymentReceipt>>> ReadAsync(CancellationToken ct);
}

/// <summary>按原操作号核验迁移结果，不重新执行迁移。</summary>
public interface IMigrationQueries
{
    /// <summary>先核验原请求属于配置迁移，再只读检查对应迁移副本的启用身份。</summary>
    /// <param name="operationId">执行/核验用原操作号。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<OperationFact>> InspectAsync(OperationId operationId, CancellationToken ct);
}
