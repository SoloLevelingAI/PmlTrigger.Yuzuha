---
name: yuzuha-toolkit
description: "Operate the PID-bound YuzuhaToolkit bridge against AVEVA AM, PDMS, or E3D: verify the selected PID/module, generate PML calls, execute an explicit PML command, or read object graphs. The .NET 10 MCP connects to a NET35 or NET48 PMLNet host on pipe yuzuha.pml.command.v1.pid-<PID>. Enforce explicit execution and no automatic retries."
---

# E3D PML Toolkit (YuzuhaToolkit)

Use the `YuzuhaToolkit` MCP server tools
(`mcp__YuzuhaToolkit__*`) for AVEVA E3D work: build PML call text,
execute one PML command, or read element attribute trees as structured JSON.

Architecture:

- **Net10 MCP server** (`YuzuhaToolkit.Mcp.exe`, trimmed self-contained
  single-file stdio server) — starts disconnected and discovers visible AVEVA
  windows before selection.
- **NET35/NET48 host** — selected by the AVEVA profile and loaded inside
  AM/PDMS or E3D. The default pipe is
  `yuzuha.pml.command.v1.pid-<AVEVA-PID>`.

Treat the AVEVA session as live state: inspect before changing and report
exactly what ran.

## MCP tools on this server

| Tool | Effect | Side effects |
|---|---|---|
| `list_aveva_sessions` | Lists visible AVEVA windows, projects, PIDs, start times, and PID-pipe availability without connecting | none |
| `select_aveva_session` | Explicitly connects one returned PID and verifies PID, start time, pipe, and module | opens local RPC only; no PML |
| `get_connection_status` | Re-verifies the explicitly selected session | none |
| `generate_pml_call` | Builds a `!!Method(...)` string from method name + ordered typed parameters | none (text only) |
| `run_pml_command` | Executes one PML command in AVEVA via named-pipe RPC | host-side; run only on explicit user request, never auto-retry |
| `run_pml_command_list` | Runs a PML expression that fills a global array, returns the array as structured JSON | host-side (runs PML); run only on explicit user request |

Full schemas, parameter tables, and response shapes:
[references/mcp-tools.md](references/mcp-tools.md).

## Workflow

1. Call `list_aveva_sessions`. Never guess a PID and never auto-select when
   multiple sessions are returned. Identify the target from `WindowTitle`,
   `Product`, and `Project`; use only a PID returned by this call.
2. Call `select_aveva_session` explicitly. Proceed only when it returns
   `TargetVerified=true`; the legacy shared pipe is never a fallback.
3. Call `get_connection_status` before execution when reachability or current
   module is uncertain.
4. Confirm the user explicitly requested execution before calling
   `run_pml_command` or `run_pml_command_list`. For discussion or drafting,
   use `generate_pml_call` only.
5. `generate_pml_call`: `methodName` without the leading `!!`; `parameters` is
   an ordered array of `{type, value}`; aliases `string/str`, `bool/boolean`,
   `double/real/number`; strings are single-quoted, booleans become `TRUE` /
   `FALSE`, numbers use invariant decimal formatting; use an empty array for a
   parameterless method.
6. `run_pml_command`: pass the complete generated text exactly once. Never
   retry automatically — a transport timeout can occur after AVEVA has already
   changed state.
7. `run_pml_command_list`: `pmlCommand` is the complete expression whose result
   is stored in the global array; `globalVar` is the array name without the
   `!!` prefix; set `deleteGlobalVar=true` to clean up after reading;
   `includeEmpty=false` removes unset/blank/empty-array items from `Items`
   (the `Summary` block always reflects the full set).
8. **Safety: this server has NO safety gate.** Unlike `engineering-agent-demo`,
   there is no TRUE→FALSE preview and no Elicitation. Commands execute exactly
   as given. Only execute commands the user explicitly asked to run.
9. Report: preserve `Success`, `Code`, `ErrorMessage`, `PmlCommand`, `Summary`,
   `Count`, `Items`, `UnparsedCount`, `Unparsed`, `RequestId`,
   `ServerRuntime`, `ServerTimeUtc`. Text beginning with `PML RPC failed:` or
   `PML RPC failed:` is a transport failure, not success.

## Deployment / diagnostics

- For installation, update, or uninstall requests, read
  [references/lifecycle.md](references/lifecycle.md) and use the matching lifecycle
  script. Run update/uninstall from an extracted setup archive, never from the
  managed installation directory. Do not bypass management-marker or MCP
  conflict checks.

- Prerequisites: Windows, a supported AVEVA NET35/NET48 PMLNet host, and
  PowerShell 5.1+. The MCP is self-contained and does not require .NET 10 to be
  installed separately.
- Select EVAR variable `Yuzuha` (no underscore), load the matching profile Host, and construct
  it on the AVEVA main thread with the current module:

  ```pml
  !!YuzuhaModel = !!fmsys.FMINFO()[0].SPLIT()[3]
  !!PmlCommandHost = object PmlCommandMethod()
  !!PmlCommandHost.RefreshModel(!!YuzuhaModel)
  !!PmlCommandHost.GetRpcServerStatus()   ! must return RUNNING
  ```

- During installation, run `scripts/Register-YuzuhaMcp.ps1`. It inspects
  `codex mcp list --json` before registration. An enabled stdio
  entry pointing to the same executable with no arguments is reused unchanged;
  a conflicting, disabled, or differently named Yuzuha entry stops installation
  with the discovered configurations reported. Never add a duplicate, remove,
  or overwrite an existing MCP automatically.
- Do not store PID/model environment variables in Codex; discover and select a
  live session at runtime.
- See [references/deployment.md](references/deployment.md) for the DSH
  `cordis.patch.yml` snippet and troubleshooting.
