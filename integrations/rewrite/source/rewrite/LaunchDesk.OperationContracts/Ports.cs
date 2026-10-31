namespace LaunchDesk.OperationContracts;

/// <summary>配置查询、稳定提交及脱敏历史结果核验。</summary>
public interface ICatalogService
{
    /// <summary>返回不可变公开配置投影，不含秘密/CredentialRef；不代替CommitId核验。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct);

    /// <summary>建立含秘密更新意图及输入标识的CommitIntent，准备会话；仅首次写会话可创建Replace新凭据再提交配置。同ID同摘要返回原确定结果/未决状态。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <param name="draft">未持久化的可编辑草稿。</param>
    /// <param name="change">保留、清除或替换本机秘密关联的明确意图。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(CommitId commitId, ProfileDraft draft, SecretUpdate change,
        Revision expected, CancellationToken ct);

    /// <summary>首次准备分配并持久记录新ID；复制独立字段，不复制秘密引用；重复ID不再生成记录。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <param name="sourceId">复制来源登录配置的稳定标识。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ProfileSnapshot>> CopyAsync(CommitId commitId, Guid sourceId, Revision expected,
        CancellationToken ct);

    /// <summary>经稳定提交仅删管理记录；已提交的重复请求返回原结果，不因记录已不存在而改判失败。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <param name="profileId">目标登录配置的稳定标识。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> DeleteAsync(CommitId commitId, Guid profileId, Revision expected, CancellationToken ct);

    /// <summary>稳定提交保存有限定义及人工绑定来源，仅存配置，不写持久环境；同ID返回原提交修订。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <param name="draft">未持久化的可编辑草稿。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Revision>> SaveDefinitionAsync(CommitId commitId, DefinitionDraft draft, Revision expected,
        CancellationToken ct);

    /// <summary>首次会话内调用发现并冻结合并结果，保护人工绑定；已有同 ID 请求不重扫或合并。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <include file="../Comments.xml" path="doc/ApplicationId/*"/>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<WorkspaceSnapshot>> RefreshDiscoveryAsync(CommitId commitId, Guid applicationId, Revision expected,
        CancellationToken ct);

    /// <summary>返回原提交状态、修订、生成 ID 及脱敏结果；不创建新提交，不解析秘密。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct);
}

/// <summary>白名单分享、导入草稿及可核验的配置提交。</summary>
public interface IConfigurationShareService
{
    /// <summary>重构允许字段，排除秘密、凭据引用、可直接采用的机器绑定、运行记录、备份和变量应用状态。</summary>
    /// <param name="selection">明确选择的登录、工具或来源。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ShareBundle>> ExportAsync(ShareSelection selection, CancellationToken ct);
    /// <summary>校验版本、大小、数量和关联，形成固定 ID 映射及待绑定草稿；外机路径仅作提示。</summary>
    /// <param name="bundle">不含秘密或执行权的分享结构。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ImportProposal>> PreviewImportAsync(ShareBundle bundle, CancellationToken ct);

    /// <summary>稳定提交保存确认草稿；同ID同摘要返回原修订/ID映射，不重复导入。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <param name="proposal">已核对来源与本机绑定的导入预览。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Revision>> CommitAsync(CommitId commitId, ImportProposal proposal, Revision expected,
        CancellationToken ct);

    /// <summary>返回原导入提交状态、修订及生成 ID 映射；不重新导入。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct);
}

/// <summary>当前配置读取、跨进程提交准备、一次有效版本切换及长期提交核验。</summary>
public interface IWorkspaceStore
{
    /// <summary>读取有效配置，区分缺失/损坏/旧格式/合法空数据；不迁移，不推断历史CommitId结果。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<WorkspaceSnapshotInternal>> ReadAsync(CancellationToken ct);
    /// <summary>先按CommitId查历史；新请求取得跨进程工作区锁后核对修订、分配稳定ID映射并可靠记录准备事实。仅首次可靠成功返回写会话；同ID同摘要返回历史只读会话，不再创建实体/凭据；异摘要拒绝。</summary>
    /// <param name="intent">稳定提交或来源刷新采用的明确意图。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<WorkspaceCommitSession>> BeginCommitAsync(CommitIntent intent, CancellationToken ct);

    /// <summary>仅接受有效首次会话；核验变更与冻结意图、ID映射及修订一致，绑定配置候选和原提交结果元数据，可靠写入后唯一切换有效版本。配置可见性须绑定可核验提交身份。</summary>
    /// <param name="session">已取得的首次或复用提交会话。</param>
    /// <param name="change">与首次提交会话冻结意图一致的完整配置候选。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<CommitReceipt>> CommitAsync(WorkspaceCommitSession session, WorkspaceChange change,
        CancellationToken ct);

    /// <summary>按可靠准备记录、有效版本身份及持久提交历史核验；后续修订不覆盖原结果。登记完整可读才返回NotFound；原会话存活可为Pending；证据不足为Indeterminate。</summary>
    /// <include file="../Comments.xml" path="doc/CommitId/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<CommitReceipt>> ReadCommitAsync(CommitId commitId, CancellationToken ct);
}

/// <summary>显式 --config、portable、默认目录的唯一解析与打开入口。</summary>
public interface IWorkspaceLocationResolver
{
    /// <summary>按优先级选择；相对 portable 路径基于 EXE，不受工作目录影响。</summary>
    /// <include file="../Comments.xml" path="doc/Options/*"/>
    /// <param name="executableDirectory">EXE目录，用于解析便携相对路径。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    Result<WorkspaceLocation> Resolve(StartupOptions options, string executableDirectory);
    /// <summary>创建缺失目录并以目录对象打开，不伪造配置。</summary>
    /// <include file="../Comments.xml" path="doc/Location/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> OpenDirectoryAsync(WorkspaceLocation location, CancellationToken ct);
}

/// <summary>按已登记应用规则发现本机入口候选，不改人工绑定。</summary>
public interface IEntryDiscovery
{
    /// <summary>根据已注册产品规则返回本机候选路径；不选择最终入口，也不修改人工绑定。</summary>
    /// <include file="../Comments.xml" path="doc/ApplicationId/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<IReadOnlyList<string>>> FindAsync(Guid applicationId, CancellationToken ct);
}

/// <summary>隔离 Windows 凭据 API，秘密只在本机短暂取得。</summary>
public interface ISecretVault
{
    /// <summary>新建引用，不原地覆盖有效旧引用。</summary>
    /// <include file="../Comments.xml" path="doc/Secret/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct);
    /// <summary>返回可释放租约，不回填页面或进入日志。</summary>
    /// <param name="reference">需解析的本机凭据引用。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct);
    /// <summary>仅清理已确认无引用凭据。</summary>
    /// <param name="unreferenced">已证明不再被配置、操作或秘密租约引用的凭据标识。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> DeleteAsync(CredentialRef unreferenced, CancellationToken ct);
}

/// <summary>采集冻结资源描述对应的一次分项观测。</summary>
public interface IResourceProbe
{
    /// <summary>核验描述，按其端点/选择参数探测一次，返回相同完整身份键及实际来源证据；不查当前配置替换参数。</summary>
    /// <param name="resource">探测/启动冻结的资源描述</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct);
}

/// <summary>提供当前资源刷新与精确身份的新鲜事实查询。</summary>
public interface IResourceMonitor
{
    /// <summary>单次读取并冻结当前资源描述，按完整身份键调度有界轮次；结果携带本次冻结身份，不保证为返回时最新配置。</summary>
    /// <param name="resourceId">许可资源的稳定标识</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ProbeSnapshot>> RefreshAsync(Guid resourceId, CancellationToken ct);
    /// <summary>仅查完全匹配expected身份及策略版本的新鲜证据；缺失则探测expected，不能改探当前同ID端点。返回分项事实，由启动规则决定业务是否继续。</summary>
    /// <param name="expected">本次启动冻结的完整资源描述；证据须匹配身份键、修订、机器及策略，不改用当前目录另一版端点。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ProbeSnapshot>> RequireFreshAsync(ResourceDescriptor expected, CancellationToken ct);
    /// <summary>先快照后增量，每条附完整身份；当前配置投影不被旧版本迟到结果替换。取消只结束订阅。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/param"/>
    /// <include file="../Comments.xml" path="doc/Result/*"/>
    IAsyncEnumerable<ProbeSnapshot> WatchAsync(CancellationToken ct);
}

/// <summary>有限操作的持久受理、查询式去重及取消请求入口。</summary>
public interface IOperationCommands
{
    /// <summary>冻结并持久受理请求；仅首次Created可尝试一次执行准入；Existing仅返回原OperationId。取得并确认执行租约后才创建处理器作用域。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<OperationId>> SubmitAsync(OperationRequest request, CancellationToken ct);
    /// <summary>先持久记录取消请求，再通知执行者；未取得执行权者由日志准入检查阻止执行。受理取消不代表已停止/回滚。</summary>
    /// <include file="../Comments.xml" path="doc/Id/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> CancelAsync(OperationId id, CancellationToken ct);
}

/// <summary>唯一把有限操作种类映射到具体处理器工厂的位置。</summary>
public interface IOperationHandlerRegistry
{
    /// <summary>用组合根工厂映射支持Launch/Deploy/ApplyVariables/MigrateConfiguration；每次返回独立作用域，协调模块不引用业务实现。</summary>
    /// <param name="kind">支持的启动/部署/变量应用/配置迁移类别。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    Result<HandlerLease> Create(OperationKind kind);
}

/// <summary>有限模块操作执行及模块状态机边界。</summary>
public interface IOperationHandler
{
    /// <summary>核对有限种类、快照版本与摘要；模块拥有业务状态及安全取消规则，事实经上下文发布。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <param name="context">当前操作持有的阶段事实记录上下文。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request, IOperationContext context,
        CancellationToken ct);
}

/// <summary>绑定一个执行租约的事实报告和释放边界。</summary>
public interface IOperationContext : IAsyncDisposable
{
    /// <summary>在当前执行租约下核验阶段转换并追加有序操作事实。</summary>
    /// <param name="fact">准备追加的有限阶段、原因码、脱敏提示及证据。</param>
    /// <param name="ct">取消等待上下文锁或日志写入的令牌；既有持久事实不自动撤销。</param>
    /// <returns>追加后的原操作快照，或租约、阶段及持久写入失败的脱敏结果。</returns>
    ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct);
}

/// <summary>持久受理、执行准入、控制请求及有序操作事实的权威存储。</summary>
public interface IRunJournal
{
    /// <summary>跨进程操作锁内持久保存首次请求及受理事实，绑定首次提交进程身份；可靠落盘后仅首次调用取得Created和一次性准入凭据。同ID同摘要返回无凭据的Existing；异摘要拒绝。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<OperationAcceptance>> AcceptAsync(OperationRequestSnapshot request, CancellationToken ct);

    /// <summary>同一操作锁内核验首次凭据、活进程身份、Accepted及无取消请求，原子持久推进至Executing并消费凭据；刷新成功才返回执行租约。已消费、执行过或持有者失效均禁止再取得。</summary>
    /// <param name="acceptance">原受理请求的稳定快照与准入结果。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<OperationExecutionLease>> TryBeginExecutionAsync(OperationAcceptance acceptance,
        CancellationToken ct);

    /// <summary>操作锁内追加有序取消控制事实；同一取消意图幂等；与执行准入竞争时按可靠落盘顺序决定准入。</summary>
    /// <include file="../Comments.xml" path="doc/Id/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<RunSnapshot>> RequestCancellationAsync(OperationId id, CancellationToken ct);
    /// <summary>仅向应用执行边界提供已受理快照；读取不授予首次执行或恢复执行权。</summary>
    /// <include file="../Comments.xml" path="doc/Id/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<OperationRequestSnapshot>> ReadRequestAsync(OperationId id, CancellationToken ct);

    /// <summary>在操作锁内核验执行权、进程身份、序号和未关闭状态，再追加带长度、版本和校验和的事实并刷新。执行日志为权威，快照为可重建缓存。</summary>
    /// <param name="execution">证明当前执行权仍有效的操作租约。</param>
    /// <param name="expectedSequence">追加事件前须匹配的原序号。</param>
    /// <param name="fact">即将持久记录的阶段和脱敏原因。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<RunSnapshot>> AppendAsync(OperationExecutionLease execution, long expectedSequence, RunEvent fact,
        CancellationToken ct);

    /// <summary>由完整有效前缀重建；Accepted原持有者死亡时待核验、无自动执行权；失效Executing亦待核验。进程身份不可证则保持未知，不以超时判死亡。</summary>
    /// <include file="../Comments.xml" path="doc/Id/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct);
    /// <summary>同一水位建立快照/增量，每操作序号单调；连续性丢失须重读或终止流，不静默漏事实。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/param"/>
    /// <include file="../Comments.xml" path="doc/Result/*"/>
    IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct);
}

/// <summary>为 UI 提供只读任务快照与订阅，不维护第二份业务状态。</summary>
public interface ITaskQueries
{
    /// <summary>缺失记录返回明确错误，不假报完成。</summary>
    /// <include file="../Comments.xml" path="doc/Id/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct);
    /// <summary>先快照再增量；取消 UI 订阅不结束任务。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/param"/>
    /// <include file="../Comments.xml" path="doc/Result/*"/>
    IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct);
}

/// <summary>执行结构化进程规格，观测子进程和窗口；不作项目成功判定。</summary>
public interface IProcessGateway
{
    /// <summary>返回父进程环境的独立只读副本；不修改或缓存为下一次启动的可变全局环境。</summary>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    Result<EnvironmentSnapshot> CaptureEnvironment();
    /// <summary>仅执行时展开 SecretSlot，应用子进程变量，返回观察句柄。</summary>
    /// <include file="../Comments.xml" path="doc/Plan/*"/>
    /// <include file="../Comments.xml" path="doc/Secret/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct);
    /// <summary>报告入口、子进程、窗口、退出；取消观察不能 Kill。</summary>
    /// <param name="process">需观察及释放跟踪资源的目标进程。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/param"/>
    /// <include file="../Comments.xml" path="doc/Result/*"/>
    IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process, CancellationToken ct);
}

/// <summary>来源刷新和选择策略；扫描/预览不执行内容。</summary>
public interface IToolSourceCatalog
{
    /// <summary>KeepSelection 保留仍有效旧版；SelectNewest 重扫完整 ZIP / 7z 并按修改时间选择。</summary>
    /// <param name="query">来源发现的目录与文件名规则。</param>
    /// <param name="current">刷新前的人工来源选择或当前状态。</param>
    /// <param name="intent">稳定提交或来源刷新采用的明确意图。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<SourceCatalog>> RefreshAsync(SourceQuery query, SourceSelection current, RefreshIntent intent,
        CancellationToken ct);

    /// <summary>支持目录、普通文件与映射，不要求先压缩。</summary>
    /// <param name="source">工具来源或来源快照</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct);
}

/// <summary>只读发现和读取 ZIP / 7z、目录及文件映射。</summary>
public interface IPackageReader
{
    /// <summary>只列完整候选，排除下载中临时文件。</summary>
    /// <param name="query">来源发现的目录与文件名规则。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<SourceCatalog>> DiscoverAsync(SourceQuery query, CancellationToken ct);
    /// <summary>校验条目路径、映射和摘要；禁止执行载荷。</summary>
    /// <param name="source">工具来源或来源快照</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct);
}

/// <summary>受保护暂存、不可变计划登记及执行前内容校验。</summary>
public interface IStagingStore
{
    /// <summary>仅在已登记保护根中复制来源并按复制后的字节计算清单摘要，完成可靠写入后发布标识。</summary>
    /// <param name="source">工具来源或来源快照</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<SourceSnapshot>> StageAsync(SourceSnapshot source, CancellationToken ct);
    /// <summary>核验摘要及完整变更范围不与保护根重叠；全部恢复资料须受保护且位于变更范围外；同PlanId内容不可替换。</summary>
    /// <include file="../Comments.xml" path="doc/Plan/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> SavePlanAsync(DeploymentPlan plan, CancellationToken ct);
    /// <summary>从受保护位置校验并读取已登记计划，不授予执行权。</summary>
    /// <param name="planId">计划的稳定标识</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<DeploymentPlan>> ReadPlanAsync(Guid planId, CancellationToken ct);
    /// <summary>重验当前保护范围、来源、暂存清单及字节；成功只代表此次核验，写入端仍须持租约再验证。</summary>
    /// <include file="../Comments.xml" path="doc/Plan/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> ValidateAsync(DeploymentPlan plan, CancellationToken ct);
}

/// <summary>部署、恢复及回滚的只读目标预览契约。</summary>
public interface IDeploymentPlanningService
{
    /// <summary>检查全部映射、目录操作和隐式变更范围，仅不与控制保护根重叠才生成计划；内容冻结于私有暂存，摘要覆盖实际变更范围、目标基线及备份策略。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<DeploymentPlan>> PreviewAsync(DeployRequest request, CancellationToken ct);
    /// <summary>读取原回执、备份及当前目标，复查保护范围和外部修改；恢复/回滚须建关联OriginalOperationId的新计划并再确认。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<DeploymentPlan>> PreviewRecoveryAsync(RecoveryRequest request, CancellationToken ct);
}

/// <summary>目标检查、持组合租约的逐项写入及部署回执读取。</summary>
public interface IFileDeliveryPort
{
    /// <summary>返回真实目标身份及内容基线；检查候选写入范围与控制根的冲突，但不授予执行许可。</summary>
    /// <param name="target">待检查的目标目录或依赖属性对象。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<TargetSnapshot>> InspectTargetAsync(TargetSpec target, CancellationToken ct);

    /// <summary>重算完整影响范围，核验组合租约、当前保护集合及回执/备份位置。全通过后逐项可靠备份、持久记录意图、写目标、核验并记录完成事实；每次写前复核路径身份。</summary>
    /// <param name="operationId">执行/核验用原操作号。</param>
    /// <include file="../Comments.xml" path="doc/Plan/*"/>
    /// <param name="lease">调用期间持有的目标互斥或秘密租约。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<DeploymentReceipt>> ApplyAsync(OperationId operationId, ConfirmedPlan plan, TargetLease lease,
        CancellationToken ct);

    /// <summary>只读校验版本、完整前缀、损坏尾部和最后意图，供恢复规划使用；读取不会恢复执行。</summary>
    /// <param name="original">关联的原操作标识</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<DeploymentReceipt>> ReadReceiptAsync(OperationId original, CancellationToken ct);
}

/// <summary>取得跨进程目标互斥和控制数据保护的组合租约。</summary>
public interface ITargetLeaseProvider
{
    /// <summary>targets为完整变更影响范围。先取控制数据保护租约，再原子准入整组真实目标；任一失败均释放已取保护租约。返回租约持有两种准入，直到写入和回执完成。</summary>
    /// <param name="targets">需持有互斥或保护检查的规范化路径。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<TargetLease>> AcquireAsync(IReadOnlyList<CanonicalPath> targets, CancellationToken ct);
}

/// <summary>登记受保护数据根，校验部署影响范围，并约束保护范围与部署写入范围的并发准入。</summary>
public interface IControlDataGuard
{
    /// <summary>按可信启动配置登记工作区、日志、回执、备份、暂存、迁移和提交核验资料的完整根范围；跨进程原子检查不与活动部署重叠后持久登记。新根先登记，再创建控制资料。</summary>
    /// <include file="../Comments.xml" path="doc/Location/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/returns"/>
    Result<Unit> RegisterWorkspace(WorkspaceLocation location);
    /// <summary>按规范路径、卷/文件身份及目录祖先关系检查完整实际变更范围；重叠任一保护范围或完整性不可证则拒绝。</summary>
    /// <param name="mutationScopes">准备写入的规范化目标范围。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> ValidateAsync(IReadOnlyList<CanonicalPath> mutationScopes, CancellationToken ct);
    /// <summary>跨进程登记门内复查并登记活动变更范围，返回保护租约；持有期间禁增重叠控制根；保护租约不代替部署目标互斥。</summary>
    /// <param name="mutationScopes">准备写入的规范化目标范围。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<ControlDataLease>> AcquireAsync(IReadOnlyList<CanonicalPath> mutationScopes, CancellationToken ct);
}

/// <summary>读取与尽力检测冲突的环境写入端口。</summary>
public interface IEnvironmentStore
{
    /// <summary>区分不存在、空值和实际值。</summary>
    /// <param name="address">变量名称与作用域</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address, CancellationToken ct);

    /// <summary>按 WindowsEnvironmentStore 契约逐步写入和核验；所有破坏性步骤前检查点须持久成功。</summary>
    /// <include file="../Comments.xml" path="doc/Plan/*"/>
    /// <param name="context">当前操作持有的阶段事实记录上下文。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> ApplyVerifiedAsync(ConfirmedVariablePlan plan, IOperationContext context,
        CancellationToken ct);
}

/// <summary>只读变量变更规划边界。</summary>
public interface IVariablePlanningService
{
    /// <summary>读取原值和新名，验证作用域和保留字段，形成待确认计划；不写 Windows。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<VariableChangePlan>> PreviewAsync(VariableRenameRequest request, CancellationToken ct);
}

/// <summary>为已确认有限写入请求 UAC，关联调用者、一次性会话、计划摘要和回执。</summary>
public interface IPrivilegeBroker
{
    /// <summary>只允许文件写入/恢复和变量应用，不接受任意命令行或登录秘密。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request, CancellationToken ct);
}

/// <summary>定义原始读取、旁路副本及有效版本切换。</summary>
public interface IMigrationStore
{
    /// <summary>读取本次明确指定的旧版配置来源并返回内部不可变快照，不保存共享的来源选择状态。</summary>
    /// <param name="sourcePath">明确选择的迁移来源文件路径；调用之间不共享选择状态。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<WorkspaceSnapshotInternal>> ReadSourceAsync(string sourcePath, CancellationToken ct);
    /// <summary>根据冻结迁移计划生成带 OperationId 的旁路副本，保留原始数据。</summary>
    /// <include file="../Comments.xml" path="doc/Request/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> PrepareAsync(OperationRequestSnapshot request, CancellationToken ct);
    /// <summary>重读旁路副本并核对格式、ID、引用、历史及凭据关联清单。</summary>
    /// <param name="operationId">执行/核验用原操作号。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Unit>> ValidateAsync(OperationId operationId, CancellationToken ct);
    /// <summary>共享普通配置提交的工作区锁，锁内比较源修订，再原子切换至已验证副本；有效标记含OperationId。</summary>
    /// <param name="operationId">执行/核验用原操作号。</param>
    /// <include file="../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<Revision>> ActivateAsync(OperationId operationId, Revision expected, CancellationToken ct);
    /// <summary>按原迁移号读取准备副本，观察当前工作空间是否采用迁移后的稳定身份；不重放启用。</summary>
    /// <param name="operationId">执行/核验用原操作号。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/*"/>
    ValueTask<Result<OperationFact>> InspectAsync(OperationId operationId, CancellationToken ct);
}
