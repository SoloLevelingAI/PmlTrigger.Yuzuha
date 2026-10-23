# Historical NET10 RPC binaries / 历史 NET10 RPC 二进制

**DEPRECATED / 已弃用（2026-10-23），二进制完整保留。**

此目录中的 `PlantHost.Rpc.Net8.dll`、`PlantHost.Rpc.Abstractions.dll`、`Newtonsoft.Json.dll` 和两个 RPC XML 文档供[旧动态代理测试](../../../tests/RpcCompatibilitySmoke/README.md)使用。当前生产 MCP 的 NET10 RPC 客户端引用 `src/PlantHost.Rpc.Net10` 源码项目，新集成不应新增对此组旧 DLL 的引用。

本批未替换或删除五个文件，也未更改旧测试的引用。NET35/NET48 的 Host DLL 不在这项弃用范围内。许可证继续见 [THIRD-PARTY](../../../THIRD-PARTY.md)。迁移与移除条件见[弃用登记](../../../docs/reference/deprecations.md)。
