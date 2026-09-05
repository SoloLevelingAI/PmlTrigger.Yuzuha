# Third-party dependencies / 第三方依赖

> 中文版（供作者审阅）。英文版 / English: [THIRD-PARTY.md](THIRD-PARTY.md)

| Component | Location | License |
|---|---|---|
| PlantHost.Rpc | `src/lib/net10`, `src/lib/net35`, `src/lib/net48` | Apache-2.0; see `src/third-party/licenses/PlantHost.Rpc-LICENSE.txt` |
| Newtonsoft.Json | `src/lib/net10`, `src/lib/net35`, `src/lib/net48` | MIT; see `src/third-party/licenses/Newtonsoft.Json-LICENSE.md` |
| MCP and Microsoft.Extensions packages | NuGet restore / Release assets | MIT |
| Microsoft.Data.Sqlite 10.0.0 | 知识库服务器 | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 2.1.13 | 知识库服务器 / `e_sqlite3.dll` | Apache-2.0；原生 SQLite 属于公有领域 |

The AVEVA SDK is not part of this project. A licensed local AVEVA installation
must provide `PMLNet.dll` and the relevant `Aveva.*` assemblies when building
or running the NET35/NET48 host.

AVEVA SDK 不属于本项目。构建或运行 NET35/NET48 Host 时，`PMLNet.dll` 及相关
`Aveva.*` 程序集必须来自本机已获许可的 AVEVA 安装。
