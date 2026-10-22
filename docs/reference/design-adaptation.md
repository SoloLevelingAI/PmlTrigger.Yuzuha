# 软件设计基线的 PMLTWIN 适配记录

记录日期为 2026年10月22日，适配版本 0.1。本文件依据 ARCH-SCRIPT-FIRST-001 v0.3 Draft，记录通用原稿在 PMLTWIN 中如何采用、调整或暂缓。当前代码参考 `4ba3f17`；本批只补来源、适配关系与文档验证，不改变业务接口。适配记录随 PR 由一名人工审阅，原稿继续保持 Draft。

[版本与来源归档](../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/index.md) · [可检索原稿](../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md) · [当前项目设计约定](pmltwin-design-contract.md) · [迭代与决定](iteration-log.md)

## 如何阅读和维护

原始 Word 回答“依据来自哪里”，项目设计约定回答“PMLTWIN 当前遵循什么”，本页回答“原文如何落实到这个项目”。已采用表示项目方向和现有落实，不表示原稿通过整体评审；实现、自动测试、人工批准和 AVEVA 实测分别判断。

原文定位链接指向固定版本中真实正文元素，构建自动生成锚点，不用 Word 缓存页码冒充定位。原稿的代码、架构图、七程序集和 36 项测试保留为该样例的历史内容，不移作 PMLTWIN 当前通过证据。

后续意见或实现变化先更新对应适配项及迭代记录，再修改当前项目约定；原始 Word 不覆盖。设计建议可以保留，但新增框架、接口、依赖或运行权限必须有自己的需求和验收。

## 条款与实际落点

| 编号 | 原稿与项目处置 | 当前落点和验证入口 | 状态与后续边界 |
| --- | --- | --- | --- |
| AD01 | [架构定位][S35]：以 AVEVA 本机开发与排查为具体目的，优先对象/函数、Form/Gadget 和数组场景 | [功能目录](../../artifacts/docfx/navigation/features/index.md)、[桌面三条流程](desktop-m1.md) | 采用；不将示例名称规范化业务当成新增产品需求 |
| AD02 | [Script 职责][S62]：纯算法、运行编排和适配器按真实职责区分 | [PmlArrayWindow](../../artifacts/docfx/navigation/source/YuzuhaToolkit.Mcp.Core/YuzuhaToolkit.Pml.PmlArrayWindow.md)、[Form/Gadget 查询](form-gadget-query.md)、[Script 目录](../../artifacts/docfx/navigation/scripts/index.md) | 适配；PmlFormQuery 不是纯计算，不为满足名称强拆现有 IPmlFormQueryRuntime |
| AD03 | [直接契约与接入][S66]：保留算法参数/结果，通道与工具层负责 CLI/MCP/RPC 映射 | [CLI/MCP](cli.md)、[后端调用链](backend-tracing.md)、[函数与数组](pmltwin-phase1.md) | 采用；既有工具名、参数和错误语义保持；文档不新增运行权限 |
| AD04 | [可选 Valid][S68]：不强制新增 Valid 项目或空验证器 | [目标、许可和预算约定](pmltwin-design-contract.md#校验-权限与原生边界)、现有后端用例 | 采用；PID/启动身份、Host、许可、类型和硬预算仍是必需前置检查 |
| AD05 | [Feature 能力与透明度][S72]：按具体功能列脚本、服务、目的和状态，CLI 语义复用现有适配 | [目录维护](catalog-maintenance.md)、capabilities.json、[功能目录](../../artifacts/docfx/navigation/features/index.md) | 部分落实；事件/Command 专用对象目录及更细生命周期清单仍待补，不伪造已接入功能 |
| AD06 | [CAF 与共享边界][S84]：现有 WPF App/MainWindow 负责装配与外壳，Core 供实际消费者复用 | [架构与守卫](architecture.md)、[工作区](workspace.md)、[DSH 拆分恢复记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-22-web-repair.md) | 适配；不新建 CAF 框架，不因原稿示例强制七程序集；动态插件与 Dock 管理另行立项 |
| AD07 | [共享测试][S180]：复用现有 xUnit 夹具与测试宿主，按实际调用和断言判断层次 | [测试说明](testing.md)、PmlArrayWindowTests、PmlFormQueryTests、桌面状态测试 | 采用；L1 直接调用真实实现，不能只测 Feature 最终成功标志或把算法换成假实现 |
| AD08 | [L0 至 L4][S138]：结构、直接算法、独立功能、契约接入、完整应用分别给证据 | [测试目录](../../artifacts/docfx/navigation/tests/index.md)、[测试关联与复刻](test-associations.md)、docs/validation | 适配；WPF 合成夹具、NET35/NET48 离线 Host 与 AVEVA 实机分开；没有统一宣告全部 L4 或实机通过 |
| AD09 | [元数据与单一来源][S118]：签名/源码自动提取，用途与归属人工维护，证据独立 | [目录维护](catalog-maintenance.md)、capabilities.json、test-map.json、Roslyn 源码与测试清单 | 部分落实；保留现有 schema，不把原稿建议对象模型直接改成生产接口；Reviewed/Verified 布尔值不作为事实 |
| AD10 | [对象文档优先][S206]：功能与 Script 是主入口，API、源码、测试可互跳 | [功能目录](../../artifacts/docfx/navigation/features/index.md)、[Script 目录](../../artifacts/docfx/navigation/scripts/index.md)、[API](api/index.md) | 部分落实；Interface 已有技术目录，事件/Command 完整对象视图和统一场景模型仍待细化 |
| AD11 | [辅助调用关系][S212]：保留用户明确要求的按钮到后端逐级调用树，帮助理解计算和排错 | [交互调用树](backend-tracing.md)、[静态边界](ui-tracing.md) | 适配；对象目录与调用树并存；动态分派、RPC 对应和未分析边界不能当实际执行、顺序或覆盖证据 |
| AD12 | [简明变更与审核][S126]：用两至四句交代变化、理由、影响与验证，关联 Git/PR 和批次报告 | [迭代记录](iteration-log.md)、Script change 字段、docs/validation | 部分落实；一名人工评审，不要求两名；差异自动审计、批准快照和 GUI 审核未完成 |
| AD13 | [候选与纳入][S135]：AI 草稿在独立目录，允许先写与验证，再由人工审核和迁移 | [AI 草稿约定](../../artifacts/docfx/navigation/repository/tests/ai-pml/README.md)、[后续管理功能](features/planned-tools.md)、[FormUse 支持登记](formuse-audit.md) | 采用边界；GUI 迁移待接入；不修改 PMLLIB/PMLUI 原生文件，不保存用户工程 |
| AD14 | [日期版技术规范][S163]：复用现有 C#、Roslyn、DocFX、xUnit、ArchUnitNET、PowerShell 与构建脚本 | [文档维护](documentation.md)、依赖锁定文件、Development.slnx、架构白名单 | 适配；Host 保留 NET35/NET48；原稿示例 xUnit 2.9.3 不触发当前 2.9.2 自动升级，预览 SDK 实测记录不等于生产选型批准 |
| AD15 | [FigCraft 变量与主题][S173]：沿 v04 变量、Token 和 WPF 资源映射核对明暗模式与关键状态 | [设计交付说明](../../artifacts/docfx/navigation/repository/design/figcraft/README.md)、已有 WPF 检查及截图报告 | 部分落实；相似截图或变量存在不能代替全部组件绑定验收；EffectCraft 尚无纳入需求 |
| AD16 | [受管理脚本与交付][S230]：构建/安装脚本记录环境、时机和副作用，复用现有按 ID 执行器 | [按 ID 测试](test-associations.md)、[CLI](cli.md)、[安装与生命周期](../../artifacts/docfx/navigation/repository/AGENT-INSTALL.zh-CN.md) | 采用；产物默认本机 artifacts，CI 标明提交/环境，不上传用户工程或专有 SDK |
| AD17 | [架构守卫及限制][S297]：生产工程白名单、类型依赖、引用图和故意违规负例并用 | [架构与守卫](architecture.md)、ArchitectureBoundaryTests、dependencies.json | 已接入既有守卫；其覆盖集合与边界明确，不能据此保证字符串反射、PML 或所有外部工具符合规则 |
| AD18 | [原稿样例与待评审][S256]：只作为参考历史归档，不复制其通过状态 | [原始版本与哈希](../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/index.md)、[项目验证目录](../../artifacts/docfx/navigation/repository/index.md) | 原稿保持 Draft；当前结果只从对应提交、环境和范围的报告判断 |

## 原稿待决事项在 PMLTWIN 的处置

原稿 D01 至 D13 仍保留原始“待决”文字。下表是本项目的回答，不回写或宣告原稿定版。

| 原文编号 | PMLTWIN 的决定 | 尚待完成 |
| --- | --- | --- |
| D01 目的与场景 | 已明确 AVEVA 本机开发、查询和排查；选函数/对象、Form/Gadget、数组作为增量 | 新功能逐项明确目的和验收 |
| D02 核心与人工审核 | AI 草稿独立，一名人工审核差异、契约和证据 | 核心清单及 GUI 批准/迁移快照 |
| D03 框架与运行环境 | 维持现有 NET10/WPF、NET35/NET48 Host 和锁定依赖 | 发布支持矩阵和正式 SDK 策略另行维护 |
| D04 契约错误与异步 | 复用已实现的目标核验、失败回执、分页预算；超时不自动重放 | 新写入功能的取消、幂等和事件策略单独定义 |
| D05 共享测试 | 复用现有夹具、按 ID 执行器及合成 Host | 按新增场景补最小测试宿主，不强建公共包 |
| D06 Valid 与 CLI | 独立 Valid 可选，必要检查保留；CLI/MCP 共用现有工具 | 各新增 Feature 声明实际启用入口 |
| D07 元数据与 ID | 继续既有 schema、项目限定类型、签名及测试 ID | 更完整 Event/Command 身份模型 |
| D08 文档范围 | 功能/Script/API/源码/调用树/测试及本批来源归档已连通 | 独立对象视图和后续发布策略 |
| D09 变更记录 | Git/PR、change 摘要和迭代决定相互引用 | 自动比较差异并强制审计的时机与例外 |
| D10 注册与生命周期 | 已有函数注册许可、会话核验和运行日志按现有契约使用 | 插件/HTML 管理及持久化事件语义 |
| D11 主题映射 | 采用 FigCraft v04 与既有明暗资源映射 | 持续核对组件状态及局部例外 |
| D12 EffectCraft 与交付 | 暂无 EffectCraft 纳入需求；受管理 PowerShell/安装脚本继续复用 | 有具体动效需求时再定，交付仍由人工审阅 |
| D13 ArchUnitNET 与例外 | 当前锁定 0.13.4，白名单和反向依赖负例已进入开发检查 | 扩大覆盖或放宽规则须记录理由与范围 |

## 可复刻核对

运行 `npm run docs:build`，会先检查归档清单、字节数和哈希，再只读生成原稿正文、图示与下载副本，最终检查适配链接和正文锚点。文档回归测试覆盖内容被修改、大小错误、路径越界、重复身份、缺失适配页及擅改 Draft 状态。

实际功能验证继续用已有测试目录和按 ID 执行器。本页不把归档完整性检查升级为功能通过或人工审核通过。更新此页时要保留未完成项，不因归档闭环完成就关闭评审、迁移或实机待验工作。

[S35]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00035
[S62]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00062
[S66]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00066
[S68]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00068
[S72]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00072
[S84]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00084
[S180]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00180
[S138]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00138
[S118]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00118
[S206]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00206
[S212]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00212
[S126]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00126
[S135]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00135
[S163]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00163
[S173]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00173
[S230]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00230
[S297]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00297
[S256]: ../../artifacts/docfx/navigation/design-baselines/software-design-20261022-v03-draft/source.md#source-00256
