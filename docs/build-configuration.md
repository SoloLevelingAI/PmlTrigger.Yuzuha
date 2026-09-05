# Build configuration

> 中文版 / Chinese: [build-configuration.zh-CN.md](build-configuration.zh-CN.md)

AVEVA assemblies are proprietary prerequisites and are never committed or
redistributed by this project.

## AVEVA profiles

| Profile | Host | AVEVA utility assembly |
|---|---|---|
| `AM` | .NET Framework 3.5 x86 | `Aveva.Pdms.Utilities.dll` |
| `PDMS` | .NET Framework 3.5 x86 | `Aveva.Pdms.Utilities.dll` |
| `E3D2.1` | .NET Framework 4.8 x86 | `Aveva.Core.Utilities.dll` |
| `E3D3.1.0` | .NET Framework 4.8 x86 | `Aveva.Core.Utilities.dll` |
| `E3D3.1.6` | .NET Framework 4.8 x86 | `Aveva.Core.Utilities.dll` |

Build every profile with:

```powershell
.\scripts\Build-AvevaProfiles.ps1 `
  -ProfileRoot 'D:\AVEVA\AvevaProfile'
```

The script writes directly loadable output to
`runtime/profiles/<profile>/<framework>` and the MCP to `runtime/net10`.
AVEVA's `PMLNet.dll` and utility assembly are reference-only and are not copied.
The MCP output is one trimmed, self-contained Windows x64 executable:
`runtime/net10/YuzuhaToolkit.Mcp.exe`. PlantHost.Rpc, its RPC contracts, and
Newtonsoft.Json are rooted because their dynamic proxy and serialization paths
use reflection; removing those roots is not a supported size optimization.

For one host build, pass `AvevaProfileRoot` and `AvevaProfile` to MSBuild:

```powershell
dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaProfileRoot='D:\AVEVA\AvevaProfile' `
  /p:AvevaProfile='E3D3.1.6'
```

Direct `AvevaInstallDir`, `AVEVA_INSTALL_DIR`, and local
`src/build/Aveva.Local.props` configuration remain supported for one profile.

## Local one-off profile builds

When an AVEVA version has no prebuilt profile, build a locally named profile
against the user's own installation. Only the NET35/NET48 PMLNet host is
built this way — it is the only component tied to the AVEVA version. The
Net10 MCP and Knowledge servers do not depend on AVEVA at all and are never
rebuilt on user machines:

```powershell
.\scripts\Build-LocalHost.ps1 `
  -AvevaInstallDir 'C:\AVEVA\Everything3D' `
  -ProfileName 'E3D3.2.0' `
  -OutputRoot '<install>\runtime\profiles'
```

The script derives the family from the utilities DLL next to `PMLNet.dll`
(E3D→net48, AM/PDMS→net35), refuses to shadow prebuilt profile names, prints
a redistribution/compatibility warning, and selects the matching host
project. It needs a source checkout (clone or source release) because the
agent package ships binaries only; the skill reference
`skill/references/local-build.md` describes the agent flow: inform the user,
collect the environment, ask for consent, state the risks, build, then
select the profile with `set Yuzuha=<name>` and verify
`GetRpcServerStatus()`.

## Knowledge server

`src/YuzuhaToolkit.Knowledge` publishes as a Native AOT single executable
(requires the Visual Studio C++ linker):

```powershell
dotnet publish src\YuzuhaToolkit.Knowledge\YuzuhaToolkit.Knowledge.csproj `
  -c Release -r win-x64
```

`Microsoft.Data.Sqlite` bundles `e_sqlite3.dll`, which includes FTS5; the
file ships next to the exe in `runtime\net10`.

## Runtime profile selection

Set the selected build profile before starting AVEVA:

```bat
set Yuzuha=E3D2.1
```

Valid values are `AM`, `PDMS`, `E3D2.1`, `E3D3.1.0`, and `E3D3.1.6`.
The custom EVAR variable name is exactly `Yuzuha` (no underscore). The PML
bootstrap uses this value to select the matching DLL.
