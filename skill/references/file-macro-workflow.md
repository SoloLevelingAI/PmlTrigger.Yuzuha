# Modification and macro-file workflow

[中文](file-macro-workflow.zh-CN.md) | English

Read this reference for rename, attribute-edit, batch modification, or
file-backed PML execution through `!!YuzuhaTriggerCommand`.

## Plan the mutation

1. Inspect the target and relevant members before composing a command.
2. Enumerate every matching field and ask the user to confirm the scope: current
   element only, selected attributes, or descendants as well.
3. Prefer a verified DBREF for mutation. Names can contain inherited-looking or
   historical path segments and are not always reliable identifiers.
4. Record the original Name, Description, Owner, and other attributes needed to
   verify or manually reverse the change.

Parent and member names are independently stored. Renaming a parent does not
imply that member names will change.

## Compose the PML

A rename normally requires the leading slash:

```pml
!!CE = =24104/12345
!!CE.NAME = '/NEW-NAME'
!!CE.Description = 'New description'
```

Treat the DBREF and values above as syntax examples only. Use identifiers and
values verified in the active model. Escape user-supplied PML strings before
placing them in a macro.

For position changes, specify the `WRT` frame explicitly. Do not depend on the
ambient CE at the start of a modifying macro.

## Execute a file

Use a new path when macro content changes, for example `rename_v2.pmlmac` after
editing `rename_v1.pmlmac`. Observed AVEVA `$M` behavior can reuse content
associated with an earlier path.

```pml
!!YuzuhaTriggerCommand.InitArgs('C:\Approved\rename_v1.pmlmac')
!!YuzuhaTriggerCommand.execute('ExecuteFile')
!!YuzuhaTriggerCommand.Query()
```

Send every line as a separate `run_pml_command` call. In particular, never join
`InitArgs` and `execute` with a comma or semicolon; that combined form has been
observed to fail in `Command.Run`.

Only execute a file the user explicitly requested and whose contents are
known. Send the execution once. Never automatically retry a timeout.

## Verify at three levels

1. **Transport:** the RPC response arrived. This alone says nothing about the
   macro body.
2. **Wrapper:** inspect `Query()`. A plain string array can be placed in the
   MCP response's `Unparsed` field rather than `Items`.
3. **Model state:** reread each intended DBREF and compare its actual values to
   the approved target state. This is authoritative.

Do not treat one wrapper success message as proof that every line completed.
A macro is not a transaction: earlier lines may remain applied when a later
line fails. After any failure, reread the target before deciding whether a
corrected execution is safe.

## Query efficiently

Start with a small result budget, shallow depth, and a targeted path:

| Purpose | Suggested shape |
|---|---|
| Identity and direct attributes | `(10,2,'')` |
| Member list | `(20,2,'member')` |
| One member | `(20,2,'member.N')` |
| Position branch | `(10,1,'hpos')` |
| Broad discovery, only when needed | `(100,3,'')` |

These are starting points, not fixed limits. Increase them only when the first
read does not contain enough evidence.

An `Unparsed` entry caused by an attribute that is inapplicable to the current
element does not automatically invalidate confirmed core attributes. Preserve
the warning and evaluate it in context instead of ignoring every `Unparsed`
entry.

## Live-session constraints

- `$P /SITE/...` prints text and must not be treated as CE navigation. Execute
  the path through the deployed command wrapper, then reread CE.
- Model names can be case-sensitive. Copy owner and path casing from the most
  recent read; do not rewrite `/AGENT1` as `/Agent1`.
- Do not run `DESIGN` or switch modules to recover from a failed modeling
  command. A module switch can restart the project and disconnect the bridge.
- Stop mutations while disconnected. Wait for heartbeat recovery and reread
  live state; do not automatically retry.
