using System.Collections.Immutable;

namespace LaunchDesk.OperationContracts;

/// <summary>有限持久操作请求，须携带稳定号及预期配置修订。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Kind">支持的启动/部署/变量应用/配置迁移类别。</param>
/// <param name="ExpectedRevision">写入前须匹配的配置修订号</param>
/// <param name="ProfileId">登录配置的稳定标识</param>
/// <param name="Deployment">用户明确确认的部署计划；其他操作类别为空。</param>
/// <param name="Variables">工具变量定义或确认后的变量计划</param>
/// <param name="Migration">冻结来源与摘要的迁移计划；其他操作类别为空。</param>
/// <param name="ElevationRequested">是否明确请求管理员执行</param>
public sealed record OperationRequest(
    OperationId Id,
    OperationKind Kind,
    Revision ExpectedRevision,
    Guid? ProfileId = null,
    ConfirmedPlan? Deployment = null,
    ConfirmedVariablePlan? Variables = null,
    MigrationPlan? Migration = null,
    bool ElevationRequested = false)
{
    /// <summary>规范化操作请求的稳定摘要。</summary>
    public string RequestDigest => CanonicalDigest.Of(new
        { Id, Kind, ExpectedRevision, ProfileId, Deployment, Variables, Migration, ElevationRequested });
}

/// <summary>受理时冻结的操作请求、机器身份、启动配置及摘要。</summary>
/// <param name="Request">受理时冻结的有限操作请求</param>
/// <param name="MachineId">拥有本机绑定或执行权的机器标识</param>
/// <param name="Launch">受理启动时冻结的应用、登录及许可配置；其他操作类别为空。</param>
/// <param name="PlanDigest">冻结计划的规范化摘要</param>
/// <param name="AcceptedAtUtc">操作受理时的 UTC 时间</param>
public sealed record OperationRequestSnapshot(
    OperationRequest Request,
    Guid MachineId,
    LaunchConfiguration? Launch,
    string PlanDigest,
    DateTimeOffset AcceptedAtUtc)
{
    /// <summary>稳定标识。</summary>
    public OperationId Id => Request.Id;
    /// <summary>定义、入口、来源或操作的有限类别。</summary>
    public OperationKind Kind => Request.Kind;
    /// <summary>规范化操作请求的稳定摘要。</summary>
    public string RequestDigest => Request.RequestDigest;
}

/// <summary>带单调序号的持久操作事件。</summary>
/// <param name="Sequence">持久操作事件的单调序号</param>
/// <param name="Stage"><include file="../Comments.xml" path="doc/StageFact/Stage/*"/></param>
/// <param name="ReasonCode"><include file="../Comments.xml" path="doc/StageFact/ReasonCode/*"/></param>
/// <param name="SafeMessage"><include file="../Comments.xml" path="doc/StageFact/SafeMessage/*"/></param>
/// <param name="ObservedAtUtc">事实观察时的 UTC 时间</param>
/// <param name="Evidence">分项探测或观察证据</param>
public sealed record RunEvent(
    long Sequence,
    RunStage Stage,
    string ReasonCode,
    string SafeMessage,
    DateTimeOffset ObservedAtUtc,
    ImmutableArray<ProbeEvidence> Evidence = default)
{
    /// <summary>分项探测或观察证据。</summary>
    public ImmutableArray<ProbeEvidence> Evidence { get; init; } = Evidence.IsDefault ? [] : Evidence;
}

/// <summary>原操作的脱敏状态投影，含观察时间与证据。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Kind">支持的启动/部署/变量应用/配置迁移类别。</param>
/// <param name="Sequence">持久操作事件的单调序号</param>
/// <param name="Stage"><include file="../Comments.xml" path="doc/StageFact/Stage/*"/></param>
/// <param name="ReasonCode"><include file="../Comments.xml" path="doc/StageFact/ReasonCode/*"/></param>
/// <param name="SafeMessage"><include file="../Comments.xml" path="doc/StageFact/SafeMessage/*"/></param>
/// <param name="ObservedAtUtc">事实观察时的 UTC 时间</param>
/// <param name="CancelRequested">是否已持久记录停止请求</param>
/// <param name="Evidence">分项探测或观察证据</param>
/// <param name="SubjectId">操作主体的稳定标识</param>
/// <param name="SubjectName">操作主体的脱敏显示名称</param>
public sealed record RunSnapshot(
    OperationId Id,
    OperationKind Kind,
    long Sequence,
    RunStage Stage,
    string ReasonCode,
    string SafeMessage,
    DateTimeOffset ObservedAtUtc,
    bool CancelRequested,
    ImmutableArray<ProbeEvidence> Evidence,
    Guid? SubjectId = null,
    string? SubjectName = null);

/// <summary>处理器向执行上下文报告的有限阶段事实。</summary>
/// <param name="Stage"><include file="../Comments.xml" path="doc/StageFact/Stage/*"/></param>
/// <param name="ReasonCode"><include file="../Comments.xml" path="doc/StageFact/ReasonCode/*"/></param>
/// <param name="SafeMessage"><include file="../Comments.xml" path="doc/StageFact/SafeMessage/*"/></param>
/// <param name="Evidence">分项探测或观察证据</param>
public sealed record OperationFact(
    RunStage Stage,
    string ReasonCode,
    string SafeMessage,
    ImmutableArray<ProbeEvidence> Evidence = default)
{
    /// <summary>分项探测或观察证据。</summary>
    public ImmutableArray<ProbeEvidence> Evidence { get; init; } = Evidence.IsDefault ? [] : Evidence;
}

/// <summary>单次处理器执行完成后向协调器返回的阶段与脱敏原因。</summary>
/// <param name="Stage"><include file="../Comments.xml" path="doc/StageFact/Stage/*"/></param>
/// <param name="ReasonCode"><include file="../Comments.xml" path="doc/StageFact/ReasonCode/*"/></param>
/// <param name="SafeMessage"><include file="../Comments.xml" path="doc/StageFact/SafeMessage/*"/></param>
public sealed record HandlerOutcome(RunStage Stage, string ReasonCode, string SafeMessage);

/// <summary>观察到的进程或窗口事实，不等同于应用项目登录成功。</summary>
/// <param name="Kind">本项证据来自服务、TCP 还是虚拟机观察。</param>
/// <param name="ObservedAtUtc">事实观察时的 UTC 时间</param>
/// <param name="ProcessId">本机进程标识</param>
/// <param name="ReasonCode">稳定且不含秘密的原因码</param>
/// <param name="SafeMessage">可展示且无秘密的说明</param>
public sealed record ProcessEvidence(
    EvidenceKind Kind,
    DateTimeOffset ObservedAtUtc,
    int ProcessId,
    string ReasonCode,
    string SafeMessage);

/// <summary>工具包来源类型、路径及普通文件映射。</summary>
/// <param name="Kind">来源格式：ZIP、7z、文件夹或普通文件集合。</param>
/// <param name="Path">明确选择的本机路径</param>
/// <param name="Files"><include file="../Comments.xml" path="doc/Files/*"/></param>
public sealed record PackageSource(PackageKind Kind, string Path, ImmutableArray<ToolFile> Files = default)
{
    /// <summary>已检查的文件/普通文件映射集。</summary>
    public ImmutableArray<ToolFile> Files { get; init; } = Files.IsDefault ? [] : Files;
}

/// <summary>来源发现使用的目录与文件名模式。</summary>
/// <param name="Directory">待解析或发现的目录路径</param>
/// <param name="Pattern">来源发现采用的文件名模式</param>
public sealed record SourceQuery(string Directory, string Pattern = "*");

/// <summary>刷新前人工选择的来源路径。</summary>
/// <param name="Path">明确选择的本机路径</param>
public sealed record SourceSelection(string? Path);

/// <summary>发现到的候选来源及修改时间、类型和大小。</summary>
/// <param name="Path">明确选择的本机路径</param>
/// <param name="Kind">来源格式：ZIP、7z、文件夹或普通文件集合。</param>
/// <param name="ModifiedAtUtc">来源最后修改的 UTC 时间</param>
/// <param name="Bytes">内容字节数</param>
public sealed record SourceCandidate(string Path, PackageKind Kind, DateTimeOffset ModifiedAtUtc, long Bytes);

/// <summary>来源候选集合与保留或明确更新后的选择。</summary>
/// <param name="Candidates">本轮发现的候选项集合</param>
/// <param name="Selection">保留或明确更新后的候选选择</param>
public sealed record SourceCatalog(ImmutableArray<SourceCandidate> Candidates, SourceCandidate? Selection = null);

/// <summary>已检查源文件的相对路径、内容摘要与大小。</summary>
/// <param name="SourcePath">明确选择的源文件路径</param>
/// <param name="RelativePath">相对目标根的路径</param>
/// <param name="Sha256">内容的 SHA-256 摘要</param>
/// <param name="Bytes">内容字节数</param>
public sealed record SourceFile(string SourcePath, string RelativePath, string Sha256, long Bytes);

/// <summary>来源预检或暂存后的不可变内容快照。</summary>
/// <param name="Source">工具来源或来源快照</param>
/// <param name="SourceDigest">来源文件及其映射的内容摘要</param>
/// <param name="Files"><include file="../Comments.xml" path="doc/Files/*"/></param>
/// <param name="StagingId">私有暂存内容的标识</param>
/// <param name="ArchiveSha256">归档文件本身的 SHA-256 摘要</param>
public sealed record SourceSnapshot(
    PackageSource Source,
    string SourceDigest,
    ImmutableArray<SourceFile> Files,
    string? StagingId = null,
    string? ArchiveSha256 = null);

/// <summary>经 Windows 路径检查取得的路径与文件系统身份。</summary>
/// <param name="Path">明确选择的本机路径</param>
/// <param name="Identity">规范化文件系统身份</param>
/// <param name="Exists">路径是否已存在</param>
/// <param name="IsDirectory">路径是否表示目录</param>
public sealed record CanonicalPath(string Path, string Identity, bool Exists, bool IsDirectory);

/// <summary>明确指定的部署目标目录。</summary>
/// <param name="Directory">待解析或发现的目录路径</param>
public sealed record TargetSpec(string Directory);

/// <summary>目标预检发现的文件、摘要与大小。</summary>
/// <param name="RelativePath">相对目标根的路径</param>
/// <param name="Sha256">内容的 SHA-256 摘要</param>
/// <param name="Bytes">内容字节数</param>
public sealed record TargetFile(string RelativePath, string Sha256, long Bytes);

/// <summary>目标根身份、当前文件及基线摘要。</summary>
/// <param name="Root">核验后的目标根目录及身份</param>
/// <param name="Files"><include file="../Comments.xml" path="doc/Files/*"/></param>
/// <param name="Digest">规范化内容摘要</param>
public sealed record TargetSnapshot(CanonicalPath Root, ImmutableArray<TargetFile> Files, string Digest);

/// <summary>冻结计划中的单个文件变更及写入前基线。</summary>
/// <param name="RelativePath">相对目标根的路径</param>
/// <param name="SourcePath">明确选择的源文件路径</param>
/// <param name="SourceSha256">来源内容的 SHA-256 摘要</param>
/// <param name="SourceBytes">来源文件大小，单位为字节</param>
/// <param name="BeforeSha256">写入前文件的内容摘要；原文件缺失时为空</param>
/// <param name="BeforeBytes">写入前文件大小</param>
/// <param name="Delete">本条变更是否移除文件</param>
public sealed record DeploymentFile(
    string RelativePath,
    string SourcePath,
    string SourceSha256,
    long SourceBytes,
    string? BeforeSha256,
    long BeforeBytes,
    bool Delete = false);

/// <summary>生成部署预览所需的来源、目标和备份选项。</summary>
/// <param name="WorkspaceRevision">受理/预览冻结的配置修订</param>
/// <param name="ToolId">关联工具的稳定标识</param>
/// <param name="Source">工具来源或来源快照</param>
/// <param name="Target">探测目标或配置目标路径</param>
/// <param name="KeepBackup">是否保留交付前文件备份</param>
/// <param name="FlattenRoot">是否展开压缩包中唯一的顶层目录</param>
public sealed record DeployRequest(
    Revision WorkspaceRevision,
    Guid ToolId,
    SourceSnapshot Source,
    TargetSpec Target,
    bool KeepBackup = true,
    bool FlattenRoot = true);

/// <summary>基于原操作回执生成恢复或回滚预览的请求。</summary>
/// <param name="WorkspaceRevision">受理/预览冻结的配置修订</param>
/// <param name="Original">关联的原操作标识</param>
/// <param name="Action">本次预览准备执行的安装、恢复或回滚动作。</param>
public sealed record RecoveryRequest(Revision WorkspaceRevision, OperationId Original, DeploymentAction Action);

/// <summary>确认前冻结的文件变更、来源及目标基线；摘要用于检测计划替换。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Action">本次预览准备执行的安装、恢复或回滚动作。</param>
/// <param name="WorkspaceRevision">受理/预览冻结的配置修订</param>
/// <param name="ToolId">关联工具的稳定标识</param>
/// <param name="SourceDigest">来源文件及其映射的内容摘要</param>
/// <param name="TargetDirectory">本次工具交付的目标目录</param>
/// <param name="TargetIdentity">预览取得的目标文件系统身份，执行前须仍然匹配。</param>
/// <param name="Files"><include file="../Comments.xml" path="doc/Files/*"/></param>
/// <param name="KeepBackup">是否保留交付前文件备份</param>
/// <param name="PlanDigest">冻结计划的规范化摘要</param>
/// <param name="Original">关联的原操作标识</param>
/// <param name="OriginalSource">本次计划冻结的原始来源描述</param>
public sealed record DeploymentPlan(
    Guid Id,
    DeploymentAction Action,
    Revision WorkspaceRevision,
    Guid ToolId,
    string SourceDigest,
    string TargetDirectory,
    string TargetIdentity,
    ImmutableArray<DeploymentFile> Files,
    bool KeepBackup,
    string PlanDigest,
    OperationId? Original = null,
    PackageSource? OriginalSource = null);

/// <summary>用户明确确认的部署计划及当时的摘要。</summary>
/// <param name="Plan">冻结的执行计划</param>
/// <param name="ConfirmedDigest">用户明确确认时的计划摘要</param>
public sealed record ConfirmedPlan(DeploymentPlan Plan, string ConfirmedDigest)
{
    /// <summary>固定用户明确确认时的计划及其摘要，或等待对话框的明确确认。</summary>
    /// <include file="../Comments.xml" path="doc/Plan/*"/>
    /// <returns>绑定原计划及其当前摘要的确认对象；该方法自身不执行部署。</returns>
    public static ConfirmedPlan Confirm(DeploymentPlan plan)
    {
        return new ConfirmedPlan(plan, plan.PlanDigest);
    }
}

/// <summary>单个文件写入意图、备份、实际应用及恢复结果的回执。</summary>
/// <param name="RelativePath">相对目标根的路径</param>
/// <param name="BeforeSha256">写入前文件的内容摘要；原文件缺失时为空</param>
/// <param name="AfterSha256">计划应用后的内容摘要</param>
/// <param name="BackupPath">原文件备份路径</param>
/// <param name="IntentWritten">单文件写入意图是否已持久记录</param>
/// <param name="Applied">是否已观察并记录应用结果</param>
/// <param name="RestoredSha256">恢复后观察到的文件摘要</param>
public sealed record DeploymentFileReceipt(
    string RelativePath,
    string? BeforeSha256,
    string AfterSha256,
    string? BackupPath,
    bool IntentWritten,
    bool Applied,
    string? RestoredSha256 = null);

/// <summary>部署操作的持久回执与文件恢复线索。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="ToolId">关联工具的稳定标识</param>
/// <param name="PlanDigest">冻结计划的规范化摘要</param>
/// <param name="TargetDirectory">本次工具交付的目标目录</param>
/// <param name="TargetIdentity">预览取得的目标文件系统身份，执行前须仍然匹配。</param>
/// <param name="State">当前持久回执、提交或资源状态。</param>
/// <param name="Files"><include file="../Comments.xml" path="doc/Files/*"/></param>
/// <param name="StartedAtUtc">开始时间，使用 UTC</param>
/// <param name="FinishedAtUtc">执行结束的 UTC 时间；未结束时为空</param>
/// <param name="ReasonCode">稳定且不含秘密的原因码</param>
/// <param name="Original">关联的原操作标识</param>
/// <param name="PlanId">计划的稳定标识</param>
/// <param name="ResolvedBy">处理该回执的恢复或回滚操作标识</param>
public sealed record DeploymentReceipt(
    OperationId Id,
    Guid ToolId,
    string PlanDigest,
    string TargetDirectory,
    string TargetIdentity,
    string State,
    ImmutableArray<DeploymentFileReceipt> Files,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string ReasonCode,
    OperationId? Original = null,
    Guid PlanId = default,
    OperationId? ResolvedBy = null);

/// <summary>环境变量的名称与作用域。</summary>
/// <param name="Name">显示名或配置名</param>
/// <param name="Scope">变量作用域</param>
public sealed record VariableAddress(string Name, VariableScope Scope);

/// <summary>读取到的变量地址与原值；空值表示变量不存在。</summary>
/// <param name="Address">变量名称与作用域</param>
/// <param name="Value">读取到的变量值；空值表示该地址不存在。</param>
public sealed record VariableSnapshot(VariableAddress Address, string? Value);

/// <summary>环境变量改名或更新的预览输入。</summary>
/// <param name="OldName">待改名变量的原名称</param>
/// <param name="NewName">用户确认的新变量名称</param>
/// <param name="NewValue">用户确认的新变量值；空值表示删除</param>
/// <param name="Scope">变量作用域</param>
public sealed record VariableRenameRequest(string OldName, string NewName, string? NewValue, VariableScope Scope);

/// <summary>冻结的旧变量、新名称、目标基线及摘要。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Before">原变量地址及读取到的值</param>
/// <param name="TargetBefore">新变量名对应地址的原值基线</param>
/// <param name="NewName">用户确认的新变量名称</param>
/// <param name="NewValue">用户确认的新变量值；空值表示删除</param>
/// <param name="PlanDigest">冻结计划的规范化摘要</param>
public sealed record VariableChangePlan(
    Guid Id,
    VariableSnapshot Before,
    VariableSnapshot TargetBefore,
    string NewName,
    string? NewValue,
    string PlanDigest);

/// <summary>用户明确确认的变量计划及当时的摘要。</summary>
/// <param name="Plan">冻结的执行计划</param>
/// <param name="ConfirmedDigest">用户明确确认时的计划摘要</param>
public sealed record ConfirmedVariablePlan(VariableChangePlan Plan, string ConfirmedDigest);

/// <summary>迁移来源路径、内容摘要及目标预期修订。</summary>
/// <param name="SourcePath">明确选择的源文件路径</param>
/// <param name="SourceSha256">来源内容的 SHA-256 摘要</param>
/// <param name="ExpectedRevision">写入前须匹配的配置修订号</param>
public sealed record MigrationPlan(string SourcePath, string SourceSha256, Revision ExpectedRevision);

/// <summary>向受信任 Worker 发送的冻结请求及父端事实记录上下文。</summary>
/// <param name="Snapshot"><include file="../Comments.xml" path="doc/Snapshot/*"/></param>
/// <param name="ControlRoot">日志、暂存、回执和执行权的保护根</param>
/// <param name="Context">父端拥有的事实记录上下文；不能通过 JSON 传输执行能力。</param>
public sealed record PrivilegedRequest(
    OperationRequestSnapshot Snapshot,
    string ControlRoot,
    [property: System.Text.Json.Serialization.JsonIgnore]
    IOperationContext? Context = null);

/// <summary>跨进程传输的冻结操作与父进程、启动时间及 nonce 身份。</summary>
/// <param name="Version">版本名称或策略版本</param>
/// <param name="Snapshot"><include file="../Comments.xml" path="doc/Snapshot/*"/></param>
/// <param name="ControlRoot">日志、暂存、回执和执行权的保护根</param>
/// <param name="Nonce">本次执行端握手的随机 nonce</param>
/// <param name="ParentProcessId">原父进程的系统标识</param>
/// <param name="ParentStartUtcTicks">父进程 UTC 启动时间的 ticks，用于防止 PID 复用</param>
public sealed record WorkerEnvelope(
    int Version,
    OperationRequestSnapshot Snapshot,
    string ControlRoot,
    string Nonce,
    int ParentProcessId,
    long ParentStartUtcTicks);

/// <summary>Worker 返回的脱敏完成状态，不携带秘密。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="ReasonCode">稳定且不含秘密的原因码</param>
/// <param name="Completed">Worker 是否确定完成了有限操作</param>
public sealed record WorkerReceipt(OperationId Id, string ReasonCode, bool Completed);