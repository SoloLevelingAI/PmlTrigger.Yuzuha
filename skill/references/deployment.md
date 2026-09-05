# Deployment — PID-bound MCP and AVEVA hosts

> 中文版 / Chinese: [deployment.zh-CN.md](deployment.zh-CN.md)

Release archives use this layout:

```text
PmlTrigger.Yuzuha/
├─ PMLLIB/
├─ PMLUI/
├─ runtime/
│  ├─ profiles/
│  │  ├─ AM/net35/
│  │  ├─ PDMS/net35/
│  │  ├─ E3D2.1/net48/
│  │  ├─ E3D3.1.0/net48/
│  │  └─ E3D3.1.6/net48/
│  └─ net10/            YuzuhaToolkit.Mcp.exe
│                       YuzuhaToolkit.Knowledge.exe (+ e_sqlite3.dll)
└─ skill/
```

AVEVA proprietary assemblies are not redistributed, and the local knowledge
database (`knowledge\*.sqlite3`) is built at runtime, never packaged.

For guarded Agent installation, update, and uninstall, use the lifecycle
scripts described in [lifecycle.md](lifecycle.md). Those scripts install to a
stable local path, manage the Skill, validate MCP ownership, and refuse to
overwrite or delete unmarked directories.

## Start the AVEVA host

Add `PMLLIB` and `PMLUI` to the corresponding AVEVA environment paths and set
the matching profile before starting AVEVA:

```bat
set Yuzuha=E3D2.1
```

The custom EVAR variable is exactly `Yuzuha`; do not add an underscore.

The bootstrap reads the current module with:

```pml
!!YuzuhaModel = !!fmsys.FMINFO()[0].SPLIT()[3]
```

It passes that value, for example `Design`, into the PMLNet host. The default
pipe is bound to the actual AVEVA process ID:

```text
yuzuha.pml.command.v1.pid-<AVEVA-PID>
```

An explicit `YUZUHA_PML_PIPE` may override the name, but the MCP still verifies
the host-reported PID, process start time, and module before every execution.

Verify in AVEVA:

```pml
!!YuzuhaRpcHost.GetRpcServerStatus()
```

The expected result is `RUNNING`.

## Register one generic Codex MCP

The Net10 MCP is a trimmed, self-contained Windows x64 single executable. It
starts disconnected and does not use PID/model environment variables. Register
it once:

```powershell
.\scripts\Register-YuzuhaMcp.ps1 `
  -McpExecutable '.\runtime\net10\YuzuhaToolkit.Mcp.exe'
```

The registration script is safe to run again while installing or updating the
Skill. It checks `codex mcp list --json` first:

- Same enabled stdio executable and no arguments: reuse it without writing.
- No entry: add it.
- Same name but a different command, arguments, transport, or disabled state,
  or a possible Yuzuha entry under another name: stop and report the discovered
  configurations. It never adds a duplicate, removes, or overwrites an MCP
  automatically.

If a conflict is intentional, inspect it first and remove it explicitly with
`codex mcp remove YuzuhaToolkit`; then rerun the script. When the package
ships the knowledge server, the same script registers
`YuzuhaToolkitKnowledge` (executable `runtime\net10\YuzuhaToolkit.Knowledge.exe`)
under the same conflict rules; see
[knowledge-base.md](knowledge-base.md) for its tools and copyright rules.

For legacy AM or PDMS, the same script can back up and update `evar.bat` or
`evars.bat`. Supply the file explicitly because AVEVA installation layouts
vary:

```powershell
.\scripts\Register-YuzuhaMcp.ps1 `
  -McpExecutable '.\runtime\net10\YuzuhaToolkit.Mcp.exe' `
  -AvevaProfile PDMS `
  -EvarBat 'D:\AVEVA\Plant\PDMS12.1.SP4\evars.bat'
```

Use `-AvevaProfile AM` for AM. The managed block is idempotent, sets `Yuzuha`,
prepends this package's `PMLLIB` and `PMLUI`, and creates a timestamped backup
before changing the file. To configure EVAR without changing Codex MCP
registration, add `-SkipMcpRegistration` and omit `-McpExecutable`. Fully
restart AM or PDMS afterwards.

At runtime:

1. Call `list_aveva_sessions`. It reads visible top-level AVEVA window titles
   and process metadata without opening an RPC connection.
2. Identify the intended `Product` and `Project` from `WindowTitle`. If zero or
   multiple sessions are plausible, stop for an explicit choice; never guess.
3. Call `select_aveva_session` with one exact PID returned by discovery. It
   connects only to `yuzuha.pml.command.v1.pid-<PID>` and verifies PID, process
   start time, pipe, and optional expected module.
4. Call `get_connection_status`; proceed only when `TargetVerified=true`.

The legacy shared pipe `yuzuha.pml.command.v1` is never used as a fallback.
Execution tools can modify the active model; call them only for explicit
requests and never automatically retry a timeout.

Window discovery requires the MCP and AVEVA to run in the same interactive
Windows session. If AVEVA is elevated, run the MCP at a compatible integrity
level so it can read the window and open its local pipe.
