# 启动工作流纵向切片

状态：首期后端及 HTML 适配已实现，隔离测试通过；实际 E3D 2.1 APS 工程窗口已打开并确认入口交接，严格 Host 模块校验未通过（Unknown）。CAF 未验收。实际证据见 [交接](launch-workflow-handoff.md)，不标记整体 change 或 Database 平台完成。

## 来源与唯一实现

| 来源语义 | 本切片映射 | 唯一实现 |
|---|---|---|
| Archon/rewrite 的 OperationId、PlanDigest、受理日志与租约思想 | operationId / planDigest / accepted / 独占文件租约 | `LaunchWorkflowService`；不复制 rewrite 的完整平台 |
| Next 的安装、项目、模块发现及尝试语义 | environment.discover / resolve → 已保存 LaunchProfile；attemptId 表示一次实际执行 | `LaunchDiscovery` 与现有 `LaunchProfileService`；静态证据不升级为登录事实 |
| Yuzuha 已有配置修订及 Monitor / Marine 启动 | expectedRevision、既有校验与进程参数 | `LaunchProfileService` / `LaunchService.Build`；协调层不重写启动算法 |

来源身份和原仓核对继续以 [source-inventory.md](../source-inventory.md) 为准。框架契约见 [contracts.md](../contracts.md)，产品设计依据见 [design.md](../design.md)。新增类的中文 XML summary 进入现有 DocFX API 构建；用户参考文档为 [启动工作流](../../../../docs/reference/launch-workflow.md)。

## 操作契约 v1

| ID | 请求 input | 授权及结果 |
|---|---|---|
| `launch.plan` | profileId、expectedRevision、operationId | 不需要 allowLaunch；返回 LaunchWorkflowPlan。只读厂商目标，但写本机非秘密计划缓存 |
| `launch.execute` | 完整 plan、password、可选 requestId | 显式 allowLaunch；口令只在本次内存调用使用。返回 LaunchReceipt；unknown 为失败 OperationResult，携带 data 和 mayHaveExecuted:true |
| `launch.observe` | operationId | 返回已有操作及有限进程观察；不启动、不重放、不读取口令 |

输入拒绝未知字段，嵌套 plan 与文件项同样声明 required / additionalProperties:false。真实输入描述由 `LaunchWorkflowOperations.Registrations` 产生，导出快照见 [launch-workflow-v1.json](../schemas/launch-workflow-v1.json)。该文件是操作目录及输入 JSON Schema 快照，不是完整响应或 Database 格式规范。

`LaunchWorkflowPlan` 固定 operationId、profileId、expectedRevision、files[{path,sha256}]、expiresAt、planDigest。计划不存口令、完整 argv 或配置环境副本。`LaunchReceipt` 固定 operationId、planDigest、attemptId、requestId，并返回 state、phase、code、message、updatedAt、可选 processId / processStartUtcTicks、processObservation、hostVerification、recoveryActions。requestId 是相关性，不替代业务 operationId。

回执的兼容新增可选字段为 entryExecutable、childCandidates[{processId,parentProcessId,processStartUtcTicks,executable,hasWindow,sessionId}]。Manager 不引用 MCP；`LaunchChildObservation` 只使用 Windows Toolhelp 进程快照及 System.Diagnostics 读取有限元数据。父 PID 必须匹配、子进程创建时间须在入口后两分钟内、EXE 与入口同目录且属于模块白名单。最多 4096 个进程快照项、16 个候选。候选不是已核验交接或登录，Host 始终 unverified；入口退出且无法可靠观察时明确提示“模块交接待核验”，不报告工程退出。

HTML：`LaunchWorkbenchServer` → `OperationExecutor` → `LaunchWorkflowOperations` → `LaunchWorkflowService`；CLI 使用同一 Manager 操作目录。发现结果须先通过既有 preview / save 取得配置修订，再生成计划。保存配置不隐式生成执行授权。

## JSON 与本机缓存的首期选择

配置沿用现有工作区修订 JSON 存储。计划与回执使用 SourceGeneration JSON DTO 和 WriteThrough / Flush(true)；计划以 CreateNew 写入缓存，回执经临时文件替换写入。计划写入中断时不受理执行，残缺缓存须重新生成或清理。不引入第二套配置决策或数据库连接。SQLite、独立 Database DLL、其他格式、迁移和统一生命周期留到整体 Database 阶段；本切片不为所有平台预建空实现。

默认根目录为 `WorkspaceSettings.StateDirectory/launch-operations`。`plans/{planDigest}.json` 保存本机冻结计划，`{operationId}.json` 保存受理及回执，独立 .lease 文件由 FileShare.None 保护。计划十分钟过期；缓存最多 256 项，生成时清理写入时间超过十五分钟的缓存。服务限制 operationId、profileId、修订、requestId、口令、目标数量和路径长度。缺失计划或观察不存在操作不创建任意编号的租约文件。

执行在租约内先读取已有事实：同 operationId / 摘要返回同 attemptId；不同摘要冲突。首次执行核对本机计划缓存、时效、配置修订与当前配置重建的完整目标集合，然后预检并再次复验。accepted 必须可靠落盘才触达启动边界；最终写回失败返回 unknown。中断留下的 accepted 在恢复时只能变成 unknown，不再执行。

文件摘要范围为显式入口、初始化文件和 EvarsEditor.SelectFile 选中的安装 EVARS，不等同于厂商启动链所有间接输入快照。跨配置及外部进程无全局原子事务；本切片不声称消除最后复验与系统启动之间的所有竞争窗口。Host 身份和真实登录需要独立证据，进程存在只表述为同 PID / 创建时刻的入口进程存在。

## 验收与未验收边界

| 场景 | 已有隔离证据 |
|---|---|
| 受理先于启动、秘密不入回执、重启后同编号不重放 | `AdmissionPrecedesBoundaryAndRestartReturnsSameAttemptWithoutPersistingSecret` |
| 文件变化或客户端篡改计划拒绝启动 | `ChangedTargetAndTamperedPlanNeverReachBoundary` |
| Marine 空 Initialization 仍冻结 EVARS；BAT 内容及 INIT 优先级变化阻止启动 | `EnvironmentContentAndInitPrecedenceChangesInvalidatePlanWithoutLaunching` |
| 缺失或歧义环境文件不生成计划 | `MissingOrAmbiguousEnvironmentCannotProduceFrozenPlan` |
| 启动边界失去可靠回执，unknown 不重放且不泄漏异常口令 | `UnknownOutcomeRemainsUnknownAcrossRestartAndDoesNotLeakException` |
| 存储不可用或跨实例独占租约拒绝副作用 | `CrossInstanceLeaseAndStorageFailurePreventAdmission` |
| 重算摘要仍不能延长本机冻结有效期或删除目标 | `RecomputedClientDigestCannotExtendFrozenExpiryOrDropTargets` |
| 不同摘要冲突；观察 PID 与创建时刻且 Host 仍 unverified | `DifferentDigestConflictsAndObservationUsesCreationTimeWithoutClaimingLogin` |
| 中断受理按原 attemptId 恢复 unknown | `InterruptedAdmissionBecomesUnknownInsteadOfReplaying` |
| 已可能执行但最终持久化失败，操作结果明确失败并保留 data | `FinalReceiptStorageFailureReturnsUnknownAndOperationFailureWithPossibleExecution` |
| unknown 的再次持久化也失败，仍返回不确定事实 | `BoundaryFailureAndFailedUnknownPersistenceStillReturnUnknown` |
| 缓存硬容量与超长 ID 检查 | `CacheHasHardCapacityAndRejectsOversizedIdentifiers` |
| 已退出入口只返回父身份、时间、目录及模块名约束均成立的候选，不升级 Host 身份 | `ExitedEntryReportsOnlyConstrainedModuleCandidatesWithoutClaimingHostOrProjectExit` |
| 无可靠入口创建时刻的旧回执仅提示交接待核验，不推定工程退出 | `ExitedLegacyEntryWithoutFrozenIdentityLeavesHandoffUnverified` |

验证命令：使用 `scripts/Use-YuzuhaDotnet.ps1` 选择 SDK 10.0.302 后运行 `dotnet test tests/YuzuhaToolkit.Manager.Tests/YuzuhaToolkit.Manager.Tests.csproj --no-restore --filter FullyQualifiedName~LaunchWorkflow`。2026-10-31：14/14 通过，均使用临时目录与注入启动委托；不启动真实 AVEVA / VM，不读取秘密。

旧 `launch.profile.start` 保留兼容输入与输出，但尚不经过本切片持久协调，不具有本操作编号幂等保证。HTML 使用新工作流；不得在 unknown 后切换旧入口重试。E3D 2.1 APS 的严格 DESIGN/Host 身份与两次内置 PML 读取已由 [独立实机证据](../../../../docs/validation/host-pml-verification-2026-10-31.md) 验收；查询协议能力仍为 0，其他产品版本、系统断电级持久性、跨进程故障杀进程演练、CAF 与统一 Database 装配仍需后续专项验收，不能由本表推定通过。

失败码、可执行排障入口和状态解释以 [用户参考文档](../../../../docs/reference/launch-workflow.md#失败码与核对入口) 为准。unknown 的唯一默认恢复动作是按原编号观察并核对，不自动补偿、回滚或再次启动。

## 2026-10-31 CLI / Host 最终实机补充

APS DESIGN 第五次启动及严格 Host/Form Query 验收通过；CLI 完整 JSON 捕获 374 ms，PID25624，Host0.3.4.2。最终实现恢复厂商原生标准句柄语义，不接管标准流、不使用 NUL、不继承 CLI stdin；Monitor DETACHED_PROCESS 与 Marine CREATE_NO_WINDOW 互斥。26 项 CLI 捕获、33 项 Host 协议测试通过。前四次失败及最终成功原始证据见 [中文验证报告](../../../../docs/validation/cli-host-compatibility-2026-10-31.md)。父终端退出后独立查询仍通过；APS已原生正常退出、无保存提示，截图已保存；工作台新PID19600沿用18642及原live-state，旧包保留。全局任务状态由根代理更新；不能由本轮结果推定其他 Feature 完成。
