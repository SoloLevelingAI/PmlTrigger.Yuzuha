# C# API 导航

覆盖执行工具、知识工具、RPC、CLI 适配、开发 HTTP、NET35 / NET48 Host 与安装计划桥。可按项目、功能模块、接口、模型和实现筛选；类型页保留当前关注范围并支持前后翻页。

<div id="workspace-explorer" data-view="types"></div>

## 入口速查

| 项目 | 建议入口 | 职责 |
| --- | --- | --- |
| 执行 Core（历史程序集名） | <xref:YuzuhaToolkit.Mcp.PmlCallTools>、<xref:YuzuhaToolkit.Mcp.YuzuhaRpcBridge> | 工具、会话选择、身份核验与 PML 调用 |
| 知识 Core | <xref:YuzuhaToolkit.Knowledge.KnowledgeRepository>、<xref:YuzuhaToolkit.Knowledge.Search.QueryPlanner> | 本地知识索引、查询规划与检索 |
| RPC | <xref:PlantHost.Rpc> | .NET 10 Named Pipe 客户端、帧协议与序列化 |
| DevHost | <xref:YuzuhaToolkit.DevHost.DevelopmentEndpoints> | 内置指南、PML 文本预览、查询计划与 OpenAPI |
| WebApps | <xref:YuzuhaToolkit.WebApps.ManagerService>、<xref:YuzuhaToolkit.WebApps.ActionCompiler> | 本地 HTML 分类、按钮、参数与既有 IPC 适配 |
| 原生 CLI | <xref:YuzuhaToolkit.Mcp.NativeCliHost>、<xref:YuzuhaToolkit.Mcp.NativeOperationCatalog> | 直接调度、JSON输入与顺序批处理 |
| 启动配置 | <xref:YuzuhaToolkit.Workspace.LaunchProfileService>、<xref:YuzuhaToolkit.Mcp.LaunchOperationCatalog> | 版本化保存、历史、恢复与CLI适配 |

[Host 源码参考](../../../artifacts/docfx/navigation/modules/host/index.md) · [安装器源码参考](../../../artifacts/docfx/navigation/modules/setup/index.md) · [源码与文档覆盖报告](../../../artifacts/docfx/navigation/coverage.md)

内置指南统一由知识 Core 中的 <xref:YuzuhaToolkit.Builtins.BuiltinGuideCatalog> 提供；执行 Core 直接引用该程序集。DevHost 调用纯计算 API，旧 MCP 服务入口已禁用。
