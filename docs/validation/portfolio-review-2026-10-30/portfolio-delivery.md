# 管理器作品集与交付独立验收 · 2026-10-30

审阅范围：本轮 OpenSpec、四份 `docs/reference/manager-*.md`、代码边界与函数管理、历史设计入口及导出截图。后续按授权修复低风险文档；未编辑 FigCraft、生产代码或秘密，未重跑业务测试、安装更新或启动 AVEVA/VM。业务测试数字仅引用实施报告，不是本次独立复跑结果。未发现适用于本目录的 AGENTS.md。

## 结论

**当前结论：独立 Feature 拆分草稿及指定截图可读性通过抽查；尚不能标为“作品集完整交付/实机验收通过”。** 后端已有可审阅的离线验证与独立 CLI 包；初次读图发现的语义问题已在修正导出中复查解决，完整任务流程与逐按钮操作映射仍待下一阶段。生产 GUI、完整安装更新和跨产品实机验收继续独立推进。

当前独立候选入口为 `docs/design/yuzuha-manager-features/README.md` 与 `index.html`，包含 00 共用基础＋9 个 Feature。v04/v08、Clear Workspace/R2 保留为历史，不能自动充当新管理器交付证据。

## 交付门槛

| 门槛 | 可通过的具体证据 | 当前判断 |
| --- | --- | --- |
| 问题与架构故事 | 说明旧“桌面直接组合服务”的问题、管理/通信进程拆分、兼容层取舍；给出前后架构图及一个失败恢复故事 | 架构正文已修；人工运行时图和完整案例故事待收口 |
| 按 Feature 控制上下文 | 00 共用设计系统＋总览连接/部署/启动/复制/知识/函数/用户数据/插件/记录，各自独立 `.figcraft`、状态稿、交接卡 | 10 原生文件与9份独立 handoff 存在；00/02/05/06 原生结构已抽查 |
| 共用 tokens | 全稿标识同一 token 版本；主文本、按钮、输入、状态、焦点、明暗色变量有来源，Feature 不自行分叉 | manifest 与9 Feature 的 v1.0.0、规范化 JSON SHA256 一致；原生样本保留16明暗变量 |
| 一致状态与交互 | 未配置、加载、空、失败、冲突、部分恢复、unknown、正常；按钮禁用理由、返回保留输入、键盘/窄窗可检查 | 有状态板与真实导航；原生返回已用back、窄窗主动作已补；完整任务变化与实际键盘仍未通过 |
| GUI↔CLI | 表中逐项给出按钮→确切 operation ID→关键输入→授权→结果/失败→证据；区分 Manager CLI、通信 CLI、本地服务 | Feature 列出 operation ID 与归属，尚非完整逐按钮映射；函数归属标注正确 |
| 可复刻演示 | 固定版本、命令、合成夹具、输出位置、预期回执；演示预览→隔离应用→回读→恢复及冲突/unknown，不依赖私人目录 | 四份 manager 文档与脚本已有基础；仍需串成一条清晰演示路线 |
| 实际/模拟证据 | 截图与结果显式标注设计稿/离线合成/真实资料只读/真实 Host/已安装；记录产品、时间、PID/Host 与证据范围 | 实施报告标注清楚；实机/VM 尚未验收，不得用合成绿灯替代 |
| 可安装与回退 | 完整 Host/PML/桌面/CLI 清单、哈希、先决条件；实际目标文件/EVARS 差异、占用与备份/恢复预览 | 仅 Manager CLI 包；tasks.md:28/74 仍未完成 |
| 截图可读性 | 单 Feature 主屏与关键失败状态的原尺寸明/暗 PNG，附尺寸与状态；总图仅作索引 | 指定5张新图无可见文字裁切，明暗一致；未全量目视，不能外推所有画板 |

## 函数管理必须是独立 Feature

**函数登记已存在，不能答“还没有”。** `src/YuzuhaToolkit.Mcp.Core/PmlFunctionRegistry.cs:53/56/76/83` 提供列表、登记/重登记、授予/撤销许可、调用前校验；`:71` 登记默认 `AllowInvoke=false`；`:125` 源码 SHA256 改变会拒绝调用。

- 已有 GUI：`src/YuzuhaToolkit.Desktop/SessionWorkbenchView.xaml:211/221/260/261/277` 提供登记、授予、撤销和执行入口；`SessionWorkbench.cs:114–123` 在本机 registry 操作后经 Host 适配器调用。
- 已有 CLI：`src/YuzuhaToolkit.Mcp.Core/CommunicationOperationCatalog.cs:166–169` 注册 `pml.function.register`、`pml.functions.list`、`pml.function.permission`、`pml.function.invoke`。登记/许可要求写授权，调用要求目标与执行授权。它们属于通信 CLI；`ManagerOperationCatalog.cs:65–92` 当前没有函数操作转接，只有 connection.* 等管理操作。
- 已有边界：`McpWorkbenchHost.cs:54–58` 把调用转至通信子进程；`CommunicationOperationCatalog.cs:47–48` 再核对许可与源码。登记不是部署或已加载证明（registry.cs:9/42）；许可不是通用 PML 沙箱。
- 明确限制：从 `.pmlfnc` 解析唯一声明，仅 STRING/REAL/BOOLEAN 标量（registry.cs:59–69）；没有独立“删除登记”API，没有保留旧源码正文，不应画成已交付的删除/历史 diff 能力。
- 设计必须分开表达：本机登记、AllowInvoke、advisory trust、目标会话实际加载。注册表不保存 session 绑定，`docs/design/pmltwin-v08/README.md:31` 的 bound session 只能是旧设计意图；本轮需写“调用目标”，不能冒充登记字段。
- 必须有的演示：空库→源码登记（默认拒绝）→授权→参数预览→单次调用回执→源码改变拒绝→重登记后再授权→撤销；unknown 不自动重发。现有 `FunctionRegistryTests.cs:17/27/35` 等只能支撑离线规则，不能证明 AVEVA 已加载。
- OpenSpec M5 应新增函数 Feature、操作归属和验收项；当前 tasks.md:53–59 未独立列出，不能在“总览/插件/记录”中隐去。

## 具体问题与修正建议

以下定位保留初审行号，已修复范围与剩余问题见文末 Resolved，不将旧问题重复计作当前缺陷。

### P1-1 · 当前状态仍写成等待凭据，阻碍执行且误导交接

`openspec/changes/separate-cli-manager-deployment/tasks.md:3/53/54`、`implementation-verification.md:3/57` 仍要求等待 DeepSeek 凭据；最新授权已允许当前模型自行设计。更新当前任务/尚未验收段落和 M5 交付条件，保留“曾等待”的时间记录；不要为此把设计或 GUI 任务直接勾选完成。`design.md:142–150/173` 和 `proposal.md:17` 同步注明 DeepSeek 为可选设计渠道，FigCraft 仍是可编辑源。

### P1-2 · 架构故事与当前代码不符

`docs/reference/architecture.md:7` 声称通信 Core 直接复用 Knowledge.Core、Desktop 直接复用这些服务；`:9` 的运行时叙述漏掉管理 CLI/通信子进程与兼容组合层。与 `src/YuzuhaToolkit.Mcp.Core/YuzuhaToolkit.Mcp.Core.csproj:12–16`、`src/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.csproj:18–21` 及 `manager-cli.md:89–91` 对照已陈旧。修正正文并复查 `docs/architecture/runtime.c4`，说明 Desktop 仍引用 Core 的契约/登记类型，并不直接构造 RPC 实现。

### P1-3 · OpenSpec 当前源码定位有三处断链

扫描所有 OpenSpec Markdown 本地链接与四份 manager 文档，发现三处不存在路径，均在 `openspec/changes/separate-cli-manager-deployment/design.md`：

| 行 | 旧定位 | 当前建议定位 |
| --- | --- | --- |
| 19 | `src/YuzuhaToolkit.Mcp.Core/NativeCliHost.cs` | `src/YuzuhaToolkit.Cli.Compatibility/NativeCliHost.cs` |
| 19 | `src/YuzuhaToolkit.Mcp.Core/NativeOperationCatalog.cs` | `src/YuzuhaToolkit.Cli.Compatibility/NativeOperationCatalog.cs`＋`Mcp.Core/CommunicationOperationCatalog.cs` |
| 24 | `src/YuzuhaToolkit.Mcp.Core/LaunchOperationCatalog.cs` | 现实现 `src/YuzuhaToolkit.Manager.Core/LaunchOperationCatalog.cs`；旧入口转接另见 `Cli.Compatibility/LaunchOperationCatalog.cs` |

该表 `design.md:20/25/27` 也仍在叙述拆分前状态。应标“2026-10-29 基线”并附“当前定位/实施状态”列，避免把保留的基线当当前结论。四份 manager 文档的本地链接本次未发现断链。

### P1-4 · 包的交付范围必须与标题一致

`implementation-verification.md:40`、`docs/reference/manager-cli.md:76` 已诚实说明只是框架依赖的 Manager CLI 包；`scripts/Build-Manager.ps1:31–34` 也声明通信与插件分发独立且 installed=false。作品集应称“管理 CLI 验证包”，不可称“新管理器完整安装包”。完整安装与实际回退前保持 tasks.md:28/74（任务 3.6/8.4）未完成。

### P2-1 · 新文档不可从主入口发现

`README.md:9/42`、`docs/reference/index.md:10`、`docs/reference/toc.yml:10–15` 没有四份 manager 文档入口。增加“管理器（候选）”入口，链接 CLI/启动复制/知识/插件与新 Feature 设计索引，标注可用层级；避免读者只看到旧 46 项入口，误以为新管理操作不存在。

### P2-2 · 源码导航分类未跟随程序集迁移

`docs/reference/modules.json:2–95` 项目清单缺 Manager.Core/Manager.Cli、Communication.Contracts、Plugins.Contracts、Builtins、Cli.Compatibility 和两个插件项目；`:130/165/172` 的正则仍把 Builtin/Native*/LaunchOperation 归旧目录。补齐项目/分类规则并重生成导航，再核对移动类型的 xref；`docs/reference/api/index.md:16` 也应说明 NativeCliHost/Catalog 的当前程序集归属。

### P2-3 · 候选规范的“未实现”应分层说明

`specs/manager-cli-boundary/spec.md:7` 的“尚未实现”、`specs/knowledge-import-validation/spec.md:7` 的“已有库导入及完整检查闭环尚未交付”与实施报告的后端完成情况冲突。规范仍可保持候选/未 sync，但状态应区分“已实施且离线验证”“GUI 待接线”“实机未验收”；不要把候选状态等同未写代码。

固定提交链接与 `extract-existing-contracts/evidence.json` 的旧 file 字段是历史取证，不属于应直接重写的断链；应保留快照并新增当前源码迁移对照。`separate-cli-manager-deployment/evidence.json:52/56` 同理。

## 本次独立检查记录

- 阅读 tasks、implementation-verification、四份 manager 参考文档与五个历史设计入口；检查编译依赖声明、操作目录和函数 registry/GUI 调度源码。
- 初审 OpenSpec＋四份 manager 文档发现 3 个相对链接断链，均列于 P1-3；文档修复后的复查为 0。外部网站和生成后的 DocFX xref 未验证。
- 目视抽查 `docs/design/current-workbench.png`：1440×1024 的日志主屏、合成标识与空闲/离线范围可识别；仅能证明该旧正式 WPF 页面。未据此判定本轮新管理器界面通过。
- 目视抽查 `docs/design/pmltwin-v08/preview-day.jpg`：19 屏总图适合目录浏览，正文缩小后不可读；本轮必须另交单 Feature PNG。
- 新 Feature 的指定截图和原生结构已完成下述只读抽查；任务级交互及生产实现仍为待验收。

## Resolved · 本次授权的文档修复

| 初审问题 | 已完成修复 | 保留边界 |
| --- | --- | --- |
| P1-1 / P2-3 当前设计条件与状态陈旧 | `design.md` D8 改为当前模型可自行设计，00 基础＋01–09 独立 Feature、同版 tokens、逐 Feature handoff、唯一 FigCraft 编辑者；六份候选 spec 状态区分后端离线/GUI/实机 | 未宣称 DeepSeek 鉴权成功；tasks、proposal、implementation-verification 由根 Agent 同步；不 sync/archive |
| P1-2 架构事实陈旧 | `docs/reference/architecture.md` 说明 Manager/通信/兼容边界、Desktop 契约引用与独立通信子进程 | `runtime.c4` 仍待架构编辑者同步，本页已明确标记旧关系不能当当前交付图 |
| P1-3 三处源码断链 | 当前源码表链接迁至 Cli.Compatibility/Manager.Core，并补通信目录、现有部署/知识定位 | 固定历史快照未改 |
| P2-1 新文档无导航 | `toc.yml` 新增候选管理器与四份参考说明入口 | README/index 主入口仍由根 Agent 维护 |
| P2-2 模块遗漏 | 实际配置文件 `docs/reference/modules.json` 补 8 个项目、4 个业务分类，更新 Builtins/Native/Launch 迁移匹配 | 未生成 DocFX 站点；API xref 后续仍需随完整文档构建复核 |
| 旧 MCP 接入指导 | `docs/aveva-discovery.md` 改用现行原生/管理 CLI、冻结预览与授权应用，旧脚本链接明确标历史用途 | 不承诺 MCP-only 客户端可直接使用 CLI；不改变安装/环境 |
| VM 证据读取参数错误 | `manager-plugins.md` 修正 `vm.task.read` 为 `outputDirectory + attemptId`，说明 1 MiB 预算和 running→unknown | 保留 VM/快照指纹变化时再次 Execute 被拒绝的设计限制，读取旧结果不表示当前 VM 状态 |

轻量复查结果：OpenSpec `validate separate-cli-manager-deployment --strict` 通过；modules JSON 与架构白名单精确覆盖 23 项目、16 分类，172 个项目 C# 源文件无未分类；分类指南存在；TOC YAML 可解析、21 个目标有效。OpenSpec/manager 文档及架构/发现说明共检查 72 个相对链接，0 断链；修改文件无行尾空白。未运行整套 build，也未据文档校验提升生产验收状态。

## 新 Feature 成品只读抽查

检查对象：manifest/tokens、02/05/06 handoff；`view_image` 实际查看 `06-functions-light.png`、`06-functions-dark.png`、`06-functions-registration.png`、`02-deployment-narrow.png`、`05-knowledge-states.png`。1440×960 主屏、960×1000 登记、960×1120 部署窄窗及1440×900状态板均未见文字裁切；部署窄窗已转为上下卡片。主屏与修正后的登记独立页都有“设计原型·合成数据”标记。

初次只读 CLI info 与原生 ZIP 内 document.json 确认：00 有52节点；02/05各298节点；06有325节点，其中205文字、2组件、10组件实例、16变量、2个flow及10条实际点击导航，无导入修复警告（修正后节点数见下）。原生保留可编辑节点，不是嵌入截图。9 Feature 的 token version/hash 与规范化 tokens JSON 一致；18组正文/状态文字配色抽算最低5.04:1，仅为所选颜色对检查，不能当作完整可访问性认证。

函数图正确区分本机登记、调用许可、人工信任、会话实际加载；源码哈希与目标会话另列。登记页从源码解析签名、默认关闭许可与重登记撤权的表述符合实现；没有把登记画成自动加载。handoff 明确函数在通信 CLI/旧GUI本机登记，Manager CLI 统一入口仍待补齐。

| 优先级 | 成品初次抽查问题（后续状态见下） | 最小修正/通过条件 |
| --- | --- | --- |
| P1 | 部署图“本次变更”写 `YUZUHA_ROOT` / `YUZUHA_PROFILE`（`source/features/02-deployment.json:42–43`），代码实际写 pmllib、PMLUI/PDMSUI、Yuzuha、YuzuhaFramework（EnvironmentTextEditor.cs:72–76） | 改成真实键与载荷路径；示例也必须对应实际 EVARS 计划 |
| P1 | 知识状态板“结果未知/待复测”卡写“活动写入：取得一致快照后重试”；活动写入在预检被拒绝，并非已发出后无回执 | 把占用归为拒绝/待处理，另画真正 unknown 及只读回收/核验下一步，避免暗示可自动重试 |
| P2 | 函数“预览调用”与“审阅状态”都只到状态总览，登记提交也到总览；授权/撤销、参数预览、源码改变拒绝与 unknown 回收没有任务链 | 至少补一条登记→默认拒绝→授权→参数预览→调用回执→变化拒绝/撤销的可审阅路径；否则仅称导航骨架 |
| P2 | 状态页返回固定 Light frame54；Dark 和960窄窗入口都会返回1440浅色主稿 | 各入口保留对应主题/视口返回，或明确原型仅导航审阅、不验证返回保持 |
| P2 | handoff 仅列 operation ID，没有逐按钮输入/授权/回执/错误表；窄窗仅留“审阅状态”，核心部署动作路径未呈现 | 补齐每个主动作映射；窄窗提供可发现的主操作或说明其位置与滚动策略 |

**初审判定：结构、视觉基础与上下文拆分可接受；流程完整性未通过作品集完成门槛。** 初审问题已通知唯一 FigCraft 编辑者，本审阅者未改原生设计。

## Resolved / Remaining · 修正导出复查

再次用 `view_image` 只读查看 `02-deployment-narrow.png`、`05-knowledge-states.png`、`06-functions-registration.png`，并用 CLI info / 原生 document.json 核对定义，没有通过生成脚本推定修复成功。

| 状态 | 复查证据与结论 |
| --- | --- |
| Resolved · P1 EVARS | 02 窄窗显示真实 `pmllib`、`PMLUI`、`Yuzuha=E3D2.1`、`YuzuhaFramework=net48`，路径与当前源码约定一致；未再出现虚构键 |
| Resolved · P1 状态语义 | 05 状态标题现为对应真实场景，“活动写入”独立显示且不再归入 unknown；保留输入/检查项的说明可读 |
| Resolved · P2 返回定义 | 02/05/06 状态页与06登记页的原生动作确为 `type:back`，没有固定Light/1440目的地；已修复错误导航定义，未实际演播来验证框架历史栈或输入保持 |
| Resolved · P2 窄窗/标识 | 02 窄窗可见“预览部署＋审阅状态”；原生02/05窄窗各2个带导航实例，06窄窗3个；登记页已补设计/合成标识。三张图均无可见文字裁切 |

修正后 CLI info：02/05各300节点，06为331节点；三个样本均16变量、2个flow，无修复警告。保留的主动作仍导航到状态审阅，因此只验收草稿导航定义，不视为业务流程实现。

Remaining：完整函数登记→默认拒绝→授权/撤销→参数预览→调用回执→源码变化拒绝/unknown证据回收任务链，以及逐按钮输入 schema / 授权 / 回执和错误映射仍待下一阶段。静态图不能证明真实表单校验、键盘可访问性、WPF 绑定、CLI 同行为或产品实机结果。

**本轮可接受为“00共享基础＋9 Feature独立可编辑拆分草稿”；不能标作品集完整通过，也不足以勾选任务6.2/6.3。** 完整流程与生产验收门槛维持未完成，本次复查只将上述具体缺陷转为 Resolved。
