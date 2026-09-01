# Yuzuha Toolkit PML API

This preview targets AM/PDMS NET35, E3D 2.1/3.1 NET48, and trusted local users.

## Read entry points

```pml
!rows = !!YuzuhaReadCurrentElement(200, 3, '')
!rows = !!YuzuhaReadDbref(200, 3, '', '/SAMPLE-ZONE/SAMPLE-EQUIPMENT')
!rows = !!YuzuhaReadGlobal(200, 3, '', 'YUZUHA_SAMPLE')
```

`YuzuhaObjectWalker` traverses CE, DBREF, PML Object, or Array values with item
and depth budgets. Attribute paths passed through RPC use dot separators such
as `Hposition.EAST` and `member.1`.

## File execution

```pml
!!YuzuhaExecuter.ExecuteFile('C:\path\to\macro.pmlmac')
!result = !!YuzuhaExecuter.Query()
```

File execution may change the active model. Invoke it only for an explicit
user request and never automatically retry after a timeout.

## Addin and runtime

Module registrations are under `PMLUI/cat|des|DRA|iso/Addins/YuzuhaAddin`.
The bootstrap selects a host using EVAR variable `Yuzuha` (no underscore):

```text
runtime/profiles/<Profile>/<net35|net48>/YuzuhaToolkit.PmlHost.<Net35|Net48>.dll
```

It obtains the current module and passes it to the host with:

```pml
!!YuzuhaModel = !!fmsys.FMINFO()[0].SPLIT()[3]
```

The default pipe is `yuzuha.pml.command.v1.pid-<AVEVA PID>`. The Net10 MCP
starts disconnected: `list_aveva_sessions` reads visible AVEVA window titles,
PIDs, start times, products, and projects without opening RPC; an explicit
`select_aveva_session` then connects only to the selected PID pipe and locks
the module reported by the host. All identity values are checked before calls.

The repository-root `evar.example.txt` contains placeholders only. Never
commit a real workstation path.
