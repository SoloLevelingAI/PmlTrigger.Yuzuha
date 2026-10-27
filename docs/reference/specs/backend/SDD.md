# 后端 SDD · 结构

启动：WPF/CLI → LaunchProfile → AvevaLaunchScript → 存储/凭据/进程 → 回执。功能做步骤，编排管顺序/失败，接线管注册，后勤管存储/传输；[入口](../../source-reading.md)。CLI 原生分派；独立 MCP 禁用，旧程序集名保留。

| 变化点 | 接口 → 实现 |
| --- | --- |
| 配置介质/修订 | IConfigurationStore<T> → WorkspaceConfigurationStore |
| 产品/安装模块 | IProductModuleCatalog → ProductModuleCatalog |
| 外部执行 | 会话身份/协议边界 → Host 主线程 |

配置流程在 LaunchProfileService；按用途归组，不强制模式。独立变化才加接口。[表达成本](../../../../artifacts/docfx/navigation/specifications/compression.md)仅比等义字数；Host 声明非实机结果。
