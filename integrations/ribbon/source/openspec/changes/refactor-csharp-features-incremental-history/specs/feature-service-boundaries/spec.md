## ADDED Requirements

### Requirement: 独立 C# Feature 服务
正式后端 SHALL 以独立 C# 进程运行，并按 Feature 划分业务。WPF/网页 SHALL 仅通过 HTTP JSON 使用相同 API。DTO SHALL 无 UI 或后端实现依赖。

#### Scenario: 桌面启动
- **WHEN** 启动器检查服务
- **THEN** SHALL 确认 C# 健康标识，复用或隐藏启动独立 C# 进程，前端 SHALL 不直接调用服务实现或写 AVEVA 文件

### Requirement: 克制的抽象和脚本说明
简单功能 SHALL 直接实现或使用脚本，并提供不超过八句的功能说明。复杂或高度共用部分 SHALL 评估抽象接口/工厂；单一实现 SHALL 避免没有用途的抽象。非业务基础设施 SHALL 优先复用现有实现。

#### Scenario: 查看实现说明
- **WHEN** 开发者查看文档
- **THEN** SHALL 能找到每个 Feature、脚本、选型与职责边界说明

### Requirement: 兼容已有数据与保护
迁移 SHALL 保留旧版本库 JSON、长期快照、五次草稿、源文件路径和托管绑定，原生/未知项 SHALL 保持只读。

#### Scenario: 打开旧工作区
- **WHEN** C# 服务打开已有文档
- **THEN** SHALL 保留文档 ID 和旧版本，不清空数据，不因迁移向真实 UIC/XML 写入

### Requirement: 增量撤销
每次撤销记录 SHALL 仅包含受影响对象、列表位置或属性前后值，不序列化整个项目。一次用户编辑 SHALL 对应一步，50 步会话限制 SHALL 保留。

#### Scenario: 一万按钮中的属性修改
- **WHEN** 连续记录 200 次属性修改
- **THEN** 每步 SHALL 仅记录该属性，不遍历或复制整个项目，性能验证 SHALL 单独标明记账与重绘/网络的边界

#### Scenario: 保存后撤销
- **WHEN** 项目规范化保存后用户撤销再重做
- **THEN** 原对象和副本 ID SHALL 持续有效，自动保存完成 SHALL 不触发全界面重建

### Requirement: 清晰的 Tab 导航
设计页 SHALL 提供浏览器式项目 Tab 条、＋创建入口及当前页选中状态，Ribbon SHALL 展示当前 Tab。大按钮与小按钮列 SHALL 具有清晰分隔。

#### Scenario: 新建项目 Tab
- **WHEN** 用户点击＋
- **THEN** SHALL 添加并选中新 Tab，结构树与设计页导航 SHALL 对齐
