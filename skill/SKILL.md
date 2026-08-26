---
name: yuzuha-toolkit
description: "Operate the YuzuhaToolkit MCP bridge against AVEVA E3D: generate PML global-method call text (generate_pml_call), execute one PML command on the captured AVEVA main thread via named-pipe RPC (run_pml_command), or dump the current element / any element's attribute graph into structured JSON (run_pml_command_list). The server is a bundled .NET 10 stdio MCP process; the .NET 4.8 YuzuhaToolkit.PmlHost.Net48 DLL is the host loaded inside AVEVA on pipe yuzuha.pml.command.v1. Use for requests to inspect the current element, query element attributes by name/path/DBREF, or run an explicitly requested E3D command. Enforce the explicit-request and no-auto-retry rules; this server has no FALSE-preview safety gate."
---

# E3D PML Toolkit (YuzuhaToolkit)

Use the `YuzuhaToolkit` MCP server tools
(`mcp__YuzuhaToolkit__*`) for AVEVA E3D work: build PML call text,
execute one PML command, or read element attribute trees as structured JSON.

Architecture:

- **Net10 MCP server** (`YuzuhaToolkit.Mcp.dll`, stdio) — the
  tools live here.
- **Net48 host** (`YuzuhaToolkit.PmlHost.Net48.dll`) — loaded inside AVEVA (E3D .NET
4.8 / PMLNet), serves RPC on named pipe `yuzuha.pml.command.v1`.

Treat the AVEVA session as live state: inspect before changing and report
exactly what ran.

## MCP tools on this server

| Tool | Effect | Side effects |
|---|---|---|
| `generate_pml_call` | Builds a `!!Method(...)` string from method name + ordered typed parameters | none (text only) |
| `run_pml_command` | Executes one PML command in AVEVA via named-pipe RPC | host-side; run only on explicit user request, never auto-retry |
| `run_pml_command_list` | Runs a PML expression that fills a global array, returns the array as structured JSON | host-side (runs PML); run only on explicit user request |

Full schemas, parameter tables, and response shapes:
[references/mcp-tools.md](references/mcp-tools.md).

## Workflow

1. Confirm the user explicitly requested execution before calling
   `run_pml_command` or `run_pml_command_list`. For discussion or drafting,
   use `generate_pml_call` only.
2. `generate_pml_call`: `methodName` without the leading `!!`; `parameters` is
   an ordered array of `{type, value}`; aliases `string/str`, `bool/boolean`,
   `double/real/number`; strings are single-quoted, booleans become `TRUE` /
   `FALSE`, numbers use invariant decimal formatting; use an empty array for a
   parameterless method.
3. `run_pml_command`: pass the complete generated text exactly once. Never
   retry automatically — a transport timeout can occur after AVEVA has already
   changed state.
4. `run_pml_command_list`: `pmlCommand` is the complete expression whose result
   is stored in the global array; `globalVar` is the array name without the
   `!!` prefix; set `deleteGlobalVar=true` to clean up after reading;
   `includeEmpty=false` removes unset/blank/empty-array items from `Items`
   (the `Summary` block always reflects the full set).
5. **Safety: this server has NO safety gate.** Unlike `engineering-agent-demo`,
   there is no TRUE→FALSE preview and no Elicitation. Commands execute exactly
   as given. Only execute commands the user explicitly asked to run.
6. Report: preserve `Success`, `Code`, `ErrorMessage`, `PmlCommand`, `Summary`,
   `Count`, `Items`, `UnparsedCount`, `Unparsed`, `RequestId`,
   `ServerRuntime`, `ServerTimeUtc`. Text beginning with `PML RPC failed:` or
   `PML RPC failed:` is a transport failure, not success.

## Deployment / diagnostics

- Prerequisites: Windows, .NET 10 runtime (framework-dependent server),
  AVEVA E3D hosting .NET 4.8 + PMLNet, PowerShell 5.1+.
- Load `YuzuhaToolkit.PmlHost.Net48.dll` in AVEVA through the site's PMLNet mechanism
  and construct it on the AVEVA main thread:

  ```pml
  !!PmlCommandHost = object PmlCommandMethod()
  !!PmlCommandHost.GetRpcServerStatus()   ! must return RUNNING
  ```

- Register the MCP server (stdio) with `dotnet.exe` pointing at the bundled
`assets/runtime/net10/YuzuhaToolkit.Mcp.dll`, cwd = that folder.
- See [references/deployment.md](references/deployment.md) for the DSH
  `cordis.patch.yml` snippet and troubleshooting.
