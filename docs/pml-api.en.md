# Yuzuha Toolkit PML API

This preview targets AVEVA E3D 2.1, .NET Framework 4.8, and trusted local users.

## Read entry points

```pml
!rows = !!YuzuhaReadCurrentElement(200, 3, '')
!rows = !!YuzuhaReadDbref(200, 3, '', '/SAMPLE-ZONE/SAMPLE-EQUIPMENT')
!rows = !!YuzuhaReadGlobal(200, 3, '', 'YUZUHA_SAMPLE')
```

`YuzuhaObjectWalker` traverses CE, DBREF, PML Object, or Array values with item
and depth budgets. Attribute paths passed through RPC use dot separators such
as `Hposition.EAST` and `member.1`.

Pass one of these ARRAY-returning expressions directly as
`run_pml_command_list.pmlCommand`. Do not pass `!!TEMP = ...` or
`!!TEMP[1] = ...`; the host performs its own temporary assignment and `SIZE()`
check.

## File execution

```pml
!!YuzuhaTriggerCommand.InitArgs('C:\path\to\macro.pmlmac')
!!YuzuhaTriggerCommand.execute('ExecuteFile')
!result = !!YuzuhaTriggerCommand.Query()
```

Run the three lines separately. The object is supplied by
`PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd`.

File execution may change the active model. Invoke it only for an explicit
user request and never automatically retry after a timeout.

## Addin and runtime

Module registrations are under `PMLUI/cat|des|DRA|iso/Addins/YuzuhaAddin`.
The bootstrap loads the Net48 host from:

```text
runtime/net48/YuzuhaToolkit.PmlHost.Net48.dll
```

The repository-root `evar.example.txt` contains placeholders only. Never
commit a real workstation path.
