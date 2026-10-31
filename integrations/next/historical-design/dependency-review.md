# 依赖选型调研与评审请求

调研日期：2026-10-27。状态：资料调研完成，DEP-01—05 已由用户批准采用，含覆盖率和界面自动化；替代方案未选用；没有安装、还原、创建 PackageReference 或运行库验证。仅修改独立设计目录，原项目/rewrite 未修改，Agent 未派发。版本取自本次实际打开的官方 NuGet 页面；不是自动跟随 latest 的配置。

## 1. 已批准范围与待验证事项

用户已批准表内主方案：NLog、CommunityToolkit.Mvvm、NUnit/adapter/Test.Sdk、Coverlet 和 FlaUI。批准采用不等于验证通过；NLog 的证据写入门槛仍需验证。xUnit、Serilog 等替代方案未被选择。表中建议栏保留调研理由，当前批准状态以本段为准。

| 决策号 | 精确包与版本 | 解决的问题／为什么需要 | 建议与替代 | 采用前验证 |
| --- | --- | --- | --- | --- |
| DEP-01 | [NLog 6.2.2](https://www.nuget.org/packages/NLog) | 文件输出、结构化记录和日志管理，避免自建完整日志设施；不能替代 Record 的定义与冻结 | 有条件候选；替代为 System.Text.Json + FileStream 单一证据写入器，或下面的 Serilog 组合 | V-LOG：顺序、脱敏、失败回传、刷新和截断恢复读取；未通过不得作为执行前证据门槛 |
| DEP-02 | [CommunityToolkit.Mvvm 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm) | 属性通知、字段校验、错误通知，减少 VM 重复模板 | 建议仅在 Desktop 使用 ObservableObject/ObservableValidator；替代为 .NET 接口 + DataAnnotations 手写实现 | V-VM：字段错误、修正、初始化、快照隔离；证明 setter 和绑定不执行后台 |
| DEP-03a | [NUnit 5.0.0](https://www.nuget.org/packages/NUnit) | 单元/集成测试发现、参数化测试、断言，以及 WPF/COM 所需 STA 测试声明 | 建议为统一框架；替代为 xUnit 方案，不同时维护两套 | V-TEST：正常/失败/跳过都正确报告，STA 场景在正确线程执行 |
| DEP-03b | [NUnit3TestAdapter 6.3.1](https://www.nuget.org/packages/NUnit3TestAdapter) | 让 VSTest/IDE 发现和执行 NUnit 测试，本身不是测试框架 | 与 03a/03c 一起评审，仅测试项目 | 命令行与 IDE 发现相同测试，输出可读结果 |
| DEP-03c | [Microsoft.NET.Test.Sdk 18.11.0](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | VSTest 测试项目的构建/执行集成 | 与 03a/03b 一起评审，不放入产品运行层 | 验证选定 SDK/IDE 下可执行与 TRX，不能只有零测试的成功退出 |
| DEP-04 | [coverlet.collector 10.1.0](https://www.nuget.org/packages/coverlet.collector) | 找出未触达的分支，辅助发现失败路径遗漏 | 可选，配本报告 VSTest 路线；替代为场景追踪与明确分支清单 | V-COV：报告含手写规则和协调器，区分生成代码；覆盖率不是行为正确性的证明 |
| DEP-05 | [FlaUI.UIA3 5.0.0](https://www.nuget.org/packages/FlaUI.UIA3) | 从界面外部操作 WPF，检查按钮、预填、修改、回显 | 可选，只在 UiTests；替代为明确记录的人工 UI 验收或直接 UIAutomation，后者代码更多 | V-UI：Windows 交互会话运行，稳定定位控件；失败留截图和 Record 关联，不能只测 VM 冒充界面 |

以上是一次候选版本组合，未执行还原或兼容性测试。拟以 .NET 10 / Windows 为验证目标；NuGet 的 TFM 兼容信息不等于这组包、SDK、IDE 已在本机联测通过。

## 2. 许可证、作用范围与依赖成本

| 包 | 页面声明的许可证 | 放置与依赖说明 |
| --- | --- | --- |
| NLog 6.2.2 | BSD-3-Clause | Platform 日志适配；包页面列 .NET Standard 目标无依赖；当前不需要 NLog.Web.AspNetCore 或 NLog.Extensions.Logging |
| CommunityToolkit.Mvvm 8.4.2 | MIT | Desktop；包页面 net8.0 目标无依赖，其他目标依赖不同；包含生成器，首版建议显式属性/SetProperty，生成属性单独评审可读性 |
| NUnit 5.0.0 / NUnit3TestAdapter 6.3.1 | MIT / MIT | 测试项目；运行器/引擎等传递依赖以后以实际锁定清单核验，不把三项直接引用误当全部依赖 |
| Microsoft.NET.Test.Sdk 18.11.0 | MIT | 测试项目，会涉及测试宿主等依赖，不进产品项目 |
| coverlet.collector 10.1.0 | MIT | 测试采集器；不额外再装另一套 coverage 平台 |
| FlaUI.UIA3 5.0.0 | MIT | 传递依赖 FlaUI.Core >= 5.0.0、Interop.UIAutomationClient >= 10.19041.0，均需在批准后的锁定清单复核 |

许可证据来自上述包页；FlaUI 另见[该版本许可证](https://www.nuget.org/packages/FlaUI.UIA3/5.0.0/License)。这里记录声明及采用时需保留的许可资料，不声称完成法律审查或传递依赖审计。不得以批准直接包为由隐瞒新增依赖；批准后先保存解析结果再进行实现，出现新增选型问题回到评审。

## 3. NLog：日志输出与执行证据不能混为一谈

官方提供结构化 [JsonLayout](https://github.com/NLog/NLog/wiki/JsonLayout)。但 [AsyncWrapper](https://github.com/NLog/NLog/wiki/AsyncWrapper-target) 的默认队列溢出策略可丢弃日志；[排错文档](https://github.com/NLog/NLog/wiki/Logging-troubleshooting)将 ThrowExceptions 限定为排错/测试手段，不建议作为生产常开机制。因此不能简单地 `logger.Info(record)` 后就宣称“记录已持久化”，也不能靠打开 ThrowExceptions 完成生产错误回传。

本项目建议的契约：先把输入复制为脱敏、不可变 Record，加入 List<Record>；证据写入器返回明确的写入结果，再决定是否允许创建进程。NLog 是否能以合理的单一路径适配该契约，需要 V-LOG 验证，不能预先声称满足。关键证据路径首版不采用默认可丢弃的异步包装；flush 完成也不保证断电不丢数据。

若 NLog 无法以简单、可证明的方式反馈每次证据写入失败，建议退回 .NET 内置 JSON + 文件写入做这一个任务，不同时维护两份权威 Record 日志；普通诊断若仍要使用 NLog，应单独说明新增价值及范围，再由用户决定。

V-LOG 验证计划（尚未执行）：

1. 输入对象变化后，内存 Record 和已写 JSON 不变；100 个并发提交的操作各自序号连续，每条 JSON 可解析。
2. 使用特殊字符、换行、长字段，逐条重读并核对字段；全文件检索预置假密码和敏感变量，必须无泄漏。
3. 故意使用不可写目标、目标为目录或故障写入器，证明调用方收到失败；创建前失败不启动，创建后失败只报告需核验，进程创建计数仍为一次。
4. 正常 flush/关闭后重读一致；模拟截断尾行时保留已完整记录并报告尾部损坏，不执行恢复启动。
5. 记录库配置、测试 TRX 和脱敏样本。失败路径未证实则 DEP-01 不具备正式采用条件。

## 4. CommunityToolkit：只减少通知和校验模板

[ObservableValidator 官方说明](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observablevalidator)说明其实现 INotifyDataErrorInfo，并继承属性通知能力；适合前端字段错误反馈。这里“需要”是减少这类重复实现，不是架构必须依赖它。

拟使用 ObservableValidator/SetProperty/ValidateAllProperties；LaunchInput 与 LaunchRequest 保持普通信息类。RelayCommand、AsyncRelayCommand、Messenger、Ioc 虽然可用，不因装了包就使用；本方案仍是按钮事件 → 薄交互入口 → 后端协调器。后端业务校验不得依赖 ViewModel。首版显式写属性更便于看清调用，源生成器只在进一步评审后采用。

V-VM：初始化正确显示；必填与格式错误可清除；字段间校验会重新检查；输入修改不改变已提交快照；属性 setter 不调用协调器；Application/Domain 不引用 Toolkit 或 WPF。架构检查先用程序集引用检查和少量反射测试，不急于引入另一套架构测试 DSL。

## 5. 测试组合为何优先 NUnit，以及替代方案

本项目既有纯规则，也有 WPF/COM 边界。[NUnit Apartment](https://docs.nunit.org/articles/nunit/writing-tests/attributes/apartment.html)允许指定 STA，这构成优先评估 NUnit 的项目理由；STA 不自动提供 Dispatcher 消息循环，真正界面流程仍需 UI 测试。测试框架内置断言先满足需求，mock/fake 初期使用少量明确的 C# 测试替身，不默认再加断言或 Mock 库。

| 替代方案，精确版本 | 能解决什么／取舍 | 验证与状态 |
| --- | --- | --- |
| [xunit.v3 4.0.2](https://www.nuget.org/packages/xunit.v3) | 同样可做单元/集成断言；该版本包实际依赖 xunit.v3.mtp-v2 4.0.2，不能按旧模板假定只有 core/assert | 可替代整个 NUnit 路线；需验证 STA/Dispatcher 组织方式与运行器，不建议混入主方案 |
| [xunit.runner.visualstudio 4.0.1](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.1) + Test.Sdk 18.11.0 | xUnit 的 VSTest/IDE 适配；xUnit 两项均为 Apache-2.0，Test.Sdk 为 MIT | 仅选择 xUnit 且需要 VSTest 时使用；纯 MTP 走另一条路线，不机械叠装 |
| [Serilog 4.4.0](https://www.nuget.org/packages/Serilog) + [Serilog.Sinks.File 7.0.0](https://www.nuget.org/packages/Serilog.Sinks.File) | 结构化日志与文件输出，均为 Apache-2.0；两个直接包，替代 NLog 而非并用 | 仍须证明 V-LOG 的错误反馈和证据边界，不能仅凭文件 sink 宣称满足 |
| .NET 内置通知/校验、JSON/文件、UIAutomation | 无新增第三方包；功能实现和维护责任回到本项目 | 用同一验证场景比较实际代码量、可理解性与失败处理；不是未经验证的默认回退 |

[xUnit 官方运行器说明](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)区分 MTP 与 VSTest，包 4.x 默认 MTP v2；纯 MTP 不需要 VSTest SDK/adapter，但运行配置和覆盖率方案须重新评审。本报告主方案只采用 VSTest 路线，避免两套命令和采集器混用。xUnit v3 包版本 4.0.2 是实际包版本，不是“xunit.v4”。

V-TEST：构造一个通过、一个故意失败、一个跳过的用例，确认非零失败退出、测试数和 TRX 正确；再覆盖快照、准入、不可写日志、失败不重启、取消及版本冲突。单元测试、临时目录/隔离子进程测试、本机真实发现分组执行；真实发现必须核对这台电脑上的已知应用，不能只写模拟目录测试。

V-COV：仅在批准 DEP-04 后采集；[Coverlet 官方说明](https://github.com/coverlet-coverage/coverlet)给出的 collector 最低 Test.Sdk 为 17.12.0，18.11.0 满足该声明的版本下限，但还需联测。检查报告遗漏的错误分支，不以固定百分比替代场景验收。

V-UI：仅在批准 DEP-05 后，在 Windows 交互会话操作预填→修改→保存→启动状态；用稳定 AutomationId 定位，超时记录截图/控件树与关联日志。FlaUI 是自动化工具，不是测试运行器；UIA3 包支持信息见[官方项目](https://github.com/FlaUI/FlaUI)。UI 外观需要视觉对比，UIA 成功不能证明美观未变；真实 AVEVA 登录另验。

## 6. 当前批准记录与后续界限

2026-10-27 用户明确批准 DEP-01—05，并指定增加 ArchUnitNET、在界面规划中使用 FigCraft。已有批准不重复请求；本轮仅同步设计与 OpenSpec，不安装、不派发。版本、用途或新增依赖发生实质变化才补充评审；锁定传递依赖、兼容性与行为验证仍未执行。

新增 DEP-06：采用方向已由用户指定，实施基线选 `TngTech.ArchUnitNET.NUnit 0.13.4`，核心 `TngTech.ArchUnitNET 0.13.4`。两者为 Apache-2.0；NUnit 扩展依赖核心，放 ArchitectureTests，不能进产品运行层。包的版本下限不等于与 NUnit 5 已联测，需实际测试。官方建议分析 Debug 构建，架构规则还必须有故意违规的反例以证明守卫有效。

依据：[NUnit 扩展包](https://www.nuget.org/packages/TngTech.ArchUnitNET.NUnit/0.13.4)、[核心包](https://www.nuget.org/packages/TngTech.ArchUnitNET/0.13.4)、[官方指南](https://github.com/TNG/ArchUnitNET/blob/main/documentation/docs/guide.md)。约束与局限详见[共用架构与界面规划](architecture-and-design.md)。这是显式选型落实，不代表已经安装或通过验证。
