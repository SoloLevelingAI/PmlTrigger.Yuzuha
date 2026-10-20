# C# API 导航

覆盖执行工具、知识工具、RPC、CLI 适配、开发 HTTP、NET35 / NET48 Host 与安装计划桥。可按项目、功能模块、接口、模型和实现筛选；类型页保留当前关注范围并支持前后翻页。

<div id="workspace-explorer" data-view="types"></div>

## 入口速查

| 项目 | 建议入口 | 职责 |
| --- | --- | --- |
| 执行 MCP | <xref:YuzuhaToolkit.Mcp.PmlCallTools>、<xref:YuzuhaToolkit.Mcp.YuzuhaRpcBridge> | 工具、会话选择、身份核验与 PML 调用 |
| 知识 MCP | <xref:YuzuhaToolkit.Knowledge.KnowledgeRepository>、<xref:YuzuhaToolkit.Knowledge.Search.QueryPlanner> | 本地知识索引、查询规划与检索 |
| RPC | <xref:PlantHost.Rpc> | .NET 10 Named Pipe 客户端、帧协议与序列化 |
| DevHost | <xref:YuzuhaToolkit.DevHost.DevelopmentEndpoints> | 内置指南、PML 文本预览、查询计划与 OpenAPI |
| CLI/MCP | <xref:YuzuhaToolkit.CommandLine.DualModeCommandLine> | 同程序双模式与会话内顺序批处理 |

[Host 源码参考](../../../artifacts/docfx/navigation/modules/host/index.md) · [安装器源码参考](../../../artifacts/docfx/navigation/modules/setup/index.md) · [源码与文档覆盖报告](../../../artifacts/docfx/navigation/coverage.md)

共享内置指南源码同时链接到两个 MCP 程序集；文档统一链接到执行 MCP 下的 <xref:YuzuhaToolkit.Builtins.BuiltinGuideCatalog>，避免重复 UID。DevHost 调用已编译后端的纯计算 API，不运行 MCP 程序入口。
