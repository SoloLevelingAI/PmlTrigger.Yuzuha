## Purpose

让设计、实现和项目展示围绕用户任务持续同步，按 Feature 控制设计上下文大小，并让评审者能够从作品集追溯到原生稿、操作契约、运行截图和各自适用的验证证据。

## ADDED Requirements

### Requirement: Desktop developer reading takes priority
开发者候选界面 SHALL 优先设计和验证桌面任务动线，常驻显示当前位置与步骤，并就地切换契约、实现和验证；MUST NOT 为共享手机布局而牺牲电脑端路径与内容组织。手机端可在后续独立设计，仅共用语义配色和契约数据。细化见[桌面阅读动线](../../features/desktop-developer-reading.md)。

#### Scenario: Inspect implementation while following a task
- **WHEN** 开发者在电脑上从Feature选中步骤并查看方法或长契约
- **THEN** 路径保持可见，长内容在对应区域滚动，主要阅读不跳转旧页面；切色保留步骤与方法状态。

#### Scenario: Plan a mobile layout later
- **WHEN** 需要设计手机端阅读
- **THEN** 允许独立布局和导航，继续使用同一配色语义及元数据，不用手机布局约束当前桌面构图。

### Requirement: Breadcrumb sibling navigation is scoped and direct
桌面面包屑 SHALL 在主页/Feature/Script/Script Method层级提供独立同级选择，不要求先返回父层；MUST 使用项目Feature、当前Feature的显式Script以及当前Script的精确方法根身份，保留主题/阅读栏目，并清除失效分支。详细范围、键盘、历史及异步门禁见[桌面阅读动线](../../features/desktop-developer-reading.md)。

#### Scenario: Change a sibling Script without leaving the workspace
- **WHEN** 用户从Script面包屑菜单选择当前Feature下的另一个Script
- **THEN** 一次进入所选Script并保留Feature、主题和阅读栏目，重新核对流程与方法，不显示旧step或旧源码；无需先回Feature或增加中间历史。

#### Scenario: Choose an exact sibling method
- **WHEN** 当前Script的方法目录为同摘要ready且用户打开方法同级菜单
- **THEN** 仅列该Script精确绑定根，选择后就地显示真实签名/源码，不把全目录或相关调用当作同级；过期/跨Script/旧摘要提交被拒绝。

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

### Requirement: Measured rendering evidence
界面验证 SHALL 区分请求DIP尺寸、实际布局尺寸、PNG像素尺寸、宿主DPI与渲染DPI；MUST NOT 仅凭文件名或图片存在声称视口验证通过。离屏画布证据不替代真实显示器缩放或实机验收。

#### Scenario: Host window is smaller than requested canvas
- **WHEN** 宿主仅1024×768而测试请求1280×720或1440×960
- **THEN** 完整画布按请求尺寸渲染，回执及PNG尺寸一致且无透明裁剪；无法满足时失败，不跳过无滚动及业务断言。

### Requirement: Layered contract presentation
HTML演示 SHALL 分别解释整体交付边界、Feature输入/状态/权限/回执/恢复、Script算法/副作用/校验手段，并关联真实源码。实现差异、空结果歧义、planned状态及非实机证据 MUST 明确呈现。

#### Scenario: Reader examines a script guarantee
- **WHEN** 读者查看PML部署链路或DBREF查询
- **THEN** 可追到各自入口的冻结与错误语义，不把C#保证泛化到PowerShell，也不把DBREF空数组等同于成功查询。

### Requirement: Metadata driven contract components
契约展示 SHALL 由版本化元数据生成总体、Feature和Script组件；MUST 保留原需求身份/状态、显式关系及验证范围，生成物不得成为第二份规范。细化见 [元数据契约](../../features/contract-atlas.md)。

#### Scenario: Regenerate project diagrams
- **WHEN** 规范、Feature目录或审阅后的Script细约发生变化
- **THEN** 提取器生成可验证的内部/公开投影，漂移检查要求重新生成；同一元数据可生成Web流程卡片和原生FigCraft组件/实例。

#### Scenario: Reuse a public component
- **WHEN** 以作品集视图展示或导出图表
- **THEN** 保留实现与证据范围，使用公开白名单投影，交互不执行工程操作；未知结果恢复支路不能被改成成功。

#### Scenario: Import untrusted metadata
- **WHEN** 用户在本地Web组件载入JSON
- **THEN** 先验证版本、预算、唯一ID和引用，按纯文本渲染，拒绝危险链接/执行内容；失败保留原图并给出原因。

### Requirement: Reading view and appearance are orthogonal
图谱 SHALL 独立提供help/portfolio阅读视图及light/dark/system外观偏好；MUST 保持实体、筛选与显式流程，不能以明暗换色作为阅读视图的主要实现。主题语义及验收见元数据契约的主题修订。

#### Scenario: Change appearance while reading a script
- **WHEN** 用户正在查看Script并切换外观，或system偏好下系统主题改变
- **THEN** 同一实体、阅读视图、筛选与流程仍保持，全部原生Web组件及后续SVG导出响应实际主题；显式外观偏好不被系统变化覆盖。

#### Scenario: Read the same contract in two views
- **WHEN** 用户在同一外观下切换项目指南与作品集
- **THEN** 契约与实现视图提供逐层核对路径，作品集先呈现目标、任务入口和交付；两者均可读取完整细约及来源，保留同一摘要/详情程度，并保持实现限制和证据排除范围可见。

### Requirement: One automatically published component revision
展示源修改 SHALL 经完整验证和共享组件模型生成同摘要的Web与FigCraft修订，成功后自动通知Web更新；MUST 保留lastgood，禁止把半成品、内存导入或仅保存源文件称为两端已同步。细化见 [共同生成链](../../features/contract-generation-pipeline.md)。

#### Scenario: Change a card and a flow
- **WHEN** 作者修改Feature/Script文案及流程连接
- **THEN** 无需手动build或刷新，Web和新原生文件使用同一metadataDigest/renderModelDigest；作者可核对修订与原生下载，已打开编辑器画布不被冒称已更新。

#### Scenario: Reject a bad or superseded build
- **WHEN** 元数据无效、原生生成失败或构建期间出现更新输入
- **THEN** 不发布该修订，继续提供原有效产物，并显示失败或新版本待构建；规范/验证事实不得被作者编辑升级。

### Requirement: Contract linked static method previews
契约图谱 SHALL 通过显式符号绑定接入实际MSBuild/Roslyn方法快照及真实DocFX成员引用；MUST 保留重载身份、源码摘要和静态边界，不能把方法可达性或源码预览当作运行验证。细化见[方法预览契约](../../features/contract-method-preview.md)。

#### Scenario: Inspect the implementation behind a script
- **WHEN** 读者在Feature或Script详情选择已绑定方法
- **THEN** 同修订展示签名、XML说明、源码行与摘要、直接静态调用关系；原契约选择与阅读状态保留，真实DocFX链接可定位到精确成员。

#### Scenario: Analysis is missing or stale
- **WHEN** 分析未生成、源码已变化或所需Host未分析
- **THEN** 显示明确不可用/过期/排除范围，不猜测同名方法、不展示旧正文为当前源码，也不隐式执行完整文档构建或工程操作。

### Requirement: Complete progressive contract reading
图谱 SHALL 为全部现有Feature/Script提供摘要与就地详情，并通过统一披露状态支持一键全部展开/折叠；MUST 保留完整规范/来源/证据入口及关键边界，不因只显示前几张卡或当前流程而丢失总体内容。细化见[阅读契约](../../features/contract-reading-disclosure.md)。

#### Scenario: Read a portfolio summary and all details
- **WHEN** 读者浏览作品集并切换全部展开/折叠
- **THEN** 全目录可发现、每卡完整字段和规范附录可展开；嵌套卡、来源、证据及已选方法遵守同一模式，关键未验证范围仍可见，焦点与阅读选择保留。

#### Scenario: Navigate or receive an updated revision
- **WHEN** 已选择全局模式或局部展开后导航、筛选、换主题或自动更新
- **THEN** 有效稳定ID的披露状态保持，失效ID清理；独立导入/草稿不串用旧事实覆盖，刷新可恢复全局模式。

### Requirement: Document navigation and shared section design
图谱 SHALL 提供完整项目目录、当前正文及按大小标题定位的本页目录；MUST 按稳定身份穿Shadow、只展开必要祖先并保留阅读状态。区块颜色与排版 SHALL 用Web/FigCraft同源token生成，不能将类别颜色当成实现/验证状态。细化见features/contract-document-navigation.md。

#### Scenario: Navigate to a script field
- **WHEN** 用户选择Script并从本页目录定位闭合详情中的字段
- **THEN** 当前契约优先显示，仅必要祖先展开，目标避开顶栏并获得焦点；URL刷新恢复目标，滚动高亮不抢焦点。

#### Scenario: Share section styles with native design
- **WHEN** 同源token定义区块颜色和正文/标题字号字重并重新生成
- **THEN** Web CSS和FigCraft静态文档参考板使用相同角色和真实文字，两主题均可读，原生画板不声明具备Web跳转交互。

### Requirement: Portfolio visual quality remains explicit
作品集 SHALL 持续满足任务价值叙事、主次构图、统一排版/间距和克制配色；MUST 以Web/FigCraft实际渲染图进行独立视觉复核，不能用分色、无溢出、结构测试或token一致代替美观验收。代理视觉复核、用户视觉认可和产品实机验收分别记录。

#### Scenario: Review a colored document layout
- **WHEN** 文档已分区着色且功能测试通过
- **THEN** 仍检查任务焦点、标题正文层级、对齐/留白及作品集叙事；大块占位色框和线框式堆叠不能自动判为作品集视觉完成，检视不足须继续改稿。

### Requirement: Readable summary subheadings
文档 SHALL 用明确小标题区分目标/交付、输入或职责摘要、实现方式、适用边界与详细细约；MUST 保留七字段原文/来源、旧稳定锚点及三轴状态，不能增加框线代替内容组织。

#### Scenario: Scan a Script summary
- **WHEN** 读者打开摘要页
- **THEN** 不需从无标签参数、语言和警告段落推断语义；可通过小标题与本页目录定位原内容，完整七项仍可展开。

### Requirement: Task map before reference material
Web与Desktop SHALL 先给出当前情况、任务入口、当前位置和下一步，完整契约/方法/证据作为可往返分支；MUST NOT 要求先滚过资料索引和完整细约寻找主路径。共享地图仅投影真实登记流程，不将阅读位置当作执行状态。细化见[任务地图动线](../../features/task-map-navigation.md)。

#### Scenario: Follow a task into its implementation
- **WHEN** 读者从项目选择启动任务和其中一步，再查看契约、静态方法或证据
- **THEN** 全程保留原flowId/stepId及返回入口，原内容和旧深链可达，无流程的Feature不猜执行顺序。

#### Scenario: Continue from an available engineering session
- **WHEN** Desktop检测到可用Host而用户未选择操作目标
- **THEN** 主入口引导明确选择并核验，成功后提供任务选择，不自动执行查询、授权、部署或PML；结果和恢复入口就近可见。
