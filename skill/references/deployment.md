# Deployment — Net10 Native AOT MCP and Net48 AVEVA host

[中文](deployment.zh-CN.md) | English

## Release layout

```text
PmlTrigger.Yuzuha/
├─ PMLLIB/
├─ PMLUI/
├─ runtime/
│  ├─ net48/
│  └─ win-x64-nativeaot/
├─ docs/
└─ skill/
```

The target requires Windows x64, PowerShell 5.1+, and a licensed AVEVA E3D
2.1/.NET Framework 4.8/PMLNet environment. The Native AOT MCP does not require
an installed .NET 10 runtime. Proprietary AVEVA assemblies are not distributed.

## Copy-mode installation

From the Release root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-Yuzuha.ps1 -RegisterCodex -InstallCodexSkill
```

`-InstallCodexSkill` installs only the Codex copy; it does not install a
DeepSeek Harness Skill. When an agent receives this Release, have it read
`INSTALL-SKILL.en.md` in the Release root. The expected user-level DeepSeek
layout is:

```text
%USERPROFILE%\.dsh\skills\yuzuha-toolkit\
|-- SKILL.md
`-- references\
```

Copy the entire contents of `skill`, not only `SKILL.md`. Restart Harness or
start a new session afterward so its Skill catalog is rebuilt.

The default destination is `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha`.
Before changing `evars.init`, the installer creates a timestamped backup and
registers the installed `PMLUI` and `PMLLIB` paths.

For a non-standard installation:

```powershell
.\Install-Yuzuha.ps1 `
  -EvarsInitPath 'D:\AVEVA\Everything3D2.10\evars.init' `
  -RegisterCodex -InstallCodexSkill
```

## AVEVA host startup

`YuzuhaAddin` loads `!!YuzuhaRpcCommand`. Its construction resolves and imports
`runtime/net48/YuzuhaToolkit.PmlHost.Net48.dll`, constructs the host on the
AVEVA main thread, and starts pipe `yuzuha.pml.command.v1`. Repeated host
construction is idempotently handled by the Net48 host.

Verify in AVEVA:

```pml
!!YuzuhaRpcHost.GetRpcServerStatus()
```

The expected value is `RUNNING`. Then call `get_connection_status` from the
agent; only `Connected` means that a recent heartbeat succeeded.

## Manual MCP registration

If installation did not use `-RegisterCodex`:

```powershell
codex mcp add YuzuhaToolkit -- `
  "$env:LOCALAPPDATA\YuzuhaToolkit\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe"
```

Run the MCP executable and AVEVA in the same Windows user session. They must
use the same `YUZUHA_PML_PIPE`; when unset, both use
`yuzuha.pml.command.v1`.

## Diagnostic order

1. Confirm that `evars.init` contains the intended PMLUI and PMLLIB paths.
2. Inspect `!!YuzuhaRuntimePath` and `!!YuzuhaAutoSetup`.
3. Verify that `GetRpcServerStatus()` returns `RUNNING`.
4. Call `get_connection_status` and inspect its last error and failure count.
5. Run a read-only query only after connection succeeds. Do not create a BOX
   or spiral merely to test connectivity.
6. Confirm that the loaded command is `!!YuzuhaTriggerCommand`, supplied by
   `PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd`. If it is unavailable, inspect
   the PMLLIB order in `evars.init` and fully restart E3D.
