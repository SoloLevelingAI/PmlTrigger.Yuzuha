# Net10 / Net48 Build and Publishing

## Verified matrix

| Target | Result | Notes |
|---|---|---|
| Net10 framework-dependent | Passed, 0 warnings / 0 errors | Requires .NET 10 on the target |
| Net10 win-x64 self-contained single-file | Published and startup-tested | No installed .NET 10 required |
| Net10 win-x64 Native AOT | Passed with 0 warnings / 0 errors; startup and MCP handshake tested | No installed .NET 10 required |
| Net48 x86 PMLNet host | Passed | Built against AVEVA Everything3D 2.10 assemblies |

## Framework-dependent Net10

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj --configfile src\NuGet.config
dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj -c Release --no-restore
```

## No installed .NET 10

Use the compatibility-first self-contained single-file profile:

```powershell
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-self-contained
```

The output is one `YuzuhaToolkit.Mcp.exe`. The target Windows x64 machine does
not need an installed .NET 10 runtime.

## Native AOT

The project uses `PlantHost.Rpc.Net10`, explicit RPC routes, and
source-generated JSON metadata. It contains no `DispatchProxy`, Newtonsoft.Json,
or reflection-generated RPC proxy. Publish with:

```powershell
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot
```

The build machine requires Visual Studio **Desktop Development for C++**. The
output is `artifacts/publish/win-x64-nativeaot`. The native executable has been
validated through MCP initialize, tools/list, `get_connection_status`, repeated
heartbeat state transitions, and graceful shutdown.

An isolated `YUZUHA_PML_PIPE=yuzuha.pml.command.v1.codex-aot-smoke` test also
completed a real RPC round trip against the AVEVA-free NET48 smoke host. The
heartbeat reported `Connected` with zero failures and `run_pml_command`
returned `Code=SMOKE_OK`. Actual AVEVA main-thread execution must still be
accepted in an E3D/PMLNet installation.

## Net48

```powershell
dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\AVEVA
```

Proprietary AVEVA assemblies are resolved only from the local installation and
are never included in the open-source repository or delivery package.
