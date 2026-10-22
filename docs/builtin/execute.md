# 执行命令和宏 / Execute command or macro

## Native development entry

Use `run_pml_command` directly to execute PML through the verified AVEVA Host main thread. `run_pml_command_list` handles expressions returning arrays. An authorized plugin-development task covers related edit/load/test operations without repeated per-command confirmation. Select the actual engineering session first; never automatically retry after a timeout.

Use `markdb(description)` before related changes and `endmarkdb()` afterwards to expose AVEVA undo-group boundaries. They do not save work or undo changes. Tool calls are recorded locally with target PID, bounded arguments, duration and result; inspect with `get_ai_runtime_status` / `get_recent_tool_calls` or the native workspace.

## Legacy execution object

The packaged `YuzuhaExcuter` object remains available for compatibility: `InitArgs`, `execute('ExecuteCommand' or 'ExecuteFile')`, and `Query`. Its legacy keyword check and `RunDangerCommand` path belong to that object. Native development tools do not require this object or its bypass. See `PMLLIB/Examples/YuzuhaExcuter.pmlobj`; Addin-created global `!!YuzuhaExecuter` keeps its historical spelling.
