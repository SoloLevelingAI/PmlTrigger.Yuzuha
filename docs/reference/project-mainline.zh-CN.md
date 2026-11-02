# 项目主线与续作顺序

Phayton 的主线是 **准备环境 → 启动并核验目标会话 → 完成一次 PML 开发或只读核查 → 回读结果与恢复**。安装、契约图集和源码导航共同支撑这条任务链。产品完成标准仍来自 canonical 的 G1 开发、G2 三维核查、G3 可持续接手，见[规格](specs/index.md)。

## 三份输入的关系

核对日期为 2026-11-02。当前本地工作分支 `codex/aps-contract-implementation` 已从 `bb52df4` 快进到 PR #14 的 `328e8ef`，本轮修复在此基础上进行；GitHub 两个 PR 尚未合并。

| 输入 | 已提交内容 | 在主线中的作用 |
| --- | --- | --- |
| [PR #13](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/pull/13)，`bb52df4` | Inno 交接、原生 WPF 准备页、共享 Manager 契约、部署与恢复、安装及修复候选证据 | 建立进入工程的条件；八阶段止于严格会话核验 |
| [PR #14](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/pull/14)，`328e8ef` | 补齐设计、契约、操作输出、Script 片段和 Roslyn API 追踪，修正阅读层级 | 让已有行为可以读懂和追查，不代替环境与业务验收 |
| [下午提交](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/commit/39a8d092bd3243e4ce6aac4941c8197936b43062)，16:29，`39a8d09` | `enable-native-pml-agent-workflows/.openspec.yaml`，仅 `schema` 与 `created` 两行 | 分支名指向原生 PML 代理任务；没有提交 proposal、design、specs、tasks 正文，无法据此认定完整方案或开始执行其中任务 |

下午分支 `codex/openspec-native-pml-agent-loop-20261102` 从 PR #14 中间提交 `1d07de9` 分出，未包含随后 `328e8ef` 的阅读修复。因此不能直接用下午分支覆盖 #14。本页的续作建议依据现有要求和源码整理，并非对缺失 GPT 正文的还原。

## 一次任务由谁负责

| 环节 | 入口与实现 | 成功需要证明什么 |
| --- | --- | --- |
| 准备环境 | [安装到工程操作](install-to-operation.zh-CN.md)；`ManagerContractCatalog`、`PreparationWorkbench`、部署与 EVARS 服务 | 明确环境、冻结预览、逐项应用回执和可核对恢复；应用安装与接入环境分别证明 |
| 启动与选定目标 | [启动工作流](launch-workflow.md)；`LaunchWorkflowService`、`DesktopCliSession` | 持久受理后只尝试一次；进程创建、登录、Host 加载及 PID/启动 ticks/模块核验分别记录 |
| 完成 PML 开发 | [PML 开发流程](pml-development-flow.md)；`PmlDevelopmentWorkflow`、`PmlDevelopment.psm1`、`Invoke-PmlDevelopment.ps1` | 实际 PMLLIB → 冻结复制与备份 → REHASH → 指定 Object/Form 重载 → 验证宏 → 结果精确比对 |
| 完成只读核查 | [对象读取](phayton-object-walker.md)、[窗体查询](form-gadget-query.md)；独立通信 CLI 与 Host | 同一明确目标的有界数据、真实下标、截断与续页；读取成功不等于业务判断正确 |
| 回读与恢复 | 原操作回执、开发报告、部署恢复服务 | 保留部分结果与冲突；unknown 不自动重发，文件恢复不宣称撤销宿主内已执行的 PML |

WPF 和管理 CLI 复用 Manager 用例；通信经过独立 CLI、PID 专用 RPC 和 NET35/NET48 Host。设计图、DocFX 与图集呈现相同契约及静态源码，不承担第二套执行逻辑。[运行关系](architecture.md)已同步去掉旧的桌面直接启动边与易陈旧操作数量。

## 续作顺序与完成标准

### 本轮默认帮助与开发参考拆分

用户已在2026-11-02选择先做分层交付，再完成27.16/27.10。`003f7e9`的完整候选已通过7179项载荷核验和本机普通升级预览，但未安装；该候选与预览保留为历史，不继续执行。它包含4727页/181.39MiB完整文档，其中UI静态追踪3004页/104.49MiB；默认载荷还包含62MiB文档源档案。这里62MiB是额外开发档案，不能全部称作逐字节重复内容。

新的默认用户站只保留准备、核验、开发或只读核查、回执与恢复等任务帮助，目标不超过40个HTML/15MiB，目录与搜索独立。完整开发站保留原有API、源码、测试和追踪路径，连同源码档案单独交付Developer Reference ZIP，按源码提交与清单SHA绑定配套身份。工作台明确区分两个入口；开发参考仅按用户选择的解压目录挂载，不后台下载、解压或写设置。新用户站不提供旧安装遗留但未登记的API/追踪文件，旧文件也不静默删除。实际新载荷体积、链接和新升级预览归第28组验收，不以目标数字代替测量结果。

分层候选现已按固定源码`04a3c94`完成：默认11页/10.44MiB，完整开发站4807页另包交付，新载荷较003完整候选减少约33%。334项应用回归、338请求实际双站验收及精确源码六项云端检查通过；用户批准新具体预览后已正常安装exit0，当前C的1224项载荷、15项托管及首次原文核对通过，默认站209请求通过，27.16完成；27.10旧所有权/环境/实机与用户视觉仍开放。详见[分层交付验证](../validation/documentation-split-2026-11-02.json)；这份后续审计不重标已冻结组件的源码身份，也不宣称旧磁盘空间已回收。

1. **闭合环境准备的剩余验收。** 27.16已完成04a3c94分层包的具体批准、普通升级与实际C字节/HTTP核验；27.10仍需旧ENV所有权移交、生产EVARS/部署、AVEVA核验和用户视觉认可。安装后新所有权移交预览已准备，ENV字节保持、不授予部署；每个实际写入仍使用各自新具体预览和原回执，不用旧已消费计划。
2. **再闭合一个可复刻的开发任务。** 首选已有 `tests/ai-pml/development-probe`，复用当前编排，记录所选目标、来源与目标 SHA、加载步骤、预期与实际结果。先跑隔离 CLI 夹具，再在明确选定并授权的宿主核验同一任务；开发报告必须保留失败、部分写入和未知结果。
3. **沿同一目标加入一项只读核查。** 从 CE 或明确 DBREF 的有界读取开始，保存本轮数据和判断依据，再扩展到对象与视图关系。默认官方知识和 PML 自动发现仍有缺口，先接通任务实际需要的来源，不将个人检索片段当成官方版本匹配证据。
4. **按任务需要推进平台扩展。** Database 活动存储切换、CAF、许可/VM 和更多 Ribbon 工作流各自保留独立边界。首个 PML 任务完成不依赖它们同时整体交付；跨产品实机不能从 E3D 2.1 历史证据推导。

## 本轮尝试修改

- 源码链接按文件内容绑定真实提交；源码与提交不一致时保留本地片段，不制造 GitHub 来源。修正高亮再次替换自身 HTML 的问题。
- 图集静态服务与实时修订通过只读路由访问已构建 DocFX；实时发布包含页面引用的 `snippets.js`。缺失文档仍明确返回 404。
- PML 开发脚本把首次核验的完整启动 ticks 绑定到后续 status、PMLLIB 读取及命令派发，防止 status 后同 PID 被新进程复用。
- README、文档入口与架构图围绕同一任务链组织，保留安装、环境、业务和人工验收各自状态。

本轮验证：

| 检查 | 结果与范围 |
| --- | --- |
| `npm run contracts:test` | 79/79；包含图集发布、只读 DocFX 路由、冻结源码与高亮回归 |
| `pwsh -NoProfile -File tests/pml-development.test.ps1` | 8 个模块场景与 4 个独立合成 CLI 进程场景全部通过；保留 PID 复用红绿回执于 `artifacts/tests/pml-development-cli*` |
| `node --test tests/documentation-tests.test.mjs tests/documentation-inventory.test.mjs` | 14/14；文档目录与来源映射回归 |
| `node tests/contract-atlas-entry-browser.mjs` | 明确选择本机 Edge，8 个页面场景通过；不代替用户视觉认可 |
| `npm run contracts:check`、`npm run architecture:check`、`npm run architecture:validate` | 生成数据一致，LikeC4 两个文件有效 |

首轮 PATH 检查未找到 SDK 10.0.302 和 OpenSpec，因此上表未包含整站构建或 OpenSpec strict。续作已在本机仓库缓存找到并实际核验 SDK 10.0.302、OpenSpec 1.15.0 和 Inno 6.7.3，无需安装或修改机器配置；27.16 现在以已提交源码重建完整候选。DocFX 路由回归本身仍使用明确标注的隔离 HTML 和源码。环境、升级及人工验收继续按平台任务分别留证，不从缓存工具可用推定任务完成；canonical 状态保持，不执行 sync 或 archive。其他测试入口见[测试说明](testing.md)。
