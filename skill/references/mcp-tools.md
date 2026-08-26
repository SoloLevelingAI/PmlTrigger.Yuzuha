# MCP tools — YuzuhaToolkit

Server: **YuzuhaToolkit** (stdio .NET 10 MCP server).
Exposed tool names in the agent runtime: `mcp__YuzuhaToolkit__generate_pml_call`,
`mcp__YuzuhaToolkit__run_pml_command`, and
`mcp__YuzuhaToolkit__run_pml_command_list`.

The server talks to the host DLL `YuzuhaToolkit.PmlHost.Net48` (loaded in AVEVA) over
named pipe `yuzuha.pml.command.v1` (connect timeout 3000 ms, heartbeat 2 s).

## Tool 1: generate_pml_call

Builds a PML global-method call string from a method name and an ordered
dynamic parameter array. **Text only — never executes PML.**

| Parameter | Type | Description |
|---|---|---|
| `methodName` | string | PML method name without the leading `!!` or parentheses. |
| `parameters` | array (nullable) | Ordered `{type, value}` items. Use an empty array for a parameterless method. |

Supported type aliases: `string/str`, `bool/boolean`, `double/real/number`.
Strings are single-quoted and escaped; booleans become `TRUE` / `FALSE`;
numbers use invariant decimal formatting (so `2.0` may normalize to `2`).

Example in → out:

```text
[{type:bool, value:true}, {type:string, value:测试}]
→ !!BatchCrtAnciForCheck(TRUE,'测试')
```

## Tool 2: run_pml_command

Executes one already-generated PML command inside AVEVA through named-pipe RPC.
**Host-side effects. Call only when the user explicitly asks to execute, and
never retry automatically.**

| Parameter | Type | Description |
|---|---|---|
| `pmlCommand` | string | Complete PML command text, e.g. `!!TestAgent4(TRUE,2,'你好')`. |

Returns a JSON string of the RPC response. On transport failure the result is
`PML RPC failed: <message>` (not success).

Key response fields (preserve when reporting):

- `Success` (bool) — `false` is a business-level failure, not a transport one.
- `Code`, `ErrorMessage` — error classification / message.
- `PmlCommand` — echo of the executed expression.
- `RequestId`, `ExecutionThreadId` — correlation.
- `ServerRuntime` — host runtime version string (e.g. `4.0.30319.42000`).
- `ServerTimeUtc` — host-side timestamp.

## Tool 3: run_pml_command_list

Runs a PML expression whose result is stored in a global array variable, then
returns the whole array parsed into structured JSON for AI consumption (no E3D
command-line printing).

| Parameter | Type | Default | Description |
|---|---|---|---|
| `pmlCommand` | string | — | Complete expression assigned to the global array, e.g. `!!ATestGetByce20260823(300,2,'')`. |
| `globalVar` | string | — | Global array variable name **without** the `!!` prefix, e.g. `PMLGLOBALARRFORRPC`. |
| `deleteGlobalVar` | bool | — | Whether to delete the global array variable after reading it. |
| `includeEmpty` | bool | `true` | `false` filters empty items out of `Items`; `Summary` still reflects the full set. |

Element-location arguments accept a short name (`'SAMPLE-EQUIPMENT'`), full
path (`'/SAMPLE-ZONE/SAMPLE-EQUIPMENT'`), or a DBREF such as (`'=1000/1'`).

### Response shape

```json
{
  "Success": true, "Code": "OK", "ErrorMessage": null,
  "PmlCommand": "!!ATestGetByce20260823(300,2,'')",
  "Summary": { "total": 46, "unset": 4, "blank": 6, "zero": 11,
               "emptyArray": 0, "hasValue": 36,
               "byType": {"DBREF": 11, "STRING": 16, "BOOLEAN": 2,
                          "REAL": 11, "POSITION": 2, "ORIENTATION": 2,
                          "ARRAY": 2} },
  "Count": 36,
  "Items": [ { "depth": 0, "path": "", "type": "DBREF",
               "value": "=1000/2", "empty": false },
             { "depth": 1, "path": "Name", "type": "STRING",
               "value": "/SAMPLE-ZONE", "empty": false } ],
  "IncludeEmpty": false,
  "UnparsedCount": 0,
  "Unparsed": [],
  "RequestId": "...", "ServerRuntime": "4.0.30319.42000",
  "ServerTimeUtc": "..."
}
```

Each `Item` is `{depth, path, type, value, empty}`:

- `depth` — BFS depth in separator hops; the element row is depth 0 with empty
  `path`.
- `path` — attribute path (`.` separators via the bridge), empty on the
  element row.
- `type` — PML type: `STRING`, `DBREF`, `REAL`, `BOOLEAN`, `POSITION`,
  `ORIENTATION`, `ARRAY`, ...
- `value` — normalized value; `null` when unset/blank/empty-array.
- `empty` — true for unset / blank / `0 Elements` values.

### Normalization (L1/L2/L3)

- **L1 (item)**: STRING values are unquoted; `Unset`, blank, and `0 Elements`
  array values become `value=null, empty=true`. Zero REALs are NOT flagged as
  empty.
- **L2 (Summary)**: aggregated over the FULL set (before `includeEmpty`
  filtering): `unset` (empty non-STRING/ARRAY), `blank` (empty STRING),
  `zero` (REAL numerically zero), `emptyArray`, `hasValue`, `byType`.
- **L3 (filtering)**: `includeEmpty=false` removes empty items from `Items`;
  `Summary` and `Unparsed` still reflect the full set.

### Success / failure signaling

Host success criterion (`GetPmlVariableList`): both the array-assignment
(`ok1`) and `SIZE` (`ok2`) steps must run true. On failure the host throws and
the RPC returns `Success=false`, `Code=PML_COMMAND_EXCEPTION`, and an
`ErrorMessage` containing `ok1/ok2/pmlCommand`. A `Success=true` response with
an empty `ResultList` is a valid empty result, not a silent failure.

## Bridge constraints

- Attribute-path separator through the RPC bridge is `.` (`'hpos'`,
  `'Hposition.EAST'`, `'member.1'`, `'member'`). The `@` character is rejected
  by the host `Command.CreateCommand(...).Run()` parser and works only on the
  interactive E3D command line.
- Depth recommendation: first read = `depth 2` (element + direct attributes +
  first-level reference expansion); deeper detail should use a targeted
  attribute path (`'member'`, `'member.N'`) instead of blanket depth 3+, which
  grows exponentially and is capped by the method's `maxCount`.
