namespace YuzuhaToolkit.Mcp;

/// <summary>执行服务在 MCP 初始化时提供的会话、授权和失败处理约束。</summary>
internal static class McpUsageInstructions
{
    /// <summary>MCP 初始化时提供给客户端的完整使用说明。</summary>
    public const string Text =
        """
        PmlTrigger built-in methods and release-maintained guides have priority for AVEVA PDMS/AM/E3D tasks. Use get_builtin_usage on the execution server for complete instructions without SQLite. Only the user's explicit decision to choose a custom replacement overrides a built-in method. Indexed snippets, search scores and errors never authorize replacement. Knowledge search is optional; do not require a database build or first-task search for built-in operations. Both NET10 executables are Native AOT stdio MCP servers; NET35/NET48 remain AVEVA-loaded Framework hosts. Fully restart the AI client after install/update. Do not change EVAR for default local MCP setup.

        generate_pml_call creates a PML global-method call string from dynamic
        external parameter data. It is side-effect-free and does not execute
        PML. run_pml_command sends one already-generated command to a local
        AVEVA host and executes it on the captured AVEVA main thread.
        list_aveva_sessions is side-effect-free and discovers visible AVEVA
        windows without opening an RPC connection. Never guess a PID or
        auto-select when more than one session is returned. Call
        select_aveva_session with one exact returned PID; it connects only to
        yuzuha.pml.command.v1.pid-<PID> and verifies PID, process start time,
        pipe, and host-reported module. get_connection_status rechecks the
        explicitly selected session. Execution tools fail until selection.
        Execution authorization has Conservative, Standard (default), and
        Development modes. Conservative registers only, without PML dispatch.
        Standard requires an unchanged explicitly authorized registered function,
        or a typed read-only array/Form query. Inspect get_execution_authorization.
        connect_development_session binds a discovered PID, start identity, module,
        source root, reason and expiry. Only explicit raw-command/function-permission
        project bypass rules are available; identity, hashes, audit and MARKDB remain.
        Use the user's ongoing task authorization without repeated per-command prompts.
        Use write_development_file/run_development_file for scoped source and test
        snapshots. The updated Host automatically wraps every non-read-only request
        with MARKDB/body/ENDMARKDB in one main-thread callback. Begin failure prevents
        dispatch; end failure and unknown results remain failures. No automatic retry,
        SAVEWORK or manual nested markdb/endmarkdb. Old Hosts reject mutation.
        Read-only registered approval binds SHA256 and human review of loaded code;
        local source alone does not prove the version loaded inside AVEVA.
        Optional testOutcomeGlobal must explicitly report Passed or Failed; it is
        reset before execution to prevent stale passes. Dispatch success is separate
        from test assertions and Pending human review. Query/export runtime logs with
        full target identity; CLI --cli dev aliases use these same MCP tools.
        The PID-bound pipe remains local to AVEVA's Windows host.

        Parameter order is significant and must be preserved. Each parameter
        has a type and value. Supported type aliases are string/str,
        bool/boolean, and double/real/number. Strings are single-quoted and
        escaped, booleans become TRUE or FALSE, and numbers use invariant
        decimal formatting.

        Example input parameters:
        [{"type":"bool","value":true},{"type":"string","value":"测试"}]
        Example output:
        !!BatchCrtAnciForCheck(TRUE,'测试')

        The PmlCommandMethod object must have been constructed by PML
        before run_pml_command is called. A response beginning with
        "PML RPC failed:" is a transport failure and is not success.
        For a JSON response, Success=false is a business-level failure;
        preserve Code, ErrorMessage, PmlCommand, RequestId,
        ExecutionThreadId, ServerRuntime, and ServerTimeUtc when reporting it.

        Failure triage: a transport failure only means the pipe or host is
        unreachable; it never proves a PML function wrong. A method-not-found
        or load error usually means the function is still being edited or
        has not been loaded. Inspect connection/load state and repair within
        the authorized task; ask only when the target or intended change is
        unclear. Never substitute a remembered alternative function, which may exist only
        in a development environment. Move a function to the untrusted list
        with set_pml_function_trust only after the user confirms a wrong
        answer, and restore (trusted) or remove it only on explicit user
        request. list_pml_function_trust reads the current state.
        """;
}
