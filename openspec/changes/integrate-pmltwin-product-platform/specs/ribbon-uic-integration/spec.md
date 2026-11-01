## Purpose

使用户在 PMLTWIN 内完成 Ribbon 编排、已有 UIC 修改和目标模块部署，减少手工准备图标与寻找加载文件，同时保留已有身份、未知内容、版本历史和部署恢复能力。

## ADDED Requirements

### Requirement: Shared commands and loss preserving authoring
系统 SHALL 保留命令定义与布局引用的分离、稳定 ID、commandKey、已有快捷键及未编辑 XML 内容；Macro MUST 仅作为数据编辑，不能因导入、预览或设计稿展示而执行。状态：partial，Ribbon 后端已有基础，平台融合 planned；见 [来源清单](../../source-inventory.md)。

#### Scenario: Reuse a command
- **WHEN** 同一命令在 Large 和 Small 引用中使用
- **THEN** 共用命令定义，按引用生成所需图标和布局，不要求用户维护两份业务命令。

#### Scenario: Restore an editing revision
- **WHEN** 用户恢复历史版本
- **THEN** 只恢复编辑工作区，不修改安装中的 UIC 或 XML，不触发 Macro。

### Requirement: Explicit deployment targets and preserved compatibility
系统 SHALL 自动发现已有文档的来源与模块引用，区分更新 UIC 文件和修改加载 XML；执行须绑定预览摘要、源修订、目标及备份。MUST 保持原有 HTTP v1 冲突、锁定和权限错误语义。状态：partial。

#### Scenario: Update a known UIC
- **WHEN** 已有文档的绑定路径唯一有效
- **THEN** 自动准备该路径与已发现引用的预览，不重复要求选择 XML；只更新 UIC 时加载 XML 保持原字节。

#### Scenario: Register another module
- **WHEN** 用户只请求加入选定模块
- **THEN** 仅更改选定加载清单，不覆盖 UIC，不重启 AVEVA，并记录备份和前后证据。

### Requirement: Real host validation is distinct
系统 SHALL 分开报告导出校验、模拟安装部署和真实产品加载；原型与离线测试 MUST NOT 代表实际加载和点击行为通过。状态：planned；原项目真实加载验收尚未完成。

#### Scenario: Validate on a sample project
- **WHEN** 探测到用户指定优先验证的 APS 项目且登录条件具备
- **THEN** 记录实际产品、版本、项目、模块和验证时间，分别核对图标、尺寸、Tooltip 与显式授权的命令行为；APS 不存在时返回不可用原因，不猜测其他工程。

### Requirement: Preserve native protection and authoritative bindings
系统 SHALL 保留官方/原生安装条目的只读保护，写入身份与路径以服务端验证及持久绑定为准；MUST 拒绝调用方伪造托管状态绕过限制。状态：partial，统一入口须继承现有保护，见原 Ribbon API 与部署实现。

#### Scenario: Attempt to bypass protection
- **WHEN** HTTP、CLI 或 CAF 入口伪造 managed/sourcePath 等字段以接管、覆盖或移除受保护原生条目
- **THEN** 拒绝操作并返回权限原因，不写文件；自动发现结果不授予写权限，合法托管操作仍须通过摘要重验。

### Requirement: Versioned frozen deployment and activation evidence
严格版本化部署入口 SHALL 绑定预览输入、源修订、权威目标、前后哈希及期限；MUST 保持旧 v1 的兼容边界并分离保存、落盘与实际加载。状态：planned；细化见 [续作契约](../../features/continuation-contracts.md)。

#### Scenario: Preview no longer matches source or target
- **WHEN** 应用冻结计划时文档历史、目标文件或计划身份已变化
- **THEN** 在业务写入前拒绝，不以新输入套用旧目标哈希，不为前置校验失败创建编辑历史。

#### Scenario: File changes complete while AVEVA is running
- **WHEN** UIC 或选定加载 XML 已写入且回读通过
- **THEN** 报告配置已部署及待重新加载，不能将其标成已在 AVEVA 加载；不会自动重启或执行宏。

### Requirement: UI uses frozen plans and preserves uncertain outcomes
Ribbon 编辑 UI SHALL 通过 v2 预览获得服务端计划，再仅以 planId/planDigest 应用。修改文档或目标后 MUST 重新预览；网络中断或 unknown MUST 保留原计划身份与核对提示，禁止自动重试或以新计划掩盖不确定写入。

#### Scenario: Apply from the UI
- **WHEN** 用户检查冻结预览并确认应用
- **THEN** 锁定重复提交，校验回执身份，分别展示部署状态和待重启状态；文件回执不能设置 runtimeVerified=true。

#### Scenario: Verify after restart
- **WHEN** 在明确授权的 APS 环境验证重启生效
- **THEN** 记录前后进程身份、启动时间、产品/模块/工程及实际可见的 Ribbon 控件证据；仅发现新进程或文件哈希相同不构成加载验收通过。
