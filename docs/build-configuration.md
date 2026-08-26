# Build configuration

AVEVA assemblies are proprietary prerequisites and are never committed or
redistributed by this project.

Configure the AVEVA SDK using one of these interfaces:

1. Copy `src/build/Aveva.Local.props.example` to
   `src/build/Aveva.Local.props` and set `AvevaInstallDir`.
2. Set the `AVEVA_INSTALL_DIR` environment variable.
3. Pass `/p:AvevaInstallDir=C:\path\to\AVEVA` to MSBuild.

`Aveva.Local.props` is ignored by Git.

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj
dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj -c Release --no-restore

dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\E3D
```

This preview targets AVEVA E3D 2.1 and .NET Framework 4.8. Net35/legacy PDMS
hosting is outside the current release scope.
