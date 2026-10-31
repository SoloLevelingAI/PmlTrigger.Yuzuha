## ADDED Requirements

### Requirement: 多 UIC 独立会话
工作区 SHALL 同时打开多个 UIC，每个文件 SHALL 有独立项目、选择、撤销/重做、脏状态及保存队列。切换 SHALL 保留编辑状态，不将文件标签当作最终菜单页。

#### Scenario: 跨文件撤销
- **WHEN** 用户编辑 A、切到 B 编辑，再回 A 撤销
- **THEN** SHALL 仅回退 A，B 的内容和撤销位置 SHALL 不变化

#### Scenario: 重复加载
- **WHEN** 用户再次打开已打开路径
- **THEN** SHALL 激活原标签并保留未完成编辑及撤销栈

### Requirement: 文档绑定的保存
保存响应 SHALL 只影响它所属的文档与修订。关闭文件 SHALL 先记录草稿，失败 SHALL 保留文件标签。

#### Scenario: 在途保存切换
- **WHEN** 保存 A 期间继续编辑并切到 B
- **THEN** A 的最新内容 SHALL 继续记录到 A，旧响应 SHALL 不替换 B 或清掉 A 的新修改

### Requirement: 可关闭的工具标签
部署 SHALL 是用户手动打开、可关闭的独立工作区标签，与多个 UIC 文件标签并列，不随每个文件自动创建部署和模块标签。部署页 SHALL 支持批量加入 UIC，从已打开文件加入，以及按文件查看部署状态；模块登记 SHALL 在需要时查看或配置。关闭工具 SHALL 不关闭文件。

#### Scenario: 多文件与手动打开部署
- **WHEN** 用户批量打开 A、B、C，随后通过部署入口或标签加号菜单打开部署
- **THEN** SHALL 保留三个独立文件标签及一个并列部署工具标签，不为每个 UIC 自动创建部署/模块页

#### Scenario: 批量加入与按需查看
- **WHEN** 用户将多个 UIC 加入部署列表并选中其中一项
- **THEN** SHALL 查看该文件的路径、登记模块和备份记录；未展开时 SHALL 以文件列表与状态摘要为主

### Requirement: 可复用的部署配置
部署环境和模块设置 SHALL 保存为可复用配置，由一个配置选择按钮选择；日常查看及更新 SHALL 不要求重复填写所有配置。执行前 SHALL 按每个 UIC 的目标和修订核对计划。

#### Scenario: 复用已有配置
- **WHEN** 用户为批量 UIC 选择已有部署配置
- **THEN** SHALL 复用该环境及模块设置，逐文件展示状态，原生只读登记 SHALL 不被默认接管

### Requirement: 独立加载登记页与标签去重
工作区 SHALL 提供手动打开的加载登记工具页，用于查看 UIC 的完整位置及向模块 XML 登记该位置。工具页 SHALL 复用已有标签；打开已加载 UIC 的编辑入口 SHALL 激活原文件标签，保留状态与撤销栈。

#### Scenario: 从登记返回已打开文件
- **WHEN** 用户在加载登记页点击已打开 UIC 的“打开编辑”
- **THEN** SHALL 跳转已有文件标签，不重复导入或重置编辑状态

#### Scenario: 登记 UIC 路径
- **WHEN** 用户选定 UIC 和目标 XML 并预览新增登记
- **THEN** SHALL 展示对应 CustomizationFile 的 Name、完整 Path 及目标 XML 路径，确认后备份并写入；已有同路径登记 SHALL 不重复加入

### Requirement: XML 数量与产品版本徽标
系统 SHALL 按规范化的不同 XML 路径统计 UIC 已登记数量，同 XML 中的重复条目 SHALL 只计一次。待新增项 SHALL 单列，不混入已登记数量。详情 SHALL 按产品及版本折叠，E3D、AM、PDMS SHALL 同时用颜色和产品文字区分，版本号 SHALL 在徽标下方显示。

#### Scenario: 展开 XML 登记详情
- **WHEN** 用户点击 UIC 的 XML 数量并展开某产品版本
- **THEN** SHALL 查看该版本对应 XML 的完整路径、模块、登记来源及只读/可管理状态，且总数 SHALL 与去重后的已登记 XML 数一致

### Requirement: UIC 功能表格与批量编辑
文件视图 SHALL 在 Ribbon 下方提供可展开的功能对照表，包含按钮、Group、显示大小、真实图标、Macro 和 Tooltip。图标 SHALL 使用实际图片并在悬停时显示 Tooltip，不以 16/32 数字代替图标。展开表格 SHALL 是同一文件的视图模式，复用文档、选择及撤销栈，不创建重复文件会话。表格与 Ribbon SHALL 双向定位；多选批量修改 SHALL 只影响勾选项，并用增量记录归为一步撤销。

#### Scenario: 批量修改并撤销
- **WHEN** 用户勾选两个按钮并批量更改 Group 或其他选定属性后撤销
- **THEN** SHALL 整体恢复该次操作前受影响的属性和布局，未选按钮 SHALL 保持不变，不保存全量项目到撤销栈

#### Scenario: 展开功能表格
- **WHEN** 用户从当前 UIC 点击“展开表格”并返回 Ribbon
- **THEN** SHALL 保留同一文件的内容、选择、未保存编辑和撤销位置

### Requirement: 紧凑且可识别的视觉
小按钮列 SHALL 左对齐。常用 1280×840 窗口 SHALL 显示当前工具栏及菜单按钮，并在窄窗按组换行。UIC 内部页签 SHALL 紧贴预览功能区上沿，不显示表单式“Ribbon 页：”前缀；文件标签 SHALL 位于命令 Ribbon 下方、内容区上方，多文件时 SHALL 提供滚动或列表访问。

#### Scenario: 无需拉大窗口
- **WHEN** 在默认窗口查看当前菜单
- **THEN** 命令 SHALL 不因水平裁切而隐藏，图标 SHALL 保留 32/16 大小

### Requirement: FigCraft 源同步
项目 SHALL 提供可编辑 FigCraft 修订，保留原 Office 设计，明确多个文件及工具页并采用左对齐图标、真实命令图形和紧凑布局。

#### Scenario: 检查设计源
- **WHEN** 打开项目内 FigCraft 文件
- **THEN** SHALL 能看到对应工作区画板与原生可编辑层，PNG SHALL 对应已保存设计
