# 问题、修订和复核

以下来自本次实际模型节点的结构化结果，由导出脚本排版。复核是架构文字审阅，验收用例仍待实现后执行。

## F01 · 受理去重尚未形成跨进程的唯一执行准入协议

级别：major；需求：R07、R09、R10、R12

涉及：OperationCoordinator、JsonRunJournal、IRunJournal、OperationExecutionContext、LaunchOperationHandler

发现证据：OperationCoordinator.SubmitAsync(OperationRequest request, CancellationToken ct) 要求同 ID 同摘要返回原操作；JsonRunJournal.AcceptAsync(OperationRequestSnapshot request, CancellationToken ct) 对新受理和重复受理均返回 RunSnapshot，未定义可区分的受理结果或唯一派发权。OperationExecutionContext 仅声明每操作一个写入者，JsonRunJournal.ReadAsync 却引用未定义取得、持有和失效规则的执行租约。两个 EXE 同时获得同一 Accepted 快照时，契约没有规定哪一步原子决定唯一执行者。AppendAsync 的序号比较和部署目标锁不能自行等同于操作执行权，尤其 Launch 没有目标写入锁。

建议：明确跨进程执行准入协议，例如原子取得 Accepted 到 Executing 的唯一转换权，并绑定执行者身份；只有取得权利的一方可创建执行作用域。重复提交只能查询，不能再次派发。执行者退出后进入待核验，不能仅因租约失效自动重放；明确受理完成但尚未取得执行权时的崩溃处理。

验收条件：未来以两个 Desktop 进程并发提交相同 OperationId 和摘要，在受理返回、执行准入、启动意图落盘及进程创建后分别设置故障点；验证最多发生一次进程创建，重复请求返回同一操作，重启不会自动再次执行。同 ID 不同摘要必须拒绝。

修订：resolved；新增 Created/Existing 受理区分和不可重颁的一次性准入凭据；通过跨进程 Accepted 到 Executing 的持久转换授予唯一执行租约。只有取得租约的原调用可创建处理器；重复提交只查询。首次返回丢失、准入未知或执行者退出均不能触发自动重放。

- inputs/class-contracts.json 的 IRunJournal.AcceptAsync 原返回 RunSnapshot，确实未区分首次与重复受理；本次改为 OperationAcceptance。
- IRunJournal.TryBeginExecutionAsync 和 JsonRunJournal 对锁、持有者身份、凭据消费、可靠落盘及未知结果分别给出顺序和错误。
- OperationCoordinator.SubmitAsync 明确先受理、再唯一准入、再创建上下文和处理器；Existing 路径不派发。
- AppendAsync 必须携带 OperationExecutionLease；IOperationContext 及 OperationExecutionContext 同步记录改动，取消有独立持久入口。
- inputs/PRODUCT-BRIEF.md 的 R07、R09、R10、R12 要求结果可判断且中断后不自动重放；以上为契约对应关系，非运行证据。

修订验收：未来在两个 Desktop 并发提交同 OperationId、同摘要时，分别于受理返回前后、准入持久化前后、启动意图落盘后和进程创建后中断。原 OperationId 最多创建一次进程；重复请求始终返回同一操作且不派发，重启只核验。受理成功但未准入即崩溃也不得自动执行。同 ID 不同摘要拒绝；取消与准入竞争按持久顺序判定。

复核：证据可接受；revised-contracts.json 的 OperationAcceptance 区分 Created/Existing；IRunJournal.TryBeginExecutionAsync(OperationAcceptance acceptance, CancellationToken ct) 要求在跨进程锁内消费一次性凭据并可靠记录 Executing，随后才返回 OperationExecutionLease。OperationCoordinator 取得租约后才创建处理器，AppendAsync 验证租约，取消使用独立持久入口；additionalRules 明确受理后崩溃、准入未知及执行者退出均不重新派发。补齐了原 contracts.json 仅凭受理快照无法决定唯一执行者的缺口。

## F02 · 部署目标未明确排除工作区和恢复资料所在范围

级别：blocking；需求：R09、R12

涉及：DeploymentPlanner、FileDeliveryPort、PrivateStagingStore、CanonicalTargetLeaseProvider、PrivilegedWorkerHost

发现证据：基线 DeploymentPlanner.PreviewAsync(DeployRequest request, CancellationToken ct) 提到检查“目标与记录根”，但提案没有给出重叠时的拒绝规则。提案 FileDeliveryPort.ApplyAsync(OperationId operationId, ConfirmedPlan plan, TargetLease lease, CancellationToken ct) 校验路径身份、租约及目标基线，却没有禁止写入当前配置、操作日志、回执、备份、迁移副本或私有暂存。即使归档路径没有穿越，用户仍可把这些目录或其祖先选为合法目标。部署锁也未与配置和日志写入者建立互斥关系，因此部署可能覆盖自己的恢复证据。

建议：定义受保护的控制数据范围，禁止部署变更覆盖这些范围或通过祖先目录操作影响它们。使用规范路径、文件身份及目录重叠规则判断；预览和实际写入者均须校验，worker 独立执行同一限制。备份与回执存储位置也必须避开本次变更范围。

验收条件：未来分别将目标指向配置目录、当前操作回执、备份目录、暂存目录及其祖先，并加入可支持的路径别名；普通与提权执行均须在目标写入前拒绝。验证原配置、日志、暂存及备份内容保持完整，仍能用于中断核验。

修订：resolved；新增共享控制根保护协议；预览、计划登记、目标租约及实际写入端都检查完整变更范围。普通进程与 worker 使用同一保护登记，回执、备份和暂存必须位于保护根且在本次变更范围外。控制根与活动部署范围的登记相互排斥，不能通过后续根迁入绕过检查。

- inputs/class-contracts.json 的 IDeploymentPlanningService.PreviewAsync 提及检查目标与记录根，但没有重叠拒绝语义；本次加入 ProtectedDataOverlap。
- IControlDataGuard 和 WindowsControlDataGuard 明确配置、日志、回执、备份、暂存、迁移及提交核验资料的保护集合，并要求规范路径、文件身份和祖先关系检查。
- CanonicalTargetLeaseProvider 先取得 ControlDataLease 再取得目标互斥；IFileDeliveryPort 与 FileDeliveryPort 在首项目标写入前独立重验。
- PrivilegedWorkerHost 从可信本机登记读取保护范围，不能信任请求缩小集合；PrivateStagingStore 只能使用已登记根。
- DesktopCompositionRoot 明确先登记控制根再开放写入。所有新增依赖均在本次 typeChanges 中声明，或已存在于输入及初稿。

修订验收：未来将目标分别设为配置根、操作日志、回执、备份、暂存、迁移目录、提交历史及其祖先，并加入受支持的大小写、短路径、硬链接或目录别名。普通与提权执行必须在首项目标写入前拒绝，原资料摘要保持不变。另测预览后新增重叠控制根、目录操作隐式影响子树、恢复资料位于变更范围内及保护登记不可读，均不得绕过保护。

复核：证据可接受；revised-contracts.json 的 IControlDataGuard、WindowsControlDataGuard 和 additionalRules 明确保护工作区整体、日志、回执、备份、暂存、迁移及提交历史，并按真实身份和祖先关系拒绝完整变更范围重叠。CanonicalTargetLeaseProvider 组合保护准入与目标互斥；FileDeliveryPort 在首项目标写入前重验，PrivilegedWorkerHost 独立读取可信登记，DesktopCompositionRoot 要求先登记再开放写入。新增控制根与活动部署范围也实行原子互斥，覆盖原发现的合法目标覆盖恢复资料问题。

## F03 · 密码框失焦即清空会破坏正常保存和显隐操作

级别：major；需求：R04、R13

涉及：PasswordEntryBehavior、ProfileEditorViewModel

发现证据：PasswordEntryBehavior.HideAndClear() 明确在离焦时清空本次输入，而 DetachInput() 才负责向 ProfileEditorViewModel.SaveAsync(SecretUpdate secretChange, CancellationToken ct) 移交秘密。WPF 中点击可聚焦的保存按钮或按 Tab 移动焦点，可以先触发密码框失焦，再执行保存命令；此时待移交输入已被清空。显隐按钮若取得焦点，也存在相同问题。契约没有规定先移交再清理的事件顺序。基线只要求离焦隐藏，提案将其扩大为销毁输入。

建议：区分隐藏明文显示与销毁输入：编辑页内部焦点移动只隐藏并清理显示缓冲；保存时先明确取得 SecretUpdate 和秘密所有权，再清空控件。离页、取消及保存尝试结束时销毁剩余输入，避免把清空后的状态误解释为 Keep 或 Clear。

验收条件：未来分别通过鼠标点击保存、Tab 后按 Enter、快捷键保存及点击显隐按钮操作本次输入；验证 Replace 移交的秘密与输入一致且仅移交一次。失焦后不可见明文，保存失败、取消和离页后临时输入按契约清理。

修订：resolved；新增 HideDisplay，只隐藏并清理显示缓冲；页内焦点移动不再销毁密码输入。Replace 保存先 DetachInput 转移唯一所有权，再清空控件；保存结束、显式取消及离页销毁剩余输入，Keep/Clear/Replace 由明确意图决定。

- inputs/class-contracts.json 的 PasswordEntryBehavior.RevealWhileEditing 原要求离焦隐藏，没有要求离焦销毁；本次撤销初稿扩大后的破坏性清理语义。
- PasswordEntryBehavior 完整方法列表分别定义 RevealWhileEditing、HideDisplay、DetachInput、HideAndClear 和 Dispose。
- ProfileEditorViewModel.SaveAsync 明确命令适配顺序、输入代次、重入限制及 finally 释放责任。
- 空输入或已消费输入不能被自动解释为 Keep 或 Clear，缺少有效 Replace 输入显示 MissingSecretInput。

修订验收：未来分别用鼠标点击保存、Tab 后 Enter、快捷键和显隐按钮操作同一输入；普通移焦后明文不可见，但 Replace 仍移交原输入且仅一次。验证双击保存、空 Replace、保存失败、取消、离页和保存中销毁页面时的所有权及清理；不得回填保存密码。

复核：证据可接受；原 contracts.json 的 HideAndClear 在离焦时销毁输入；修订后的 PasswordEntryBehavior.HideDisplay() 只隐藏并清理显示缓冲，保留未移交输入。DetachInput() 先移交唯一所有权再清空；ProfileEditorViewModel.SaveAsync 明确 Dispatcher 命令顺序、输入代次、防重入和 finally 释放。普通移焦不再决定 Keep/Clear/Replace，契约已排除鼠标保存及显隐按钮先清空输入的反例。

## F04 · 启动使用的许可证据未明确绑定冻结的资源版本

级别：major；需求：R05、R06、R07

涉及：IResourceMonitor、ResourceSupervisor、LaunchOperationHandler、ProbeSnapshot

发现证据：ResourceSupervisor.RequireFreshAsync(Guid resourceId, CancellationToken ct) 声称检查配置修订，但调用参数只有稳定资源 ID，没有启动请求期望的资源修订或冻结 ResourceDescriptor。LaunchOperationHandler.ExecuteAsync(OperationRequestSnapshot request, IOperationContext context, CancellationToken ct) 使用冻结启动配置，却未明确比较返回证据与该配置的许可端点。反例是受理启动后，同一 ResourceId 的端点由 A 改为 B：监测返回 B 的新鲜成功事实，而冻结启动环境仍指向 A。仅检查证据未过期以及匹配当前配置，不能证明它适用于这次启动。

建议：让新鲜度请求携带冻结资源描述或明确的资源修订、端点摘要和机器身份；探测轮次、缓存及返回证据绑定同一身份。LaunchOperationHandler 在采用证据前核对其与冻结启动计划一致，版本不匹配时明确拒绝或针对冻结端点重新探测。

验收条件：未来在启动受理与探测之间修改同一资源 ID 的端点，并令 A 不可达、B 可达；验证 B 的成功事实不能满足指向 A 的启动检查。并发探测不同修订以及旧轮次迟到完成时，也不得覆盖或串用证据。

修订：resolved；RequireFreshAsync 接收冻结 ResourceDescriptor；探测、轮次、缓存和快照绑定 ResourceId、ResourceRevision、MachineId、DescriptorDigest 及策略版本。启动环境与描述来自同一冻结配置，处理器采用证据前再次核对身份和时效。

- inputs/class-contracts.json 的 IResourceMonitor.RequireFreshAsync 原仅接收 Guid，无法表达启动期望版本；本次接口及 ResourceSupervisor 均替换参数。
- 基线 IResourceProbe.ObserveAsync 已接收 ResourceDescriptor，本次收紧为不得按 ResourceId 重读当前配置，并回传原身份。
- ResourceDescriptor、ProbeObservation、ProbeRound、ProbeSnapshot、LaunchPlan 和 OperationRequestSnapshot 均列入类型变更，明确身份传播。
- ConnectionHealthPolicy 拒绝跨身份归并；ResourceSupervisor 的缓存及共享轮次按完整身份键隔离。
- LaunchPlanBuilder 和 LaunchOperationHandler 明确环境、冻结端点及新鲜证据的一致性检查，不再接受仅同 ID 的当前成功事实。

修订验收：未来在启动受理后将同 ResourceId 的端点由 A 改为 B，并使 A 不可达、B 可达。B 的成功证据不能放行指向 A 的启动；只能探测冻结 A 或明确失败。并发探测不同修订、同修订旧轮次迟到、策略变更及证据过期时均不得串用或覆盖较新证据。

复核：证据可接受；RequireFreshAsync(ResourceDescriptor expected, CancellationToken ct) 已替换仅接收 Guid 的契约。ResourceDescriptor、ProbeObservation、ProbeRound 和 ProbeSnapshot 传播资源修订、机器及描述摘要，缓存和轮次另绑定 PolicyVersion；探针不得读取当前配置替换参数。LaunchPlanBuilder 从同一冻结绑定生成环境与描述，LaunchOperationHandler 使用前核对身份和时效。因此同 ResourceId 的 B 端点成功事实不能满足冻结 A 端点的启动检查。

## F05 · 直接配置命令的提交结果未知缺少可调用的核验契约

级别：major；需求：R03、R04、R11、R12

涉及：JsonWorkspaceStore、IWorkspaceStore、WorkspaceCatalogService、ICatalogService、ConfigurationShareService、ProfileEditorViewModel

发现证据：JsonWorkspaceStore.CommitAsync(WorkspaceChange change, Revision expected, CancellationToken ct) 只说明提交带唯一标识；WorkspaceCatalogService.SaveProfileAsync(...) 和 ConfigurationShareService.CommitAsync(...) 可返回 CommitIndeterminate，ProfileEditorViewModel.SaveAsync(...) 又要求保留提交标识供核验。但公开契约没有规定该标识如何传入或返回，也没有按标识查询提交结果的方法及记录保留规则。若未知提交后另一进程再次成功保存，ReadAsync 返回的当前快照不能可靠回答前一次提交是否发生。涉及新建、复制及凭据替换时，更不能仅凭当前字段相似推断结果。

建议：为直接配置提交定义稳定 CommitId、请求摘要及结果核验入口，记录已提交修订和生成的实体 ID；未知错误须返回可核验标识。核验依据应跨后续提交和进程重启保留，同 ID 重试不得产生第二次配置变更。此机制可留在直接用例路径，无需把 CRUD 纳入操作协调器。

验收条件：未来在配置替换成功但返回结果前注入故障，再由另一进程保存后续修订并重启；按原 CommitId 必须仍能确定原提交结果及生成的 ID。同 ID 重试不得重复新增、复制或创建凭据关联；结果尚未确定时不得清理候选凭据或显示未保存。

修订：resolved；直接配置命令接收稳定 CommitId，先持久准备并取得唯一写会话，再分配结果、创建凭据及切换配置。相同 ID 同摘要只核验原结果，不能再次变更。持久历史保留原提交修订、生成 ID 和脱敏投影，并通过明确查询入口跨后续修订及重启核验。

- inputs/class-contracts.json 的 IWorkspaceStore 只有 ReadAsync 和 CommitAsync，确实没有稳定标识核验入口；本次新增 BeginCommitAsync、ReadCommitAsync 并替换提交签名。
- CommitId、CommitIntent、WorkspaceCommitSession 和 CommitReceipt 明确标识、摘要、唯一写能力和五种核验状态。
- JsonWorkspaceStore 将配置候选、提交身份和结果绑定到不可变代，并要求后续切换保留此前提交历史，避免仅凭当前快照或候选文件存在推断。
- ICatalogService、WorkspaceCatalogService、IConfigurationShareService 和 ConfigurationShareService 的完整公开方法列表均同步 CommitId 及 InspectCommitAsync。
- ProfileEditorViewModel 和 ProfilesPageViewModel 保留标识、查询未决结果并阻止自动新 ID 重试；秘密创建只能发生在首次有效写会话中。

修订验收：未来在配置切换成功但返回前中断，再由另一进程提交后续修订并重启；原 CommitId 必须仍返回原 CommittedRevision、生成 ID 和原结果。并发重复新增、复制、导入及凭据替换不得产生第二次配置变更或第二次凭据创建。准备返回丢失、凭据写后崩溃、切换未知、历史损坏分别保持可辨状态；未确定前不能清理候选凭据或显示未保存。同 ID 不同意图必须拒绝。

复核：证据可接受；revised-contracts.json 新增 CommitId、CommitIntent、WorkspaceCommitSession 和 CommitReceipt；IWorkspaceStore 提供 BeginCommitAsync、CommitAsync 和 ReadCommitAsync，目录及分享服务提供 InspectCommitAsync。首次准备先于凭据创建，重复会话无写入能力；JsonWorkspaceStore 将配置、提交身份及结果绑定至不可变代和有效标记，后续提交及迁移必须保留历史索引，未决结果不得清理。原提交可按 CommitId 跨后续修订核验，不再依赖当前配置相似性推断。
