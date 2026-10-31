## Purpose

让设计、实现和项目展示围绕用户任务持续同步，按 Feature 控制设计上下文大小，并让评审者能够从作品集追溯到原生稿、操作契约、运行截图和各自适用的验证证据。

## ADDED Requirements

### Requirement: Independent feature design handoff
每个 Feature SHALL 有独立原生 FigCraft 文件与简短交接，记录共享设计 token 版本、状态、操作映射和验收入口；共用基础 MUST 可追溯。状态：partial；Yuzuha 已有独立草稿，统一范围待实施，见 [来源清单](../../source-inventory.md)。

#### Scenario: Edit a single feature
- **WHEN** 开发者修改 Ribbon 部署流程
- **THEN** 只需读取该 Feature 交接、契约及共享基础，不必载入全部产品画板；历史原稿保留，新拆分文件记录来源。

### Requirement: Usability backed by operation contracts
可操作控件 SHALL 映射输入 schema、自动预填来源、校验、权限、结果及恢复行为；MUST 覆盖空数据、多候选、无权限、超时、源变化与未知结果。状态：planned；现有草稿不能替代此验收。

#### Scenario: UI and CLI parity
- **WHEN** 从界面与 CLI 提交等价输入
- **THEN** 返回等价业务决定和错误码；界面不得独有关键默认值或恢复规则。

### Requirement: Portfolio provenance and bounded claims
作品集 SHALL 解释任务价值、减少的用户操作、后端责任和维护方式，并关联实现证据；MUST 区分拟议、部分、已验证和历史，分别标记视觉认可与产品实机验收。状态：partial，LaunchDesk 与 Ribbon 已有独立作品集。

#### Scenario: Reuse a portfolio scene
- **WHEN** PMLTWIN 展示来自 LaunchDesk 或 Ribbon 的画面
- **THEN** 保留来源、版本及适用范围；未连接后端的原型不能标成运行截图，作品集不执行部署、登录或 VM 动作。

### Requirement: Design export does not infer business behavior
设计转换工具 SHALL 输出布局及诊断，由显式绑定接入业务；MUST 保留可编辑源稿并报告不支持项，不把设计转换等同于生成 UIC、CAF 插件或业务实现。状态：partial，FigCraft.D2C 已有布局转换基础。

#### Scenario: Export a feature frame
- **WHEN** 单个 Frame 导出到 WPF 或其他受支持目标
- **THEN** 保留源文件，记录 Frame、工具版本和输出证据；业务契约、权限与数据库访问仍须单独验证。

### Requirement: Compact workspace with Dashboard home
管理器 SHALL 默认显示 Dashboard，总览、工程、开发、知识、设置五个主入口复用现有视图实例。入口压缩 MUST NOT 删除授权、协议、版本、精确回执校验或丢弃未保存输入。FigCraft SHALL 以独立精简修订记录同一分组，保留历史 Feature 原稿。

#### Scenario: Open and navigate the workbench
- **WHEN** 启动工作台并跨五个工作区切换
- **THEN** 首页是总览，全部12功能视图可达，启动及笔记草稿保留；1280×720 首页折叠摘要不要求纵向滚动，明细仍可展开，窄窗允许纵向滚动。
