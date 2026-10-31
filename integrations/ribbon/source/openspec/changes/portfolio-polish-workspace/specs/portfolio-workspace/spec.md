## ADDED Requirements

### Requirement: 按 Feature 并行审查与单一写入者
设计工作流 SHALL 按 Editor、Editing、Registration & Deployment 拆分只读审查；SHALL 允许子代理并发读取设计，不得并发写入同一个 FigCraft 文档。合并者 SHALL 记录采纳、部分采纳及否决原因。

#### Scenario: 多个 Feature 同时审查
- **WHEN** 用户要求多个子代理并行审查 FigCraft
- **THEN** 三个只读评审 SHALL 并发执行，单一写入者 SHALL 在保留原文的情况下统一合并，不得覆盖人类或其他代理未保存的修改

### Requirement: 轻盈且可复用的视觉系统
设计源 SHALL 包含原生可编辑组件、文本样式与语义变量，保持窄蓝标题栏、白色工作面、细线和明确的交互状态，SHALL NOT 将整套界面扁平化为图片。

#### Scenario: 检查控件状态
- **WHEN** 评审主/次按钮、输入框与文件 Tab
- **THEN** SHALL 能查看 Default、Hover、Pressed、Focus、Disabled 状态，并在原生组件中修改文本属性

### Requirement: 保留主视角与常见编辑工况
1280×800 与 1100×740 的默认状态 SHALL 完整展开编辑器 Ribbon。收起态 SHALL 保留常用操作、标题栏撤销/重做、更多命令及展开入口，中央 UIC 预览 SHALL 始终保留。展开/收起 SHALL 不改变窗口尺寸。

#### Scenario: 收起后访问命令
- **WHEN** 用户手动收起 Ribbon
- **THEN** 未常显的命令 SHALL 可在更多命令中找到，完整展开入口 SHALL 始终可见；菜单展示不等于执行实际业务

### Requirement: 明确单项与批量修改的作用域
当前活动按钮 SHALL 与勾选批量集合分开表达。右侧单项设置 SHALL 标明单项，批量修改 SHALL 切换为明确的字段选择面板，SHALL 仅应用勾选字段，并作为一个撤销操作。

#### Scenario: 只改两项 Tooltip
- **WHEN** 用户勾选两个按钮，仅选择 Tooltip 正文并填写值后应用
- **THEN** 两个按钮的 Tooltip 正文 SHALL 更新，其余字段 SHALL 保持原值；撤销 SHALL 一次恢复两个值，重做 SHALL 一次重新应用

### Requirement: 来源保护与逐项目标预览
XML 观察 SHALL 使用产品版本、安装路径与来源区分登记，官方原生项 SHALL 受保护。部署预览 SHALL 逐项列出所有勾选目标、完整路径、备份位置与关联登记范围；只读目标 SHALL 不可勾选。

#### Scenario: 更新和首次写入并行预览
- **WHEN** 用户选择一个现有 UIC 与一个首次写入的编辑草稿
- **THEN** 预览 SHALL 同时展示两个目标，现有目标 SHALL 显示备份去向，已有模块 XML SHALL 保持不变，新登记 SHALL 另行预览确认

### Requirement: 设计交付与运行时验证边界
作品集与说明 SHALL 明确数据为示例、原型不执行磁盘操作。设计交付 SHALL 包含可编辑源、关键状态预览、审查记录及自动几何审计；SHALL NOT 声称已经集成 WPF 或验证运行时性能。

#### Scenario: 查看设计成果
- **WHEN** 用户打开 0.12 交付
- **THEN** SHALL 能查看原尺寸产品、控件库和关键流程状态，并能区分静态设计样例与后续真实数据集成任务
