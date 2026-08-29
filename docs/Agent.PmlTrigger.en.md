# What an Agent Must Know About PmlTrigger.Yuzuha

[中文](Agent.PmlTrigger.zh-CN.md) | English

## 1. Components and ownership

- `runtime/win-x64-nativeaot/YuzuhaToolkit.Mcp.exe` is the .NET 10 Native AOT
  stdio MCP server. Each MCP client starts its own process; .NET 10 does not
  need to be installed.
- `runtime/net48` contains the PMLNet/RPC host loaded into AVEVA E3D.
- One E3D environment shares its registered `PMLUI` and `PMLLIB`; do not add a
  separate copy for every agent.
- The default local named pipe is `yuzuha.pml.command.v1`. It is transport, not
  an authorization boundary.

A `SKILL.md` teaches agent behavior, while ordinary Markdown documents the
project. Neither replaces the MCP process or the AVEVA-side host.

## 2. Standard workflow

1. Call `get_connection_status`. Use host-dependent tools only when the state
   is `Connected`.
2. Use `generate_pml_call` for drafting; it does not execute PML.
3. Use `run_pml_command_list` for object-graph or array results.
4. Execute `run_pml_command` or `run_pml_command_list` only after an explicit
   user request.
5. Read and record the target before a mutation, then read it again afterward.
6. Never automatically retry an execution after a timeout: the first request
   may already have changed E3D.
7. Preserve `Success`, `Code`, `ErrorMessage`, `RequestId`, and `PmlCommand` in
   the report.
8. Do not run `DESIGN` or switch modules as a repair attempt. A module switch
   can restart the project and disconnect heartbeat.

## 3. Reading E3D

`size` is a traversal/result budget, not a promised item count. Start with a
small budget and `depth=2`, then increase or target a branch only when needed.
`startStr` uses dot-separated paths such as `member.1` or `hpos.origin`; the
interactive-command-line `@` separator is not supported by this bridge.

### 3.1 Current element

```pml
!!YuzuhaReadCurrentElement(!size is real, !depth is real,
                           !startStr is string) is array

!!YuzuhaReadCurrentElement(20,2,'')
!!YuzuhaReadCurrentElement(20,3,'member')
!!YuzuhaReadCurrentElement(30,3,'hpos.origin')
```

Not every element exposes `HPOS` or every child property. An empty result does
not by itself prove a connection failure; also inspect the RPC status and
error fields.

### 3.2 Named element or DBREF

```pml
!!YuzuhaReadDbref(!size is real, !depth is real,
                  !startStr is string, !name is string) is array

!!YuzuhaReadDbref(20,3,'','100-FW-202')
!!YuzuhaReadDbref(20,3,'','/100-FW-202')
!!YuzuhaReadDbref(20,3,'member','=2013286668/56')
```

`name` may be a short name, a full slash-prefixed name, or an equals-prefixed
DBREF. The function temporarily changes CE and attempts to restore it; verify
CE after an exceptional return.

### 3.3 Global variable

```pml
!!YuzuhaReadGlobal(!size is real, !depth is real,
                   !startStr is string, !globalVar is string) is array

!!YuzuhaReadGlobal(20,3,'','YuzuhaRuntimePath')
```

Pass `globalVar` without `!!`. It is dynamically resolved and must therefore
come from a trusted source. Target a useful `startStr` when inspecting large
PMLOBJ or array values.

## 4. Executing functions and modifications

Prefer an existing typed PML function for a complex operation. For example:

```pml
!!YuzuhaCreateBoxExample(!localAtStr is string, !name is string,
                         !xlen is real, !ylen is real,
                         !zlen is real) is array

!!YuzuhaCreateBoxExample('ABC111','TestName2',50,50,50)
```

This changes the model. Confirm the target EQUI and dimensions before running.

### 4.1 Rename, attribute edit, and one-line command

This project supplies `YuzuhaTriggerCommand` through
`PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd`. Obsolete spellings are not part
of the current API. If the object is unavailable, inspect the PMLLIB search
path in `evars.init` and fully restart E3D.

```pml
!!YuzuhaTriggerCommand.InitArgs('/SITE-PIPING-AREA03')
!!YuzuhaTriggerCommand.execute('ExecuteCommand')
!!YuzuhaTriggerCommand.Query()

!!YuzuhaTriggerCommand.InitArgs('name /AgentRenameNow')
!!YuzuhaTriggerCommand.execute('ExecuteCommand')
!!YuzuhaTriggerCommand.Query()
```

Each line above is a separate RPC call. Never join `InitArgs` and `execute`
with a comma or semicolon. `$P /SITE/...` prints text and is not reliable CE
navigation; reread CE after navigating.

Its DELETE/KILL string warning is illustrative and is not a security sandbox.

### 4.2 Macro file

Use `ExecuteFile` only when the user explicitly requests execution of a known
file in an approved local directory:

```pml
!!YuzuhaTriggerCommand.InitArgs('C:\Approved\demo_v1.pmlmac')
!!YuzuhaTriggerCommand.execute('ExecuteFile')
!!YuzuhaTriggerCommand.Query()
```

Use a new filename when content changes. A macro may partially modify the
model before a later line fails, so always reread the intended targets before
correcting or retrying. See the
[macro and modification workflow](../skill/references/file-macro-workflow.md).

## 5. Spiral demonstration

The agent must know the site-provided spiral demonstration even though its
private implementation is not part of the public payload. If it is not loaded,
report that it is unavailable; do not invent a replacement.

```pml
!!NewSpiral(!Owner is string, !startHei is real, !TotalHei is real,
            !pipeDiam is real, !pipePerTimes is real,
            !equiOutDiam is real) is array

!!NewSpiral('/AGENT1',-1500,1680,108,140,2824)

!!RotateMyspiral(!equi is string) is array
!!RotateMyspiral('$!!ce.name')
```

Confirm the owner and every dimension before creation. Confirm that CE is the
intended spiral or equipment before rotation; a verified full name is safer
than a changing CE. Preserve the exact case returned by the latest read.

## 6. Success and failure

- `Connected` means a recent heartbeat succeeded; it does not prove that a PML
  command succeeded.
- RPC `Success=false` is a business failure. Text beginning with
  `PML RPC failed:` is a transport failure.
- A plain array returned by `Query()` may appear in `Unparsed` rather than
  `Items`; inspect both.
- The authoritative verification for a mutation is a fresh read of the target
  model state, not the command text, heartbeat, or local log alone.
- `run_pml_command_list.pmlCommand` must be an expression returning ARRAY, not
  a global assignment. Use `YuzuhaReadCurrentElement`, `YuzuhaReadDbref`, or
  `YuzuhaReadGlobal`.
