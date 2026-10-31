## ADDED Requirements

### Requirement: 一致的原生控件视觉
桌面端 SHALL 按已验证 Office Ribbon 色板统一 Ribbon、按钮、输入、下拉、页签、树、表格、自有弹窗及菜单，提供清晰的正常、悬停、按下、选中、禁用与焦点状态。

#### Scenario: 查看按钮状态
- **WHEN** 按钮进入悬停、按下、选中或禁用状态
- **THEN** 背景、边界或透明度 SHALL 对应状态变化，禁用操作 SHALL 不执行动作

### Requirement: 大小图标与布局
Ribbon SHALL 支持 32px 大图标和 16px 小图标混排，属性面板 SHALL 提供直接可见的 Large/Small 对照，选中状态 SHALL 与项目数据一致。

#### Scenario: 切换按钮大小
- **WHEN** 用户通过属性区或右键菜单选择 Small
- **THEN** 该布局引用 SHALL 更新为 small，预览 SHALL 使用 16px 图标并明确选中状态

### Requirement: 无边框外壳
主窗、管理、部署、确认与输入窗口 SHALL 使用无系统边框外壳，主窗 SHALL 提供最小化、最大化/还原及关闭按钮，保留系统标题区拖动和边缘缩放能力。

#### Scenario: 窗口繁忙
- **WHEN** 客户端正在调用后端
- **THEN** 业务控件 SHALL 按原操作规则锁定，标题栏窗口控件 SHALL 不被业务内容禁用策略一并锁死，部署关闭防护 SHALL 保持

### Requirement: 有上下文的右键菜单
树与预览按钮 SHALL 提供重命名、独立复制、大小、分组移动、排序及删除；管理与历史 SHALL 提供适用的查看、编辑、加载、恢复和下载；模块与输入框 SHALL 提供相应快捷操作。不可适用和只读操作 SHALL 禁用。

#### Scenario: 复制导入按钮
- **WHEN** 用户右键复制导入按钮
- **THEN** 副本 SHALL 使用新 ID、无原 sourceId、独立的新命令 Key 生成位置，原按钮和实际 UIC SHALL 不因复制直接改变

#### Scenario: 右键选择对象
- **WHEN** 用户在未选中的预览按钮或树节点打开右键菜单
- **THEN** 属性面板、选中样式与菜单操作目标 SHALL 一致，菜单 SHALL 保持可操作
