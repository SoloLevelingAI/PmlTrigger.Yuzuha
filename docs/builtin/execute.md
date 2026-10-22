# 执行命令和宏 / Execute command or macro

## Native development entry

Inspect `get_execution_authorization` first. Conservative permits registration only; Standard permits unchanged, explicitly authorized registered functions and typed read-only queries. For development, explicitly `connect_development_session` using a discovered PID, expected module, source root, reason and expiry. `raw-command` and `function-permission` are separate project rule bypasses; neither bypasses identity, source hashes, audit or automatic MARKDB. Use the existing task authorization without repeated per-command approval.

`write_development_file` creates or hash-checks an update to source/test code within the root. `run_development_file` reconnects only the exact saved identity, verifies a macro snapshot and emits execution/test evidence; human review stays Pending. Optional testOutcomeGlobal is reset before execution and must report Passed or Failed explicitly. Successful dispatch alone is not assertion success. CLI equivalents: `--cli dev connect|write|run|evidence|logs|export` with JSON arguments.

Every non-read-only request is grouped by the updated Host in one main-thread callback: MARKDB, body, ENDMARKDB. A failed begin prevents body dispatch. Unknown/timeout is never automatically retried. Old Hosts are rejected before mutation. Do not call manual markdb/endmarkdb around managed execution. Typed existing-array/Form queries and hash-bound reviewed read-only registered functions may be exempt; an array-producing expression and cleanup still require grouping. Local source identity is not proof of AVEVA's loaded version; check the loaded revision before confirming read-only.

Logs record full target identity, authorization, source revision, group receipt and outcome. Query/export retained events using query_runtime_logs/export_runtime_logs; retained rolling logs are bounded. No automatic SAVEWORK. Execution uses a local PID-bound pipe on the AVEVA Windows machine; cloud Git development does not imply remote AVEVA execution.

## Legacy execution object

The packaged `YuzuhaExcuter` object remains available for compatibility: `InitArgs`, `execute('ExecuteCommand' or 'ExecuteFile')`, and `Query`. Its legacy keyword check and `RunDangerCommand` path belong to that object. Native development tools do not require this object or its bypass. See `PMLLIB/Examples/YuzuhaExcuter.pmlobj`; Addin-created global `!!YuzuhaExecuter` keeps its historical spelling.
