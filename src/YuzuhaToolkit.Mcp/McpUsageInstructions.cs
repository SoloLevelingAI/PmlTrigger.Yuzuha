namespace YuzuhaToolkit.Mcp;

internal static class McpUsageInstructions
{
    public const string Text =
        """
        generate_pml_call creates a PML global-method call string from dynamic
        external parameter data. It is side-effect-free and does not execute
        PML. run_pml_command sends one already-generated command to a local
        AVEVA host and executes it on the captured AVEVA main thread.
        run_pml_command has host-side effects: call it only when the user
        explicitly asks to execute a command, and do not retry automatically.

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
        """;
}
