# Third-party dependencies

> 中文版 / Chinese: [THIRD-PARTY.zh-CN.md](THIRD-PARTY.zh-CN.md)

| Component | Location | License |
|---|---|---|
| PlantHost.Rpc | `src/lib/net10`, `src/lib/net35`, `src/lib/net48` | Apache-2.0; see `src/third-party/licenses/PlantHost.Rpc-LICENSE.txt` |
| Newtonsoft.Json | `src/lib/net10`, `src/lib/net35`, `src/lib/net48` | MIT; see `src/third-party/licenses/Newtonsoft.Json-LICENSE.md` |
| MCP and Microsoft.Extensions packages | NuGet restore / Release assets | MIT |
| Microsoft.Data.Sqlite 10.0.0 | Knowledge server | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 2.1.13 | Knowledge server / `e_sqlite3.dll` | Apache-2.0; native SQLite is public domain |

The AVEVA SDK is not part of this project. A licensed local AVEVA installation
must provide `PMLNet.dll` and the relevant `Aveva.*` assemblies when building
or running the NET35/NET48 host.
