# PmlTrigger.Yuzuha

Experimental local bridge for agents to inspect and operate AVEVA E3D through
PML. A .NET 10 MCP server communicates with a .NET Framework 4.8 PMLNet host
over a versioned local named pipe.

> Preview software. It can execute arbitrary PML in the active E3D session.
> Use it only with trusted local users and explicit execution requests.

## Features

- Build typed PML global-method calls without touching E3D.
- Execute one explicitly requested PML command on the captured E3D main thread.
- Read CE, DBREF, and PML global-variable object graphs as structured JSON.
- Execute a PML macro through `!!YuzuhaTargetCommand.ExecuteFile(path)` and
  read its result with `!!YuzuhaTargetCommand.Query()`.

## Repository layout

```text
PMLLIB/   Production PML commands, bootstrap, and traversal functions
PMLUI/    Addin registration for CAT, DES, DRA, and ISO modules
src/      .NET 10 MCP server and .NET 4.8 PMLNet host source
samples/  Optional PML examples; not loaded by the production index
skill/    Codex skill instructions and MCP tool reference
docs/     Build, publishing, deployment, and API documentation
```

Runtime binaries are published as GitHub Release assets and are intentionally
not committed to Git history. AVEVA proprietary assemblies are never bundled.

## Build

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj
dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj -c Release --no-restore

dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\AVEVA
```

See [build configuration](docs/build-configuration.md) and
[deployment](skill/references/deployment.md) for details.

## Supported preview target

- Windows x64 agent host with .NET 10, or the self-contained executable.
- AVEVA Everything3D 2.1 with a licensed local PMLNet/.NET Framework 4.8 installation.
- Net35/legacy PDMS hosting is not included in this preview.

## Safety

The named pipe is a local transport, not an authorization boundary.
Execution tools can change the active model. Do not expose the pipe through a
network proxy. Never retry an execution automatically after a timeout because
the first call may already have completed in E3D. See [SECURITY.md](SECURITY.md).

## License

Project source is licensed under the [Apache License 2.0](LICENSE). Bundled
dependencies retain their own licenses; see [THIRD-PARTY.md](THIRD-PARTY.md).

---

这是一个帮助 Agent 通过 PML 读取和操作 AVEVA E3D 的实验性本地桥接项目。
当前为 `v0.1` 预览版，仅面向可信本机用户；执行型工具能够直接修改活动模型。
