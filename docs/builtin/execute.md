# 执行命令和宏 / Execute command or macro

## Native development entry

Use `op invoke pml.command.execute --pid <pid> --expected-model <module> --allow-execution --input-file <json>` to execute PML through the verified AVEVA Host main thread. `pml.array.read` accepts an array-returning expression: pass `!!Function(...)`, not `!!Output = !!Function(...)`, because the Host adds the assignment. An authorized plugin-development task covers related edit/load/test operations without repeated per-command confirmation. Never automatically retry after a timeout or a failure marked `mayHaveExecuted`.

Use `pml.undo.begin` and `pml.undo.end` for AVEVA undo-group boundaries. They do not save work or undo changes. Calls are recorded locally with target PID, bounded arguments, duration and result; inspect with `runtime.status` / `runtime.calls` or the native workspace. Configuration and knowledge writes use `--allow-write`; creating an AVEVA entry process uses `--allow-launch`. These permissions are independent of PML execution.

## Legacy execution object

The packaged `YuzuhaExcuter` object remains available for compatibility: `InitArgs`, `execute('ExecuteCommand' or 'ExecuteFile')`, and `Query`. Its legacy keyword check and `RunDangerCommand` path belong to that object. Native development tools do not require this object or its bypass. See `PMLLIB/Examples/YuzuhaExcuter.pmlobj`; Addin-created global `!!YuzuhaExecuter` keeps its historical spelling.
