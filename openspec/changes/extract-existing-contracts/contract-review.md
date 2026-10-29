# 契约提取审查稿（未批准）

取证：`codex/quality-baseline-20261026` @ [`abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143`](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/commit/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143)。日期：2026-10-27。

这是现有系统的固定基线审查快照。唯一生效规范仍为 [`contract.json`](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/docs/specification/contract.json)；本稿没有批准、升级状态或新增产品承诺。

## 审查顺序

1. 先看待审决定与公共契约，确认业务语义和当前边界。
2. 再逐条核对 R01–R24；规则和验收均原文提取，状态沿用原记录。
3. 输入的精确字段、required、类型、范围和授权看 [完整操作目录](operation-inventory.json)；定位与哈希看 [证据清单](evidence.json)。
4. 本轮测试结果见 [verification.md](verification.md)；审查结论另行记录，勿将文件存在视为验收通过。

## 取证范围

- 24 条需求：verified=15，partial=5，planned=4；28 个符号绑定、15 个规范测试绑定、30 个界面状态、45 条跳转仍以原 JSON 为准。
- 46 项实际 CLI 操作的完整输入契约；当前 contractVersion 全为 1。
- 包括共享服务、授权、失败、目标身份、有界读取、配置并发、PML 文件流程和知识/日志边界。
- 不声称完整覆盖所有结果 DTO、跨运行时 RPC 帧/二进制兼容、安装升级或每个 PML 内置函数签名；这些可作为后续专项审查。
- 本轮没有 AVEVA 调用；历史 E3D 2.1 证据不推广到其他产品。NET35/NET48 缺少本地 SDK，当前仅有声明定位，没有对应 Host 的 Roslyn 编译语义或实机验收。

## 待审决定

| ID | 需要审查的结论 | 当前处理 |
| --- | --- | --- |
| Q01 | 现有规则/验收是否准确表达希望保留的行为？ | 原文逐条列出，等待审查；本次不重写要求。 |
| Q02 | 今后继续由 contract.json 管规范，还是另行迁移到 OpenSpec 主 spec？ | 本次继续使用 contract.json；迁移需要单独方案及 R/B/T/S 保真验证。 |
| Q03 | R23 的 verified 是否有足够验收依据，是否补上明确 T 绑定？ | 原 tests 为空。新找到的 StartupPolicy 相关测试只作补充，不能证明所有 Host 映射和每新会话一次的承诺。 |
| Q04 | R08/R09/R10/R11/R19 的 partial 与 R01/R07/R12/R20 的 planned 是否仍准确，下一步先闭合哪项？ | 保留原状态；某条相关单测通过不等于整条业务流程交付。 |
| Q05 | batch 的 completed 是否接受“含首个失败在内的已返回结果数”这一现有语义？ | 从代码提取，不把它重命名为成功数或改变行为。 |
| Q06 | 是否另开结果 Schema、RPC/旧运行时、安装协议专项审查？ | 本轮只完整导出 CLI 输入 Schema，保留范围缺口。 |
| Q07 | AM/PDMS/E3D3.1 的实机验收计划与状态如何记录？ | 维持 E3D 2.1 的历史证据边界，本轮不触发实机操作。 |

## 公共契约（代码观察）

下列 C 标识是本次解释索引，不是新增的规范 R 标识。具体观察以所链基线代码为准。

### C01 · 操作发现、输入与回执

`op list/describe/invoke/batch` 使用同一个原生调度器。输入在 inline/file/stdin 三者中至多选一个；invoke 无输入时为 `{}`。JSON 长度上限 1,048,576 个 .NET 字符，非 UTF-8 字节。重复/未知 CLI 选项拒绝。外层为 `schemaVersion/operationId/success/exitCode/data/error`；data 保留业务内部大小写。证据：[OperationCli.RunAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/OperationCli.cs#L10)，[OperationResult](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/Operations.cs#L26)，[T08: FileStdinAndInlineJsonUseTheSameDispatcherAndRejectAmbiguity](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L94)。

### C02 · 授权门独立

写入、创建进程、PML 执行分别来自 `--allow-write/--allow-launch/--allow-execution`，默认拒绝；input JSON 不能自授权限。需要目标的操作另外要求正 PID。参数和授权门在处理器调用前检查。只读的数组/Form 查询仍要求 execution 标志，因为通过 Host 执行只读查询。证据：[OperationExecutor.InvokeCoreAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/Operations.cs#L126)，[T06: PermissionDenialAndUnknownPatchFieldsLeaveFileUntouched](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L228)，[T09: ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L134)。

### C03 · 批处理不是事务

batch 接收 1–1000 项，先校验全部“请求信封”的形状；每项业务 Schema/授权仍在执行到该项时检查。逐项顺序调用，首错停止，先前结果和副作用保留，不重试、不自动回滚。`data.completed = results.Length`，包括首个失败结果；不是成功数。每项超时默认 120 秒，允许 1–3600 秒；不是整个 batch 的共享总时限。证据：[OperationCli.RunBatchAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/OperationCli.cs#L110)，[InvokeTimedAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/OperationCli.cs#L102)。

### C04 · 失败与未知结果

退出码 0 成功；2 输入/校验；3 未知操作；4 授权；5 目标；6 Host；7 传输；8 取消/超时；9 内部；10 配置修订冲突。`mayHaveExecuted=true` 是保守的未知执行标记，不是执行成功证据；false 也不应被当成所有类别副作用的事务保证。CLI 的取消/异常兜底按 descriptor.requiresExecution 决定该标志。不得据超时/断线自动重发。证据：[OperationExecutor 取消路径](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/Operations.cs#L149)，[配置冲突映射](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/LaunchOperationCatalog.cs#L73)，[T09: ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L134)。

### C05 · 会话与 Host 能力

每次目标调用重新核验 PID、进程启动时刻、专用管道、模块和 Host 身份。CLI 不沿用上次进程的内存选择，不回退到共享旧管道。旧 Host 缺少 bounded-array/form-query v1 时，在发送 PML 前返回 `PML_BOUNDED_ARRAY_UNSUPPORTED/PML_UI_HOST_UNSUPPORTED`，退出码 6、mayHaveExecuted=false。证据：[B14: YuzuhaRpcBridge.SelectSessionAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/YuzuhaRpcBridge.cs#L72)，[B15: PmlFormQueryRequestSender.SendAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/PmlFormQueryRequestSender.cs#L9)，[BoundedArrayRequest.SendAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/BoundedArrayRequest.cs#L10)，[T09: ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L134)。

### C06 · 有界读取与原生缺失值

array.window/forms/gadgets 的分页预算默认 startIndex=1、maxItems=100、maxCharacters=32768；Schema 上限分别为 2147483647、1000、262144。array.window 仅读已有数组，不执行 producer、不删除数组。原生稀疏位置也消耗扫描预算；空文本、0、false、unavailable 和 error 不互换。目标、种类、参数或预算变化需丢弃旧续页。证据：[B13: NativeOperationCatalog.Create](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/NativeOperationCatalog.cs#L13)，[B24: SessionWorkbench.QueryAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/SessionWorkbench.cs#L122)，[T14: FormListsUseNativeManagerAndHonorScanBudget](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlFormQueryTests.cs#L44)，[旧 Host 拒绝测试](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlArrayWindowTests.cs#L42)。

### C07 · 函数登记与信任边界

登记固定源码身份及标量签名，默认无调用许可；源码变化需重新登记与授权。参数顺序、类型、引号和数值格式由调用构造器校验。登记不等于加载 PML，许可与 advisory trust 均不是通用 PML 沙箱。证据：[B30: PmlFunctionRegistry](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/PmlFunctionRegistry.cs#L43)，[源码变化撤销许可测试](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/FunctionRegistryTests.cs#L35)。

### C08 · 配置修订与并发

WPF/CLI 共用 LaunchProfileService。保存/更新以 expectedRevision、锁和文件提交防止旧草稿覆盖；未知已有字段保留，但 patch 中的未知字段拒绝。冲突为 CONFIGURATION_CONFLICT / 10，并带 actualRevision。预览不持久化，保存不启动，保存只影响下次启动。证据：[B03: LaunchProfileService.Save](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L78)，[B04: WorkspaceConfigurationStore.Commit](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/WorkspaceConfigurationStore.cs#L33)，[T03: ConcurrentEditorsCannotOverwriteTheFirstSavedChange](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L114)，[T05: JsonUpdatePreservesUnknownWorkspaceAndProfileFields](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L88)。

### C09 · 历史恢复和启动结果

恢复先预览，并按当前规则重新校验，只恢复目标配置、生成新修订；不回写旧密码、其他配置或历史。启动独立授权，启动进程的回执不等于登录或 Host 身份核验。证据：[B05: LaunchProfileService.PreviewRestore](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L146)，[B06: LaunchProfileService.Restore](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L155)，[launch.profile.start](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/LaunchOperationCatalog.cs#L53)，[T04: RestoreCreatesNewRevisionAndKeepsOtherProfilesAndOldHistory](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L148)。

### C10 · PML 文件流程与业务结果

部署预览冻结源/目标哈希，写前重新核验并限制路径；备份保留在注册库外。REHASH/重载/执行的传输成功与业务结果匹配分别判断；未知执行停止，不自动继续或重发。已有这些机制，但 R10/R19 的整体产品工作流仍为 partial。证据：[PmlDevelopmentWorkflow](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/PmlDevelopmentWorkflow.cs#L70)，[T12: ChangedTargetStopsWholeCopyBeforeAnyWrites](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L107)，[未知 REHASH 停止测试](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlDevelopmentWorkflowTests.cs#L51)。

### C11 · 知识来源

已有知识检索、分层来源、块读取和显式建立索引入口；普通检索不自动重建。默认官方 HTML/源码、产品版本引用和无资料提示的完整目标流程仍为 R07/R20 planned；不能因 knowledge.layers.search 可调用就宣称闭合。证据：[B17: KnowledgeRepository](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Knowledge.Core/KnowledgeLayers.cs#L27)，[T07: EveryFormerToolHasARealNativePositivePathAndRecordedFailureGates](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L17)，R07/R20 原文。

### C12 · 日志与界面状态

日志关联开始/结束、目标、耗时和实际结果，命名秘密脱敏；进程退出但调用未结束记为未知。日志失败不把已完成业务改写成失败来诱发重试。查询结果与运行记录分开展示，筛选和未知结果入口的完整业务状态仍按 R09/R11 partial 保留。证据：[B18: RuntimeJournal.Complete](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L98)，[B19: RuntimeReader.Recent](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L209)，[T11: JournalPersistsCorrelatedStartAndCompletionWithRedactedNamedSecrets](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L157)，[T13: ExitedProcessWithUnfinishedCallIsReportedAsUnknownRatherThanSuccess](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L178)。

### C13 · 启动策略

现有要求为 E3D2.1→2.1/NET48、3.1 系列→3.1.6/NET48、AM/PDMS→NET35。YUZUHA_STARTUP_ACTIONS 只接受 none/空 或 rehash-all；显式 override 优先，只影响新进程。R23 在原规范中为 verified，但没有 T 绑定；补充测试只覆盖部分策略语义，见 Q03。证据：[B10: LaunchService.Preview](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchService.cs#L11)，[StartupPolicy.Normalize](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/StartupPolicy.cs#L13)；补充测试：[none 优先及清理关联](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlDevelopmentWorkflowTests.cs#L88)、[策略/关联 ID 校验](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlDevelopmentWorkflowTests.cs#L95)（未写回原 T 绑定）。

### C14 · 架构与文档边界

开发 HTTP Host 只读指南并计算预览，不提供 PML 执行；核心依赖方向由 dependencies.json 和 ArchUnitNET 守护。签名、源码位置、调用及候选实现来自生成链；候选/静态关系不是实机执行证据。证据：[架构约束测试](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Architecture.Tests/ArchitectureBoundaryTests.cs#L78)，[T15: compact metadata preserves every value](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/handoff-specifications.test.mjs#L15)。

## 46 项操作与授权矩阵

精确输入见 [operation-inventory.json](operation-inventory.json)。四列分别来自 requiresPid / requiresExecution / requiresWrite / requiresLaunch，✓ 表示需要；空列表示该项不要求此门，并不代表业务无任何风险。

| 操作 ID | 版本 | PID | 执行授权 | 写入授权 | 启动授权 | 必填输入字段 |
| --- | --- | --- | --- | --- | --- | --- |
| `aveva.session.select` | 1 | ✓ |  |  |  | — |
| `aveva.session.status` | 1 | ✓ |  |  |  | — |
| `aveva.sessions.list` | 1 |  |  |  |  | — |
| `builtin.usage` | 1 |  |  |  |  | — |
| `knowledge.chunk.read` | 1 |  |  |  |  | chunkId |
| `knowledge.database.build` | 1 |  |  | ✓ |  | — |
| `knowledge.database.check` | 1 |  |  |  |  | dbPath |
| `knowledge.databases.list` | 1 |  |  |  |  | — |
| `knowledge.experience.record` | 1 |  |  | ✓ |  | title, content, context |
| `knowledge.layers.search` | 1 |  |  |  |  | query |
| `knowledge.search` | 1 |  |  |  |  | query |
| `knowledge.source.register` | 1 |  |  | ✓ |  | role, name |
| `launch.modules.list` | 1 |  |  |  |  | product |
| `launch.profile.default` | 1 |  |  | ✓ |  | profileId, expectedRevision |
| `launch.profile.delete` | 1 |  |  | ✓ |  | profileId, expectedRevision |
| `launch.profile.duplicate` | 1 |  |  | ✓ |  | profileId, expectedRevision |
| `launch.profile.favorite` | 1 |  |  | ✓ |  | profileId, expectedRevision, favorite |
| `launch.profile.history` | 1 |  |  |  |  | — |
| `launch.profile.list` | 1 |  |  |  |  | — |
| `launch.profile.preview` | 1 |  |  |  |  | profile, expectedRevision |
| `launch.profile.read` | 1 |  |  |  |  | profileId |
| `launch.profile.restore` | 1 |  |  | ✓ |  | profileId, expectedRevision, revision |
| `launch.profile.restore.preview` | 1 |  |  |  |  | profileId, expectedRevision, revision |
| `launch.profile.save` | 1 |  |  | ✓ |  | profile, expectedRevision |
| `launch.profile.start` | 1 |  |  |  | ✓ | profileId, expectedRevision, password |
| `launch.profile.update` | 1 |  |  | ✓ |  | profileId, expectedRevision, changes |
| `launch.profile.validate` | 1 |  |  |  |  | profile |
| `launch.workspace.read` | 1 |  |  |  |  | — |
| `pml.array.read` | 1 | ✓ | ✓ |  |  | pmlCommand, globalVar, deleteGlobalVar |
| `pml.array.window` | 1 | ✓ | ✓ |  |  | globalVar |
| `pml.call.generate` | 1 |  |  |  |  | methodName |
| `pml.command.execute` | 1 | ✓ | ✓ |  |  | pmlCommand |
| `pml.forms.list` | 1 | ✓ | ✓ |  |  | — |
| `pml.function.invoke` | 1 | ✓ | ✓ |  |  | functionName, parameters |
| `pml.function.permission` | 1 |  |  | ✓ |  | functionName, allowInvoke |
| `pml.function.register` | 1 |  |  | ✓ |  | sourceFile, functionName |
| `pml.functions.list` | 1 |  |  |  |  | — |
| `pml.gadget.read` | 1 | ✓ | ✓ |  |  | formName, gadgetName |
| `pml.gadgets.list` | 1 | ✓ | ✓ |  |  | formName |
| `pml.trust.list` | 1 |  |  |  |  | — |
| `pml.trust.set` | 1 |  |  | ✓ |  | functionName, state |
| `pml.undo.begin` | 1 | ✓ | ✓ |  |  | — |
| `pml.undo.end` | 1 | ✓ | ✓ |  |  | — |
| `runtime.calls` | 1 |  |  |  |  | — |
| `runtime.status` | 1 |  |  |  |  | — |
| `workspace.pmllib.inspect` | 1 |  |  |  |  | — |

## 24 条原规范要求（无改写）

以下“原状态”来自原规范。规则和验收保留原文；绑定说明只帮助跳转，不补造覆盖。符号若为 Roslyn-declaration，仅有声明定位；完整分辨率见 evidence.json。

### R01 · FE · planned

原规则：常用任务从工作台完成；字段、下一步与失败出口可见。

原验收：非实现者按 PRD 完成启动配置、检索、查询、历史恢复；不编辑 JSON，不需要解释控件含义；记录步骤、阻塞和误操作。

目标/优先级：G3 / P0；界面状态：S01, S02, S04, S08。

操作：原记录未绑定操作。

实现绑定：[B20: MainWindow.PreviewProfileChanges](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/MainWindow.LaunchLifecycle.xaml.cs#L106)。

原测试绑定：无；不得解释为已具备自动化验收覆盖。

### R02 · FE · verified

原规则：产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。

原验收：E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。

目标/优先级：G1 / P0；界面状态：S01, S02, S24, S25, S26。

操作：`launch.modules.list`、`launch.profile.validate`。

实现绑定：[B02: LaunchProfileService.Validate](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L36)；[B08: IProductModuleCatalog.Get](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L46)；[B09: ProductModuleCatalog.Get](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ProductModuleCatalog.cs#L9)；[B22: MainWindow.ProductIdentityChanged](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/MainWindow.LaunchLifecycle.xaml.cs#L62)。

原测试绑定：[T02: InvalidProductModuleRejectsBeforePersistence](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L43)。

### R03 · FE · verified

原规则：配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。

原验收：两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。

目标/优先级：G3 / P0；界面状态：S01, S02。

操作：`launch.profile.list`。

实现绑定：[B01: LaunchProfileService.List](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L28)；[B21: MainWindow.LaunchProfileFilterChanged](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/MainWindow.LaunchLifecycle.xaml.cs#L190)。

原测试绑定：[T10: FiltersFavoritesDefaultsAndHistoryUseSameService](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L249)。

### R04 · FE · verified

原规则：编辑→差异预览→保存→重开；未保存草稿离开需确认。

原验收：保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。

目标/优先级：G3 / P0；界面状态：S03, S04, S05。

操作：`launch.profile.preview`、`launch.profile.save`、`launch.profile.read`。

实现绑定：[B03: LaunchProfileService.Save](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L78)；[B20: MainWindow.PreviewProfileChanges](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/MainWindow.LaunchLifecycle.xaml.cs#L106)。

原测试绑定：[T01: SavedProfileIsReadByNewServiceAndDraftDoesNotMutateFile](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L30)。

### R05 · FE · verified

原规则：过期修订进入冲突状态；保留草稿，允许重新读取并比较。

原验收：外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。

目标/优先级：G3 / P0；界面状态：S06。

操作：`launch.profile.save`。

实现绑定：[B03: LaunchProfileService.Save](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L78)；[B25: ConfigurationConflictException](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L30)。

原测试绑定：[T03: ConcurrentEditorsCannotOverwriteTheFirstSavedChange](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L114)。

### R06 · FE · verified

原规则：历史先预览再恢复；恢复生成新修订。

原验收：选历史→查看差异→确认；取消不写入；恢复后其他配置不变。

目标/优先级：G3 / P0；界面状态：S07, S08, S09。

操作：`launch.profile.history`、`launch.profile.restore.preview`、`launch.profile.restore`。

实现绑定：[B05: LaunchProfileService.PreviewRestore](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L146)；[B06: LaunchProfileService.Restore](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L155)；[B23: MainWindow.ConfirmRestoreProfile](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/MainWindow.LaunchLifecycle.xaml.cs#L248)。

原测试绑定：[T04: RestoreCreatesNewRevisionAndKeepsOtherProfilesAndOldHistory](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L148)。

### R07 · FE · planned

原规则：知识入口默认官方 HTML 与源码；个人经验为补充，结果显示来源、产品与版本。

原验收：首次使用无需手动登记函数；无官方资料显示定位/导入入口；经验不会覆盖官方来源。

目标/优先级：G1 / P0；界面状态：S10, S11。

操作：`knowledge.layers.search`。

实现绑定：[B17: KnowledgeRepository](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Knowledge.Core/KnowledgeLayers.cs#L27)。

原测试绑定：无；不得解释为已具备自动化验收覆盖。

### R08 · FE · partial

原规则：能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。

原验收：CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。

目标/优先级：G1 / P0；界面状态：S12。

操作：`pml.functions.list`。

实现绑定：[B13: NativeOperationCatalog.Create](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/NativeOperationCatalog.cs#L13)；[B30: PmlFunctionRegistry](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/PmlFunctionRegistry.cs#L43)。

原测试绑定：[T07: EveryFormerToolHasARealNativePositivePathAndRecordedFailureGates](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L17)。

### R09 · FE · partial

原规则：查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。

原验收：改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。

目标/优先级：G2 / P0；界面状态：S13, S14, S15。

操作：`pml.forms.list`、`pml.gadgets.list`、`runtime.calls`。

实现绑定：[B24: SessionWorkbench.QueryAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/SessionWorkbench.cs#L122)；[B19: RuntimeReader.Recent](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L209)。

原测试绑定：[T14: FormListsUseNativeManagerAndHonorScanBudget](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlFormQueryTests.cs#L44)。

### R10 · FE · partial

原规则：脚本流程包含写入计划、备份、加载、执行与结果对比。

原验收：先看文件差异与执行目标；加载失败有回执；修正后独立验证，不用脚本合集替代工作流。

目标/优先级：G1 / P0；界面状态：S17, S18。

操作：`workspace.pmllib.inspect`、`pml.command.execute`。

实现绑定：[B16: WorkspaceTools](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/WorkspaceTools.cs#L9)。

原测试绑定：[T12: ChangedTargetStopsWholeCopyBeforeAnyWrites](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L107)。

### R11 · FE · partial

原规则：执行模式与权限可见；结果不明有单独状态和核验入口。

原验收：关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。

目标/优先级：G2 / P0；界面状态：S16, S19, S22, S23, S27, S28。

操作：`runtime.status`。

实现绑定：[B18: RuntimeJournal.Complete](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L98)；[B19: RuntimeReader.Recent](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L209)。

原测试绑定：[T09: ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L134)；[T13: ExitedProcessWithUnfinishedCallIsReportedAsUnknownRatherThanSuccess](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L178)。

### R12 · FE · planned

原规则：业务错误定位字段或动作，并保留可恢复上下文。

原验收：键盘完成筛选与确认；焦点可见；空/加载/拒绝/冲突/未知状态都有文案和出口；可用性实测另记。

目标/优先级：G3 / P1；界面状态：S06, S19, S20。

操作：原记录未绑定操作。

实现绑定：[B20: MainWindow.PreviewProfileChanges](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/MainWindow.LaunchLifecycle.xaml.cs#L106)。

原测试绑定：无；不得解释为已具备自动化验收覆盖。

### R13 · BE · verified

原规则：WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。

原验收：同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。

目标/优先级：G3 / P0；界面状态：S04。

操作：`launch.profile.save`。

实现绑定：[B03: LaunchProfileService.Save](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L78)；[B07: IConfigurationStore`1.Commit](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L23)；[B08: IProductModuleCatalog.Get](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L46)；[B13: NativeOperationCatalog.Create](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/NativeOperationCatalog.cs#L13)。

原测试绑定：[T01: SavedProfileIsReadByNewServiceAndDraftDoesNotMutateFile](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L30)；[T02: InvalidProductModuleRejectsBeforePersistence](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L43)。

### R14 · BE · verified

原规则：提交使用 expectedRevision、文件锁与历史；未知字段保留。

原验收：并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。

目标/优先级：G3 / P0；界面状态：S05, S06。

操作：`launch.profile.update`。

实现绑定：[B04: WorkspaceConfigurationStore.Commit](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/WorkspaceConfigurationStore.cs#L33)；[B07: IConfigurationStore`1.Commit](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L23)；[B25: ConfigurationConflictException](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L30)。

原测试绑定：[T03: ConcurrentEditorsCannotOverwriteTheFirstSavedChange](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L114)；[T05: JsonUpdatePreservesUnknownWorkspaceAndProfileFields](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L88)。

### R15 · BE · verified

原规则：恢复重新校验当前环境，只恢复目标配置，产生新历史。

原验收：失效路径拒绝；其他配置、旧历史与独立凭据不被回写。

目标/优先级：G3 / P0；界面状态：S08, S09。

操作：`launch.profile.restore`。

实现绑定：[B05: LaunchProfileService.PreviewRestore](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L146)；[B06: LaunchProfileService.Restore](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L155)。

原测试绑定：[T04: RestoreCreatesNewRevisionAndKeepsOtherProfilesAndOldHistory](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L148)。

### R16 · BE · verified

原规则：CLI 原生分派 JSON；写配置、启动与执行分别授权。

原验收：inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。

目标/优先级：G1 / P0；界面状态：S12, S16。

操作：`builtin.usage`、`launch.profile.save`。

实现绑定：[B11: NativeCliHost](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/NativeCliHost.cs#L11)；[B12: OperationExecutor.InvokeAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/Operations.cs#L117)；[B13: NativeOperationCatalog.Create](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/NativeOperationCatalog.cs#L13)。

原测试绑定：[T06: PermissionDenialAndUnknownPatchFieldsLeaveFileUntouched](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs#L228)；[T07: EveryFormerToolHasARealNativePositivePathAndRecordedFailureGates](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L17)；[T08: FileStdinAndInlineJsonUseTheSameDispatcherAndRejectAmbiguity](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L94)。

### R17 · BE · verified

原规则：每次目标调用核验 PID、启动时间、模块与 Host 协议。

原验收：错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。

目标/优先级：G2 / P0；界面状态：S13, S20, S26, S29。

操作：`aveva.session.status`、`pml.forms.list`。

实现绑定：[B14: YuzuhaRpcBridge.SelectSessionAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/YuzuhaRpcBridge.cs#L72)；[B15: PmlFormQueryRequestSender.SendAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/PmlFormQueryRequestSender.cs#L9)。

原测试绑定：[T09: ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L134)。

### R18 · BE · verified

原规则：查询受项数/字符预算约束；续页保留边界与原生缺失值。

原验收：空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。

目标/优先级：G2 / P0；界面状态：S13, S14。

操作：`pml.array.window`、`pml.gadget.read`。

实现绑定：[B15: PmlFormQueryRequestSender.SendAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/PmlFormQueryRequestSender.cs#L9)；[B24: SessionWorkbench.QueryAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Desktop/SessionWorkbench.cs#L122)；[B31: PmlCommandRpcService](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.PmlHost.Net48/PmlCommandRpcService.cs#L8)。

原测试绑定：[T14: FormListsUseNativeManagerAndHonorScanBudget](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/PmlFormQueryTests.cs#L44)。

### R19 · BE · partial

原规则：复制计划冻结源/目标哈希，执行保留备份；与 PML 加载/调试串联。

原验收：复制前变更任一端即拒绝；路径不能越界；部署成功与运行成功分开记录。

目标/优先级：G1 / P0；界面状态：S17, S18。

操作：`workspace.pmllib.inspect`、`pml.command.execute`。

实现绑定：[B16: WorkspaceTools](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Mcp.Core/WorkspaceTools.cs#L9)；[B29: DirectoryInspector.Scan](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/DirectoryInspector.cs#L50)。

原测试绑定：[T12: ChangedTargetStopsWholeCopyBeforeAnyWrites](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L107)。

### R20 · BE · planned

原规则：官方 HTML/源码是知识主源；按产品版本建立引用，个人资料独立补充。

原验收：保留来源/版本/块 ID；旧索引可辨识；无资料不编造答案；默认官方流程尚待实现。

目标/优先级：G1 / P0；界面状态：S10, S11。

操作：`knowledge.layers.search`。

实现绑定：[B17: KnowledgeRepository](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Knowledge.Core/KnowledgeLayers.cs#L27)。

原测试绑定：无；不得解释为已具备自动化验收覆盖。

### R21 · BE · verified

原规则：日志关联开始、结束、耗时与目标；未结束操作归为未知。

原验收：命名秘密脱敏；日志故障不把已执行改成失败并诱发重试；记录结果与业务结果分别保存。

目标/优先级：G3 / P0；界面状态：S15, S19。

操作：`runtime.calls`、`runtime.status`。

实现绑定：[B18: RuntimeJournal.Complete](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L98)；[B19: RuntimeReader.Recent](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/RuntimeJournal.cs#L209)。

原测试绑定：[T11: JournalPersistsCorrelatedStartAndCompletionWithRedactedNamedSecrets](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L157)；[T13: ExitedProcessWithUnfinishedCallIsReportedAsUnknownRatherThanSuccess](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs#L178)。

### R22 · BE · verified

原规则：代码、JSON、TOML 用同一业务路径测试，逐步保存输入、输出与结论。

原验收：解析失败/超时保留部分结果；历史证据与本轮运行区分；报告按新目录保存。

目标/优先级：G3 / P0；界面状态：S18。

操作：`launch.workspace.read`、`launch.profile.save`。

实现绑定：[B03: LaunchProfileService.Save](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L78)；[B12: OperationExecutor.InvokeAsync](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/PmlTwin.Phayton/Operations.cs#L117)。

原测试绑定：[T07: EveryFormerToolHasARealNativePositivePathAndRecordedFailureGates](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L17)；[T08: FileStdinAndInlineJsonUseTheSameDispatcherAndRejectAmbiguity](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs#L94)。

### R23 · BE · verified

原规则：启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。

原验收：E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。

目标/优先级：G1 / P0；界面状态：S01, S16。

操作：`launch.profile.preview`。

实现绑定：[B10: LaunchService.Preview](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchService.cs#L11)。

原测试绑定：无；不得解释为已具备自动化验收覆盖。

### R24 · BE · verified

原规则：文档从 Roslyn 生成签名、源码定位、静态调用与实现候选；手写只写业务约束。

原验收：缺符号/失效源码哈希/悬空需求引用使构建失败；静态候选不得描述为实机执行；契约元数据可无损展开。

目标/优先级：G3 / P1；界面状态：S21。

操作：原记录未绑定操作。

实现绑定：[B03: LaunchProfileService.Save](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/LaunchProfileService.cs#L78)；[B04: WorkspaceConfigurationStore.Commit](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/WorkspaceConfigurationStore.cs#L33)；[B07: IConfigurationStore`1.Commit](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L23)；[B08: IProductModuleCatalog.Get](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/src/YuzuhaToolkit.Workspace/ConfigurationContracts.cs#L46)。

原测试绑定：[T15: compact metadata preserves every value](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/abd89defa6d2f6e4d36ef5ca6bfdcb1dd173a143/tests/handoff-specifications.test.mjs#L15)。

## 再现与变更纪律

先用同一基线运行现有测试和文档生成链，再导出 CLI 目录；无需安装或操作 AVEVA。命令与本轮结果见 verification.md。
本稿是审查证据，不随日常代码变化自动更新；若要修正规则/状态，先修改唯一规范源及其真实证据，再新建 OpenSpec 变更或明确批准的迁移。
evidence.json 的 sources[].sha256 对应固定提交的 Git blob 字节，workingTreeSha256 另记本次 checkout 的原始字节，以区分 Windows CRLF。操作清单由 openspec/.gitattributes 固定为 LF。
