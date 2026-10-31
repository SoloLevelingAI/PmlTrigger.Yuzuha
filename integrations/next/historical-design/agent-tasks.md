# 最终 Agent 任务清单：待用户确认，未派发

本清单取代此前追加式任务说明。依赖已批准，不重复请求同一选型批准；本清单的执行授权仍待用户确认。当前不创建工程、不安装依赖、不启动或唤醒 Agent，不修改原项目和 rewrite。

## 1. 共同目标、范围与批准项

只交付一个端到端 Feature：**本机发现应用 → 选择候选 → 修改预填 → 保存配置 → 按配置启动 → 查看状态与快照日志**。用户无需理解内部架构才能使用；架构为后续 Feature 提供可复用的输入、执行、记录与验证机制，不额外建设通用插件平台或增加业务功能。

候选独立项目根：`C:/Users/26648/Documents/ChatGPT/Example/LaunchDesk.Next/`。下面的写入路径都相对于该根；当前根目录尚未创建。原 `LaunchDesk.Archon` 与 `LaunchDesk.Archon.OpenSpec`（包括所有 rewrite、配置及设计资料）只读；可按授权复制必要 UI 资源到新项目，不回写、不共享编译源、不引用旧程序集。原数据、凭据、许可服务与安装不修改。

共同输入：[八股工程方案](launch-by-profile.md)、[客户简介](customer-brief.md)、[共用架构及 FigCraft 规则](architecture-and-design.md)、[已批准依赖与验证计划](dependency-review.md)、[OpenSpec 变更记录](openspec/changes/csharp-discovery-and-evidence/proposal.md)。依赖版本只在依赖报告维护，本清单引用 DEP-01—06，避免重复维护版本表。

已批准：NLog、CommunityToolkit.Mvvm、NUnit/Adapter/Test.Sdk、Coverlet、FlaUI，以及用户指定的 ArchUnitNET；FigCraft 用于设计，不是产品运行依赖。C# 实现产品能力，代理过程可用 PowerShell；新增语言或未批准的库/工具仍需单独评审。Inno Setup 不在本轮安装交付范围。批准包不等于行为验证已通过，NLog 的写入失败契约不能省略。

## 2. A：界面规划与前端交互

**模型：`gpt-6.1-sol`。唯一 FigCraft 编辑者，所有 FigCraft 操作由 A 串行完成。**

**任务：**

1. 只读核对原视觉基线与可复用资源；在新 FigCraft 文件规划发现候选、预填编辑、保存和启动反馈。覆盖多候选、空结果、字段错误、等待、失败、未确认及窗口出现；复用组件、变量和原型关系，导出后实际查看预览。
2. 按确认契约实现 ViewModel 的显示/格式校验及 LaunchInteraction 的显式事件调用。初始化 LaunchInput，提交不可变 LaunchRequest，接收 LaunchResult/事件；ViewModel 不持有协调器，绑定和属性 setter 不执行后台业务。
3. 接入 C 的 C# 发现结果，保留发现/预设/人工修改来源，多个候选可选、全部预填可改，重扫不覆盖人工编辑；接入 B 的配置保存和运行状态，切页只取消订阅。
4. 使用已批准 Toolkit 通知/校验与 FlaUI 界面验证；不因包内存在 RelayCommand、Messenger 等就自动使用。密码不进入普通输入对象或日志。

**独占写入范围：**

- `src/LaunchDesk.Next.Desktop/Features/Launch/**`
- `src/LaunchDesk.Next.Desktop/Assets/**`
- `tests/LaunchDesk.Next.UiTests/**`，项目文件除外
- `docs/design/launch-workbench.figcraft`、`docs/design/previews/**`
- `artifacts/frontend/**`

**交付物：** 原 UI 基线与资源来源清单；可编辑 FigCraft 原稿及已检查预览；前端代码；FlaUI 结果、失败截图/控件树、输入与快照关联记录；UI 差异说明。文档修改建议交 C 合并。

**验收：**

- 用户能从实际界面完成候选选择、修改、保存、启动与状态查看，不能只有静态页面或 VM 测试。
- UI 操作顺序与状态含义保持原目标，原布局/图标/颜色/间距等基线有可视对比；工程化不得降低美观，差异明确列出。
- 输入改变不修改已提交快照；错误修正后提示更新；切页不停止后台观察；Created/窗口出现不显示为项目登录成功。
- FigCraft 只有 A 一个编辑者；其他 Agent 仅提出文字意见，不竞争编辑文档。

## 3. B：输入契约、后端执行与证据记录

**模型：`gpt-6.1-sol`。**

**任务：**

1. 先交 LaunchInput、LaunchRequest、LaunchResult、Record、发现候选 DTO 和所需端口的最小契约；明确字段、状态、错误、秘密生命周期和取消。A/C 共同核对后使用，避免各写一套转换和类型。
2. 第一条可运行流程就有 `List<Record>` + Log。先执行 V-LOG，核验不可变快照、脱敏、顺序、可重读、写入失败与刷新；通过后接入副作用，不能以 logger 返回代替落盘结果。若 NLog 无法满足，提交证据及替代建议，不擅自换库或维护两份权威日志。
3. 实现独立配置保存/读取、版本核对、参数规则、许可检查、秘密解析、子环境和进程创建；LaunchCoordinator 明确串联这些能力，不读取控件或 ViewModel。
4. 处理配置级准入、同 AttemptId 重入、创建前失败与创建后不确定结果；运行注册表拥有观察和状态，页面订阅取消不强杀目标。不引入未经约定的持久恢复承诺。

**独占写入范围：**

- `src/LaunchDesk.Next.Application/**`
- `src/LaunchDesk.Next.Domain/**`
- `src/LaunchDesk.Next.Platform/**`，**排除 `Discovery/**`**
- `tests/LaunchDesk.Next.UnitTests/**` 与 `tests/LaunchDesk.Next.IntegrationTests/**`，**排除 `Discovery/**`**
- `artifacts/backend/**`

所有项目文件均由 C 管理；B 不写 ArchitectureTests、FigCraft 或最终文档正文。

**交付物：** 可评审的契约摘要与对应代码；完整正常/失败流程；Record 和脱敏日志样例；NUnit/TRX、覆盖率及故障注入记录；NLog 适配结论及尚未通过事项。

**验收：** V-LOG/V-VM 中的后台边界与 V-TEST/V-COV 相关场景通过；绕过前端仍拒绝无效输入；真实输入改变不影响旧 Record；创建前证据写入失败不启动，创建后失败不重复启动；秘密不进快照；窗口观察与实际登录分开。使用临时目录和隔离进程，不借测试修改原环境。

## 4. C：本机发现、工程装配、架构守卫与文档

**模型：`gpt-6.1-sol`。任务分阶段推进，不与 A/B 争写文件。**

**任务及验收：**

| 子任务 | 交付物 | 必须满足的验收 |
| --- | --- | --- |
| C1 独立骨架与依赖 | `.slnx`、项目文件、批准版本及实际解析/许可清单、组合根 | 无旧程序集/共享编译源依赖；不修改原项目；仅采用批准选型；新增问题显式记录 |
| C2 本机 C# 发现 | 产品专用发现器、可配置搜索根/上限/预设、命令行验证工具、本机对照清单 | 只读盘点已知安装，再用实际 C# 逻辑找到对应路径；桌面与工具调用同一实现；不能只用模拟目录或空实现 |
| C3 ArchUnitNET | ARC-01—06 测试、非空加载检查、违规夹具及 V-ARCH 报告 | 正确依赖通过，每条有意违规被识别；未加载程序集/空目标不能假通过；报告能指出规则与违规位置 |
| C4 装配与文档 | 可运行工程、DocFX、OpenSpec、源码索引、客户八股与工程说明、Git Diff | 完整切片可操作，链接/构建可验；六类问题有实质答案，事实只维护一处，客户不必读内部架构才能使用 |

**独占写入范围：**

- `LaunchDesk.Next.slnx`、所有项目的 `*.csproj`、必要的 `Directory.Build.*`、`Directory.Packages.props`、`global.json` 与依赖锁定文件
- `src/LaunchDesk.Next.Desktop/Bootstrap/**`、`App.xaml`、`App.xaml.cs`（仅外壳与装配，页面视觉交 A）
- `src/LaunchDesk.Next.Platform/Discovery/**`、`tools/LaunchDesk.Next.Discovery/**`
- `tests/LaunchDesk.Next.UnitTests/Discovery/**`、`tests/LaunchDesk.Next.IntegrationTests/Discovery/**`
- `tests/LaunchDesk.Next.ArchitectureTests/**`
- `docs/**`，**排除 A 的 FigCraft 原稿与 previews**；`openspec/**`
- `artifacts/discovery/**`、`artifacts/architecture/**`、`artifacts/integration/**`、`artifacts/documentation/**`

**本机发现专项验收：** 输出扫描时间、配置/实现版本、范围、已知安装清单、候选和失败诊断；逐项比较预期与实测，记录未找到的原因。项目/MDB/许可等预设须标记来源且能修改；多候选不能静默取第一个；超时/权限失败可见；重扫不覆盖人工配置。至少完成本机实际已安装目标的证据验证；不存在的产品标记未验证，不用假路径代替。扫描不启动目标应用、不改注册表/安装/凭据。

**ArchUnitNET 专项验收：**

- ARC-01：Domain 不依赖上层、WPF、Toolkit 或日志实现；不以笼统 System 白名单掩盖副作用。
- ARC-02：Application 不依赖 Desktop、具体 Platform、WPF 或 Toolkit。
- ARC-03：ViewModel 不依赖协调器、平台适配或 OS 副作用 API。
- ARC-04：输入与请求信息类不依赖 WPF、ViewModel、通知或命令接口。
- ARC-05：Desktop 仅 Bootstrap 能依赖具体 Platform，按类型/命名空间验证例外。
- ARC-06：Platform 不反依赖 Desktop，模块无环，产品无旧项目和测试依赖。
- Debug 架构检查与 Release 构建分开；项目引用/共享源另检查，XAML、反射和运行副作用不靠 ArchUnitNET 单独证明。违规夹具留在测试范围，不污染产品。

## 5. D：独立评审与复核

**模型：`gpt-6-astra`。不参与作者实现，不操作 FigCraft。**

**任务：** 先只读客户简介及工程文档，回答价值、范围、操作、失败、前后端职责、基础能力、扩展与验证；记录无法回答的问题后再查源码，不能靠代码补答案后判文档通过。核对 A/B/C 全部交付，独立复核本机发现、日志证据、ARC 违规反例和视觉对比。

**独占写入范围：** `artifacts/review/**`。只读其他文件，可在自己的评审输出目录生成测试结果；不修改作者代码、项目文件或设计稿，修复由原作者完成。

**交付物：** `independent-review.md`、六类问题覆盖表、问题/证据/影响/责任人清单、复核记录；区分源码观察、自动测试、本机实测、人工视觉核对与尚缺真实厂商环境的验证。

**验收：**

- 实际复核“发现 → 预填 → 修改 → 保存 → 查看快照”及启动失败/取消路径；不能只引用作者的 PASS。
- 检查全部 ARC 守卫是否能抓住违规、FigCraft 是否保留原视觉目标、本机发现是否真实。
- 核查 UI 绑定无业务调用、VM 无越权、请求/Record 不可变、秘密无泄漏、不确定结果不重启、原项目无修改。
- 阻塞问题修复后复核；未完成/未具备条件的验证留缺口，不能用“框架很完整”替代用户任务成功。

## 6. 执行顺序与文件交接

1. **用户确认本清单后才启动。** C 建独立骨架，B 提交最小输入/记录/发现契约；A 可并行做只读 UI 基线与 FigCraft 规划。
2. A/C 核对 B 的契约后：A 实现交互，B 实现证据与执行，C 实现发现及守卫。调整共享契约必须说明调用方影响，由 B 修改并通知 A/C。
3. **第一切片必须同时有本机候选、可编辑预填和可重读 Record/Log**，不能先堆完界面或抽象再补可验证性。
4. C 装配端到端流程、整理 DocFX/OpenSpec 和 Git Diff；A 检查视觉，B 核对异常与秘密边界。
5. D 作为独立审阅 Agent 在前三者交付后执行；原作者修正，D 复核。三名执行 Agent 加一名独立评审 Agent，不要求四者同时运行。

所有权优先：C 独占项目配置文件；A 独占 FigCraft 与预览；C 独占 Discovery 和 ArchitectureTests；B 独占其余后台；D 只写评审目录。新文件先按职责落入对应范围，跨范围变更由所有者完成，避免双方修改同一文件。

## 7. 最终交付与判定

应交付新项目源码、独立配置样例、FigCraft 原稿/预览、可运行的发现工具、Record/Log 样例、构建及测试报告、覆盖率、架构正反例、真实本机发现证据、客户八股/工程八股、DocFX/OpenSpec 索引、独立评审和 Git Diff。

完成判据是用户能走通该 Feature、维护者能看懂并验证边界；不是库安装完成、八个标题齐全或测试数量够多。真实 AVEVA 登录/许可签出若缺环境，单列未验证，不扩大为“全部兼容”。真实厂商启动和环境配置改动不在自动测试授权内。

当前状态仍为：**清单已整合，等待用户确认；未创建工程、未安装依赖、未分发 Agent。** 模型沿用既定配置；本轮不重新进行模型选型。
