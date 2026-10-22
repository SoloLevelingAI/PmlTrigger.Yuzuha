# PML TWIN / PmlTwin.Phayton — first command kernel

This development increment adds a C# command registry and two frontends. It does
not replace the released installer or deploy anything into AVEVA. The core is
`src/PmlTwin.Phayton`; Yuzuha remains the compatibility adapter.

## Registration and reuse

`OperationDescriptor` defines a stable dotted ID, contract version, description,
JSON input schema, PID requirement and execution requirement. `OperationRegistry`
rejects duplicate IDs and exposes a sorted immutable catalog. `OperationExecutor`
validates input and policy before calling an `IOperationHandler`. CLI and MCP use
the registrations in `YuzuhaOperationCatalog`; a future TUI/GUI can call the same
executor. No runtime reflection, assembly scanning or PML function discovery is
used to register operations.

| Stable ID (contract v1) | Input | PID | Explicit execution |
| --- | --- | --- | --- |
| `aveva.sessions.list` | `{}` | No | No |
| `aveva.session.select` | `{}` | Required | No |
| `aveva.session.status` | `{}` | Optional; without PID checks this process's selection | No |
| `pml.call.generate` | `methodName`, optional ordered `parameters` | No | No |
| `pml.command.execute` | `pmlCommand` | Required | Required |
| `pml.array.read` | `pmlCommand`, `globalVar`, `deleteGlobalVar`, optional `includeEmpty` | Required | Required |

`op describe` returns the complete input schema. Inputs reject unknown fields,
duplicate keys, missing fields, wrong types, blank commands and malformed global
names. The validator implements the schema subset used here: `type`, `anyOf`,
`properties`, `required`, closed objects, `items`, `enum`, `minLength`, `pattern`.
`default` in descriptors is informational; the array handler applies the documented
`includeEmpty=true` default. This is not a general-purpose JSON Schema engine.
Breaking behavior requires a new contract version/ID; IDs do not depend on CLR
method names or localized labels.

Generation accepts dot-separated global object/member methods such as
`YuzuhaExecuter.ExecuteFile` as well as global functions. The legacy MCP adapter
also preserves trimming and case-insensitive parameter type aliases; new JSON
operation inputs use the documented lowercase aliases.

## Build and CLI

On Windows with .NET 10 SDK:

```powershell
dotnet build src/PmlTwin.Phayton.Cli -c Release
$cli = '.\src\PmlTwin.Phayton.Cli\bin\Release\net10.0\PmlTwin.Phayton.Cli.exe'
& $cli op list
& $cli op describe pml.array.read
& $cli op invoke pml.call.generate --input '{"methodName":"YuzuhaReadCurrentElement","parameters":[{"type":"real","value":30},{"type":"real","value":2},{"type":"str","value":""}]}'
& $cli op invoke aveva.sessions.list
```

After the user chooses one returned PID, a separate CLI process must bind again:

```powershell
# Replace 12345 with a PID just returned by discovery. No PML is executed here.
& $cli op invoke aveva.session.status --pid 12345 --expected-model Design
# Only after the user explicitly requests this PML execution:
& $cli op invoke pml.command.execute --pid 12345 --expected-model Design --allow-execution --input '{"pmlCommand":"!!YuzuhaReadCurrentElement(30,2,\u0027\u0027)"}'
```

All CLI commands write exactly one JSON result to stdout. The envelope contains
`schemaVersion`, `operationId`, `success`, `exitCode`, `data`, and `error`.
`data` retains legacy PascalCase host/status fields; list summaries retain their
original field names. Generated call data is a string. `error.mayHaveExecuted`
is conservative after dispatch/cancellation; it is never permission to retry.

| Exit code | Meaning |
| --- | --- |
| 0 | Success |
| 2 | CLI/JSON/input contract error |
| 3 | Unknown operation ID |
| 4 | Execution not explicitly authorized |
| 5 | Missing PID, discovery/selection/identity failure, or no selected target |
| 6 | Host returned `Success=false`; original diagnostics remain in `data` |
| 7 | RPC/transport failure; execution may already have happened |
| 8 | Cancelled; do not retry automatically |
| 9 | Unexpected internal failure |

`select` only affects the current process. It does not save PID state to disk,
environment variables, or another CLI process. CLI execution always requires
`--pid` and `--allow-execution`. Pure generation/discovery never connect, even
when a caller supplies a PID. Array reads also run PML and can delete the result
global, so they use the same execution gate.

## RPC and legacy MCP compatibility

The original MCP executable/assembly remains `YuzuhaToolkit.Mcp`. Its six PML
tool names and parameter signatures remain available, as do trust and built-in
guide tools. The adapters preserve call-string output, host/status JSON, list
summary/filtering/unparsed rows, trust warnings and the `PML RPC failed:` marker.
An explicit call to an existing execution tool retains its established execution
semantics; it resolves and checks the selected PID, then uses the shared kernel.

Three additive tools expose the registry: `op_list`, `op_describe`, `op_invoke`.
`op_invoke` accepts `operationId`, `inputJson`, optional `processId` and
`expectedModel`, and `allowExecution` (false by default). Execution requires a
PID even if an earlier MCP call selected a session.

Each explicit-PID execution owns a fresh bridge/connection. Selection re-discovers
the visible AVEVA window and validates PID, process start ticks, exact PID pipe and
module. `RunVerifiedAsync` rechecks identity immediately before dispatch under
the existing connection lock. A failed selection clears the previous selection.
Connections keep the existing RPC client, heartbeat, service/operation names and
`yuzuha.pml.command.v1.pid-<PID>` convention; no shared-pipe fallback or execution
retry is added. NET35/NET48 Host code, AVEVA main-thread dispatch, PMLNET types,
Host DLL identities and installation layout are untouched.

The connection/discovery interfaces make rejection paths testable; production
frontends instantiate the existing concrete implementations. Adapter source is
linked into the CLI and compiled in the existing MCP assembly to retain the old
public type/assembly identities. The core library has no MCP, logging, vendor SDK
or RPC dependency. Relocating adapter types into a separate DLL is deferred until
assembly forwarding can be reviewed.

The execution flag records caller authorization; it is not a PML sandbox. The
existing Host still accepts explicitly requested PML text under the workstation's
established policy. This change does not introduce an unguarded alternative path.
TUI, GUI, domain-specific commands and trust mutation registration are future work.

## Verification

```powershell
dotnet run --project tests/Phayton -c Release
python -X utf8 tests/Phayton/frontends.py --cli src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.exe --mcp src/YuzuhaToolkit.Mcp/bin/Release/net10.0/win-x64/YuzuhaToolkit.Mcp.exe
dotnet publish src/PmlTwin.Phayton.Cli -c Release -r win-x64 -p:PublishAot=true -o artifacts/phayton/cli
dotnet publish src/YuzuhaToolkit.Mcp -c Release -o artifacts/phayton/mcp
python -X utf8 tests/Phayton/frontends.py --cli artifacts/phayton/cli/PmlTwin.Phayton.Cli.exe --mcp artifacts/phayton/mcp/YuzuhaToolkit.Mcp.exe
```

Native AOT needs the standard Windows C++ build toolchain. CI runs these checks
in `.github/workflows/phayton.yml`. Tests use synthetic services and impossible
discovery PIDs; they never execute PML in a real AVEVA session. See the dated
[validation record](validation/2026-10-16-phayton.md) for actual local results and
[PML review](phayton-pml-review.md) for the unmodified walker and paging boundary.
