## ADDED Requirements

### Requirement: 原生独立客户端
系统 SHALL 提供可构建运行的 WPF 桌面客户端，复用已验证的 Office Ribbon 资源，所有 UIC 业务 SHALL 通过独立 HTTP API 调用。

#### Scenario: 复用已运行后端
- **WHEN** 用户启动桌面客户端且后端已运行
- **THEN** 客户端 SHALL 连接该后端，不启动重复实例，能打开现有文档

### Requirement: 网页功能迁移
客户端 SHALL 支持新建/示例/JSON、导入 UIC、Tab/Group/Button 增删排序、大小图标和 Group 位置、Macro/Key/Tooltip 编辑、图标上传移除、校验及导出。

#### Scenario: 编辑导入文件
- **WHEN** 用户修改导入 UIC 并保存或导出
- **THEN** 客户端 SHALL 保留来源 XML 与节点标识，将图标压缩、生成及校验交给后端，编辑阶段源文件 SHALL 不变

### Requirement: 持久化与权限管理
客户端 SHALL 提供自动草稿、长期手动快照、历史打开/恢复/下载、模块条目筛选、只读来源说明、接管及托管加载移除恢复；保留策略 SHALL 由后端执行。

#### Scenario: 历史恢复
- **WHEN** 用户恢复旧版本
- **THEN** 编辑器 SHALL 打开恢复结果，实际 UIC SHALL 不被自动覆盖

### Requirement: 可审核部署与模块关联
已有 UIC 更新 SHALL 不要求选择 XML。模块关联 SHALL 使用双栏穿梭框，区分已有关联与待加入项，并提供实际 XML 前后预览。实际写入 SHALL 使用预览摘要，由后端备份并持久记录。

#### Scenario: 多模块登记
- **WHEN** 用户预览并确认两个待加入模块
- **THEN** 每个模块 SHALL 展示准确的 Name/Path 与完整 XML，后端 SHALL 逐项记录执行结果，部分失败 SHALL 保留剩余待加入项
