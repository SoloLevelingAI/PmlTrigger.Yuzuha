# Spec Delta

## Purpose

让用户通过网页配置 AVEVA Ribbon 的按钮、分组和普通选项卡，由独立后端输出可供目标 AVEVA 验收的 UIC，并保存可继续编辑的项目，使未来桌面客户端能够复用相同生成能力。

## ADDED Requirements

### Requirement: 参数化编辑 Ribbon

系统 SHALL 允许用户新增、编辑、删除和排序普通 Tab、Group 与按钮，编辑其标识和显示名称，并将按钮放入指定 Group。

#### Scenario: 创建分组并调整按钮顺序
- **WHEN** 用户在一个 Tab 下创建两个 Group 并将按钮从第一个 Group 移到第二个 Group
- **THEN** 编辑器和导出结果中的归属及顺序 SHALL 与编辑后的项目一致

### Requirement: 配置图标和显示大小

系统 SHALL 为按钮接受一个 PNG 或 JPEG 图标，并允许用户为每处 Group 内按钮配置 Large 或 Small。显示大小 SHALL 与图标内容分别保存。

#### Scenario: 切换为大图标布局
- **WHEN** 用户将按钮从 Small 改为 Large 后导出
- **THEN** 对应 Group 内 Tool 引用 SHALL 写出 `LargeImage="True"`，其按钮定义 SHALL 保留已选图标

#### Scenario: 图片内容无效
- **WHEN** 上传的文件无法解码为支持的图片，即使扩展名为 PNG 或 JPEG
- **THEN** 后端 SHALL 返回指向图片字段的错误并阻止该项目导出

### Requirement: 配置 Macro 和提示

系统 SHALL 允许编辑按钮 Macro、Tooltip 正文和可选标题，保留 Macro 原文，并仅将其作为命令文本写入 UIC。生成、预览和项目操作 SHALL 不执行该命令。

#### Scenario: 包含特殊字符
- **WHEN** Macro 或 Tooltip 包含中文、换行、引号、`&` 或 `<`
- **THEN** 生成文件 SHALL 是合法 XML，解析后 SHALL 恢复原始文本，且没有任何 Macro 被执行

### Requirement: 后端独立提供生成能力

系统 SHALL 提供不依赖浏览器状态的项目校验和 UIC 导出 API。只要提交相同有效项目，非网页客户端 SHALL 能直接调用后端获得相同 UIC 内容。

#### Scenario: 不打开网页生成 UIC
- **WHEN** API 客户端向后端提交一个有效项目并请求导出
- **THEN** 后端 SHALL 返回 UTF-8 UIC 文件，不要求运行前端或读取前端缓存

### Requirement: 导出前校验

后端 SHALL 拒绝标识重复、布局引用未定义按钮、必填名称或 Macro 为空、没有任何 Group 或可见按钮的项目。错误 SHALL 包含对象标识或字段路径，失败导出 SHALL 不返回伪成功 UIC。

#### Scenario: 存在悬空引用
- **WHEN** Group 引用了不存在的按钮标识
- **THEN** 后端 SHALL 返回该引用位置的校验错误而不是生成 UIC

### Requirement: 生成 AVEVA XML 结构

系统 SHALL 输出扩展名 `.uic`、根元素 `UserInterfaceCustomization`、XML 命名空间 `www.aveva.com` 和 `Version=1.0`，分别生成按钮定义与 Tab/Group 中的引用。按钮 SHALL 使用 Macro 命令、稳定 Key、图标和区分大小写的 Tooltip 字段。

#### Scenario: 导出包含大小按钮的项目
- **WHEN** 项目包含一个 Tab、两个 Group 以及 Large 和 Small 按钮
- **THEN** XML 解析 SHALL 成功，定义与引用 SHALL 一致，LargeImage SHALL 分别为 `True` 和 `False`，所有图片 Base64 SHALL 可解码

### Requirement: 保存并重新编辑项目

系统 SHALL 提供含版本号的项目文件下载和重开能力，保留布局、图标、Macro、Tooltip 与稳定标识；不支持的项目版本 SHALL 给出明确错误。

#### Scenario: 关闭后恢复
- **WHEN** 用户下载项目文件，关闭页面，再重新打开该文件
- **THEN** 编辑内容 SHALL 完整恢复，未修改项目再次导出的 UIC 内容 SHALL 相同

### Requirement: 提供结构预览和文件交付

网页 SHALL 展示按 Tab、Group 和大小排列的布局示意，并支持下载 UIC。预览 SHALL 标明其为示意，兼容性声明 SHALL 仅覆盖实际完成加载验收的 AVEVA 产品和版本。

#### Scenario: 尚未完成实机验收
- **WHEN** 用户在尚无实机验收记录的版本使用预览并导出
- **THEN** 页面 SHALL 标明效果以 AVEVA 加载为准，且 SHALL 不宣称已通过 AM、PDMS 或 E3D 兼容性认证
