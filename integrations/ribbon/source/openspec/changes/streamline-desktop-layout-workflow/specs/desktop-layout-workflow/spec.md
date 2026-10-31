## ADDED Requirements

### Requirement: 直接编辑多级结构
桌面树与 Ribbon SHALL 支持 Tab 排序、所属 Tab 内 Group 排序、按钮排序与跨 Group 移动，移动 SHALL 保留原 ID、源标识、图标及大小。

#### Scenario: 移动按钮到其他分组
- **WHEN** 用户在树或 Ribbon 将按钮拖到目标 Group
- **THEN** 原布局引用 SHALL 移至目标分组，按钮定义 SHALL 不增加，原文件 SHALL 只在部署确认后改变

#### Scenario: 无效投放
- **WHEN** 目标是自身、不兼容层级或旧文档对象
- **THEN** 客户端 SHALL 拒绝移动且不修改结构

### Requirement: 上下文剪贴板
结构节点 SHALL 提供剪切、复制、粘贴及 Ctrl+X/C/V；文本输入 SHALL 保留原生文本剪贴板行为。右键顶层可操作项 SHALL 不超过七项，属性与布局 SHALL 分到子菜单。

#### Scenario: 剪切后粘贴
- **WHEN** 用户剪切按钮并在另一 Group 粘贴
- **THEN** 按钮 SHALL 在粘贴成功时移动且保留身份，剪切时 SHALL 不删除对象

#### Scenario: 多次复制粘贴
- **WHEN** 用户多次粘贴复制的 Tab、Group 或按钮
- **THEN** 每次副本 SHALL 具有新 ID、清除源标识和命令键并重新映射按钮引用

### Requirement: 精简设计到部署流程
主窗 SHALL 提供设计和部署两个可切换工作页。设计 SHALL 仅展示当前项目 Tab；部署 SHALL 复用穿梭框选择，编辑修订后 SHALL 重新生成部署上下文。Ctrl+S SHALL 保存长期快照且不要求导出文件。

#### Scenario: 往返设计与模块选择
- **WHEN** 用户在没有编辑修订时往返工作页
- **THEN** 模块选择 SHALL 保留；实际写入执行时 SHALL 阻止设计编辑和主窗关闭

### Requirement: 连续操作说明
文档 SHALL 覆盖加载或新建、结构编辑、完成保存、原 UIC 更新、首次部署与模块登记的最短路径，并说明草稿、长期快照及恢复的含义。

#### Scenario: 更新共享 UIC
- **WHEN** 用户修改已被多个模块引用的原 UIC
- **THEN** 说明 SHALL 引导用户备份覆盖该 UIC，并仅在额外模块需要时新增 XML 登记
