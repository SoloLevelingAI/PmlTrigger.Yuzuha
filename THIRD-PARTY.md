# Third-party dependencies / 第三方依赖

## 中文

| 组件 | 位置 | 许可证 |
|---|---|---|
| PlantHost.Rpc | `src/lib/net10`、`src/lib/net48` | Apache-2.0；见 `src/third-party/licenses/PlantHost.Rpc-LICENSE.txt` |
| Newtonsoft.Json | `src/lib/net48` | MIT；见 `src/third-party/licenses/Newtonsoft.Json-LICENSE.md` |
| MCP 与 Microsoft.Extensions | NuGet 恢复/Release 资产 | MIT |

AVEVA SDK 不属于本项目。构建或运行 Net48 Host 时，必须由用户已获得许可的本机
AVEVA 安装提供 `PMLNet.dll` 和相关 `Aveva.*` 程序集。

## English

| Component | Location | License |
|---|---|---|
| PlantHost.Rpc | `src/lib/net10`, `src/lib/net48` | Apache-2.0; see `src/third-party/licenses/PlantHost.Rpc-LICENSE.txt` |
| Newtonsoft.Json | `src/lib/net48` | MIT; see `src/third-party/licenses/Newtonsoft.Json-LICENSE.md` |
| MCP and Microsoft.Extensions packages | NuGet restore / Release assets | MIT |

The AVEVA SDK is not part of this project. A licensed local AVEVA installation
must provide `PMLNet.dll` and the relevant `Aveva.*` assemblies when building
or running the Net48 host.
