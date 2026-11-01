# Design

## PR审查修复实施决定 · 2026-11-01

本轮以PR #13头提交`578c1e0c86133fc50c6288dba023ad72bf4cf117`为审查基线。先补当前源码注释门禁，再为启动/会话补分层历史证据，随后接入契约CI与紧凑阅读。完整报告只经loopback只读接口以已登记source ID和SHA读取；拒绝未公开批准、过期摘要、越界路径和被替换的文件，不把源码或报告原文装入公共元数据。界面保持契约/实现/验证分轴，Script不继承Feature的独立通过结论。手机先选任务再看装饰轨道，桌面仅减少空白和重复来源权重，不删除原文。新增CI与原文档/WPF门禁并行执行；没有真实FigCraft或Host时明确说明范围，不伪造执行成功。原27项业务任务、canonical状态及人工视觉认可均保持。

最终交接提交的云端专项暴露Windows原子rename间歇EPERM。仅对同一临时文件的原子替换进行短时有界重试，不重试业务save/build，不先删除目标；每次尝试前复验来源CAS或发布输入，等待期间来源变化必须拒绝。耗尽时原目标及lastgood保持，错误仍向CI传播。验收包含可注入的瞬时锁、持续锁及外部来源变化反例，不以整套测试自动重跑掩盖失败。

## Context

动机见 [proposal.md](proposal.md)。这是整体集成的候选设计，不是把所有仓库改名合并。PMLTWIN 生效需求仍为 `docs/specification/contract.json` 的 G1（开发便捷）、G2（三维辅助）、G3（持续接手）；现有管理器 change 已有后端实现，本轮不会重置其完成状态。

2026-10-31 用户明确“拆单”指 rewrite 项目代码。已定位 Archon/rewrite 的工作副本，包含操作协调、部署恢复、分享迁移、平台与提权 Worker；Next 是更聚焦的登录切片，不能替代 rewrite 全部能力。当前证据及原仓核对情况见 [source-inventory.md](source-inventory.md)。用户指定 APS 为优先实际验证项目；项目可用性须实查，不把“通常存在”当成保证，不将登录秘密写入文档。

## Goals / Non-Goals

**Goals:** 用完整任务验证可复用性；一个业务能力只有一个决策后端；自动探测和修复建议可脚本调用；统一操作事实与数据生命周期；Feature 可分别交付、设计与验收。

**Non-Goals:** 本提案不接管真实安装、不迁移数据库、不启动 AVEVA/VM、不执行 Macro；不承诺一个 DLL 兼容所有 .NET 运行时，不把任意第三方动态加载和热卸载作为首期目标。

## Decisions

### 1. 按用户任务组织产品，按责任保留项目

| 任务与价值 | 主要来源 | PMLTWIN 所有者及增量 |
|---|---|---|
| 找到环境、少填路径与模块 | Next 发现脚本、Ribbon 注册表扫描、Yuzuha 安装发现 | 环境服务整合证据与偏好；不运行厂商 BAT 来做静态发现 |
| 编辑一次，多入口可靠启动 | rewrite 启动规划/操作协调、Next 模型快照 | Manager 启动 Feature；融合语义，执行后端唯一 |
| 安装工具并可恢复 | rewrite Delivery/Worker、Yuzuha 事务部署 | 部署 Feature；保留摘要/租约/恢复记录，复制与 EVARS 分开预览 |
| 管理配置、分享、迁移、环境变量、健康 | rewrite Catalog/Resources/Handlers | 单独 Feature/操作，独立存储契约与机器绑定，不漏掉 Next 没覆盖的能力 |
| 知识、会话、函数注册与调用 | Yuzuha 知识/通信/旧 GUI | 管理入口接统一操作；注册/许可/源信任/Host 实际加载分开 |
| 编排 Ribbon、维护 UIC | Ribbon Studio 独立后端 | 编排和部署两个 Feature，共享命令模型及编辑历史 |
| 许可诊断、VM 验证 | Yuzuha worker、Next 开发工具 | 可选 Feature，实验依赖不进入基础产品 |
| 理解价值和维护系统 | rewrite/Next 文档、作品集、FigCraft.D2C | 独立文档与作品集构建，不进入产品业务依赖 |

放弃整仓直接复制：会引入多个协调器、存储事实和生命周期。也不在前端做接口拼盘：那会继续要求用户自己协调路径与状态。每个迁移切片须写“来源 → 契约映射 → 唯一实现 → 兼容适配 → 验收”。

### 2. 可组合宿主与 CAF 隔离

建议依赖方向：GUI / Manager CLI / CAF adapter → Feature contracts → Application use cases → Database contracts 与系统能力端口。Database、Windows、通信 CLI 与 Ribbon HTTP adapter 为基础设施；宿主只装配依赖。

Feature 描述见 [contracts.md](contracts.md)。首期显式注册已编译工厂或清单固定的 worker；启动时装配。统一 Feature 身份不意味着强迫 worker 改成进程内插件。未知或冲突版本拒绝装配；禁用不取消已受理操作，操作归应用生命周期，重启后按记录核对。

具体 CAF SDK 尚未在这些项目落实。先冻结与 CAF 无关的业务契约，通过环境/程序集元数据确认目标宿主及 API，再实现薄适配。不能将 XML Addin 或 WPF 装配称为已兼容 CAF。DevHost、Scalar、Aspire 保持开发工具，产品不依赖它们。

### 3. 易用性归后端，而不只归向导

自动流程为 Discover → Resolve → Validate → Plan → Execute → Observe。调用者可止于任一只读阶段。每个字段返回值、来源、可编辑性、冲突和失效原因；扫描保留人工覆盖。

探测优先级：显式目标 > 经验证的用户偏好 > 唯一可信候选。多候选不按目录顺序碰运气。系统目录、注册表、进程、初始化文本、加载清单均由有预算的只读适配器采集；候选目录失败不阻断其他来源。秘密不扫描、不导出。运行时环境求值只由实际启动链完成；INIT/BAT 的有效规则沿用既有实现，不因统一扫描而放宽。

验证目标：可探测字段在首次及重复流程中无强制手填；每个提问必须关联未解决的选择或权限；CLI 无交互模式返回 needsInput。以场景计数验证，不虚构效率百分比。失败返回“原因＋影响＋下一步＋证据”，不用空 Error 或仅异常堆栈。

### 4. 一个操作协调者与可恢复事实

复用 rewrite 的 OperationId、ExpectedRevision、PlanDigest、受理日志和租约思想，映射 Next AttemptId 及 Yuzuha requestId。requestId 是调用相关性，operationId 是一次业务操作，attemptId 是一次实际执行；禁止把相同请求自动映射为第二次执行。相同 operationId/摘要返回已有事实；同 ID 不同摘要冲突。副作用开始前持久受理，无法持久化则不执行。

页面关闭或调用者取消等待不抹去已发生动作。Worker 提权保持同用户、双向进程身份、nonce、目标摘要及父进程确认；CAF 注册不是提权许可。跨数据库和外部文件没有虚假的全局原子事务：分阶段记录、冻结备份哈希、补偿及 unknown，外部动作后再更新完成事实；恢复只能针对本操作拥有且未被外部修改的对象。

### 5. 独立 Database DLL 与运行时边界

采纳用户“像 AVEVA 一样用单独 Database DLL”的组织方式，首期建议 `PmlTwin.Database.Contracts` + `PmlTwin.Database.dll`（现代管理进程，net10）。UI 和业务层只引用契约；数据库程序集负责连接、事务、修订冲突、迁移、备份、查询与审计存取。它不负责启动程序、改 EVARS、复制安装文件或执行 PML。

建议普通配置、文档修订、绑定及操作索引用 SQLite，文件/图片/大型证据作为内容寻址资产；技术原型先验证现有 Microsoft.Data.Sqlite 依赖与锁/备份行为，不在规划期安装新库。知识库保留现有产品/版本独立 SQLite 文件和 FTS 格式，统一生命周期 API，不强行把所有数据塞进单文件。秘密由本机秘密存储管理，数据库仅保存本机引用及授权关系。

旧 JSON / Ribbon 文件工作区先由只读 Repository adapter 暴露，再用显式迁移计划切换。SQLite 备份使用一致性快照，不在活跃 WAL 时直接复制单个 db 文件。外部文件和历史 blob 有大小/哈希/路径限制，恢复测试包括中断、并发、磁盘不足及新版本拒绝降级写入。

AVEVA 工程数据库另经通信协议访问；如需抽出宿主内访问 DLL，则用产品匹配的 NET35/NET48 adapter，DTO 不公开供应商对象，DBREF 绑定会话/工程。net10 DLL 不进旧宿主，不能以 netstandard 声称覆盖 net35。现阶段 Ribbon net48 后端保留进程隔离，通过适配器消费统一存储契约，待切片迁移后再决定是否迁入现代宿主；不双写同一权威数据。

替代方案：所有 Feature 自己保存 JSON 易造成多写者与迁移分裂；一个通用 DLL 同时带 AVEVA SDK、UI 和 SQLite 会破坏兼容和权限边界，因此不采用。

### 6. Ribbon 与设计工具链

保留 HTTP v1 作为过渡接口，Manager 操作适配器维护原 403/409/423 语义与原始错误。UIC 编辑模型保留定义/引用、未知 XML、稳定命令身份和增量撤销。保存草稿、恢复历史、生成 UIC、覆盖 UIC、登记加载是不同动作。

FigCraft.D2C 只负责设计期布局转换；不会从画板推导业务、生成 UIC 或产生 CAF 插件。共享基础 + 每 Feature 原生文件 + 交接 + 契约映射 + 运行证据为交付单元。建议文件分组：环境/启动、工具部署、配置分享迁移、环境变量、健康、会话/函数、知识、Ribbon 编排、UIC 部署、可选插件、记录。保留旧原稿，只在实施时另存拆分版本；作品集另建叙事文件，不能替代产品稿。

## Risks / Trade-offs

- 多个 rewrite 副本版本不同 → 原仓身份与源码哈希固定后才迁移，副本不按目录名推定最新。
- 自动发现误判 → 保留来源、扫描覆盖、有效期与歧义，写入前重验。
- 数据迁移或跨资源失败 → 原位置保留、恢复演练、阶段日志，不把补偿等同于无损原子事务。
- CAF 与旧运行时限制 → 适配隔离，具体兼容矩阵未验收前标 unsupported/unverified。
- 设计比实现领先 → 原稿、运行图、离线证据、实机证据分别记录，作品集不扩大结论。

## Migration Plan

M0 固定来源与原 ID 映射；M1 统一 Feature/操作/探测/Database 契约和兼容样例；M2 用“发现→APS候选→计划→启动→观察”纵向切片验证唯一后端；M3 迁移 rewrite 部署/分享/变量/健康与存储；M4 接 Ribbon 和 CAF 适配；M5 分 Feature FigCraft、界面与 CLI 等价验收；M6 打包、升级/回退、实际产品矩阵与作品集复审。

每阶段均可保持旧入口可用；活动数据切换须显式计划，失败回原目录。现有管理器 change 未完成任务继续归原账，不因本提案获得完成勾选。APS 在本机发现后可按用户授权用于登录验证，但登录不自动授权写工程数据库、运行任意 Macro 或改生产安装；证据只保留非秘密信息。

## Open Questions

- CAF 具体 SDK、宿主版本和装配 API：由后续环境探测/已有文档定位；只阻塞 CAF adapter 的具体实现，不阻塞独立业务契约。
- APS 在各目标产品实际的模块和项目路径：由扫描解决；不存在时记录 unavailable，不能用合成结果替代。

## 桌面面包屑同级切换实施决定 · 2026-11-01

按features/desktop-developer-reading.md的补充契约，名称返回上层，独立下拉选择同级。公共模型生成项目Feature及当前Feature的Script候选，内部方法组件仅向当前明确Script暴露ready/同摘要的精确根选择接口；候选不复制进公共图谱。Feature/Script一次更新实体路由并保留阅读栏目和配色，旧步骤/方法清理，方法保持现有面板内导航语义。非模态搜索面板遵循同一六主题、原ID和键盘/焦点约定；只读视觉导航不改变原27项业务状态。
# 首期实施决定 · 2026-10-31

用户指定先贯通自动发现、启动和回执。该切片使用 HTML 本机适配器与管理 CLI 共用 OperationExecutor，复用 LaunchProfileService / LaunchService，不复制 Archon 的启动后端。UI 自动收集目录/注册表/初始化候选，首次只问不能可靠推导的用户/MDB和有歧义的选择；后续复用保存的非秘密字段。

首期存储选择现有修订 JSON + 非秘密冻结计划/回执 JSON；独立 Database DLL 是后续接口迁移目标，本轮不伪装已接入 SQLite/CAF。C# XML 文档纳入 DocFX，HTML 资产按 Feature 独立，FigCraft 从实际 DOM 采集几何和 token，滚动需求以浏览器实测为准。

实施、测试、实际 APS 与 Host mismatch 的边界见 [启动切片交接](features/launch-workflow-handoff.md)。原平台设计与未完成验收仍有效。

## 续作实施决定 · 2026-10-31

基于已纳入单仓的 `39e12f7`，用户授权 Sol 并行续作。先细化 [续作契约](features/continuation-contracts.md)，再将 WPF 入口接既有启动协调者；Ribbon 以显式 v2 冻结计划增强部署且保留 v1；迁移适配器先提供只读预览，不提前切换活动存储。三者拥有独立文件边界，共享任务和规范由主代理维护。此决定更新规划阶段的只读范围，不表示已实施或已实机验收。

## 元数据图谱实施决定 · 2026-11-01

用户追加可自动填入元数据的Web/Figma(FigCraft)契约组件。采用版本化提取器+显式展示映射，保留canonical并从同源数据生成交互图谱、SVG及新原生组件板；Figma以正规Plugin交付，执行验收与FigCraft分开。公开投影不携带机器环境与原始日志，不自动发布。元数据、组件输入、预算与验收先落盘至 features/contract-atlas.md，再并行实现提取、Web和设计适配，主代理统一校验。

## 方法预览实施决定 · 2026-11-01

用户继续要求契约接DocFX/Roslyn预览方法。复用实际编译输入的UiTrace生产者，独立internal方法目录通过明确符号绑定关联Feature/Script；公开规范与事实模型不装入源码。目录、DocFX快照和公开图共同发布，方法身份/源码hash/静态边界可核对，缺失或过期明确说明。先冻结features/contract-method-preview.md，再实现生产者、目录与Web消费者，真实源码/DocFX链接及浏览器分别验收；不触发工程动作。

## 完整内容与阅读实施决定 · 2026-11-01

用户要求作品集更直观、契约与实现内容完整且支持一键摘要/详情。先细化features/contract-reading-disclosure.md，再补齐全部18 Feature/14 Script目录、共享IR只读规范附录和32项实体方法范围。Web以中心披露控制器向嵌套原生组件传递稳定key状态，阅读视图、主题和全局展开程度分别保存；长字段/原文/方法正文可折叠，关键限制与证据排除仍可见。FigCraft继续使用同一事实模型，纸面画板不宣称获得Web交互。通过原文对照、真实布局、状态保持和共同服务验收，保留原平台未完成任务。

## 文档导航与视觉完成度实施决定 · 2026-11-01

用户继续要求常规文档网页、左右目录、区块提示色及Web/FigCraft同源CSS。先冻结features/contract-document-navigation.md，再实现完整左层级、当前正文优先、右侧稳定大小标题、跨Shadow必要展开、URL/历史及章节相对位置恢复；响应式目录使用原生折叠，不用遮罩锁滚。六类颜色和字号/字重从同一token生成CSS及原生板。用户强调美观不能被分色取代后，补独立视觉条款与20.6，精修任务价值首屏、移动工具权重、原生构图和紧凑图例。真实渲染代理复核、用户视觉认可及业务实机验收分别记录；不把结构通过当审美通过。

## Dashboard与摘要小标题实施决定 · 2026-11-01

实际检查确认默认Dashboard已存在，但后开E3D持续发现未接通，StatusAsync异常也可能残留旧核验。按features/dashboard-session-health.md先细化契约，再实现独立只读monitor及工作目标epoch撤销，严格区分Host可用性与显式操作目标；灰/黄/绿/红均有文字和时间，不执行PML或自动切换目标。文档以小标题组织摘要，完整七字段/来源/旧锚点不变，原生板对应分组。38项边界测试、WPF合成矩阵及真实APS /ALL DESIGN无会话→可用绿灯分别留证，原27项平台任务不随本切片勾选。
