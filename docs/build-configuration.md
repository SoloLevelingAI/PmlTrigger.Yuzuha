# Build configuration

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

## Runtime profile selection

Set the selected build profile before starting AVEVA:

```bat
set Yuzuha=E3D2.1
```

Valid values are `AM`, `PDMS`, `E3D2.1`, `E3D3.1.0`, and `E3D3.1.6`.
The custom EVAR variable name is exactly `Yuzuha` (no underscore). The PML
bootstrap uses this value to select the matching DLL.
