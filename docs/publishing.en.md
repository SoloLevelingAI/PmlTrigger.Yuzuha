# Net10 / Net48 Build and Publishing

## Verified matrix

| Target | Result | Notes |
|---|---|---|
| Net10 framework-dependent | Passed, 0 warnings / 0 errors | Requires .NET 10 on the target |
| Net10 win-x64 self-contained single-file | Published and startup-tested | No installed .NET 10 required |
| Net10 win-x64 Native AOT | Code analysis passed; final link not completed | Build machine lacks the Visual Studio C++ linker |
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

Tool-response JSON now uses source-generated metadata, and the project includes
this profile:

```powershell
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot
```

The build machine requires Visual Studio **Desktop Development for C++**. The
review machine has no `link.exe`, so no unverified AOT binary is included.
After installing the toolchain, continue with runtime validation of
PlantHost.Rpc proxy generation and MCP tool discovery.

## Net48

```powershell
dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\AVEVA
```

Proprietary AVEVA assemblies are resolved only from the local installation and
are never included in the open-source repository or delivery package.
