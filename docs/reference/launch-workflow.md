---
uid: pmltwin.launch-workflow
title: 启动工作台：发现、计划与回执
---

# 启动工作台：发现、计划与回执

启动 Feature 将本机环境发现、字段解析、配置修订和执行回执串成同一条工作流。HTML、Manager CLI 与 C# 调用共享 `OperationExecutor`；页面不执行 BAT、不自行判断安装有效性。

## 打开与连接

从源码根目录运行 `powershell -ExecutionPolicy Bypass -File .\scripts\Start-LaunchWorkbench.ps1`，或在该脚本上右键选择“使用 PowerShell 运行”。脚本复用 `Use-YuzuhaDotnet.ps1` 选择仓库固定 SDK，先增量构建 Manager CLI，再自动打开本机浏览器，不执行发布。服务窗口按 Ctrl+C 退出。

需要自行打开浏览器时使用 `-NoBrowser`；固定端口用 `-Port 51820`，省略端口则自动选择空闲端口。已确认本机构建可用时可加 `-NoBuild`，切换配置可用 `-Configuration Release`。源码入口为 `scripts/Start-LaunchWorkbench.ps1` → `src/YuzuhaToolkit.Manager.Cli/LaunchWorkbenchCommand.cs` → <xref:YuzuhaToolkit.Manager.LaunchWorkbenchServer>。

通过 Manager 的 `workbench` 宿主打开页面。宿主只监听 loopback，并为 HTML 注入本次会话令牌。直接打开 `docs/design/launch-workflow/index.html` 可检查布局，但会显示“本机服务未连接”，不会填充演示工程或伪造回执。

页面使用同源 `GET /api/operations` 读取操作目录，使用 `POST /api/operations/{id}` 调用操作。两者都带 `X-PmlTwin-Token`。请求体为 `{input,allowWrite,allowLaunch}`；响应为 `OperationResult`，含 `operationId`、`success`、`exitCode`、`data`、`error`、`schemaVersion`。不启用跨源访问。

## 从发现到启动

1. 页面首次进入调用 `environment.discover`。省略 roots 时，后端查询注册表及标准 AVEVA 安装目录，遵守时间、深度和文件预算。
2. 打开安装、项目或模块候选前刷新发现。使用安装身份 `candidateId` 调用 `environment.resolve`，人工覆盖通过 `overrides` 传递。刷新不会抹去人工输入。
3. 检查 `coverage`、字段 `origin`、`validation` 和 `needsInput`。`ENVIRONMENT_NEEDS_INPUT` 是需要选择的可读结果，候选仍在 `data` 中。
4. 点击“保存配置并生成计划”。页面读取 `launch.workspace.read` 的 revision，经 `launch.profile.preview` 校验，显式调用 `launch.profile.save`（allowWrite），再以保存返回的 `snapshot.revision` 调用 `launch.plan`。
5. 审阅计划目标、修订与有效期。输入口令后点击“确认计划并启动”，调用 `launch.execute`（allowLaunch）。页面不自动启动，也不自动重试。
6. 使用 `launch.observe` 和原 operationId 核对执行事实。关闭页面不撤销已经受理的动作。

> [!IMPORTANT]
> 新工作流的回执 `phase: entry-process-created`、`state: success` 仅代表入口进程已创建，`hostVerification` 仍为 `unverified`。旧 `launch.profile.start` 使用 `entry-process-created-host-not-verified`。静态安装、APS 目录存在、进程存活与 Host 登录完成是不同证据；页面不会据此宣称工程已连接。

## 口令与故障

口令仅在本页输入框和本次 POST 中使用，启动请求结束后清空。不写入 localStorage、URL、配置、计划或诊断输出。页面只显示后端返回的回执，不回显请求密码。

未连接时先检查 Manager workbench 是否运行；HTTP 401/403 时使用宿主新打开的页面，避免复用旧会话令牌。发现不完整时检查 `coverage.roots`、预算和 diagnostics，再调整明确根目录。修订冲突、文件哈希变化或计划过期需要重新生成计划；执行结果 unknown 时先观察原编号，不能盲目再启动。

## 源码定位

实际调用链为 HTML `launch-workflow.js` → `LaunchWorkbenchServer` → `ManagerOperationCatalog.Create` → `OperationExecutor` → `LaunchWorkflowOperations` → `LaunchWorkflowService`。CLI 经 `ManagerCliHost` 接入同一个执行器；直接 C# 调用可使用 <xref:YuzuhaToolkit.Manager.LaunchWorkflowService>。计划及复验使用 `LaunchProfileService.ReadProfile/Validate`，最终进程描述复用 `YuzuhaToolkit.Workspace.LaunchService.Build`，没有第二套 Monitor/Marine 参数算法。

| 责任 | 仓库路径 |
|---|---|
| 安装、项目、模块及字段来源 | `src/YuzuhaToolkit.Manager.Core/LaunchDiscovery.cs` |
| 发现操作与 JSON 契约 | `src/YuzuhaToolkit.Manager.Core/LaunchDiscoveryOperations.cs` |
| 配置保存、修订和启动入口 | `src/YuzuhaToolkit.Manager.Core/LaunchOperationCatalog.cs` |
| 冻结计划、受理、租约、持久回执及观察 | `src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs` |
| plan / execute / observe 操作契约 | `src/YuzuhaToolkit.Manager.Core/LaunchWorkflowOperations.cs` |
| 本机 HTTP 薄适配器 | `src/YuzuhaToolkit.Manager.Core/LaunchWorkbenchServer.cs` |
| 无通信依赖的 Windows 直接子进程快照 | `src/YuzuhaToolkit.Manager.Core/LaunchChildObservation.cs` |
| OperationResult 与调用契约 | `src/PmlTwin.Phayton/Operations.cs` |
| 页面适配器 | `docs/design/launch-workflow/launch-workflow.js` |
| 设计与接线说明 | `docs/design/launch-workflow/handoff.md` |

设计文件和离线测试只能证明界面与适配器行为。实机验证需要单独保存脱敏的发现、计划、执行和观察回执，不能用截图替代业务证据。

## 冻结计划与存储边界

首期沿用现有修订配置存储及独立 JSON 回执，不创建另一套配置库，也不为所有数据格式建立平台。SQLite、独立 Database DLL、格式迁移和跨 Feature 的生命周期仍归整体 Database 阶段。

`launch.plan` 只读配置及厂商目标，但会写入本机非秘密计划缓存。默认目录为 `WorkspaceSettings.StateDirectory/launch-operations`，可由 C# 构造参数 `receiptRoot` 隔离。生成计划不保存配置、不启动进程、不修改初始化文件。

| 本机文件 | 内容与约束 |
|---|---|
| `plans/{planDigest}.json` | `LaunchWorkflowPlan`：operationId、profileId、expectedRevision、files（path / sha256）、expiresAt、planDigest；不含口令或完整启动 argv |
| `plans.lease` | 跨进程缓存维护租约；最多 256 个缓存文件，生成新计划时清理写入时间超过 15 分钟的旧缓存 |
| `{operationId}.lease` | 一次业务操作的独占文件租约，防止不同入口同时执行 |
| `{operationId}.json` | `LaunchReceipt`：固定 attemptId、requestId、状态、阶段、错误码、进程身份及恢复入口；先持久写入 accepted，再触达启动边界 |

计划有效期为 10 分钟。执行核对客户端摘要、本机冻结缓存、配置修订及由当前配置重建的完整目标摘要集合。入口、显式初始化文件和 EvarsEditor.SelectFile 选中的安装 EVARS 被哈希；不宣称已冻结厂商启动链运行时读取的全部间接文件。摘要不包含口令，也不能将任意修改后重算摘要的客户端计划当作本机计划。

安装环境文件选择沿用 `EvarsEditor.SelectFile`：优先 INIT，AM/PDMS 仅在确认没有 INIT 时回退 BAT；缺失、歧义或不可读时不生成可执行计划。Marine 的 Initialization 即使为空也冻结选中 EVARS；计划后修改 BAT 或新增优先 INIT 会使旧计划失效。该检查不执行或重新解析厂商脚本。

operationId 仅允许 1–128 位 ASCII 字母、数字、横线与下划线。profileId 最长 128、修订最长 256、requestId 最长 128；目标文件 2–3 个、路径最长 4096，内存口令最长 4096 且不含控制字符。输入契约快照可运行 `scripts/Export-LaunchWorkflowSchema.ps1` 从已构建 CLI 的真实目录导出；服务层还执行上述长度和数量检查。

## 失败码与核对入口

| 代码 | 含义与下一步 |
|---|---|
| `CONFIGURATION_CONFLICT` | 配置修订改变；重新读取配置、预览并生成计划，不能覆盖新修订 |
| `LAUNCH_VALIDATION_FAILED` / `INVALID_INPUT` | 使用 `launch.profile.validate` 核对产品、模块、路径或输入字段 |
| `PLAN_DIGEST_INVALID` / `PLAN_CACHE_INVALID` / `PLAN_NOT_FOUND` | 计划被修改、本机缓存损坏或缺失；重新预览，不能自行修改摘要绕过 |
| `PLAN_EXPIRED` / `TARGET_CHANGED` | 计划超时或入口/初始化文件改变；核对来源后重新生成计划 |
| `EVARS_SELECTION_FAILED` | 安装环境文件缺失、歧义或不可读；重新发现并检查 INIT 优先及 BAT 回退条件 |
| `PLAN_CACHE_FULL` | 256 项缓存容量已满；等待旧缓存过期后再次生成，不会自动执行旧计划 |
| `OPERATION_CONFLICT` | 相同 operationId 已绑定不同计划；先 `launch.observe` 核对原操作，不用另一个入口重放 |
| `LICENSE_*` / `INVALID_LICENSE_POLICY` / `UNSUPPORTED_LICENSE_POLICY` | 许可策略不满足；检查配置与许可诊断。端口可达仍不等于实际签出 |
| `ACCESS_DENIED` | 检查本次 allowLaunch 和本机状态目录权限；授权不由发现或计划推导 |
| `LAUNCH_STORAGE_OR_LEASE_FAILED` | 回执存储不可用或另一调用持有租约；保留原编号，先观察并检查存储权限/可用空间 |
| `LAUNCH_OUTCOME_UNKNOWN` / `ADMISSION_INTERRUPTED` / `RECEIPT_PERSISTENCE_FAILED` | 动作可能已发生，或受理后最终回执无法落盘；只观察原编号并人工核对，禁止自动重放 |
| `LAUNCH_RECORD_NOT_FOUND` | 指定配置或操作记录不存在；检查编号和所用状态目录 |

`launch.execute` 遇到 unknown 返回 `success:false`、退出码 7、`error.mayHaveExecuted:true`，并在 `data` 保留原回执。受理后的最终存储失败也按 unknown 返回；已持久化的 accepted 记录使重启后的同编号调用仍不会重放。`launch.observe` 成功返回表示“取得观察事实”，不能把其中的 unknown 状态改读为启动成功。

`launch.observe` 的响应新增可选 `entryExecutable` 和 `childCandidates`。候选项包含 processId、parentProcessId、processStartUtcTicks、executable、hasWindow、sessionId。Windows 只读进程快照最多检查 4096 项，返回最多 16 个候选；必须为入口的直接子进程、创建于入口后两分钟内、与入口同安装目录、且为受支持模块 EXE。只保存进程身份及窗口是否存在，不读取子进程命令行或密码。旧回执可从仍存在且摘要匹配的原计划缓存恢复入口路径；缺少创建时刻或可靠路径则不推定交接。

入口退出仍返回 `processObservation: entry-process-absent`；有候选时提示核验模块，无候选时提示“入口已退出，模块交接待核验”，不能据此断言工程已退出。候选和窗口均不证明登录，`hostVerification` 保持 `unverified`，不会跳过通信层已有的 Host 目标核验。

## 兼容与验收状态

新工作流已实现，`LaunchWorkflowTests` 在 .NET SDK 10.0.302 下有 14 项隔离测试通过，覆盖先持久受理、跨实例租约、重复编号、目标改变、伪造计划、进程创建时刻、未知结果、最终落盘失败及缓存容量。测试中的启动边界为注入委托，不启动真实 AVEVA；此证据不代表 APS 登录或 Host 验证通过。

旧 `launch.profile.start` 保留原输入、授权及进程回执以兼容现有调用者。它复用相同配置/启动后端，但尚未迁到本切片的持久受理协调层；不能宣称旧入口已有 operationId 幂等或 unknown 恢复保证。可靠自动化应采用 plan → execute → observe，不能在 unknown 后改用旧入口重试。
