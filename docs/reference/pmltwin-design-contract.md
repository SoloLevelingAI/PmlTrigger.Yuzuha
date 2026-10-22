# PMLTWIN 项目设计约定

版本 0.1，日期 2026年10月22日，随本批 PR 人工审阅。依据《软件设计要求（20261022脚本设计优先版）》v0.3 Draft，将 Script、Feature、应用装配、验证和文档要求适配到现有 PMLTWIN。产品版本、协议版本与本约定版本分别管理。

PMLTWIN 面向 AVEVA AM、PDMS、E3D 的本机开发与排查：选择真实目标，复用 PML 和 C# 能力完成查询与已授权操作，保留可复刻测试及可定位记录。用户应能找到具体脚本、理解其计算和调用关系，再核对实现、设计与运行证据。

[按功能阅读](../../artifacts/docfx/navigation/features/index.md) · [Script 目录](../../artifacts/docfx/navigation/scripts/index.md) · [自动发现与人工增删改查](catalog-maintenance.md) · [迭代决定](iteration-log.md)

## 已确定的技术与实现范围

| 责任 | 当前落点 | 项目约定 |
| --- | --- | --- |
| 桌面与装配 | 现有 WPF Desktop、App、MainWindow 与 SessionWorkbench | 保留当前项目引用和业务接口，按功能组织说明；不为 CAF 名称新建框架或强制拆包 |
| 业务服务与通道 | MCP 工具、共享 CLI 适配、YuzuhaRpcBridge、Named Pipe | 功能定义场景语义，适配器处理通道；复用已有工具名、参数和 RPC 路由 |
| 原生 Host | AM/PDMS NET35，E3D NET48 | 在目标主线程操作；版本与能力显式核验；不能将 NET10 示例 API 直接用于旧框架 |
| 文档工具 | tools/Documentation 的 Roslyn、DocFX、LikeC4 | 扩充已有源码、测试与调用关系索引，不另建大型文档平台 |
| 测试 | 现有 xUnit、ArchUnitNET、PowerShell 执行器、Native AOT Smoke | 先增加适用测试再实现，验证通过后提交，PR 保留人工审查 |
| UI 设计 | FigCraft v04 及已有主题 Token、WPF 资源字典 | 保留设计建议和实现状态；Light/Dark 与关键状态按变量映射验收 |
| 后续能力 | 评审迁移、桌面测试执行、HTML 应用管理 | 保留合理设计，按独立场景交付；页面存在不表示后端已接入 |

依赖白名单仍以 `docs/architecture/dependencies.json` 为准，生产项目与模块索引复用 `docs/reference/modules.json`。通用基线的七程序集样例用于解释责任，不作为本项目必须迁移的目录结构。

## Script 与 Feature 的项目含义

Script 是可定位、可调用、可验证的具体实现单元。当前目录支持原生 PMLFNC、PMLOBJ、PMLFRM、历史 PMLCMD，以及人工登记的 C# 类型或唯一方法和受管理的 PowerShell 文件。一个文件或类型进入扫描结果，不自动证明它是经过认定的业务 Script。

C# 的算法、运行编排与适配器分别标注职责。例如 `PmlArrayWindow.Read` 是有界窗口算法；`PmlFormQuery` 同时组织扫描与原生查询；`PmlFormQueryHost` 是目标框架适配器。原生查询保留既有 `IPmlFormQueryRuntime` 边界，不能为了归类把它描述成纯计算，也不为文档改变其接口。

Feature 是用户可理解的具体功能，例如“原生 Form 与 Gadget 查询”，不是一个程序集或一页 XAML 的同义词。一个 Feature 可以复用多个 Script，一个 Script 也可以服务多个 Feature。当前 `capabilities.json` 建立这个多对多关系，类型与实际调用仍由既有源码索引和调用树提供。

每项功能至少给出目的、当前边界、真实入口、复用的 Script/服务、错误和状态、可复刻验证。未使用的 CLI、独立 Valid、CRUD、事件或界面不需要空实现；计划能力保持计划状态。

## 校验 权限与原生边界

独立 Valid 组件可选。目标 PID/启动身份、管道、Host 能力、调用许可、标量类型、固定查询种类与硬预算等必要检查，继续由现有入口和后端执行。不能因未启用独立 Valid 而关闭这些约束。

函数登记只说明本机源码与签名；AllowInvoke 是注册调用入口的独立许可；它们均不证明 AVEVA 已加载对应版本。原始命令工具仍按其既有任务授权使用，不通过文档目录新增或撤销权限。取消等待和传输失败可能留下未知结果，不自动重放已发送操作。

Form/Gadget 复用 PMLFORMS/FMSYS。现阶段不修改 PMLLIB/PMLUI 原生文件，不重写原生窗体管理。实机试验不保存用户工程；本机输出留在 artifacts，不上传用户工程、知识数据库、记录或专有 SDK。

## AI 草稿 人工审核与迁移

AI 新写的测试 PML 放在 `tests/ai-pml/<任务名>/` 的独立目录；本机临时输出放 artifacts。脚本可以先编写和验证，再由一名人工审核对应差异、契约和证据，决定是否迁移。文档发现、功能登记、源码审核、调用许可、复制、重加载、加载核验与实机验证分别记录。

人工批准应绑定具体源码版本或哈希、影响范围与验证引用。后续迁移界面复用已有复制、哈希和备份，补批准快照、目标、时间、结果及恢复线索。当前 GUI 迁移待接入，静态 DocFX 不执行审核、部署或模型操作。

## 验证与变更

| 层次 | 本项目证据 | 不据此推断 |
| --- | --- | --- |
| 说明与结构 | 对象身份、有效关联、ArchUnitNET 和工程引用检查 | 已检查的程序集之外也全部合规 |
| Script 直接验证 | 真实算法/脚本及边界断言，可复用已有夹具 | 仅调用完整 Feature 就覆盖了所有算法分支 |
| 独立功能 | 会话状态、权限、预算、错误、分页等真实用例 | 合成 Host 就是 AVEVA 实机 |
| 接入与协作 | CLI/MCP、RPC 兼容、参数映射和实际应用装配 | 工具目录存在就证明命令已执行 |
| 应用与实机 | WPF 实际入口、生命周期及按产品/Host 分类的人工案例 | 离线截图、旧报告或其他产品通过就是当前通过 |

测试继续使用既有 test-map、按 ID 运行器和证据目录，不复制一套测试状态。运行报告标明提交、dirty、环境和范围；通过、失败、未执行、跳过、不适用及结果未知按实际含义保留。静态调用关系与测试关联均不是运行结果。

实质变更用两到四句说明改了什么、为什么改、影响及验证。当前 Script 的简短摘要可维护在人工目录的 change 字段，详细差异与证据引用 Git/PR 和 `docs/validation`；不要从注释布尔值生成审核或通过事实。按差异自动强制审计、GUI 审核和 GUI 迁移仍属于后续实现。

## DocFX 如何对应这些约定

| 设计对象 | 人工维护的权威来源 | 自动生成的可访问位置 |
| --- | --- | --- |
| 具体功能与实现状态 | `docs/reference/capabilities.json` 的 features；guide 指向现有手写说明 | `reference/features/index.html` 与每个功能页 |
| 原生 Script 的身份和声明 | PMLLIB、PMLUI、tests/ai-pml 的仓库源码 | `reference/scripts/index.html`、脚本详情与源码行快照 |
| C# Script 的认定 | capabilities.json 的 scripts 选择实际类型或唯一方法 | Roslyn 提供真实签名，详情链接 C# API、源码和可用调用树 |
| 尚未认定的 C# 类型 | Roslyn 源码索引，不自动认定为 Script | `reference/scripts/candidates.html`，可搜索再人工登记 |
| 用途 归属与变更摘要 | capabilities.json 的 scripts 条目 | 脚本详情和功能聚合；未维护者明确显示未归属 |
| 详细解释与操作例子 | 普通 Git 管理的 Markdown，脚本或功能的 guide 字段指向它 | DocFX 手写概念页，与生成页相互链接 |
| API 与调用链 | 真实 C#、XAML、编译输入及有依据的 RPC 对应 | 现有 C# API、源码参考和逐级调用树 |
| 测试与证据 | 现有 test-map、源码与运行回执 | 现有测试详情、复制命令、关联及证据位置 |

DocFX 首页先提供按功能、按 Script 和查找维护说明的入口。对象详情先解释用途和真实声明，再提供调用链、源码、变更与验证入口。调用树继续用于逐层理解计算与排错，不因对象目录建立而移除。

自动发现只扫描仓库管理的固定目录；不扫描用户的 PML 注册表、实际工程或任意本机目录。C# 候选来自源码声明，不执行反射加载或业务脚本。PML 声明来自文本识别，不能视为 AVEVA 语法或执行验证。

## 人工增删改查与验收

具体步骤、可复制的 JSON 和命令见[目录维护说明](catalog-maintenance.md)。人工编辑源码、清单和普通 Markdown；生成页位于 artifacts，不作为人工编辑入口。浏览器提供检索、筛选和跳转，当前不提供写回仓库的表单。

验收至少检查：新增 PML 能自动出现；C# 新类型能进入待分类候选；人工认定后按功能聚合；修改说明后页面更新；删除登记后真实原生源码仍可发现；失效 ID、路径、类型、重载选择或功能关联会失败；历史与计划功能没有通过标识；源码、调用链和测试链接可访问。

先以 Form/Gadget、已有数组及对象查询形成真实样例，再按相同机制补充剩余功能。改变接口、引入动态插件框架、提供网页写回或扩大执行权限，应作为有单独需求和验收的功能，不能由补文档附带实现。
