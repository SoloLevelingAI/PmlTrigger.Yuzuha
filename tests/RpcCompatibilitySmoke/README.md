# Historical RPC compatibility fixture / 旧 RPC 兼容夹具

**DEPRECATED / 已弃用（2026-10-23），文件与运行方式继续保留。**

这里的 NET10 客户端使用 `CreateProxy<IIdentityService>()` 和 `src/lib/net10` 的旧二进制。当前生产 MCP 使用 `src/PlantHost.Rpc.Net10` 源码项目；当前 CI 使用 `tests/AotRpcSmoke` 验证 NET35/NET48 与 Native AOT 客户端。

新验证使用仓库根目录中的以下入口：

```powershell
./scripts/run-documented-test.ps1 -TestId 'smoke:rpc-net35'
./scripts/run-documented-test.ps1 -TestId 'smoke:rpc-net48'
```

历史客户端、服务端、项目引用、旧 DLL/XML 和许可证保持原样，可供已有手工工作流复现。保留不表示本批重新认证旧动态代理路线；现行 AOT 协议测试也不代表真实 AVEVA 模型操作通过。

完整边界见[弃用登记](../../docs/reference/deprecations.md)。将来如需移除，应将本夹具与专用 NET10 DLL 一起评估，不能只删 DLL 留下失效项目。
