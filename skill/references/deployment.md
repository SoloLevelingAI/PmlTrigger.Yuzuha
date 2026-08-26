# Deployment — Net10 MCP server + Net48 AVEVA host

Release archives use this layout:

```text
PmlTrigger.Yuzuha/
├─ PMLLIB/
├─ PMLUI/
├─ runtime/
│  ├─ net48/
│  ├─ net10/                    # framework-dependent option
│  └─ win-x64-self-contained/  # no installed .NET 10 required
└─ skill/
```

## Prerequisites

- Windows and PowerShell 5.1 or newer.
- A licensed AVEVA E3D 2.1 installation with PMLNet/.NET Framework 4.8.
- .NET 10 runtime only when using the framework-dependent Net10 package.

AVEVA proprietary assemblies are not redistributed.

## Start the Net48 host

Add the extracted `PMLLIB` and `PMLUI` folders to the corresponding AVEVA
environment paths. `YuzuhaAddin` resolves and imports
`runtime/net48/YuzuhaToolkit.PmlHost.Net48.dll`, constructs the PMLNet host on
the AVEVA main thread, and starts named pipe `yuzuha.pml.command.v1`.

Verify in AVEVA:

```pml
!!YuzuhaRpcHost.GetRpcServerStatus()
```

The expected result is `RUNNING`.

## Register the MCP server

Framework-dependent:

```yaml
command: 'C:\Program Files\dotnet\dotnet.exe'
args:
  - '<PKG>\runtime\net10\YuzuhaToolkit.Mcp.dll'
cwd: '<PKG>\runtime\net10'
```

Self-contained:

```yaml
command: '<PKG>\runtime\win-x64-self-contained\YuzuhaToolkit.Mcp.exe'
args: []
cwd: '<PKG>\runtime\win-x64-self-contained'
```

Execution tools can modify the active model. Call them only for explicit user
requests and never automatically retry a timed-out execution.
