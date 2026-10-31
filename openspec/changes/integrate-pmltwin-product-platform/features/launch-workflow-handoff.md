# 启动 Feature 交接 · 2026-10-31

先读本文件、[切片契约](launch-workflow.md)和当前 Feature 的[界面交接](../../../../docs/design/launch-workflow/handoff.md)。需要跨 Feature 决策时再读整体 contracts；不要把所有设计、旧报告和工作区差异一次塞进上下文。

后续平台基础切片见 [当前交接](platform-foundation-handoff.md)。本文件及 `launch-workflow-files.json` 的哈希/操作总数是启动验收时点记录，后续共享文件已有修改；当前源码身份以整体 `baseline/source-manifest.json` 为准，实际目录以 `op list` 为准，不重写历史实机证据。

## 本轮目的与实现

用户只需选择已发现的安装及工程，第一次提供工程用户/MDB，后续复用已保存配置。后端给出带来源与覆盖范围的候选，生成可直接预览/保存的契约草稿；HTML 和管理 CLI 共用 OperationExecutor。没有新造启动参数引擎，仍调用 Workspace 的 LaunchService。

首期选择 JSON：沿用现有 workspace 修订存储，增加有界冻结计划缓存及持久回执。没有同时引入 TOML/XML/SQLite。独立 Database DLL、CAF/WPF、Ribbon/UIC、部署及知识库后续切片仍保留原任务，不能由本轮启动完成推定它们完成。

五个新增注册操作为 environment.discover、environment.resolve、launch.plan、launch.execute、launch.observe；启动切片验收时管理器总目录 57 项。旧通信操作 ID 不变。旧 launch.profile.start 兼容入口不具有本轮的持久幂等保证，unknown 后禁止用旧入口重试。

## 一条命令打开

在仓库运行 `./scripts/Start-LaunchWorkbench.ps1`。脚本选择固定 SDK、构建管理器并打开本机页面；源码和 API 文档可从页面进入。发行包使用 `Yuzuha.Manager.Cli.exe workbench`。均不自动写厂商 EVARS。

开发验证使用隔离状态目录 `artifacts/manager/launch-workflow/live-state`。不要把其中的测试配置当作用户正式默认配置。现有验证服务使用回环端口 18642；重启只停止核对过命令行身份的本次服务，不停止 AVEVA。

## 排查入口

| 现象 | 源码与检查点 |
|---|---|
| 候选缺失/范围不完整 | Manager.Core/LaunchDiscovery.cs 与 InstallationDiscovery.cs；先读 coverage/diagnostics |
| 草稿无法保存 | LaunchDiscoveryResult 的专用 JSON converter；用真实 OperationExecutor resolve→preview→save 测试 |
| 模块不匹配 | Workspace/ProductModuleCatalog.cs；不能直接使用静态内部模块名 |
| 计划拒绝执行 | LaunchWorkflowService.Revalidate、EvarsEditor.SelectFile；比较修订与完整冻结文件集合 |
| 没看到窗口但入口已创建 | 先按 operationId 观察，核对模块交接；不要重复点击启动 |
| unknown | 保留 attemptId，按原 operationId 恢复观察，不自动重放 |
| 管道可见但 Host 不通过 | 独立通信 CLI/Toolkit 的目标身份校验；不能取消模块核验来制造成功 |
| 页面调用失败 | LaunchWorkbenchServer 的同源/token/白名单；业务错误仍从 OperationResult 读取 |

C# 类/方法 XML 文档进入 DocFX。`dotnet tool run docfx docfx.json` 生成 `artifacts/docfx/site`。当前生成的远程源码链接需对应提交推送后才能在 GitHub 打开；页面本机 `/source/` 白名单可立即查看本次源码。

## 当前 Host/PML 结论

后续仅更新实装 resolver/Rpc 两文件并留存备份，E3D 2.1 APS 的 PID 8408 在 expectedModel=DESIGN 下严格 TargetVerified=true；内置当前元素与 YuzuhaRuntimePath 读取均 OK。本次测试会话已正常退出，未修改模型、DLL 或 EVARS。Host 协议能力仍为 0，不能宣称 Manager 查询协议灯全绿。新报告、摘要、回退步骤及 JSON 精度限制见 [严格验收](../../../../docs/validation/host-pml-verification-2026-10-31.md)。旧启动 receipt 的 hostVerification=unverified 保留，外部核验不追写成功。

管理 CLI 的 Out-String EOF/继承输出句柄疑似等待问题仍待修：退出 APS 后管道立即完成，支持后代持有输出句柄的解释，但未确认根因或修复。后续按原任务推进 Database/CAF 和其他 Feature。

## 首次 APS 验证历史（失败保留）

本轮仅对 E3D 2.1 的 APS 发起一次启动。operationId 为 `df0dfa8c-ec75-45d8-8755-3cdd57b0c48f`；Monitor 入口 PID 1916 交接到 Design PID 14916，其父 PID 为 1916。Windows 会话一致，实际窗口标题 `ProjAPS | ALL | Model - AVEVA Everything3D`。启动参数与 Archon rewrite 和本机厂商快捷方式一致；没有修改厂商环境文件或工程模型。

窗口与管道已发现。Toolkit 指定 expectedModel=Design 的身份核验被拒绝：`E3D_TARGET_MODEL_MISMATCH`，Host 报告 `Unknown`。因此本轮证明 APS 工程窗口打开，但 **Host/PML 通信验收未通过**；没有降低校验、执行 PML、重放启动或把外部验证结果写成 receipt 的 hostVerification=verified。后续应定位已加载 Host 的模块识别，保留当前工程给用户继续核验。

E3D 3.1 也静态发现 APS，但没有再次登录；AM 未发现 APS。不能把 E3D 2.1 的结果推广到这两套环境。原生窗口截图入口未获工具访问（等待授权超时），窗口标题和 PID 证据来自只读会话发现；HTML 截图另行记录。

## 下一步边界

Host 旧启动链问题已按上述后续验收验证；[Host 调查](launch-workflow-host-followup.md)保留定位过程。继续处理 CLI 等待疑点与原任务的 Database/CAF 和其他 Feature。视觉稿需要用户审阅；分辨率实测和 FigCraft 导出不能代替用户的视觉认可。尚未提交或推送 GitHub，保留已有工作区修改。

最终测试、产物和已知边界集中在 [本轮验收](../../../../docs/validation/launch-workflow-2026-10-31.md)；界面变量/滚动实测见 [视觉交接](../../../../docs/design/launch-workflow/design-review.md)。文件身份以同目录 `launch-workflow-files.json` 为准；源文件变化后必须重新检查，不从聊天记忆宣称仍通过。

## 2026-10-31 CLI / Host 最终实机补充

APS DESIGN 第五次启动及严格 Host/Form Query 验收通过；CLI 完整 JSON 捕获 374 ms，PID25624，Host0.3.4.2。最终实现恢复厂商原生标准句柄语义，不接管标准流、不使用 NUL、不继承 CLI stdin；Monitor DETACHED_PROCESS 与 Marine CREATE_NO_WINDOW 互斥。26 项 CLI 捕获、33 项 Host 协议测试通过。前四次失败及最终成功原始证据见 [中文验证报告](../../../../docs/validation/cli-host-compatibility-2026-10-31.md)。父终端退出后独立查询仍通过；APS已原生正常退出、无保存提示，截图已保存；工作台新PID19600沿用18642及原live-state，旧包保留。全局任务状态由根代理更新；不能由本轮结果推定其他 Feature 完成。
