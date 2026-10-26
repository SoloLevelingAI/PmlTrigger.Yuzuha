# 后端 SDD · 结构

WPF / CLI → 共享用例 → 存储 / Host。CLI 原生分派；独立 MCP 禁用，旧程序集名保留。

| 变化点 | 接口 → 实现 |
| --- | --- |
| 配置介质/修订 | IConfigurationStore<T> → WorkspaceConfigurationStore |
| 产品/安装模块 | IProductModuleCatalog → ProductModuleCatalog |
| 外部执行 | 会话身份/协议边界 → Host 主线程 |

保存、复制、默认项、恢复内聚于 LaunchProfileService；实际独立变化才加端口，不逐方法包装。[表达成本](../../../../artifacts/docfx/navigation/specifications/compression.md)比较等义字数；架构取舍仍看变化压力。静态候选非运行选择；缺 SDK 的 Host 只作声明索引。
