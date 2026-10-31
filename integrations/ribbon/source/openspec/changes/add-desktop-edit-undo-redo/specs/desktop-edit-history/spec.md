## ADDED Requirements

### Requirement: 会话编辑撤销与重做
客户端 SHALL 提供 Ctrl+Z 撤销与 Ctrl+Y 重做，并覆盖 Tab/Group/按钮排序、跨 Group 移动和剪切或复制粘贴。回放 SHALL 保留按钮身份、布局大小及引用完整性。

#### Scenario: 撤销拖拽并重做
- **WHEN** 用户拖动按钮到另一 Group 后撤销，再重做
- **THEN** 布局 SHALL 依次恢复到移动前、移动后，按钮定义数量和 ID SHALL 保持一致

#### Scenario: 重做粘贴副本
- **WHEN** 用户撤销复制粘贴后重做
- **THEN** 副本 SHALL 恢复原粘贴时的 ID 和命令身份，不再生成第二套身份

### Requirement: 清晰的撤销边界
客户端 SHALL 只为实际编辑入栈；无效拖放、复制或待剪切、自动保存与手动快照 SHALL 不新增编辑步骤。新编辑 SHALL 清空重做分支，加载文档 SHALL 清空会话栈。

#### Scenario: 撤销后继续编辑
- **WHEN** 用户撤销后进行新的实际编辑
- **THEN** 原重做分支 SHALL 清空，新的编辑 SHALL 可撤销

#### Scenario: 保存后回退
- **WHEN** 用户在自动保存或手动快照之后执行撤销
- **THEN** 最近的实际编辑 SHALL 被撤销，回放结果 SHALL 自动记录草稿，已保存的长期快照 SHALL 保留

### Requirement: 输入与部署隔离
文本输入中的 Ctrl+Z/Ctrl+Y SHALL 保持原生文字编辑；文档撤销 SHALL 提供工具栏入口及适用的禁用状态，并且不增加右键顶层项。实际文件部署 SHALL 不由编辑撤销反向写入。

#### Scenario: 文本输入有焦点
- **WHEN** 用户在 Macro 或其他输入框按 Ctrl+Z
- **THEN** 操作 SHALL 交给该文本控件，结构撤销快捷键 SHALL 不抢占

#### Scenario: 撤销已部署后的编辑
- **WHEN** 用户在设计页撤销当前编辑内容
- **THEN** 文档 ID、源 UIC 原文和部署绑定 SHALL 保留，部署页 SHALL 失效旧修订上下文并重新预览

### Requirement: 有限的会话资源
会话编辑栈 SHALL 具有最多 50 步及快照容量限制，并且不复制原始 UIC XML 到每个编辑状态。

#### Scenario: 持续编辑超过上限
- **WHEN** 用户连续编辑超过容量
- **THEN** 最旧的编辑状态 SHALL 被释放，最近编辑 SHALL 仍可回退
