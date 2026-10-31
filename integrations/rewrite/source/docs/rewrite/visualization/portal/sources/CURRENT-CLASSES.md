# 当前审阅用类与接口（含 F06 说明勘误）

**架构提案，等待人工审阅；没有生成业务源码。**

F06 已由当前会话进行接口说明对齐，未再调用模型；原始复核和契约保持原状，见 [勘误记录](editorial-corrections.json)。

来源：[原生报告](REVIEW-PACKET.md)、[完整机器契约](revised-contracts.json)。本文件是脚本生成的阅读视图，未列为原生模型输出。

所有保留类型继承基线完整方法；修改类型使用本轮明确给出的替换方法。新增类型的项目归属若尚未规定，须在实现前落实。

## ICatalogService

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/ICatalogService.cs；模块：contracts

配置查询、稳定提交及脱敏历史结果核验。

本轮理由：F05：所有直接配置变更显式接收 CommitId 并提供核验入口。

依赖：无

场景：AR02、AR03、AR04、AR18、AR20、AR27

禁止承担：未知结果按新 ID 自动重试；返回 CredentialRef；把当前快照当作历史提交证明

### `ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct)`

返回不可变公开配置投影，不包含秘密或 CredentialRef；不代替按 CommitId 核验。

失败约定：ConfigurationMissing、StoreUnavailable、InvalidFormat。

### `ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(CommitId commitId, ProfileDraft draft, SecretUpdate change, Revision expected, CancellationToken ct)`

先建立含秘密更新意图及输入标识的 CommitIntent 并准备会话；仅首次写会话可创建 Replace 新凭据，再提交配置。同 ID 同摘要返回原确定结果或原未决状态。

失败约定：InvalidReference、CommitConflict、RevisionConflict、CredentialWriteFailed、CommitPending、CommitNotApplied、CommitIndeterminate；未决结果携带 CommitId。

### `ValueTask<Result<ProfileSnapshot>> CopyAsync(CommitId commitId, Guid sourceId, Revision expected, CancellationToken ct)`

首次准备分配并持久记录新 ID；复制独立字段，不复制秘密引用。重复 ID 不生成第二份记录。

失败约定：ProfileNotFound、InvalidReference、CommitConflict、RevisionConflict、StoreUnavailable、CommitPending、CommitNotApplied、CommitIndeterminate。

### `ValueTask<Result<Unit>> DeleteAsync(CommitId commitId, Guid profileId, Revision expected, CancellationToken ct)`

通过稳定提交协议仅删除管理记录；已提交重复请求返回原结果，不因当前记录已不存在而改判失败。

失败约定：ProfileNotFound、ReferenceConflict、CommitConflict、RevisionConflict、CommitPending、CommitNotApplied、CommitIndeterminate。

### `ValueTask<Result<Revision>> SaveDefinitionAsync(CommitId commitId, DefinitionDraft draft, Revision expected, CancellationToken ct)`

经稳定提交保存有限定义和人工绑定来源；仅配置保存，不写持久环境。同 ID 返回原提交修订。

失败约定：InvalidDefinition、InvalidReference、CommitConflict、RevisionConflict、StoreUnavailable、CommitPending、CommitNotApplied、CommitIndeterminate。

### `ValueTask<Result<WorkspaceSnapshot>> RefreshDiscoveryAsync(CommitId commitId, Guid applicationId, Revision expected, CancellationToken ct)`

首次会话内调用发现并冻结合并结果，保护人工绑定；已有同 ID 请求不重新扫描或合并。

失败约定：ApplicationNotFound、DiscoveryUnavailable、CommitConflict、RevisionConflict、CommitPending、CommitNotApplied、CommitIndeterminate。

### `ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct)`

返回原提交状态、修订、生成 ID 及脱敏结果；不创建新提交，不解析秘密。

失败约定：StoreUnavailable、CorruptCommitHistory、UnsupportedCommitVersion。

## WorkspaceCatalogService

种类：class；生命周期：应用级单例；写会话、草稿和秘密为调用级。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Catalog/WorkspaceCatalogService.cs；模块：catalog

配置 CRUD、稳定 ID、发现合并及直接提交结果核验。

本轮理由：F05：在任何实体分配或凭据创建之前完成持久去重。

依赖：IWorkspaceStore、ISecretVault、IEntryDiscovery

场景：AR02、AR03、AR04、AR18、AR20、AR27

禁止承担：去重前创建凭据；重复会话重新分配 ID；保存明文密码；未知提交后清理候选凭据；保存或发现时执行外部程序

### `public ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct)`

返回移除秘密和 CredentialRef 的不可变公开投影，区分缺失配置与合法空集合。

失败约定：ConfigurationMissing、StoreUnavailable、InvalidFormat；取消抛 OperationCanceledException。

### `public ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(CommitId commitId, ProfileDraft draft, SecretUpdate change, Revision expected, CancellationToken ct)`

规范化意图后先 BeginCommitAsync；重复会话只读取原结果。首次会话验证引用和修订后，Replace 创建独立新凭据，Keep 保留引用，Clear 解除关联，再调用 CommitAsync。确定未提交才可考虑清理本次新引用；未知保留新旧引用。旧引用只有证明无配置、操作或秘密租约引用时才可清理，首版无法证明则保留。

失败约定：InvalidReference、CommitConflict、RevisionConflict、CredentialWriteFailed、CommitPending、CommitNotApplied、CommitIndeterminate；均脱敏。清理失败不改写已提交结果，未决结果附 CommitId。

### `public ValueTask<Result<ProfileSnapshot>> CopyAsync(CommitId commitId, Guid sourceId, Revision expected, CancellationToken ct)`

从首次会话取得持久分配的新 ID，复制独立可编辑数据和经核验本机绑定，清空凭据关联。重复请求返回原副本 ID 及原提交投影，不另建副本。

失败约定：ProfileNotFound、InvalidReference、CommitConflict、RevisionConflict、StoreUnavailable、CommitPending、CommitNotApplied、CommitIndeterminate。

### `public ValueTask<Result<Unit>> DeleteAsync(CommitId commitId, Guid profileId, Revision expected, CancellationToken ct)`

先查稳定提交历史，再对首次请求校验关联并提交管理记录删除；不删除应用或部署文件。保留历史操作关联，成功后才考虑可证明无引用的凭据清理。

失败约定：ProfileNotFound、ReferenceConflict、CommitConflict、RevisionConflict、CommitPending、CommitNotApplied、CommitIndeterminate。

### `public ValueTask<Result<Revision>> SaveDefinitionAsync(CommitId commitId, DefinitionDraft draft, Revision expected, CancellationToken ct)`

通过首次会话保存有限定义、稳定 ID 和人工绑定标记；路径失效保留待绑定，变量只保存配置。重复提交返回原修订，不重新应用变更。

失败约定：InvalidDefinition、InvalidReference、CommitConflict、RevisionConflict、StoreUnavailable、CommitPending、CommitNotApplied、CommitIndeterminate。

### `public ValueTask<Result<WorkspaceSnapshot>> RefreshDiscoveryAsync(CommitId commitId, Guid applicationId, Revision expected, CancellationToken ct)`

首次会话内发现并冻结候选合并结果，保留人工绑定和来源时间；失效人工路径标记待绑定。重复请求不重新扫描；发现失败不提交合并。

失败约定：ApplicationNotFound、DiscoveryUnavailable、CommitConflict、RevisionConflict、CommitPending、CommitNotApplied、CommitIndeterminate。

### `public ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct)`

调用存储按标识核验并返回脱敏原结果，明确区分原提交修订和当前工作区修订；不创建凭据或补做变更。

失败约定：StoreUnavailable、CorruptCommitHistory、UnsupportedCommitVersion。

## IConfigurationShareService

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IConfigurationShareService.cs；模块：contracts

白名单分享、导入草稿及可核验的配置提交。

本轮理由：F05：导入仍走直接用例，但需稳定提交标识及核验入口。

依赖：无

场景：AR19、AR20、AR27

禁止承担：导入确认触发执行；恢复外机凭据引用；未知结果自动再次导入

### `ValueTask<Result<ShareBundle>> ExportAsync(ShareSelection selection, CancellationToken ct)`

重构允许字段，排除秘密、凭据引用、可直接采用的机器绑定、运行记录、备份和变量应用状态。

失败约定：SelectionNotFound、InvalidReference、StoreUnavailable。

### `ValueTask<Result<ImportProposal>> PreviewImportAsync(ShareBundle bundle, CancellationToken ct)`

校验版本、大小、数量和关联，形成固定 ID 映射及待绑定草稿；外机路径仅作提示。

失败约定：UnsupportedFormat、InvalidBundle、LimitExceeded、InvalidReference。

### `ValueTask<Result<Revision>> CommitAsync(CommitId commitId, ImportProposal proposal, Revision expected, CancellationToken ct)`

以稳定提交协议保存已确认草稿；同 ID 同摘要返回原修订和原 ID 映射，不再次导入。

失败约定：ProposalInvalid、CommitConflict、RevisionConflict、CommitPending、CommitNotApplied、CommitIndeterminate。

### `ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct)`

返回原导入提交状态、修订及生成 ID 映射；不重新导入。

失败约定：StoreUnavailable、CorruptCommitHistory、UnsupportedCommitVersion。

## ConfigurationShareService

种类：class；生命周期：应用级单例；导入草稿与提交会话为调用级。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Catalog/ConfigurationShareService.cs；模块：catalog

白名单导出、导入预览和稳定配置提交。

本轮理由：F05：导入结果必须能跨后续提交核验。

依赖：IWorkspaceStore

场景：AR19、AR20、AR27

禁止承担：序列化完整本机配置；重复导入产生第二套实体；导入触发启动、部署或变量应用

### `public ValueTask<Result<ShareBundle>> ExportAsync(ShareSelection selection, CancellationToken ct)`

从白名单构造逻辑配置，排除密码、CredentialRef、可直接采用的本机绑定、操作记录、备份及变量应用状态。

失败约定：SelectionNotFound、InvalidReference、StoreUnavailable。

### `public ValueTask<Result<ImportProposal>> PreviewImportAsync(ShareBundle bundle, CancellationToken ct)`

校验格式和限制，分配并冻结导入 ID 映射，生成待绑定草稿；不执行包内内容。

失败约定：UnsupportedFormat、InvalidBundle、LimitExceeded、InvalidReference。

### `public ValueTask<Result<Revision>> CommitAsync(CommitId commitId, ImportProposal proposal, Revision expected, CancellationToken ct)`

重算包含确认草稿及 ID 映射的请求摘要，先准备提交会话。首次会话重验引用及修订后提交；重复会话只返回原结果。缺少本机路径或凭据保留待绑定。

失败约定：ProposalInvalid、CommitConflict、RevisionConflict、CommitPending、CommitNotApplied、CommitIndeterminate；未知错误附 CommitId，不能自动生成新 ID 导入。

### `public ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct)`

从提交历史读取原状态、修订及 ID 映射；当前配置变化不覆盖原提交结果。

失败约定：StoreUnavailable、CorruptCommitHistory、UnsupportedCommitVersion。

## IResourceMonitor

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IResourceMonitor.cs；模块：contracts

提供当前资源刷新与精确身份的新鲜事实查询。

本轮理由：F04：启动新鲜度请求改为携带冻结资源描述。

依赖：无

场景：AR05、AR06、AR07、AR08

禁止承担：按 ResourceId 单独复用启动证据；替换冻结端点；端口可达推断许可签出

### `ValueTask<Result<ProbeSnapshot>> RefreshAsync(Guid resourceId, CancellationToken ct)`

一次读取并冻结当前资源描述，再按完整身份键调度有界轮次；返回结果携带该次冻结身份，不保证仍是返回时的最新配置。

失败约定：ResourceNotFound、StoreUnavailable、ProbeUnavailable、PolicyInvalid。

### `ValueTask<Result<ProbeSnapshot>> RequireFreshAsync(ResourceDescriptor expected, CancellationToken ct)`

只查找完全匹配 expected 身份及策略版本的新鲜证据；没有则针对 expected 本身探测，不能改探当前同 ID 端点。返回分项事实，业务是否可继续由启动规则判断。

失败约定：InvalidResourceDescriptor、MachineMismatch、ResourceVersionMismatch、EvidenceExpired、ProbeUnavailable、ResourceUnavailable。

### `IAsyncEnumerable<ProbeSnapshot> WatchAsync(CancellationToken ct)`

先快照后增量，每条附完整身份；当前配置投影不被旧版本迟到结果替换。取消只结束订阅。

失败约定：取消抛 OperationCanceledException；基础设施故障终止流并允许重连。

## ResourceSupervisor

种类：class；生命周期：应用级单例；每完整身份键独立轮次，应用持有停止令牌。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Resources/ResourceSupervisor.cs；模块：supervisor

按冻结资源身份和版本化策略调度探测及发布监测投影。

本轮理由：F04：轮次、缓存和监测投影必须按完整资源身份隔离。

依赖：IResourceProbe、ConnectionHealthPolicy、IWorkspaceStore

场景：AR05、AR06、AR07、AR08

禁止承担：跨修订共享轮次或失败计数；迟到结果改贴新端点；监测失败阻止工作台启动；控制外部服务或 VM

### `public ValueTask<Result<ProbeSnapshot>> RefreshAsync(Guid resourceId, CancellationToken ct)`

从 IWorkspaceStore 读取当前描述并冻结；共享轮次键包含 ResourceId、ResourceRevision、MachineId、DescriptorDigest 和策略版本。同键合并或串行，不同修订独立；调用者取消只退出本次等待。

失败约定：ResourceNotFound、StoreUnavailable、ProbeUnavailable、PolicyInvalid。

### `public ValueTask<Result<ProbeSnapshot>> RequireFreshAsync(ResourceDescriptor expected, CancellationToken ct)`

校验描述摘要和机器，查询同身份新鲜缓存；不足时仅探测冻结 expected。接收结果时逐项核对身份、轮次及有效期；不能探测冻结端点则明确失败，不借用当前版本成功事实。

失败约定：InvalidResourceDescriptor、MachineMismatch、ResourceVersionMismatch、EvidenceExpired、ProbeUnavailable、ResourceUnavailable。

### `public IAsyncEnumerable<ProbeSnapshot> WatchAsync(CancellationToken ct)`

发布带身份的快照和变化；旧修订结果可留作该修订证据，但不能替换当前修订投影。重启后的历史 Ready 不直接恢复为当前 Ready。

失败约定：取消抛 OperationCanceledException；探针故障进入对应快照，订阅设施故障终止流。

## ConnectionHealthPolicy

种类：class；生命周期：不可变策略实例，按策略版本复用。；决定：refine

拟议位置：rewrite/LaunchDesk.Kernel/ConnectionHealthPolicy.cs；模块：kernel

根据完整资源身份、显式时间和策略版本归并分项证据。

本轮理由：F04：纯规则也必须拒绝不同资源修订之间的状态归并。

依赖：无

场景：AR06、AR07、AR08

禁止承担：跨修订继承 Ready 或计数；隐式读取时间；把 TCP 事实升级为许可签出

### `public ConnectionHealthPolicy(int policyVersion, int attemptsPerRound, TimeSpan retryDelay, TimeSpan probeTimeout, int failedRoundsToNotify, int successfulRoundsToRecover, TimeSpan freshnessLifetime)`

冻结有效策略参数；各阈值仍须批准，不能把初稿数值视为需求。

失败约定：非法范围抛 ArgumentOutOfRangeException；装配边界映射为 PolicyInvalid。

### `public ProbeSnapshot Reduce(ProbeSnapshot previous, ProbeRound round, DateTimeOffset now)`

只归并身份键和策略版本完全一致且序号前进的完成轮次。不同身份必须使用独立初始状态；旧轮次不覆盖新状态。VM、服务和 TCP 分项保留，过期证据变为未知或待复查。

失败约定：身份不符或无效证据返回带原因的 Unknown；乱序轮次保留已有较新证据，不产生 IO。

## ProcessEnvironmentPolicy

种类：class；生命周期：Domain.Contracts 无状态纯规则。；决定：refine

拟议位置：rewrite/LaunchDesk.Kernel/ProcessEnvironmentPolicy.cs；模块：kernel

生成每次启动独立的子进程环境。

本轮理由：仅覆盖选中许可变量不足以消除父环境残留；必须表达删除项并统一 Windows 名称比较规则。

依赖：无

场景：AR05、AR18

禁止承担：修改父进程、用户或系统环境；复用可变环境字典

### `public Result<EnvironmentOverlay> Build(EnvironmentSnapshot parent, LicenseBinding license, IReadOnlyList<ToolBinding> tools)`

以 Windows 不区分大小写规则复制父环境；移除全部受策略保留的许可变量，再注入本次许可。EnvironmentOverlay 同时表达移除项与设置项；工具变量不得覆盖保留名，输入对象不变。

失败约定：ReservedVariable、DuplicateVariable、InvalidVariableName、InvalidLicenseBinding。

## LaunchPlanBuilder

种类：class；生命周期：无状态构建器。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Launch/LaunchPlanBuilder.cs；模块：launch-handler

从冻结本机配置构造身份一致且不含明文秘密的启动计划。

本轮理由：F04：确保许可环境和资源检查源于同一冻结配置。

依赖：ProcessEnvironmentPolicy

场景：AR02、AR03、AR05、AR08、AR27

禁止承担：读取当前资源覆盖冻结输入；解析秘密；执行计划；猜测缺失绑定；未经编码拼接 shell 参数

### `public Result<LaunchPlan> Build(LaunchConfiguration snapshot, EnvironmentSnapshot parent)`

校验机器、本机入口和初始化文件；从同一冻结 LicenseBinding 取得 ResourceDescriptor 并构造环境，核验描述摘要与许可端点一致。EXE 使用参数列表，支持的批处理使用专用编码，秘密仅为 SecretSlot。

失败约定：MissingEntrypoint、MissingInitFile、BindingRequired、MachineMismatch、InvalidArguments、UnsupportedEntrypoint、InvalidResourceDescriptor、ResourceVersionMismatch。

## IOperationCommands

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IOperationCommands.cs；模块：contracts

有限操作的持久受理、查询式去重及取消请求入口。

本轮理由：F01：公开命令接口同步唯一执行准入和重复提交语义。

依赖：无

场景：AR09、AR11、AR16、AR18、AR22

禁止承担：重复提交派发处理器；自动接管失效执行者；以取消请求接受代替业务终态

### `ValueTask<Result<OperationId>> SubmitAsync(OperationRequest request, CancellationToken ct)`

冻结并持久受理请求。仅首次 Created 结果允许尝试一次执行准入；Existing 只返回原 OperationId。取得并确认执行租约后才能创建处理器作用域。

失败约定：UnsupportedOperationKind、RevisionConflict、InvalidPlan、DuplicateOperationConflict、JournalUnavailable、AcceptanceIndeterminate、ExecutionAdmissionIndeterminate；未知结果不得再次派发。

### `ValueTask<Result<Unit>> CancelAsync(OperationId id, CancellationToken ct)`

先持久记录取消请求，再通知现有执行者；尚未取得执行权的操作由日志准入检查阻止执行。请求接受不代表已停止或已回滚。

失败约定：RunNotFound、CancellationNotAllowed、JournalUnavailable、CommitIndeterminate。

## OperationCoordinator

种类：class；生命周期：应用级单例；上下文、处理器和执行租约按操作创建。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Operations/OperationCoordinator.cs；模块：coordinator

管理有限操作受理、首次执行准入、处理器作用域及取消通知。

本轮理由：F01：把受理去重与唯一执行权明确分为两个持久步骤。

依赖：IRunJournal、IOperationHandlerRegistry、OperationExecutionContext、IWorkspaceStore

场景：AR09、AR10、AR11、AR14、AR16、AR18、AR22、AR26

禁止承担：Existing 受理结果再次派发；取得执行权前创建可执行处理器；重启后自动领取 Accepted 或失效 Executing 操作；直接判断模块业务成功；直接依赖 WPF 或 Aspire

### `public ValueTask<Result<OperationId>> SubmitAsync(OperationRequest request, CancellationToken ct)`

先按 OperationId 和规范化请求摘要检查已有受理；已有相同请求只返回原操作，不用当前配置重新解释。新请求经模块校验后冻结并调用 AcceptAsync；仅 Created 调用 TryBeginExecutionAsync。执行权可靠落盘并返回有效租约后，才读取已受理快照、创建一个上下文和处理器。支持 Launch、Deploy、ApplyVariables、MigrateConfiguration。

失败约定：UnsupportedOperationKind、RevisionConflict、InvalidPlan、DuplicateOperationConflict、JournalUnavailable、AcceptanceIndeterminate、ExecutionAdmissionIndeterminate。任一受理或准入结果未知均停止派发，仅核验。

### `public ValueTask<Result<Unit>> CancelAsync(OperationId id, CancellationToken ct)`

调用 RequestCancellationAsync 持久记录控制请求，再通知已存在的执行作用域。执行者在模块安全边界响应；无现存执行者时不为取消创建处理器。

失败约定：RunNotFound、CancellationNotAllowed、JournalUnavailable、CommitIndeterminate；不把已发生副作用报告为零变更取消。

## OperationStatePolicy

种类：class；生命周期：无状态纯策略。；决定：refine

拟议位置：rewrite/LaunchDesk.Kernel/OperationStatePolicy.cs；模块：kernel

校验公共生命周期、操作身份、序号和终结约束。

本轮理由：F01：补齐不可逆执行准入状态，禁止从待核验回到可执行。

依赖：无

场景：AR09、AR16、AR18、AR22

禁止承担：系统 IO；租约授予；操作种类业务分支；待核验操作自动恢复执行

### `public Result<RunSnapshot> Apply(RunSnapshot previous, OperationFact fact)`

允许一次 Accepted 到 Executing 的可靠准入事实；执行者失效或结果未知进入待核验投影，不允许原操作返回 Accepted。取消请求独立于业务终态；Completed 仍只表示执行阶段结束。仅验证持久准入事实的形状，不负责授予执行权。

失败约定：InvalidTransition、SequenceConflict、OperationMismatch；不判断窗口、文件摘要、变量或迁移成功。

## OperationExecutionContext

种类：class；生命周期：操作级；一个有效租约对应一个上下文和一个串行报告入口。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Operations/OperationExecutionContext.cs；模块：coordinator

在唯一执行权下串行持久提交单次操作事实。

本轮理由：F01：明确执行租约与串行写入者的关系。

依赖：IRunJournal、OperationStatePolicy、OperationExecutionLease

场景：AR09、AR11、AR16、AR18、AR22、AR26

禁止承担：无租约追加；释放执行权后重建同操作上下文；模块业务成功判断；Dispose 时 Kill

### `public ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct)`

校验绑定租约及操作身份，运行公共状态策略，再以 expectedSequence 追加。写前意图失败不进入副作用；写后事实失败停止下一步。遇取消导致的序号冲突先重读并验证，不重复副作用。

失败约定：ExecutionLeaseInvalid、InvalidTransition、SequenceConflict、JournalUnavailable、CommitIndeterminate；仅内存故障提示不能冒充可靠恢复标记。

### `public ValueTask DisposeAsync()`

关闭报告入口，异步等待已开始的必要检查点，释放订阅及执行租约。worker 通道关闭后拒绝新检查点，目标程序继续由其自身生命周期管理。

失败约定：幂等；不阻塞 UI 线程，不以释放错误覆盖原操作错误。

## IOperationHandlerRegistry

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IOperationHandlerRegistry.cs；模块：contracts

唯一把有限操作种类映射到具体处理器工厂的位置。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `Result<HandlerLease> Create(OperationKind kind)`

使用组合根提供的工厂映射，支持 Launch、Deploy、ApplyVariables、MigrateConfiguration；每次返回独立作用域，不在协调模块引用具体业务实现。

失败约定：UnsupportedOperationKind、HandlerUnavailable；重复映射在装配阶段拒绝。

## OperationHandlerRegistry

种类：class；生命周期：OperationCoordination 应用级只读注册表。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/Bootstrap/OperationHandlerRegistry.cs；模块：bootstrap

根据有限操作种类创建独立处理器租约。

本轮理由：基线直接依赖具体处理器，并把普通导入纳入协调；应由组合根注入有限工厂，增加迁移种类。

依赖：IOperationHandler

场景：AR19、AR22、AR24、AR25

禁止承担：反射加载任意类型或 DLL；导入配置操作注册；共享操作级可变处理器

### `public Result<HandlerLease> Create(OperationKind kind)`

使用组合根提供的工厂映射，支持 Launch、Deploy、ApplyVariables、MigrateConfiguration；每次返回独立作用域，不在协调模块引用具体业务实现。

失败约定：UnsupportedOperationKind、HandlerUnavailable；重复映射在装配阶段拒绝。

## IOperationHandler

种类：interface；生命周期：契约；实现按操作创建。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IOperationHandler.cs；模块：contracts

有限模块操作执行及模块状态机边界。

本轮理由：执行端必须接收已接受的不可变快照，而不是可重新解释的原始请求。

依赖：IOperationContext

场景：AR09、AR16、AR18、AR22

禁止承担：直接改写公共日志；自动重放未知副作用

### `ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request, IOperationContext context, CancellationToken ct)`

核对有限种类、快照版本与摘要；模块拥有业务状态及安全取消规则，事实经上下文发布。

失败约定：WrongOperationKind、InvalidPlan、UnsupportedSnapshotVersion、Indeterminate；有副作用后的取消保留实际事实。

## LaunchOperationHandler

种类：class；生命周期：操作级；观察可长于页面生命周期，但不恢复首次执行权。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Launch/LaunchOperationHandler.cs；模块：launch-handler

拥有启动状态机、启动意图及后续只读观察。

本轮理由：F01、F04：唯一执行权和冻结许可证据必须在进程创建前同时成立。

依赖：LaunchPlanBuilder、IResourceMonitor、ISecretVault、IProcessGateway

场景：AR04、AR05、AR08、AR09、AR10、AR11、AR27

禁止承担：凭同 ID 当前 Ready 放行冻结端点；重复调用进程创建修复未知结果；窗口出现即登录成功；取消或释放时 Kill；记录秘密或展开参数

### `public ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request, IOperationContext context, CancellationToken ct)`

仅由持唯一执行租约的上下文调用；校验请求与机器，捕获独立父环境并构建计划。把计划的冻结 ResourceDescriptor 传入 RequireFreshAsync，并核对返回完整身份、策略版本和有效期；不符时拒绝采用。启动意图可靠落盘后才临时解析秘密并调用一次进程创建，立即释放秘密，再提交创建事实。交接、窗口、明确登录、未确认和退出分别报告；重启只能另行只读核验身份。

失败约定：WrongOperationKind、ExecutionLeaseInvalid、BindingRequired、CredentialUnavailable、InvalidResourceDescriptor、ResourceVersionMismatch、EvidenceExpired、ResourceUnavailable、ProcessStartFailed、IdentityUnconfirmed、Indeterminate；创建或日志结果未知不得重新创建。

## DeliveryOperationHandler

种类：class；生命周期：ToolDeployment 操作级；租约覆盖目标复核至最后可靠回执。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Delivery/DeliveryOperationHandler.cs；模块：delivery-handler

拥有安装、恢复和回滚的部署状态机。

本轮理由：必须把来源重验、私有暂存、真实写入方锁所有权与回执核验串成可执行协议。

依赖：IStagingStore、IFileDeliveryPort、ITargetLeaseProvider、IPrivilegeBroker

场景：AR14、AR15、AR16、AR17、AR26

禁止承担：执行时用可变来源替换暂存内容；无锁写入；自动恢复或回滚；父子进程重复持有同一目标锁

### `public ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request, IOperationContext context, CancellationToken ct)`

读取已登记计划，重新计算并核对 PlanDigest、确认摘要和冻结请求；重新核验来源及私有暂存。普通写入由本处理器取得目标租约后复核目标基线，再调用文件端口；需提权时只委托 worker，父进程不持锁等待 worker。每项结果以回执事实归并；恢复和回滚必须是用户新确认的计划。

失败约定：WrongOperationKind、PlanExpired、SourceChanged、TargetBusy、TargetChanged、PermissionDenied、RecoveryRequired、RollbackConflict、Indeterminate。

## VariableOperationHandler

种类：class；生命周期：EnvironmentManagement 操作级。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Delivery/VariableOperationHandler.cs；模块：delivery-handler

持久变量应用、改名检查点和部分完成核验。

本轮理由：变量改名是可能部分完成的业务状态机，不能只包装一次名为 CompareExchange 的调用。

依赖：IEnvironmentStore、IPrivilegeBroker

场景：AR18、AR26

禁止承担：保存变量定义时隐式调用；默认系统作用域；承诺跨外部写入者原子比较交换

### `public ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request, IOperationContext context, CancellationToken ct)`

核对确认摘要、MachineId、作用域、旧新名称及两端基线；系统作用域显式提权。调用 ApplyVerifiedAsync，将写新名、核验、删旧名、再次核验分别记录；已发生任何写入后不能把取消显示为无变更。自身成功不自动改写已保存配置。

失败约定：WrongOperationKind、PlanExpired、VariableConflict、ReservedVariable、PermissionDenied、PartialApplied、Indeterminate；不盲目重试或补偿外部修改。

## IToolSourceCatalog

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IToolSourceCatalog.cs；模块：contracts

来源刷新和选择策略；扫描/预览不执行内容。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `ValueTask<Result<SourceCatalog>> RefreshAsync(SourceQuery query, SourceSelection current, RefreshIntent intent, CancellationToken ct)`

KeepSelection 保留仍有效旧版；SelectNewest 重新扫描完整 ZIP / 7z 并按修改时间选择。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)`

支持目录、普通文件与映射，不要求先压缩。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## ToolSourceCatalog

种类：class；生命周期：Singleton；决定：keep

拟议位置：rewrite/LaunchDesk.UseCases/Delivery/ToolSourceCatalog.cs；模块：delivery-handler

来源刷新和选择策略；扫描/预览不执行内容。

本轮理由：基线已区分 KeepSelection 和用户显式 SelectNewest，支持目录与文件检查，且不执行来源内容，符合 A2 的直接发现路径。

依赖：IPackageReader（constructor）

场景：AR12、AR13

禁止承担：不得在刷新/选择时保存工具定义、部署文件或执行来源内容。

### `ValueTask<Result<SourceCatalog>> RefreshAsync(SourceQuery query, SourceSelection current, RefreshIntent intent, CancellationToken ct)`

KeepSelection 保留仍有效旧版；SelectNewest 重新扫描完整 ZIP / 7z 并按修改时间选择。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)`

支持目录、普通文件与映射，不要求先压缩。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## DeploymentPlanner

种类：class；生命周期：无请求状态的规划服务；计划按 ID 持久登记。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Delivery/DeploymentPlanner.cs；模块：delivery-handler

形成绑定实际内容、完整变更范围及恢复资料位置的不可变部署预览。

本轮理由：F02：在预览阶段拒绝能够破坏控制和恢复资料的目标。

依赖：IPackageReader、IStagingStore、IFileDeliveryPort、IControlDataGuard

场景：AR12、AR13、AR14、AR16、AR17、AR26

禁止承担：目标包含控制根仍允许执行；用 bool 替代摘要确认；预览写目标；把备份放入本次受影响范围

### `public ValueTask<Result<DeploymentPlan>> PreviewAsync(DeployRequest request, CancellationToken ct)`

先展开映射及文件、目录操作的完整影响范围并调用保护校验；冲突即停止。通过后冻结来源字节，选择已登记控制根中的暂存、回执和备份位置，确认其均在变更范围之外。摘要覆盖动作、机器、修订、来源与暂存摘要、映射、变更范围、目标基线和备份策略，再登记计划；首版拒绝混合持久变量副作用。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、UnsafeArchiveEntry、SourceUnavailable、SourceChanged、TargetUnavailable、InvalidPath、StagingUnavailable、MixedSideEffectsUnsupported。

### `public ValueTask<Result<DeploymentPlan>> PreviewRecoveryAsync(RecoveryRequest request, CancellationToken ct)`

读取原回执、备份和当前目标，先校验保护范围及恢复资料位置，再检查外部修改。生成关联 OriginalOperationId 的 Recover 或 Rollback 新摘要；有冲突不得成为可执行计划。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、ReceiptNotFound、InvalidReceipt、BackupUnavailable、RollbackConflict、TargetUnavailable。

## IWorkspaceStore

种类：interface；生命周期：契约；提交锁及历史覆盖工作区内所有进程。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IWorkspaceStore.cs；模块：contracts

当前配置读取、跨进程提交准备、一次有效版本切换及长期提交核验。

本轮理由：F05：提供准备、一次提交和按稳定 ID 核验的完整直接提交协议。

依赖：无

场景：AR03、AR04、AR19、AR20、AR22

禁止承担：先创建凭据再做 CommitId 去重；以当前字段相似推断提交成功；重复会话再次写入；普通读取隐式迁移

### `ValueTask<Result<WorkspaceSnapshotInternal>> ReadAsync(CancellationToken ct)`

读取有效配置并区分缺失、损坏、旧格式及合法空数据；读取不迁移，也不推断任一历史 CommitId 的结果。

失败约定：ConfigurationMissing、CorruptConfiguration、MigrationRequired、UnsupportedFormat、StoreUnavailable。

### `ValueTask<Result<WorkspaceCommitSession>> BeginCommitAsync(CommitIntent intent, CancellationToken ct)`

先按 CommitId 查历史，再对新请求取得跨进程工作区锁、核对修订、分配稳定 ID 映射并可靠记录准备事实。首次可靠成功才返回写会话；相同 ID 同摘要只返回历史只读会话，不再创建实体或凭据；摘要不同拒绝。

失败约定：CommitConflict、RevisionConflict、InvalidCommitIntent、StoreUnavailable、CommitIndeterminate；未知结果不得创建凭据或继续提交。

### `ValueTask<Result<CommitReceipt>> CommitAsync(WorkspaceCommitSession session, WorkspaceChange change, CancellationToken ct)`

只接受有效首次会话；校验变更与冻结意图、ID 映射和修订一致，将配置候选及原提交结果元数据绑定后可靠写入，再进行唯一有效版本切换。配置可见性与可核验提交身份不得分离。

失败约定：InvalidCommitSession、CommitConflict、RevisionConflict、ValidationFailed、StoreUnavailable、CommitIndeterminate；未知错误携带 CommitId，不重复切换。

### `ValueTask<Result<CommitReceipt>> ReadCommitAsync(CommitId commitId, CancellationToken ct)`

按可靠准备记录、有效版本身份和持久提交历史核验；后续修订不覆盖原结果。NotFound 仅在登记完整且可读时返回；原会话存活则可为 Pending，证据不足为 Indeterminate。

失败约定：StoreUnavailable、CorruptCommitHistory、UnsupportedCommitVersion；读取错误不能降级为 NotFound 或 NotCommitted。

## JsonWorkspaceStore

种类：class；生命周期：工作区级单例；写会话持跨进程提交锁，提交历史首版不自动裁剪。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/Storage/JsonWorkspaceStore.cs；模块：storage

实现稳定 CommitId 去重、版本化准备记录和可跨修订核验的配置提交。

本轮理由：F05：明确配置替换成功、返回失败及后续提交之间的核验依据。

依赖：IWorkspaceLocationResolver

场景：AR03、AR04、AR19、AR20、AR21、AR22

禁止承担：把配置文件与事后回执写入描述为天然原子；删除未决记录；后续修订覆盖去重记录；重复提交创建新实体或凭据；存入明文密码

### `public ValueTask<Result<WorkspaceSnapshotInternal>> ReadAsync(CancellationToken ct)`

从有效版本标记读取并校验当前配置；缺失、损坏、旧格式和合法空数据分别表达，不边读边改写。

失败约定：ConfigurationMissing、CorruptConfiguration、MigrationRequired、UnsupportedFormat、StoreUnavailable。

### `public ValueTask<Result<WorkspaceCommitSession>> BeginCommitAsync(CommitIntent intent, CancellationToken ct)`

在共享工作区锁内先查询 CommitId；已存在同摘要返回只读历史结果，即使当前修订已变也不重新保存。新请求核对修订，记录原有效版本身份、稳定生成 ID 和原进程身份，可靠刷新后返回唯一写会话。锁覆盖本次凭据创建及切换；首次准备结果丢失后不重颁会话。

失败约定：CommitConflict、RevisionConflict、InvalidCommitIntent、StoreUnavailable、CommitIndeterminate。

### `public ValueTask<Result<CommitReceipt>> CommitAsync(WorkspaceCommitSession session, WorkspaceChange change, CancellationToken ct)`

校验会话后构造不可变候选代，包含配置、CommitId、请求及变更摘要、提交修订、生成 ID 和脱敏结果；刷新并验证后记录切换意图，再原子切换有效版本标记。有效标记同时选择配置代及对应提交索引。后续写入前必须把此前已发生切换的结果纳入可靠历史，保留代及链路，不能仅覆盖当前文件后另写独立回执。

失败约定：InvalidCommitSession、CommitConflict、RevisionConflict、ValidationFailed、StoreUnavailable、CommitIndeterminate；切换后取消依据实际结果报告，并始终保留 CommitId。

### `public ValueTask<Result<CommitReceipt>> ReadCommitAsync(CommitId commitId, CancellationToken ct)`

核对准备记录、有效标记、不可变代及历史索引。只存在候选不算已提交；位于可靠已提交历史中的原代即使被后续代替代仍返回 Committed 和原 ID。原持有者死亡且能证明未切换才返回 NotCommitted；缺失必要证据保持 Indeterminate。

失败约定：StoreUnavailable、CorruptCommitHistory、UnsupportedCommitVersion；核验不可用时不得伪报未保存。

## IWorkspaceLocationResolver

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IWorkspaceLocationResolver.cs；模块：contracts

显式 --config、portable、默认目录的唯一解析与打开入口。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `Result<WorkspaceLocation> Resolve(StartupOptions options, string executableDirectory)`

按优先级选择；相对 portable 路径基于 EXE，不受工作目录影响。

失败约定：InvalidLocation / AccessDenied，不静默切换另一个空工作空间。

### `ValueTask<Result<Unit>> OpenDirectoryAsync(WorkspaceLocation location, CancellationToken ct)`

创建缺失目录并以目录对象打开，不伪造配置。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## WorkspaceLocationResolver

种类：class；生命周期：Singleton；决定：keep

拟议位置：rewrite/LaunchDesk.Windows/Storage/WorkspaceLocationResolver.cs；模块：storage

显式 --config、portable、默认目录的唯一解析与打开入口。

本轮理由：基线已明确显式目录优先级、portable 基于 EXE、失败不静默切换，满足目录定位边界；创建目录不等于创建配置。

依赖：无

场景：AR20、AR21、AR22

禁止承担：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。

### `Result<WorkspaceLocation> Resolve(StartupOptions options, string executableDirectory)`

按优先级选择；相对 portable 路径基于 EXE，不受工作目录影响。

失败约定：InvalidLocation / AccessDenied，不静默切换另一个空工作空间。

### `ValueTask<Result<Unit>> OpenDirectoryAsync(WorkspaceLocation location, CancellationToken ct)`

创建缺失目录并以目录对象打开，不伪造配置。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## ISecretVault

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/ISecretVault.cs；模块：contracts

隔离 Windows 凭据 API，秘密只在本机短暂取得。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct)`

新建引用，不原地覆盖有效旧引用。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct)`

返回可释放租约，不回填页面或进入日志。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Unit>> DeleteAsync(CredentialRef unreferenced, CancellationToken ct)`

仅清理已确认无引用凭据。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## WindowsSecretVault

种类：class；生命周期：Singleton；决定：keep

拟议位置：rewrite/LaunchDesk.Windows/WindowsSecretVault.cs；模块：platform

隔离 Windows 凭据 API，秘密只在本机短暂取得。

本轮理由：新引用写入、短命 SecretLease 和仅清理已证明无引用凭据的端口已符合秘密隔离；提交顺序由 WorkspaceCatalogService 负责。

依赖：无

场景：AR04

禁止承担：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。

### `ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct)`

新建引用，不原地覆盖有效旧引用。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct)`

返回可释放租约，不回填页面或进入日志。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Unit>> DeleteAsync(CredentialRef unreferenced, CancellationToken ct)`

仅清理已确认无引用凭据。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## IProcessGateway

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IProcessGateway.cs；模块：contracts

执行结构化进程规格，观测子进程和窗口；不作项目成功判定。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `Result<EnvironmentSnapshot> CaptureEnvironment()`

返回父进程环境的独立只读副本；不修改或缓存为下一次启动的可变全局环境。

失败约定：EnvironmentUnavailable。

### `ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct)`

仅执行时展开 SecretSlot，应用子进程变量，返回观察句柄。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process, CancellationToken ct)`

报告入口、子进程、窗口、退出；取消观察不能 Kill。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## WindowsProcessGateway

种类：class；生命周期：WindowsAndSourceAdapters 单例；TrackedProcess 按启动或只读附着创建。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/WindowsProcessGateway.cs；模块：platform

实现产品入口启动及进程、窗口和可用会话证据采集。

本轮理由：必须把产品交接证据与可复用 PID 区分，并避免取消或 Aspire 退出连带终止目标。

依赖：无

场景：AR04、AR05、AR09、AR10、AR11、AR24

禁止承担：单凭进程名或 PID 确认身份；把 mon.exe 退出当目标退出；日志记录秘密；Dispose 时 Kill

### `public Result<EnvironmentSnapshot> CaptureEnvironment()`

返回独立只读父环境快照；不与后续启动共享可变字典。

失败约定：EnvironmentUnavailable。

### `public ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct)`

核验路径及产品适配能力，临时展开 SecretSlot，应用移除项及覆盖项；返回包含机器、PID、创建时间、映像及交接关联证据的观察句柄。目标不得加入随工作台退出终止的 Job 或资源组。

失败约定：EntrypointUnavailable、UnsupportedAdapter、PermissionDenied、ProcessStartFailed、StartIndeterminate；创建后取消不能宣称未启动。

### `public IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process, CancellationToken ct)`

每次附着先核验完整进程身份；分别发送入口退出、候选交接、目标窗口、明确登录和确认退出事实。无法关联时只报告候选或未知；允许对持久身份进行只读恢复观察。释放只解绑事件与句柄。

失败约定：取消抛 OperationCanceledException；身份不符、访问受限或证据缺失报告 IdentityUnconfirmed、ObservationUnavailable，不冒充目标退出。

## IResourceProbe

种类：interface；生命周期：契约；每次调用独立超时。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IResourceProbe.cs；模块：contracts

采集冻结资源描述对应的一次分项观测。

本轮理由：F04：探测端口必须使用传入描述并回传原身份。

依赖：无

场景：AR05、AR06、AR07、AR08

禁止承担：按 ResourceId 重读并替换端点；内部无限重试；推断许可签出或业务登录

### `ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct)`

校验描述并按其端点或选择参数进行一次探测，返回相同完整身份键及实际来源证据；不查询当前配置替换参数。

失败约定：InvalidResourceDescriptor、MachineMismatch、ProbeUnavailable、AccessDenied；超时作为对应身份的观测事实，取消抛 OperationCanceledException。

## WindowsResourceProbe

种类：class；生命周期：应用级单例；调用级观测状态及超时。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/WindowsResourceProbe.cs；模块：platform

实现冻结 TCP、服务或虚拟机入口描述的一次 Windows 观测。

本轮理由：F04：适配器不能在观测期间切换到新的同 ID 端点。

依赖：无

场景：AR05、AR06、AR07、AR08

禁止承担：读取当前配置替换描述；自动控制资源；把服务失败解释为 VM 丢失；推断许可签出

### `public ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct)`

开始时冻结并校验传入描述，整个调用仅使用这些参数；完成时返回原身份、实际来源和时间。TCP 记录实际解析地址，服务与 VM 保留各自证据，不合并推断。

失败约定：InvalidResourceDescriptor、MachineMismatch、ProbeUnavailable、AccessDenied；超时为观测事实，取消抛 OperationCanceledException。

## IPackageReader

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IPackageReader.cs；模块：contracts

只读发现和读取 ZIP / 7z、目录及文件映射。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `ValueTask<Result<SourceCatalog>> DiscoverAsync(SourceQuery query, CancellationToken ct)`

只列完整候选，排除下载中临时文件。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)`

校验条目路径、映射和摘要；禁止执行载荷。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## PackageReader

种类：class；生命周期：WindowsAndSourceAdapters 单例；读取句柄调用级。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/PackageReader.cs；模块：platform

只读枚举和内容检查。

本轮理由：需要明确 ZIP/7z、文件夹及映射中的路径、链接和资源上限，发现完整性不能仅凭扩展名。

依赖：无

场景：AR12、AR13、AR14

禁止承担：执行包内脚本；只检查字符串前缀判定安全；解包到业务目标

### `public ValueTask<Result<SourceCatalog>> DiscoverAsync(SourceQuery query, CancellationToken ct)`

枚举支持的 ZIP、7z、文件夹和文件候选，排除临时文件并验证可读取性；来源版本和确定性排序独立保留，不改变用户选择。

失败约定：SourceUnavailable、AccessDenied、UnsupportedSource、IncompleteSource。

### `public ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)`

检查绝对路径、上跳、驱动器和 UNC 逃逸、ADS、大小写重复、链接及映射冲突；首版拒绝无法安全解析的链接。限制条目数、解包大小和嵌套程度，按实际字节计算清单摘要。

失败约定：UnsafeArchiveEntry、UnsupportedLink、DuplicateTarget、LimitExceeded、SourceChanged、CorruptArchive。

## IFileDeliveryPort

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IFileDeliveryPort.cs；模块：contracts

目标检查、持组合租约的逐项写入及部署回执读取。

本轮理由：F02：实际写入端必须独立拒绝控制资料重叠。

依赖：无

场景：AR14、AR15、AR16、AR17、AR26

禁止承担：信任预览时的保护检查代替写入前复核；无组合租约写入；回执读取触发恢复

### `ValueTask<Result<TargetSnapshot>> InspectTargetAsync(TargetSpec target, CancellationToken ct)`

返回真实目标身份及内容基线；检查候选写入范围与控制根的冲突，但不授予执行许可。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、InvalidTarget、TargetUnavailable、UnsupportedLink、AccessDenied。

### `ValueTask<Result<DeploymentReceipt>> ApplyAsync(OperationId operationId, ConfirmedPlan plan, TargetLease lease, CancellationToken ct)`

重算完整影响范围，校验组合租约、当前保护集合及回执和备份位置。全部通过后逐项执行可靠备份、持久意图、目标写入、核验和完成事实；每项写入前复核路径身份。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、PlanExpired、InvalidLease、TargetChanged、BackupUnavailable、JournalUnavailable、RecoveryRequired、RollbackConflict、Indeterminate。

### `ValueTask<Result<DeploymentReceipt>> ReadReceiptAsync(OperationId original, CancellationToken ct)`

只读校验版本、完整前缀、损坏尾部和最后意图，供恢复规划使用；读取不会恢复执行。

失败约定：ReceiptNotFound、UnsupportedReceiptVersion、InvalidReceipt、ReceiptIncomplete。

## FileDeliveryPort

种类：class；生命周期：单例；每次 Apply 独占对应回执写入权，实际写入者持有组合租约。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/FileDeliveryPort.cs；模块：platform

执行经控制范围保护、逐项可核验的文件变更。

本轮理由：F02：防止合法目标覆盖自身恢复依据。

依赖：IStagingStore、IControlDataGuard

场景：AR14、AR15、AR16、AR17、AR26

禁止承担：覆盖控制资料或其祖先目录；无备份覆盖既有文件；日志失败后继续下一项；自动覆盖外部修改进行回滚

### `public ValueTask<Result<TargetSnapshot>> InspectTargetAsync(TargetSpec target, CancellationToken ct)`

解析真实身份、内容摘要及目录边界，检查控制范围重叠；缺失对象绑定最近可确认父目录，不支持的重解析点拒绝。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、InvalidTarget、TargetUnavailable、UnsupportedLink、AccessDenied。

### `public ValueTask<Result<DeploymentReceipt>> ApplyAsync(OperationId operationId, ConfirmedPlan plan, TargetLease lease, CancellationToken ct)`

先重算变更范围并验证组合租约、保护集合、暂存和锁内目标基线；回执及备份必须位于已登记保护根且在变更范围外。每项先可靠复制备份并验证，持久记录意图，再受控替换目标、核验并追加完成事实。每次写入复查路径身份；控制范围冲突须在首项目标写入前拒绝。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、PlanExpired、InvalidLease、TargetChanged、BackupUnavailable、JournalUnavailable、RecoveryRequired、RollbackConflict、Indeterminate；必要日志或备份失败不进入该项破坏性写入。

### `public ValueTask<Result<DeploymentReceipt>> ReadReceiptAsync(OperationId original, CancellationToken ct)`

从受保护位置读取版本化回执及完整有效前缀，保留损坏尾部和最后意图证据，不自动修复目标。

失败约定：ReceiptNotFound、UnsupportedReceiptVersion、InvalidReceipt、ReceiptIncomplete。

## ITargetLeaseProvider

种类：interface；生命周期：契约；TargetLease 绑定实际写入进程。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/ITargetLeaseProvider.cs；模块：contracts

取得跨进程目标互斥和控制数据保护的组合租约。

本轮理由：F02：目标租约须同时持有控制范围保护准入。

依赖：无

场景：AR14、AR15、AR16、AR17、AR26

禁止承担：返回未受控制范围保护的有效租约；只锁字符串完全相同的路径；部分准入后继续写入

### `ValueTask<Result<TargetLease>> AcquireAsync(IReadOnlyList<CanonicalPath> targets, CancellationToken ct)`

targets 表示完整变更影响范围。先取得控制数据保护租约，再对整组真实目标原子准入；任一失败释放已取得的保护租约。返回租约持有两种准入至写入及回执完成。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、TargetBusy、InvalidTarget、IdentityUnavailable、LockUnavailable、UnsupportedLink。

## CanonicalTargetLeaseProvider

种类：class；生命周期：每进程单例；组合 TargetLease 从锁内复核持有至最后可靠回执。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/CanonicalTargetLeaseProvider.cs；模块：platform

按真实目标范围提供包含控制数据保护的跨进程组合租约。

本轮理由：F02：部署目标互斥本身不能保护配置、日志和恢复资料。

依赖：IControlDataGuard

场景：AR14、AR15、AR16、AR17、AR26

禁止承担：仅使用 SemaphoreSlim；父子路径分别获准冲突写入；仅按工作区目录隔离锁；租约到期接管仍存活进程

### `public ValueTask<Result<TargetLease>> AcquireAsync(IReadOnlyList<CanonicalPath> targets, CancellationToken ct)`

先调用 IControlDataGuard.AcquireAsync，再在目标准入门内比较整组规范路径、卷和文件身份及父子范围；全部准入或全部拒绝。租约组合持有保护范围和目标互斥，按相反顺序释放。进程存活不能仅靠超时判断。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、TargetBusy、InvalidTarget、IdentityUnavailable、LockUnavailable、UnsupportedLink；失败释放本次已取得资源。

## IEnvironmentStore

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IEnvironmentStore.cs；模块：contracts

读取与尽力检测冲突的环境写入端口。

本轮理由：以真实平台能力替代误导性的 CompareExchangeAsync，并引入逐步检查点。

依赖：IOperationContext

场景：AR18、AR26

禁止承担：声明对任意外部写入者原子 CAS

### `ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address, CancellationToken ct)`

区分不存在、空值和实际值。

失败约定：InvalidScope、VariableUnavailable、PermissionDenied。

### `ValueTask<Result<Unit>> ApplyVerifiedAsync(ConfirmedVariablePlan plan, IOperationContext context, CancellationToken ct)`

按 WindowsEnvironmentStore 契约逐步写入和核验；所有破坏性步骤前检查点须持久成功。

失败约定：VariableConflict、ReservedVariable、PermissionDenied、JournalUnavailable、PartialApplied、Indeterminate。

## WindowsEnvironmentStore

种类：class；生命周期：WindowsAndSourceAdapters 单例；写入锁和核验状态调用级。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/WindowsEnvironmentStore.cs；模块：platform

持久变量读取及带检查点的尽力冲突检测写入。

本轮理由：删除误导性的 CompareExchangeAsync；Windows 持久环境没有针对任意外部写入者的原子比较交换能力。

依赖：IOperationContext

场景：AR18、AR26

禁止承担：承诺系统级原子 CAS；发现冲突后继续覆盖；默认系统作用域；广播环境变化失败就重写变量

### `public ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address, CancellationToken ct)`

按机器、用户身份或系统作用域读取；区分不存在、空值和具体值，变量名采用 Windows 比较规则。

失败约定：InvalidScope、VariableUnavailable、PermissionDenied。

### `public ValueTask<Result<Unit>> ApplyVerifiedAsync(ConfirmedVariablePlan plan, IOperationContext context, CancellationToken ct)`

取得应用各进程共享的作用域及旧新名称锁，重读两端基线；逐步持久报告意图，写新名并核验，再重读旧名后删除并核验。大小写等价名称按同一变量处理。任何步骤前日志失败立即停止；外部竞争仍可能发生，返回的结果仅代表可观察核验结果。

失败约定：VariableConflict、ReservedVariable、PermissionDenied、JournalUnavailable、PartialApplied、Indeterminate；写后冲突不盲目恢复旧值。

## IPrivilegeBroker

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IPrivilegeBroker.cs；模块：contracts

为已确认有限写入请求 UAC，关联调用者、一次性会话、计划摘要和回执。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request, CancellationToken ct)`

只允许文件写入/恢复和变量应用，不接受任意命令行或登录秘密。

失败约定：PermissionDenied / InvalidRequest / WorkerDisconnected。

## WindowsPrivilegeBroker

种类：class；生命周期：WindowsAndSourceAdapters 应用级单例；每操作独立一次性提权会话。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/WindowsPrivilegeBroker.cs；模块：platform

建立受限提权会话并转交已确认操作。

本轮理由：UAC 拒绝、worker 断开和已发生写入必须区分；提权也必须保留同一计划及回执身份。

依赖：无

场景：AR14、AR15、AR16、AR17、AR18、AR26

禁止承担：任意脚本或命令执行代理；登录秘密传入 worker；断开后自动再次执行

### `public ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request, CancellationToken ct)`

请求绑定调用者身份、OperationId、一次性随机会话、期限、协议版本、PlanDigest 和允许目标；通过受 ACL 保护的本地通道交付，不在命令行传秘密。等待 UAC 时父进程不持目标锁；连接后只接受匹配回执。

失败约定：PermissionDenied、InvalidRequest、WorkerUnavailable、WorkerDisconnected、Indeterminate；UAC 取消且 worker 未执行时才报告零副作用，连接丢失先读回执核验。

## IRunJournal

种类：interface；生命周期：契约；实现须覆盖同一工作区的多个进程。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IRunJournal.cs；模块：contracts

持久受理、执行准入、控制请求及有序操作事实的权威存储。

本轮理由：F01：定义跨进程唯一准入、执行权校验及独立取消写入入口。

依赖：无

场景：AR09、AR11、AR16、AR18、AR22

禁止承担：只凭序号比较授予执行权；自动重放；向 UI 暴露执行凭据或冻结秘密引用

### `ValueTask<Result<OperationAcceptance>> AcceptAsync(OperationRequestSnapshot request, CancellationToken ct)`

在跨进程操作锁内持久保存首次请求及受理事实，绑定首次提交进程身份；可靠落盘后仅该调用得到 Created 和一次性准入凭据。同 ID 同摘要返回 Existing 且不携带凭据，不同摘要拒绝。

失败约定：DuplicateOperationConflict、UnsupportedRecordVersion、JournalUnavailable、AcceptanceIndeterminate；未知受理结果不能重新颁发首次凭据。

### `ValueTask<Result<OperationExecutionLease>> TryBeginExecutionAsync(OperationAcceptance acceptance, CancellationToken ct)`

在同一操作锁内验证首次凭据、活进程身份、Accepted 状态及无取消请求，原子持久推进至 Executing 并消费凭据；刷新成功后才返回执行租约。已经消费、执行过或持有者失效均不得再取得。

失败约定：InvalidAdmission、ExecutionAlreadyClaimed、CancellationRequested、OwnerUnavailable、JournalUnavailable、ExecutionAdmissionIndeterminate。

### `ValueTask<Result<RunSnapshot>> RequestCancellationAsync(OperationId id, CancellationToken ct)`

在操作锁内追加有序取消控制事实；相同取消意图幂等。与执行准入竞争时，以可靠落盘顺序决定准入是否仍可发生。

失败约定：RunNotFound、CancellationNotAllowed、JournalUnavailable、CommitIndeterminate。

### `ValueTask<Result<OperationRequestSnapshot>> ReadRequestAsync(OperationId id, CancellationToken ct)`

仅向应用执行边界提供已受理快照；读取不授予首次执行或恢复执行权。

失败约定：RunNotFound、CorruptRecord、UnsupportedRecordVersion、JournalUnavailable。

### `ValueTask<Result<RunSnapshot>> AppendAsync(OperationExecutionLease execution, long expectedSequence, RunEvent fact, CancellationToken ct)`

在操作锁内核验执行权、进程身份、序号和未关闭状态，再追加带长度、版本和校验和的事实并刷新。执行日志为权威，快照为可重建缓存。

失败约定：ExecutionLeaseInvalid、SequenceConflict、JournalUnavailable、CommitIndeterminate；结果未知先核对序号及事实标识，不重复外部动作。

### `ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)`

由完整有效前缀重建。Accepted 原持有者死亡显示待核验且无自动执行权；失效 Executing 显示待核验。进程身份无法证明时保持未知，不用超时判定死亡。

失败约定：RunNotFound、CorruptJournal、UnsupportedRecordVersion；损坏尾部返回可恢复前缀及不完整标记。

### `IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)`

按同一水位建立快照及增量，每操作序号单调；连续性丢失时要求重读或终止流，不静默遗漏事实。

失败约定：取消抛 OperationCanceledException；基础设施故障终止流并允许重连。

## JsonRunJournal

种类：class；生命周期：工作区级单例；持久锁覆盖所有相关进程，执行凭据仅在原进程有效。；决定：refine

拟议位置：rewrite/LaunchDesk.Windows/Storage/JsonRunJournal.cs；模块：storage

实现 IRunJournal 的跨进程受理、一次执行准入及版本化事实日志。

本轮理由：F01：落实持久准入点及崩溃后的不可重放规则。

依赖：IWorkspaceLocationResolver

场景：AR09、AR11、AR16、AR18、AR20、AR22

禁止承担：以进程内锁代替跨进程准入；租约失效后自动重领；发布未落盘成功事实；删除恢复仍引用的日志

### `public ValueTask<Result<OperationAcceptance>> AcceptAsync(OperationRequestSnapshot request, CancellationToken ct)`

锁内查重并写入冻结请求、摘要、首次提交进程身份及 Accepted 事实，刷新后返回 Created。重复调用仅返回 Existing；首次返回丢失后不得通过重试取回准入凭据。

失败约定：DuplicateOperationConflict、UnsupportedRecordVersion、JournalUnavailable、AcceptanceIndeterminate。

### `public ValueTask<Result<OperationExecutionLease>> TryBeginExecutionAsync(OperationAcceptance acceptance, CancellationToken ct)`

锁内验证不可伪造的一次性凭据、进程创建身份、Accepted 状态和取消状态，可靠记录 Executing 与唯一执行代次后返回租约。结果未知或调用方丢失租约时不重新颁发。

失败约定：InvalidAdmission、ExecutionAlreadyClaimed、CancellationRequested、OwnerUnavailable、JournalUnavailable、ExecutionAdmissionIndeterminate。

### `public ValueTask<Result<RunSnapshot>> RequestCancellationAsync(OperationId id, CancellationToken ct)`

锁内持久追加取消事实后发布；若取消先于执行准入可靠落盘，后续准入拒绝。不会替调用者创建执行作用域。

失败约定：RunNotFound、CancellationNotAllowed、JournalUnavailable、CommitIndeterminate。

### `public ValueTask<Result<OperationRequestSnapshot>> ReadRequestAsync(OperationId id, CancellationToken ct)`

校验版本、完整性和摘要后返回应用内部冻结请求，读取不能恢复执行权。

失败约定：RunNotFound、CorruptRecord、UnsupportedRecordVersion、JournalUnavailable。

### `public ValueTask<Result<RunSnapshot>> AppendAsync(OperationExecutionLease execution, long expectedSequence, RunEvent fact, CancellationToken ct)`

锁内验证租约及序号，追加完整记录并刷新后发布。取消控制事实可能改变序号；写入者重读后重新验证公共迁移，不能因此重做副作用。

失败约定：ExecutionLeaseInvalid、SequenceConflict、JournalUnavailable、CommitIndeterminate。

### `public ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)`

从完整前缀重建并检查原执行者身份。受理后未准入即崩溃、准入后未写意图即崩溃、意图后结果未知分别保留可靠阶段，均不自动派发。

失败约定：RunNotFound、CorruptJournal、UnsupportedRecordVersion；无法检查持有者时保留待核验。

### `public IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)`

从一致水位发送快照和增量；缓存失效可重建，序号缺口必须显式通知或结束订阅。

失败约定：取消抛 OperationCanceledException；日志故障终止流。

## ITaskQueries

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/ITaskQueries.cs；模块：contracts

为 UI 提供只读任务快照与订阅，不维护第二份业务状态。的公开边界

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)`

缺失记录返回明确错误，不假报完成。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)`

先快照再增量；取消 UI 订阅不结束任务。

失败约定：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

## OperationProjectionService

种类：class；生命周期：OperationCoordination 应用级单例。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Operations/OperationProjectionService.cs；模块：coordinator

向 UI 暴露脱敏操作状态及版本。

本轮理由：需要为重连、序号缺口和敏感请求隔离提供明确协议。

依赖：IRunJournal

场景：AR09、AR10、AR11、AR16

禁止承担：第二份业务状态机；查询触发执行或恢复；返回秘密引用

### `public ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)`

返回最近可靠状态、模块载荷、证据时间与不确定标记；不暴露冻结请求、CredentialRef 或恢复内部路径。

失败约定：RunNotFound、JournalUnavailable、UnsupportedRecordVersion。

### `public IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)`

继承日志快照加增量协议，保持每操作序号；页面检测重复或缺口后重读，不把枚举结束解释为操作结束。

失败约定：取消抛 OperationCanceledException；基础设施故障结束订阅并允许重连。

## ShellViewModel

种类：class；生命周期：Presentation.Wpf 主窗口级，关闭时异步释放。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/ShellViewModel.cs；模块：desktop

导航、全局操作摘要与固定主要操作区状态。

本轮理由：基线只有激活没有释放契约，可能重复订阅或在窗口关闭时同步等待 UI 回调。

依赖：ITaskQueries、ThemeResourceCatalog

场景：AR01、AR11、AR23

禁止承担：持有操作执行令牌；同步阻塞 UI；窗口释放触发目标进程终止

### `public ValueTask ActivateAsync(CancellationToken ct)`

幂等建立摘要订阅；后台读取快照，仅通过 Dispatcher 更新绑定属性。固定主要操作区不放入内容 ScrollViewer。

失败约定：查询故障显示可重连状态；取消仅取消本次激活或订阅。

### `public ValueTask DisposeAsync()`

在 Dispatcher 上标记释放并解绑 UI 事件，取消自身订阅，异步等待枚举退出；不使用 Wait 或 Result，不取消应用操作。

失败约定：重复释放安全；Dispatcher 已关闭时跳过属性更新并记录脱敏释放故障。

## ProfilesPageViewModel

种类：class；生命周期：页面级；持久结果位于提交历史或操作日志。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/ProfilesPageViewModel.cs；模块：desktop

配置列表交互、稳定直接命令及启动提交。

本轮理由：F05：复制和删除调用同步稳定 CommitId 及未知结果核验。

依赖：ICatalogService、IOperationCommands

场景：AR02、AR03、AR11、AR20、AR27

禁止承担：直接访问进程或凭据；未知复制自动生成新请求；从选中记录填充空白新增；离页终止启动

### `public ValueTask RefreshAsync(string searchText, Guid? applicationId, CancellationToken ct)`

读取公开快照后筛选，以请求代次丢弃过时结果并在 Dispatcher 更新；列表读取不代替提交核验。

失败约定：查询错误显示在页内；取消不清除有效列表。

### `public ProfileDraft CreateNew()`

返回无 ProfileId、无秘密和前条记录残留的空白草稿；实际实体 ID 由首次提交准备分配。

失败约定：已释放抛 ObjectDisposedException。

### `public ValueTask CopyAsync(Guid sourceId, Revision expected, CancellationToken ct)`

每次明确复制动作先分配 CommitId 并冻结源 ID 和修订；调用目录服务并保留核验标识。重入或未知结果只核验原 ID，不再发起复制；成功展示原提交生成的副本 ID。

失败约定：显示 ProfileNotFound、CommitConflict、RevisionConflict 或提交未决状态，不局部伪造新记录。

### `public ValueTask DeleteAsync(Guid profileId, Revision expected, CancellationToken ct)`

冻结删除意图及 CommitId 后提交，仅成功后刷新；重复请求使用原标识，未知结果进入核验。

失败约定：显示引用、修订、提交冲突或未决错误，不以当前列表缺项证明原删除成功。

### `public ValueTask VerifyCommitAsync(CommitId commitId, CancellationToken ct)`

读取原提交回执并显示状态、原修订及生成 ID；确定结果后另行刷新当前列表，不自动补做命令。

失败约定：核验不可用保持未决，不推定未提交。

### `public ValueTask LaunchSelectedAsync(CancellationToken ct)`

提交选中 ID、修订及稳定 OperationId，受理后由全局查询观察；已有操作不重新派发。

失败约定：BindingRequired、RevisionConflict、AcceptanceIndeterminate、ExecutionAdmissionIndeterminate；未知结果不自动再次启动。

### `public ValueTask DisposeAsync()`

取消页面读取及等待，解绑事件，拒绝迟到更新；不取消已受理操作，也不使提交记录失效。

失败约定：幂等；不同步等待 UI 回调。

## ProfileEditorViewModel

种类：class；生命周期：编辑页级；每次输入和逻辑保存有独立代次，提交历史由存储持久保留。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/ProfileEditorViewModel.cs；模块：desktop

编辑草稿、一次性接收秘密、管理稳定保存意图和显示核验结果。

本轮理由：F03、F05：保证输入先移交再清理，并提供未知提交的明确核验路径。

依赖：ICatalogService、PasswordEntryBehavior

场景：AR03、AR04、AR20

禁止承担：焦点移动时销毁待保存输入；未知保存按新 ID 自动重试；保留秘密用于重试；绑定已保存密码

### `public ValueTask SaveAsync(SecretUpdate secretChange, CancellationToken ct)`

命令适配在 Dispatcher 上明确 Keep、Clear 或 Replace；Replace 先通过 DetachInput 取得唯一秘密所有权，再调用本方法。冻结草稿及输入标识并分配 CommitId 后提交，阻止同一输入重入。保存调用 finally 释放已移交秘密并清理剩余输入。未知结果保留 CommitId 和非秘密意图，显示待核验；不缓存秘密或自动重试。

失败约定：显示 MissingSecretInput、CommitConflict、RevisionConflict、CredentialWriteFailed、CommitPending、CommitNotApplied、CommitIndeterminate；空输入不自动改为 Keep 或 Clear。

### `public ValueTask VerifySaveAsync(CommitId commitId, CancellationToken ct)`

调用 ICatalogService.InspectCommitAsync。Committed 显示原修订和实体 ID，再独立刷新当前配置；Pending 或 Indeterminate 保持待核验；确定 NotCommitted 后才允许用户重新输入并以新 CommitId 发起新保存。

失败约定：存储或历史校验错误显示核验不可用，不显示未保存，不清理未知候选凭据。

### `public ValueTask DisposeAsync()`

Dispatcher 上标记失效、隐藏并销毁未移交输入、解绑事件；取消页面等待，已移交对象仍由保存调用 finally 释放。禁止迟到属性更新，不把离页当作配置未提交。

失败约定：幂等；不使用 Wait、Result 或同步等待 Dispatcher。

## LicensePageViewModel

种类：class；生命周期：Presentation.Wpf 页面级。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/LicensePageViewModel.cs；模块：desktop

展示分项证据、时间及复查状态。

本轮理由：补充监测订阅与释放，避免离页停止全局监测或迟到回调访问已释放页面。

依赖：IResourceMonitor

场景：AR05、AR06、AR07、AR08

禁止承担：页面自行探测；TCP 成功显示许可已签出；离页停止外部资源

### `public ValueTask ActivateAsync(CancellationToken ct)`

订阅 IResourceMonitor，Dispatcher 更新 VM、服务、TCP 各自状态与时效。

失败约定：订阅故障显示监测不可用，重连后重读快照。

### `public ValueTask RefreshAsync(Guid resourceId, CancellationToken ct)`

请求用例复查，不自行计数或推断许可签出。

失败约定：显示 ResourceNotFound、ProbeUnavailable 及新鲜度错误。

### `public ValueTask DisposeAsync()`

取消本页订阅并异步等待退出，解绑事件；监测服务继续运行。

失败约定：幂等释放；不使用同步 Dispatcher 等待。

## ToolPageViewModel

种类：class；生命周期：Presentation.Wpf 页面级。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/ToolPageViewModel.cs；模块：desktop

来源选择、计划预览、摘要绑定确认及结果导航。

本轮理由：基线缺少变量确认提交入口，且必须让编辑导致旧确认失效并明确释放订阅。

依赖：ICatalogService、IToolSourceCatalog、IOperationCommands、IDeploymentPlanningService、IVariablePlanningService

场景：AR12、AR13、AR14、AR18、AR26

禁止承担：直接写目标或持久环境；沿用编辑前确认；页面取消代表操作取消

### `public ValueTask PreviewDeploymentAsync(DeployRequest request, CancellationToken ct)`

展示规划服务返回的登记计划及摘要；来源、映射、目标或备份策略改变立即清除旧确认。

失败约定：显示来源、目标、暂存或安全路径错误，不创建执行操作。

### `public ValueTask PreviewVariableAsync(VariableRenameRequest request, CancellationToken ct)`

展示旧新名称、两端基线、作用域和摘要；默认不选择系统作用域。

失败约定：显示 VariableConflict、ReservedVariable、InvalidScope。

### `public ValueTask RefreshSourcesAsync(RefreshIntent intent, CancellationToken ct)`

普通刷新传 KeepSelection，只有用户显式选最新才传 SelectNewest；选择变化使旧部署确认失效。

失败约定：来源不可用时保留选择并标记不可执行。

### `public ValueTask SubmitConfirmedAsync(ConfirmedPlan plan, CancellationToken ct)`

只提交当前已展示并经用户确认的 PlanId 和 PlanDigest；受理后展示 OperationId。

失败约定：PlanExpired、TargetBusy、AcceptanceIndeterminate；不得自动重新确认新计划。

### `public ValueTask SubmitVariablesAsync(ConfirmedVariablePlan plan, CancellationToken ct)`

显式提交 ApplyVariables，保存配置按钮不能调用此方法。

失败约定：PlanExpired、VariableConflict、PermissionDenied、AcceptanceIndeterminate。

### `public ValueTask DisposeAsync()`

取消预览、页面等待及订阅，清除确认对象并异步释放；已经受理的操作继续。

失败约定：重复释放安全；迟到结果不更新已释放页面。

## PasswordEntryBehavior

种类：class；生命周期：密码输入控件级；输入代次和显隐状态仅在当前编辑会话有效。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/PasswordEntryBehavior.cs；模块：desktop

管理本次输入的显示缓冲、一次性移交及销毁。

本轮理由：F03：将失焦隐藏与销毁输入分离，避免保存或显隐按钮先清空密码。

依赖：无

场景：AR04、AR23

禁止承担：页内失焦销毁待保存输入；读取保存密码；通过属性通知或日志发布秘密；重复移交同一输入

### `public void RevealWhileEditing()`

仅在当前有效编辑会话按用户动作短暂显示本次输入。显隐按钮获得焦点不销毁输入；显示动作结束、离开显隐交互或编辑页失焦时调用 HideDisplay。

失败约定：非 Dispatcher 线程抛 InvalidOperationException；已释放抛 ObjectDisposedException；无输入时无操作。

### `public void HideDisplay()`

立即隐藏明文并清理显示缓冲，保留密码输入控件中的本次未移交输入。密码框移焦至保存按钮、其他字段或显隐按钮，以及窗口暂时停用，均执行此方法。

失败约定：要求 Dispatcher 线程；重复调用安全，不改变 Keep、Clear 或 Replace 意图。

### `public TransientSecret DetachInput()`

在 Dispatcher 上先取得本次输入的唯一 TransientSecret 所有权，再将输入标为已消费并清空控件和显示缓冲。相同输入代次只能移交一次；不能把此前清空后的空状态推断为 Keep 或 Clear。

失败约定：非 UI 线程或已释放抛对应异常；无有效 Replace 输入抛 InvalidOperationException，由命令边界映射为 MissingSecretInput。

### `public void HideAndClear()`

显式取消编辑、离页、保存尝试结束或销毁控件时，隐藏并销毁尚未移交输入及显示缓冲。普通页内焦点移动不调用此方法。

失败约定：要求 Dispatcher 线程；重复清理安全，不释放已经移交给保存调用的对象。

### `public void Dispose()`

Dispatcher 上解绑控件事件并销毁剩余输入；已移交秘密由保存调用负责释放。

失败约定：重复释放安全；不承诺消除托管内存或目标命令行的全部残留。

## ThemeResourceCatalog

种类：class；生命周期：Presentation.Wpf 应用级不可变资源目录。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/ThemeResourceCatalog.cs；模块：desktop

集中管理经批准的视觉令牌和控件状态资源。

本轮理由：未提供 FigCraft 图稿，不能宣称已固定视觉值；原返回类型与结构化错误描述也不一致。

依赖：无

场景：AR01、AR11、AR23

禁止承担：用默认主题宣称符合图稿；以资源目录代替页面布局验收

### `public Result<ThemeSnapshot> Resolve(ThemePreference preference)`

解析明确提供的资源键及主题；图稿缺失时标记未核定项，不编造颜色、间距、图标或圆角验收值。

失败约定：ThemeUnavailable、MissingResource、DesignReferenceMissing。

## PrivilegedWorkerHost

种类：class；生命周期：每次提权操作独立进程；实际写入期间持有本进程目标租约。；决定：refine

拟议位置：rewrite/LaunchDesk.Worker/PrivilegedWorkerHost.cs；模块：worker

在父操作唯一执行权下，执行有限提权步骤并承担实际写入者责任。

本轮理由：F01、F02：worker 须独立执行保护校验，且不能成为第二个操作执行权持有者。

依赖：IFileDeliveryPort、IEnvironmentStore、ITargetLeaseProvider、IStagingStore、IOperationContext、IControlDataGuard

场景：AR14、AR15、AR16、AR17、AR18、AR26

禁止承担：接受任意脚本或登录秘密；依据父进程检查结果跳过独立保护校验；自行重新领取操作执行权；通道断开后继续未记录步骤

### `public ValueTask<int> RunAsync(WorkerEnvelope request, CancellationToken ct)`

验证调用者、协议、一次性会话、期限、OperationId、执行代次和摘要；独立读取冻结计划及可信控制根登记，不信任请求缩小保护范围。重算变更范围并检查保护资料，随后由 worker 取得组合目标租约、锁内复核及调用文件端口。变量检查点经认证上下文代理由父进程持久确认；worker 不获取第二份执行租约。通道丢失停止下一副作用。

失败约定：InvalidRequest、CallerMismatch、ReplayRejected、ExecutionLeaseInvalid、PlanExpired、ProtectedDataOverlap、ProtectionUnavailable、TargetBusy、PermissionDenied、RecoveryRequired；退出码不代替业务回执。

## DesktopCompositionRoot

种类：class；生命周期：每 Desktop 进程一次；保护登记跨进程持久共享。；决定：refine

拟议位置：rewrite/LaunchDesk.Desktop/Bootstrap/DesktopCompositionRoot.cs；模块：bootstrap

装配同一 Desktop 运行时、保护登记及操作和直接提交协议。

本轮理由：F02：控制根登记必须先于任何控制资料创建和部署写入能力开放。

依赖：WorkspaceCatalogService、ConfigurationShareService、ResourceSupervisor、OperationCoordinator、OperationHandlerRegistry、OperationProjectionService、LaunchOperationHandler、DeliveryOperationHandler、VariableOperationHandler、WorkspaceMigrationService、JsonWorkspaceStore、JsonRunJournal、JsonMigrationStore、PrivateStagingStore、WindowsControlDataGuard、WindowsSecretVault、WindowsEntryDiscovery、WindowsProcessGateway、WindowsResourceProbe、PackageReader、FileDeliveryPort、CanonicalTargetLeaseProvider、WindowsEnvironmentStore、WindowsPrivilegeBroker、ThemeResourceCatalog

场景：AR01、AR14、AR16、AR20、AR21、AR22、AR24、AR25、AR26

禁止承担：控制根登记前创建恢复资料或开放部署；引用 AspireDevelopmentHost；启动时自动重放；复制模块业务规则；停止外部资源

### `public Result<DesktopRuntime> Build(RuntimeOptions options)`

先解析唯一工作区位置并建立 WindowsControlDataGuard，登记完整控制根及共享登记根，再装配存储、暂存、锁、迁移、用例和页面工厂。相同 guard 策略用于普通写入者及 worker。操作启动只索引和核验未完成状态，不重领执行权；提交历史随工作区保留。普通入口不依赖 Aspire，诊断失败降级本地记录。

失败约定：ConfigurationUnavailable、ProtectionUnavailable、ProtectedScopeConflict、IdentityUnavailable、DuplicateRegistration、PolicyInvalid、UnsupportedPlatform；保护登记失败不得开放破坏性写入，也不得切换空工作区。

## AspireDevelopmentHost

种类：class；生命周期：CompositionAndDiagnostics 开发会话级；通过进程产物边界启动 Desktop。；决定：refine

拟议位置：rewrite/LaunchDesk.DevHost/Aspire/AspireDevelopmentHost.cs；模块：devhost

提供可替换开发入口及开发资源所有权管理。

本轮理由：仅编排模拟桌面不足以满足 ADR-08；必须使用真实 Aspire AppHost 启动同一 Desktop 可执行文件，解除对 DesktopCompositionRoot 的直接实例依赖。

依赖：无

场景：AR01、AR24、AR25

禁止承担：把 Aspire 类型带入核心或 Desktop 普通入口；直接重建另一套业务容器；结束真实许可服务、VMware 或 AVEVA

### `public ValueTask<int> RunAsync(DevelopmentOptions options, CancellationToken ct)`

在 LaunchDesk.AppHost 使用真实 Aspire AppHost 启动与普通入口相同的 Desktop 产物，传入显式隔离数据目录和可选诊断配置。许可服务、AVEVA、VMware 登记为外部所有；停止仅回收自有开发资源和工作台，目标会话不得加入随 AppHost 终止的进程组。

失败约定：DevelopmentResourceUnavailable、DesktopStartFailed；诊断端点失联时 Desktop 继续本地业务，AppHost 故障不伪造业务完成或远端执行结果。

## IDeploymentPlanningService

种类：interface；生命周期：契约，无实例状态。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IDeploymentPlanningService.cs；模块：contracts

部署、恢复及回滚的只读目标预览契约。

本轮理由：F02：规划接口必须明确控制范围重叠属于不可执行错误。

依赖：无

场景：AR13、AR14、AR16、AR17、AR26

禁止承担：预览写业务目标；对保护范围冲突允许确认覆盖；复用旧恢复确认

### `ValueTask<Result<DeploymentPlan>> PreviewAsync(DeployRequest request, CancellationToken ct)`

检查所有映射、目录操作及隐式变更范围，与受保护控制根不重叠才生成计划；内容冻结到私有暂存，计划摘要覆盖实际变更范围、目标基线及备份策略。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、UnsafeArchiveEntry、SourceUnavailable、SourceChanged、TargetUnavailable、InvalidPath、StagingUnavailable、MixedSideEffectsUnsupported。

### `ValueTask<Result<DeploymentPlan>> PreviewRecoveryAsync(RecoveryRequest request, CancellationToken ct)`

读取原回执、备份及当前目标，重新检查保护范围和外部修改；恢复或回滚建立关联 OriginalOperationId 的新计划并重新确认。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、ReceiptNotFound、InvalidReceipt、BackupUnavailable、RollbackConflict、TargetUnavailable。

## IOperationContext

种类：interface；生命周期：单操作契约；worker 代理不能转移执行权。；决定：refine

拟议位置：rewrite/LaunchDesk.OperationContracts/IOperationContext.cs；模块：contracts

绑定一个执行租约的事实报告和释放边界。

本轮理由：F01：上下文的事实写入必须受唯一执行权约束。

依赖：无

场景：AR09、AR16、AR18、AR22、AR26

禁止承担：作为服务定位器；自行创建执行权；释放后仍接受新报告

### `ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct)`

串行验证公共迁移，通过绑定的执行租约追加并在落盘后发布；检查点失败时拒绝下一副作用。控制事实引起序号变化时重读验证，不重做外部步骤。

失败约定：ExecutionLeaseInvalid、InvalidTransition、SequenceConflict、JournalUnavailable、CommitIndeterminate。

### `ValueTask DisposeAsync()`

停止接收新报告，等待已开始的必要报告完成，释放本地资源和执行租约；不结束目标进程。

失败约定：幂等；释放错误不覆盖原错误，不声称已持久记录停止。

## VariablePlanner

种类：class；生命周期：EnvironmentManagement 无请求状态的规划服务。；决定：refine

拟议位置：rewrite/LaunchDesk.UseCases/Delivery/VariablePlanner.cs；模块：delivery-handler

只读生成持久变量变更计划。

本轮理由：需要同时冻结旧新名称的存在性和值、作用域和确认摘要，并处理 Windows 名称大小写等价。

依赖：IEnvironmentStore

场景：AR18

禁止承担：预览隐式持久应用；系统作用域默认选中；忽略新名已存在

### `public ValueTask<Result<VariableChangePlan>> PreviewAsync(VariableRenameRequest request, CancellationToken ct)`

读取旧名和新名，分别记录不存在、空值或具体值；校验保留名、机器、用户或系统作用域。计划摘要覆盖这些基线与预期值；同名大小写变体不生成先写再删操作。预览不写 Windows，也不保存配置。

失败约定：VariableConflict、ReservedVariable、InvalidVariableName、InvalidScope、VariableUnavailable。

## IVariablePlanningService

种类：interface；生命周期：Contract；决定：keep

拟议位置：rewrite/LaunchDesk.OperationContracts/IVariablePlanningService.cs；模块：contracts

只读变量变更规划边界。

依赖：无

场景：通过调用方覆盖

禁止承担：遵守上层边界

### `ValueTask<Result<VariableChangePlan>> PreviewAsync(VariableRenameRequest request, CancellationToken ct)`

读取原值和新名，验证作用域和保留字段，形成待确认计划；不写 Windows。

失败约定：VariableConflict / ReservedVariable / VariableUnavailable。

## IEntryDiscovery

种类：interface；生命周期：契约；适配器由组合根装配。；决定：add

拟议位置：待分配；模块：待分配

Configuration 定义的只读入口候选发现端口。

本轮理由：基线缺少入口发现端口，新增后可保护配置用例与 Windows 扫描的依赖方向。

依赖：无

场景：AR02、AR27

禁止承担：执行候选程序；写入配置

### `ValueTask<Result<IReadOnlyList<string>>> FindAsync(Guid applicationId, CancellationToken ct)`

根据已注册产品规则返回本机候选路径；不选择最终入口，也不修改人工绑定。

失败约定：UnsupportedApplication、DiscoveryUnavailable、AccessDenied。

## WindowsEntryDiscovery

种类：class；生命周期：WindowsAndSourceAdapters 应用级单例；扫描状态调用级。；决定：add

拟议位置：待分配；模块：待分配

WindowsAndSourceAdapters 中实现 IEntryDiscovery。

本轮理由：为自定义入口与可扩展本机发现提供具体系统适配归属。

依赖：无

场景：AR02、AR27

禁止承担：覆盖人工绑定；用启动程序验证候选

### `public ValueTask<Result<IReadOnlyList<string>>> FindAsync(Guid applicationId, CancellationToken ct)`

只读应用已配置的有限发现规则，返回规范化候选路径并去重；不扫描凭据，不执行候选验证。

失败约定：UnsupportedApplication、DiscoveryUnavailable、AccessDenied；局部扫描失败不得删除原人工入口。

## IStagingStore

种类：interface；生命周期：契约；内容保留受计划、操作和恢复引用约束。；决定：refine

拟议位置：待分配；模块：待分配

受保护暂存、不可变计划登记及执行前内容校验。

本轮理由：F02：暂存和计划登记必须明确属于受保护控制资料。

依赖：无

场景：AR13、AR14、AR16、AR17

禁止承担：在部署变更范围内存放暂存或计划；删除恢复仍引用的内容；执行来源内容

### `ValueTask<Result<SourceSnapshot>> StageAsync(SourceSnapshot source, CancellationToken ct)`

仅在已登记保护根中复制来源并按复制后的字节计算清单摘要，完成可靠写入后发布标识。

失败约定：ProtectionUnavailable、SourceChanged、StagingUnavailable、UnsafeArchiveEntry、LimitExceeded。

### `ValueTask<Result<Unit>> SavePlanAsync(DeploymentPlan plan, CancellationToken ct)`

验证摘要、完整变更范围与保护根无重叠，并确认全部恢复资料位置受保护且在变更范围外；同 PlanId 内容不可替换。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、PlanConflict、InvalidDigest、StagingUnavailable。

### `ValueTask<Result<DeploymentPlan>> ReadPlanAsync(Guid planId, CancellationToken ct)`

从受保护位置校验并读取已登记计划，不授予执行权。

失败约定：PlanNotFound、UnsupportedPlanVersion、CorruptPlan。

### `ValueTask<Result<Unit>> ValidateAsync(DeploymentPlan plan, CancellationToken ct)`

重验当前保护范围、来源、暂存清单及字节；成功只代表此次核验，写入端仍须持租约再验证。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、SourceUnavailable、SourceChanged、StagingChanged、PlanExpired。

## PrivateStagingStore

种类：class；生命周期：工作区级单例；根位置先登记后使用。；决定：refine

拟议位置：待分配；模块：待分配

在已登记控制根内维护冻结来源及不可变计划。

本轮理由：F02：私有目录属性不足以防止被本应用部署覆盖。

依赖：IPackageReader、IWorkspaceLocationResolver、IControlDataGuard

场景：AR13、AR14、AR16、AR17

禁止承担：将只读属性当作保护证明；静默更新确认计划；覆盖同 ID 计划；清理活动引用内容

### `public ValueTask<Result<SourceSnapshot>> StageAsync(SourceSnapshot source, CancellationToken ct)`

确认暂存根已受保护，再复制和刷新内容，检查复制前后来源及复制后字节；不发布半成品标识。

失败约定：ProtectionUnavailable、SourceChanged、StagingUnavailable、UnsafeArchiveEntry、LimitExceeded。

### `public ValueTask<Result<Unit>> SavePlanAsync(DeploymentPlan plan, CancellationToken ct)`

重算摘要，检查变更范围和控制根不重叠，核验暂存、回执及备份位置位于保护根；原子登记且同 ID 不可替换。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、PlanConflict、InvalidDigest、StagingUnavailable。

### `public ValueTask<Result<DeploymentPlan>> ReadPlanAsync(Guid planId, CancellationToken ct)`

从受保护目录读取并验证版本及完整性，返回不可变计划。

失败约定：PlanNotFound、UnsupportedPlanVersion、CorruptPlan。

### `public ValueTask<Result<Unit>> ValidateAsync(DeploymentPlan plan, CancellationToken ct)`

重新检查保护范围，通过 IPackageReader 重验来源，并核验暂存实际字节；写入仍需使用已核验句柄或逐项再次核验。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、SourceUnavailable、SourceChanged、StagingChanged、PlanExpired。

## WorkspaceMigrationService

种类：class；生命周期：PersistenceAndMigration；预览调用级，执行由注册表按操作创建。；决定：add

拟议位置：待分配；模块：待分配

PersistenceAndMigration 拥有配置迁移状态机，实现 IOperationHandler。

本轮理由：基线把旧格式处理塞入普通存储，缺少独立旁路转换、验证、切换和中断核验状态机。

依赖：IMigrationStore

场景：AR20、AR21、AR22

禁止承担：通过导出秘密迁移；修改唯一原始副本；读取即迁移；把同引用解释为凭据必然可用

### `public ValueTask<Result<OperationRequest>> PreviewAsync(Revision expected, CancellationToken ct)`

只读检查旧格式，生成有限 MigrateConfiguration 请求；计划冻结原数据摘要、源和目标格式、目录、修订及确认摘要，列出关联和历史验证要求。

失败约定：MigrationNotRequired、UnsupportedFormat、CorruptConfiguration、RevisionConflict、MigrationPreviewFailed。

### `public ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request, IOperationContext context, CancellationToken ct)`

确认原数据未变，持久记录迁移意图后创建旁路副本；验证 ID、关联、历史及凭据引用，再持久记录切换意图并切换有效版本。保留原始数据；切换结果未知先通过迁移标记核验。恢复必须来自用户新确认请求。

失败约定：WrongOperationKind、PlanExpired、MigrationValidationFailed、StoreUnavailable、ActivationIndeterminate、RecoveryRequired；不自动重放转换或切换。

## IMigrationStore

种类：interface；生命周期：契约；迁移记录保留至恢复引用解除。；决定：add

拟议位置：待分配；模块：待分配

定义原始读取、旁路副本及有效版本切换。

本轮理由：迁移需要独立于普通配置提交的系统端口和结果核验。

依赖：无

场景：AR20、AR22

禁止承担：自动执行恢复；序列化秘密

### `ValueTask<Result<WorkspaceSnapshotInternal>> ReadSourceAsync(CancellationToken ct)`

只读兼容读取源数据及原始摘要，保留未知字段或明确报告不可迁移字段。

失败约定：ConfigurationMissing、UnsupportedFormat、CorruptConfiguration、StoreUnavailable。

### `ValueTask<Result<Unit>> PrepareAsync(OperationRequestSnapshot request, CancellationToken ct)`

根据冻结迁移计划生成带 OperationId 的旁路副本，保留原始数据。

失败约定：SourceChanged、MigrationConversionFailed、StoreUnavailable。

### `ValueTask<Result<Unit>> ValidateAsync(OperationId operationId, CancellationToken ct)`

重读旁路副本并核对格式、ID、引用、历史及凭据关联清单。

失败约定：CandidateNotFound、MigrationValidationFailed、StoreUnavailable。

### `ValueTask<Result<Revision>> ActivateAsync(OperationId operationId, Revision expected, CancellationToken ct)`

在与普通配置提交共享的工作区锁内比较源修订，再原子切换到已验证副本；有效标记携带 OperationId。

失败约定：RevisionConflict、CandidateNotValidated、StoreUnavailable、ActivationIndeterminate。

### `ValueTask<Result<OperationFact>> InspectAsync(OperationId operationId, CancellationToken ct)`

只读核对有效标记、原始副本和候选文件，返回实际迁移阶段事实。

失败约定：MigrationNotFound、CorruptMigrationRecord、StoreUnavailable。

## JsonMigrationStore

种类：class；生命周期：PersistenceAndMigration 工作区级单例。；决定：add

拟议位置：待分配；模块：待分配

PersistenceAndMigration 实现 IMigrationStore。

本轮理由：为旁路迁移和切换故障窗口提供具体持久化责任。

依赖：IWorkspaceLocationResolver

场景：AR20、AR21、AR22

禁止承担：删除唯一源副本；绕过普通配置提交锁；缺失记录时假报迁移成功

### `public ValueTask<Result<WorkspaceSnapshotInternal>> ReadSourceAsync(CancellationToken ct)`

读取选定目录中的旧格式及摘要，绝不自动覆盖源文件。

失败约定：ConfigurationMissing、UnsupportedFormat、CorruptConfiguration、StoreUnavailable。

### `public ValueTask<Result<Unit>> PrepareAsync(OperationRequestSnapshot request, CancellationToken ct)`

复核源摘要，写独立候选目录并刷新文件；迁移记录关联源、候选、格式版本和 OperationId。

失败约定：SourceChanged、MigrationConversionFailed、StoreUnavailable。

### `public ValueTask<Result<Unit>> ValidateAsync(OperationId operationId, CancellationToken ct)`

重新解析候选文件并比对迁移清单，验证后保存与候选摘要绑定的验证事实。

失败约定：CandidateNotFound、MigrationValidationFailed、StoreUnavailable。

### `public ValueTask<Result<Revision>> ActivateAsync(OperationId operationId, Revision expected, CancellationToken ct)`

持有共享工作区提交锁后复查源修订及候选摘要；原子更新有效版本标记，保留原始副本和迁移记录。

失败约定：RevisionConflict、CandidateNotValidated、StoreUnavailable、ActivationIndeterminate。

### `public ValueTask<Result<OperationFact>> InspectAsync(OperationId operationId, CancellationToken ct)`

以磁盘有效标记和内容摘要核验实际切换结果，不依赖最后一条公共日志猜测。

失败约定：MigrationNotFound、CorruptMigrationRecord、StoreUnavailable。

## OperationAcceptance

种类：record；生命周期：Created 凭据仅在首次受理调用所在进程内短暂存活；公开快照可独立读取。；决定：add

拟议位置：待分配；模块：待分配

不可变受理结果，包含 OperationId、RequestDigest、Snapshot、Disposition；仅 Created 结果携带进程绑定、一次性且不可序列化的准入凭据。

本轮理由：F01：必须区分首次受理与重复查询，并限制首次执行准入资格。

依赖：无

场景：AR09、AR16、AR18、AR22

禁止承担：由调用者伪造 Created 或准入凭据；向 UI、导出或重启恢复代码提供准入凭据；把 Existing 结果解释为派发许可

## OperationExecutionLease

种类：class；生命周期：单次操作执行作用域；持有至处理器结束及必要事实提交完成。；决定：add

拟议位置：待分配；模块：待分配

封装已可靠记录的唯一执行权，绑定 OperationId、请求摘要、执行代次、机器、进程创建身份和随机实例标识。

本轮理由：F01：执行权必须具有明确的持有者及不可转移规则。

依赖：无

场景：AR09、AR16、AR18、AR22

禁止承担：仅凭 PID 或超时判断持有者死亡；转移、续租接管或重新颁发同一操作的执行权；把租约释放解释为业务成功或零副作用

### `ValueTask DisposeAsync()`

幂等关闭本地报告准入并释放进程句柄；已开始的必要报告先完成。释放不撤销历史执行权，不使相同操作重新可执行，也不终止目标程序。

失败约定：释放故障保留脱敏诊断；日志不可写时不得声称已持久记录终止状态。

## IControlDataGuard

种类：interface；生命周期：契约；保护登记跨进程且首版保守保留，写入范围租约按操作持有。；决定：add

拟议位置：待分配；模块：待分配

登记受保护数据根，校验部署影响范围，并约束保护范围与部署写入范围的并发准入。

本轮理由：F02：控制数据保护不能仅依赖目标之间的互斥。

依赖：无

场景：AR14、AR15、AR16、AR17、AR20、AR26

禁止承担：仅按字符串前缀判定安全；接受页面或 worker 请求缩小保护集合；自动删除已登记保护根

### `Result<Unit> RegisterWorkspace(WorkspaceLocation location)`

由可信启动配置登记工作区、日志、回执、备份、暂存、迁移及提交核验资料的完整根范围；跨进程原子检查与活动部署范围不重叠后持久登记。新根必须先登记，后创建控制资料。

失败约定：ProtectedScopeConflict、ProtectionUnavailable、IdentityUnavailable、InvalidLocation；失败时不得开放该工作区的写入能力。

### `ValueTask<Result<Unit>> ValidateAsync(IReadOnlyList<CanonicalPath> mutationScopes, CancellationToken ct)`

按规范路径、卷和文件身份、目录祖先关系检查全部实际变更范围。与任一受保护范围重叠，或范围完整性无法证明时拒绝。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、IdentityUnavailable、UnsupportedLink。

### `ValueTask<Result<ControlDataLease>> AcquireAsync(IReadOnlyList<CanonicalPath> mutationScopes, CancellationToken ct)`

在跨进程登记门内重新检查并登记活动变更范围，返回保护租约；租约期间不得新增与其重叠的控制根。保护租约不代替部署目标互斥。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、IdentityUnavailable、UnsupportedLink；取消仅撤销本次等待。

## ControlDataLease

种类：class；生命周期：由 TargetLease 组合持有，覆盖目标核验、写入及最后可靠回执。；决定：add

拟议位置：待分配；模块：待分配

持有经核验的部署影响范围与控制根登记之间的保护准入。

本轮理由：F02：校验后仍须防止新的控制资料被放入正在执行的部署范围。

依赖：无

场景：AR14、AR15、AR16、AR26

禁止承担：充当业务恢复证明；扩大已批准变更范围；释放受保护根登记

### `ValueTask DisposeAsync()`

幂等撤销本次活动变更范围并释放身份句柄；不移除受保护根，也不修改目标。

失败约定：无法确认释放时保守保留准入记录并报告脱敏故障；不得以超时释放仍存活的持有者。

## WindowsControlDataGuard

种类：class；生命周期：每进程单例；登记持久共享，活动范围绑定实际写入进程。；决定：add

拟议位置：待分配；模块：待分配

实现 IControlDataGuard，维护受 ACL 保护的跨进程控制根登记及活动变更范围。

本轮理由：F02：提供普通进程与提权 worker 共用的保护范围判定。

依赖：IWorkspaceLocationResolver

场景：AR14、AR15、AR16、AR17、AR20、AR26

禁止承担：信任请求携带的保护根白名单；目录别名不明仍允许写入；超时接管活租约；承诺约束任意外部程序

### `public Result<Unit> RegisterWorkspace(WorkspaceLocation location)`

从可信本机位置解析控制根并登记；登记表自身及 worker 控制资料根也受保护。已有根保守保留；新根与活动部署冲突时拒绝登记，不先写控制资料。各存储端口只可在已登记根下分配资料。

失败约定：ProtectedScopeConflict、ProtectionUnavailable、IdentityUnavailable、InvalidLocation。

### `public ValueTask<Result<Unit>> ValidateAsync(IReadOnlyList<CanonicalPath> mutationScopes, CancellationToken ct)`

解析存在对象的卷和文件身份、硬链接别名及祖先关系；缺失路径绑定最近可确认父目录。任一变更范围包含保护根、落在保护根内或与其别名重合均拒绝；不支持的链接拒绝。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、IdentityUnavailable、UnsupportedLink。

### `public ValueTask<Result<ControlDataLease>> AcquireAsync(IReadOnlyList<CanonicalPath> mutationScopes, CancellationToken ct)`

在统一跨进程门内完成检查和活动范围登记；绑定持有者进程创建身份并固定可支持的路径身份。普通与提权进程使用同一登记命名空间，无法读取完整保护集合时拒绝写入。

失败约定：ProtectedDataOverlap、ProtectionUnavailable、IdentityUnavailable、UnsupportedLink。

## ResourceDescriptor

种类：record；生命周期：配置快照、探测轮次和启动请求内的不可变值。；决定：refine

拟议位置：待分配；模块：待分配

冻结不可变资源身份：ResourceId、ResourceRevision、MachineId、探测种类、端点或服务/VM 选择参数，以及按版本化规范编码计算的 DescriptorDigest；不含秘密。

本轮理由：F04：稳定 ResourceId 不足以标识实际被探测的许可端点版本。

依赖：无

场景：AR05、AR06、AR07、AR08

禁止承担：执行时按 ResourceId 隐式替换为当前配置；只用显示名称计算身份；写入密码

## ProbeObservation

种类：record；生命周期：单次探测事实，可进入对应身份的轮次。；决定：refine

拟议位置：待分配；模块：待分配

不可变单次观测，携带完整资源身份键、实际探测来源、开始和完成时间及分项事实；网络探测另保留实际解析地址证据。

本轮理由：F04：一次观测必须保留其真正使用的资源身份。

依赖：无

场景：AR05、AR06、AR07、AR08

禁止承担：完成时改贴当前配置的身份；把 TCP 成功标记为许可签出

## ProbeRound

种类：record；生命周期：单个资源身份键的一轮探测。；决定：refine

拟议位置：待分配；模块：待分配

冻结同一完整资源身份键、策略版本、轮次序号和同身份观测集合。

本轮理由：F04：同一资源不同修订不能合并成一个探测轮次。

依赖：无

场景：AR06、AR07、AR08

禁止承担：混合不同端点或修订的观测；迟到轮次覆盖较新轮次

## ProbeSnapshot

种类：record；生命周期：不可变监测投影；过期后不能作为启动前新鲜证据。；决定：refine

拟议位置：待分配；模块：contracts

在原状态、证据及时间字段之外明确携带 ResourceRevision、MachineId、DescriptorDigest、PolicyVersion、RoundSequence；所有分项证据属于同一资源身份键。

本轮理由：F04：新鲜度和健康状态必须绑定冻结资源身份。

依赖：无

场景：AR05、AR06、AR07、AR08

禁止承担：把当前版本 Ready 用于旧版本启动；跨身份继承失败计数或最后就绪时间；省略证据来源及有效期

## OperationRequestSnapshot

种类：record；生命周期：受理前冻结，受理后持久不可变；由操作及恢复引用保留。；决定：refine

拟议位置：待分配；模块：contracts

保留有限种类、机器、工作区修订、冻结配置和计划；新增明确的规范化 RequestDigest。Launch 快照包含完整冻结 ResourceDescriptor，PlanDigest 覆盖该描述身份及许可环境输入。

本轮理由：F01、F04：受理摘要及冻结许可证据要求必须可稳定核对。

依赖：无

场景：AR05、AR08、AR09、AR16、AR18、AR22

禁止承担：重试时从当前配置重新解释原请求；存入明文秘密或执行准入凭据；向 UI 或分享导出内部请求

## LaunchPlan

种类：record；生命周期：每次启动独立不可变计划。；决定：refine

拟议位置：待分配；模块：contracts

保留入口、初始化文件、参数令牌、环境和秘密槽；明确携带冻结 ResourceDescriptor 及其摘要，与 WorkspaceRevision、MachineId 一并进入计划摘要。

本轮理由：F04：启动环境与许可证据必须指向同一个冻结描述。

依赖：无

场景：AR02、AR05、AR08、AR27

禁止承担：使用当前同 ID 描述替换冻结描述；包含明文秘密；将观测成功本身当作登录成功

## CommitId

种类：record；生命周期：跨页面重建、后续提交及进程重启有效。；决定：add

拟议位置：待分配；模块：待分配

一次逻辑配置提交的稳定随机标识，键空间绑定工作区身份；在发起提交前分配并保留。

本轮理由：F05：直接配置命令需要独立于 OperationId 的稳定核验标识。

依赖：无

场景：AR03、AR04、AR19、AR20

禁止承担：以当前修订或实体名称替代；未知结果后生成新 ID 自动重试；复用于编辑后的另一请求

## CommitIntent

种类：record；生命周期：提交前冻结；非秘密意图和摘要随核验记录持久保留。；决定：add

拟议位置：待分配；模块：待分配

不可变提交意图，包含 CommitId、有限命令种类、ExpectedRevision、规范化非秘密输入、生成 ID 槽位、SecretUpdateKind 和本次 Replace 输入随机标识；由用例重新计算版本化 RequestDigest。

本轮理由：F05：稳定标识还须绑定可核对的请求内容与生成 ID 需求。

依赖：无

场景：AR03、AR04、AR19

禁止承担：持久化密码或可用于猜测密码的摘要；信任调用者提供的摘要而不重算；把 CredentialRef 放入公开意图；相同输入标识代表不同秘密输入

## WorkspaceCommitSession

种类：class；生命周期：首次会话从准备落盘覆盖凭据创建和配置切换；重复会话只读且无写锁所有权。；决定：add

拟议位置：待分配；模块：待分配

封装一次配置准备结果：CommitId、摘要、稳定 ID 映射和已有回执；仅首次成功准备返回绑定原进程的写入能力，重复结果只读。

本轮理由：F05：配置去重必须发生在分配实体和创建凭据之前。

依赖：无

场景：AR03、AR04、AR19、AR20

禁止承担：序列化或向 UI 暴露写入能力；重复会话创建凭据或再次提交；跨进程转移会话；按超时接管活会话

### `ValueTask DisposeAsync()`

关闭该会话写入能力并释放工作区提交锁。仅在可靠证明没有有效版本切换时可记录 NotCommitted；不能可靠落盘则保留 Pending 或 Indeterminate，不删除候选凭据。

失败约定：幂等；释放失败返回或记录脱敏诊断，不把未知提交改报为未提交。

## CommitReceipt

种类：record；生命周期：首版在工作区生命周期内保留确定结果和去重墓碑；未决记录不得清理。；决定：add

拟议位置：待分配；模块：待分配

不可变公开核验结果，包含 CommitId、RequestDigest、State、CommittedRevision、生成实体 ID 映射、原提交的脱敏结果投影和安全原因；State 为 NotFound、Pending、Committed、NotCommitted 或 Indeterminate。

本轮理由：F05：核验必须返回历史提交结果，而非猜测当前快照。

依赖：无

场景：AR03、AR04、AR19、AR20、AR22

禁止承担：把 Pending、Indeterminate 或存储不可读解释为未保存；包含密码、CredentialRef 或内部恢复路径；用后续当前快照替代原提交结果

## 本轮移除的类型

- **ImportConfigurationHandler**：普通导入确认没有可恢复外部执行副作用，进入协调器增加重复状态和错误边界；由 ConfigurationShareService.CommitAsync 替代。旧格式转换另由 WorkspaceMigrationService 承担。

## 修订后的跨类执行规则

- 本次是相对初稿的增量契约修订。typeChanges 中的方法列表替换对应公开方法列表；记录类型 methods 为空表示只有不可变数据约束，不生成构造器、序列化器或实现代码。未列类型保持初稿，其调用须遵循这里的跨类协议。
- F01 执行顺序固定为：规范化请求并查重 → 持久受理 → 仅 Created 原调用尝试唯一准入 → Executing 及持有者可靠落盘 → 创建一个上下文和处理器 → 模块写前意图落盘 → 副作用 → 写后事实落盘。任一步未知都不能通过重新派发补偿。
- OperationId 的相同请求摘要依据调用方冻结的请求内容计算，首次受理再绑定其解析出的执行快照；重复提交不得用当前配置重建不同快照。错误证据保留 OperationId，首次受理凭据和执行租约不可进入 UI、日志载荷、分享数据或重启恢复请求。
- 受理后未取得执行权即崩溃可以明确显示没有获得执行准入，但仍不自动领取执行。用户若确需继续，须先核验原记录，再明确提交新的关联操作；原 OperationId 永不重新获得执行权。执行者身份未知、失效或日志损坏都不构成自动接管授权。
- 取消控制事实与执行事实在同一操作日志序列中有序提交。上下文遇序号变化只能重读并重新验证尚未发生的步骤；不能重做已发生副作用。父进程与 worker 的会话绑定执行代次，worker 只获得有限步骤委托，不获得第二份操作执行权。
- F02 的受保护集合至少包含选定工作区整体、配置有效标记及历史、操作请求和日志、提交意图及核验历史、回执、备份、暂存、计划、迁移原始副本与候选、worker 控制资料以及保护登记表自身。首版所有这些资料只能位于启动阶段已登记根内；新增外部资料根必须先通过同一登记协议。
- 部署影响范围必须由实际文件和目录动作计算：目录删除、移动、替换或权限变更按整棵可能受影响子树处理；不得只检查最终文件列表。任何影响范围与保护范围相交，或通过祖先操作可能影响保护范围，均返回 ProtectedDataOverlap；用户确认和 UAC 均不能覆盖该错误。
- 保护登记与活动变更范围准入使用跨进程原子检查。目标租约取得顺序固定为控制保护准入、目标互斥；失败逆序释放。新控制根登记与活动部署重叠时拒绝登记。实际写入者使用可支持的身份句柄固定及写前复核；无法证明链接、别名或文件系统能力安全时拒绝破坏性执行。
- 预览检查不是写入许可。DeliveryOperationHandler 继续由实际写入者取得新版组合 TargetLease，FileDeliveryPort 独立重算影响范围并验证。恢复和回滚同样适用保护限制；父进程不能持目标租约等待 worker。
- F03 的焦点事件只决定明文是否可见，不决定保存意图。保存命令在 Dispatcher 上先确定 Keep、Clear 或 Replace；Replace 先移交秘密，再清空输入。显隐交互可短暂显示，交互结束或其他失焦立即隐藏。显式取消编辑、离页及保存尝试结束才销毁未移交输入。
- SecretUpdate 的 Replace 必须携带当前输入代次的随机非秘密标识；修改输入即生成新标识。标识不是密码摘要，不得跨不同输入复用。相同 CommitId 的重复请求走历史核验，不读取或比较新密码；用户修改草稿或重新输入秘密形成新逻辑请求，须在原结果得到处理后明确使用新 CommitId。
- F04 的完整资源身份键由 ResourceId、ResourceRevision、MachineId 和版本化 DescriptorDigest 构成；缓存及轮次另包含 PolicyVersion。摘要覆盖实际探测参数和许可端点选择。实际 DNS 解析地址作为观测证据保留，不把不同配置端点仅因解析到同一地址视为同一身份。
- 许可证据不因同 ID 当前配置变化而改贴标签。当前监测投影只展示其绑定版本，旧版本迟到事实最多更新旧版本历史。启动处理器在真正采用证据时再次检查身份及有效期；若已过期，仅可复查同一冻结描述或停止本次启动检查。
- F05 直接配置命令仍不进入 OperationCoordinator。首次准备可靠成功前不得创建凭据或提交实体；重复会话无写入能力。所有配置变更入口，包括复制、删除、定义保存、发现合并及导入，均采用相同 CommitId 去重和核验协议。
- 直接提交状态规则为：未登记且登记完整可读时 NotFound；首次准备后为 Pending；有效版本切换有可靠历史证明时 Committed；持有者结束且能证明未切换时 NotCommitted；任一关键证据不足时 Indeterminate。Committed 和 NotCommitted 为该 CommitId 的不可逆结果；失败后用户重新提交使用新 CommitId，不复活旧会话。
- 直接提交摘要包含有限命令种类、预期修订、规范化非秘密输入、生成 ID 槽位或已确认导入映射及秘密更新意图，排除明文秘密和密码摘要。存储会话产生的 ID 映射在准备阶段持久固定；凭据引用仅可进入受保护内部配置，不能进入公开 CommitReceipt。
- 配置候选存在不证明已提交，独立回执写入成功也不能代替有效版本切换证明。下一次配置提交或迁移切换必须在共享工作区锁下先核验此前未决切换，并保留提交索引和可追溯历史；无法核验时拒绝继续切换。迁移须保留此控制历史，不能通过格式转换或清理丢失 CommitId 去重能力。
- 首版提交核验历史及去重墓碑在工作区生命周期内不自动裁剪；未决会话、原代、候选与恢复引用资料不得清理。若后续引入保留期限，必须先另行定义仍能拒绝旧 ID 重放的持久墓碑协议。工作区历史与未知候选凭据均采取保守保留。
- 提交开始或有效版本切换后，页面取消只结束等待；不能映射为确定未保存。CommitPending、CommitIndeterminate 及相关存储错误携带调用方已知的 CommitId。只有明确的 NotCommitted 才能支持未保存结论；凭据清理仍须独立满足无引用证明。
- 全部 acceptanceCheck 为未来验证要求。工具读取和文字审阅仅支持接口与协议结论，不证明 Windows 文件系统、WPF 焦点事件、跨进程准入、凭据管理器或进程创建行为已经实现。

## 初稿调用流程（比较材料）

以下保留初稿上下文；与修订后的方法或跨类规则有冲突时，以修订为准，不能把旧步骤视为最终规范。

### 配置与凭据提交

1. ProfileEditorViewModel 明确传入 Keep、Replace 或 Clear，秘密不属于 ProfileDraft。
2. WorkspaceCatalogService 校验引用与 expected 修订；Replace 先调用 WindowsSecretVault 创建新引用，禁止覆盖旧引用。
3. JsonWorkspaceStore 在跨进程提交锁内复查修订，提交引用新凭据的配置并保存提交标识。
4. 确认配置未提交才可清理本次新引用；确认已提交才考虑清理旧引用。需要把当前配置、未完成操作快照及活动秘密租约都纳入引用证明。
5. 首版无法可靠证明无引用的旧凭据或崩溃遗留候选一律保留，不自动扫除；不得为了清理扩大凭据读取范围。
6. 保存调用 finally 释放本次秘密，UI 只接收脱敏快照。

失败路径：凭据写失败不提交配置；配置提交未知保留新旧引用并核验，不直接回滚凭据。旧凭据清理失败作为独立维护诊断，不把已成功配置提交报告为失败。

### 启动与进程交接

1. 页面以配置 ID、修订和稳定请求标识提交，协调器持久化冻结请求后创建 LaunchOperationHandler。
2. 处理器验证 MachineId、入口和初始化文件；获取新鲜的分项资源证据，捕获独立父环境。
3. ProcessEnvironmentPolicy 移除继承许可专用变量，再注入本次许可；LaunchPlanBuilder 仅保留 SecretSlot。
4. 启动意图可靠落盘后解析 SecretLease，执行进程创建，立即释放秘密；创建事实必须另行提交。
5. WindowsProcessGateway 跟踪入口与目标关联，记录机器、PID、创建时间、映像和交接证据；mon.exe 退出只结束入口事实。
6. 窗口出现、登录确认、观察超时及目标退出分别进入 Launch 状态；页面切换通过 OperationProjectionService 继续观察。
7. 重启只核验持久身份并只读附着；身份无法确认则保持未确认，不自动重启目标。

失败路径：创建成功而日志失败时停止后续自动动作并保留最后启动意图，恢复时核验外部事实；不得再次启动。缺少窗口或登录证据只表示未确认，停止观察及释放句柄均不 Kill。

### 许可复查与证据过期

1. WindowsResourceProbe 返回带资源、来源和时间的一次 VM、服务或 TCP 事实。
2. ResourceSupervisor 按资源合并复查轮次，以版本化参数控制次数、间隔和单次超时；不同资源独立执行。
3. ConnectionHealthPolicy 只统计完成轮次，瞬时超时进入复查，按失败阈值通知并按恢复阈值恢复。
4. RequireFreshAsync 校验资源、配置修订和有效期；过期证据必须复查，不能继续当 Ready。
5. LicensePageViewModel 独立显示 VM 存活、服务故障、端口状态及证据时间；取消页面订阅不停止监测。

失败路径：探针异常使对应证据未知或不可用，不报告虚拟机丢失，不阻止工作台启动；许可签出必须有独立证据。所有时间阈值仍须在行为验收前批准。

### 交付预览、执行、恢复与回滚

1. 用户保留或显式选择来源版本；PackageReader 检查归档、目录或文件映射，PrivateStagingStore 冻结实际字节。
2. DeploymentPlanner 绑定来源、暂存、目标身份及基线、映射、动作、备份策略和工作区修订，确定性计算 PlanDigest 并登记不可变计划。
3. 用户确认 PlanId 和 PlanDigest；编辑任何绑定字段立即使旧确认失效。协调器持久保存相同冻结计划。
4. 执行前重验来源和暂存；实际写入者取得跨进程目标范围租约，再复核目标基线。提权时由 worker 持锁，父进程不持锁等待。
5. 每项先备份并可靠刷新和验证，后持久记录写入意图，再写目标、核验内容、记录完成事实；公共操作事实在可靠提交后才发布。
6. 重启只读取完整日志前缀、回执、备份与实际目标，展示待核验，不自动继续或回滚。
7. 用户选择恢复或回滚后生成关联 OriginalOperationId 的新预览；重新检查当前目标、备份摘要和冲突，重新确认后按同一协议执行。

失败路径：来源变化、摘要不符、锁冲突或目标变化均拒绝旧计划。备份或写前日志失败则本项不写；写后日志失败则停止下一项，并按实际内容核验。回滚遇外部修改拒绝覆盖，保留冲突及备份。

### 变量保存、改名与持久应用

1. SaveDefinitionAsync 只保存变量配置，不访问用户或系统环境。
2. VariablePlanner 冻结旧名和新名的存在性、值、作用域、机器及摘要；Windows 大小写等价名称按同一地址处理。
3. 用户显式确认 ApplyVariables；系统作用域必须显式选择，按需请求 UAC。
4. 实际写入者取得应用跨进程名称锁，重读两端基线；写新名之前持久记录意图，写后核验。
5. 删除旧名之前再次读取旧名，记录删除意图，再删除并核验；每一已完成步骤分别记录。
6. 部分完成或中断后只读核验实际两端值，用户重新确认后才能继续或补偿。

失败路径：检测到外部变化立即停止；新名已写而旧名未删显示 PartialApplied。检查与写入之间仍存在外部竞争窗口，不能承诺绝对不覆盖；若验收要求这种绝对保证，需共享外部协调机制或禁用自动改名。

### 配置迁移与有效版本切换

1. WorkspaceLocationResolver 根据显式目录或 EXE 基准解析唯一位置，普通读取遇旧格式返回 MigrationRequired。
2. WorkspaceMigrationService 预览源摘要、源和目标格式、修订及关联清单，用户确认后提交 MigrateConfiguration。
3. 协调器持久记录冻结请求及迁移意图，JsonMigrationStore 写旁路副本，保留原始文件。
4. 重读并验证 ID、关联、历史和凭据引用；不解析或导出秘密，不假定凭据在目标环境可用。
5. 切换意图可靠落盘后，在与普通配置提交共享的锁内复核源修订和候选摘要，原子切换有效版本标记。
6. 若公共日志未记录完成，重启通过有效标记中的 OperationId 和内容摘要核验切换结果；仅展示恢复选择。

失败路径：转换、验证或写入失败保留原有效版本；切换结果未知不重复切换。原始数据、候选副本和迁移记录在恢复引用解除前不得清理。

### 密码临时显示与 UI 线程释放

1. PasswordEntryBehavior 只管理本次输入，不依赖凭据解析端口，不公开可绑定 Password 字符串。
2. RevealWhileEditing 在 Dispatcher 上显示本次输入；离焦、窗口停用、保存和离页时隐藏并清空。
3. DetachInput 在 UI 线程移交 TransientSecret 所有权并立即清空控件，ProfileEditorViewModel 将其交给保存调用。
4. 保存成功、失败或取消均在 finally 释放临时秘密；运行时 SecretLease 仅存在于 Launch 执行边界。
5. 页面释放先在 Dispatcher 上标记失效、解绑事件和清空控件，再异步取消并等待订阅结束；禁止 Wait、Result 或同步 Dispatcher 回调形成死锁。

失败路径：错误与属性通知均不携带秘密。页面关闭不取消已受理业务操作；迟到回调检查页面代次及释放状态。尽力清理不等于证明托管内存或目标命令行无残留。

### UI 查询与普通 EXE/Aspire 可替换入口

1. 普通 Desktop 直接由 DesktopCompositionRoot 装配同一组用例、适配器及本地协调器，离线启动不要求 Aspire。
2. 开发入口使用真实 Aspire AppHost 启动同一 Desktop 产物，仅注入隔离目录与可选诊断配置。
3. UI 经命令提交操作，经 ITaskQueries 读取快照及单调序号增量；断线、缺口或页面重建后重新读取。
4. 订阅取消只结束页面等待；后台操作由应用持有。主要操作区保持在内容滚动区域外。
5. 诊断端点不可达时降级本地诊断；未来执行端不可达时报告对应操作不可用，不把远端请求静默改成本机执行。
6. 开发会话停止只清理自有资源和工作台；许可服务、AVEVA、VMware 与已启动目标会话不属于终止范围。

失败路径：两种入口须运行同一行为契约，并另测诊断失联、AppHost 结束和进程所有权。工具依赖检查只能证明装配约束，不能证明这些行为已经成立。
