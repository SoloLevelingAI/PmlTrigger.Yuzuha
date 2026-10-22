using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PmlTwin.Phayton;
using System.Text.Json.Nodes;

namespace YuzuhaToolkit.Mcp;

[McpServerToolType]
public sealed class PmlCallTools
{
    private readonly YuzuhaRpcBridge _bridge;
    private readonly OperationExecutor _executor;

    public PmlCallTools(
        YuzuhaRpcBridge bridge,
        PmlFunctionTrustStore trust)
    {
        _bridge = bridge;
        _executor = YuzuhaOperationCatalog.Create(bridge, trust, bridge.CreateIndependent);
    }

    [McpServerTool]
    [Description(
        "Discovers visible AVEVA E3D, PDMS, and Marine sessions without " +
        "opening an RPC connection or executing PML. Returns window title, " +
        "project, PID, process start time, expected PID-bound pipe, and " +
        "whether that pipe was observed. Never selects a session automatically.")]
    public string ListAvevaSessions()
    {
        return InvokeLegacyAsync(YuzuhaOperationIds.Sessions, new(), new(), default).GetAwaiter().GetResult();
    }

    [McpServerTool]
    [Description(
        "Selects one PID returned by list_aveva_sessions, then connects only " +
        "to its PID-bound pipe and verifies PID, process start time, pipe, " +
        "and host-reported module. This does not execute PML. An optional " +
        "expectedModel such as Design makes module matching mandatory.")]
    public async Task<string> SelectAvevaSession(
        [Description(
            "Exact process ID returned by list_aveva_sessions. Do not guess.")]
        int processId,
        [Description(
            "Optional expected AVEVA module, for example Design or Draft. " +
            "Leave null to accept and lock the module reported by the host.")]
        string? expectedModel = null,
        CancellationToken cancellationToken = default)
    {
        return await InvokeLegacyAsync(YuzuhaOperationIds.Select, new(), new(processId, expectedModel), cancellationToken);
    }

    [McpServerTool]
    [Description(
        "Checks the explicitly selected AVEVA session without executing PML. " +
        "TargetVerified is true only when PID, process start time, model, " +
        "and PID-bound pipe all match. Returns E3D_TARGET_NOT_SELECTED until " +
        "select_aveva_session succeeds.")]
    public async Task<string> GetConnectionStatus(
        CancellationToken cancellationToken)
    {
        return await InvokeLegacyAsync(YuzuhaOperationIds.Status, new(), new(), cancellationToken);
    }

    [McpServerTool]
    [Description(
        "Generates a PML global-method call string from a method name and " +
        "an ordered dynamic parameter array. This only builds text and does " +
        "not execute PML. Example: methodName BatchCrtAnciForCheck with " +
        "bool true followed by string 测试 returns " +
        "!!BatchCrtAnciForCheck(TRUE,'测试').")]
    public string GeneratePmlCall(
        [Description(
            "PML method name without the leading !! or parentheses.")]
        string methodName,
        [Description(
            "Ordered dynamic parameters. Each item contains type and value. " +
            "Use an empty array for a parameterless method.")]
        List<DynamicParameterInput>? parameters)
    {
        var normalized = parameters?.Select(p => p == null ? null! : new DynamicParameterInput
        { Type = p.Type?.Trim().ToLowerInvariant() ?? string.Empty, Value = p.Value }).ToList() ?? [];
        var input = new JsonObject { ["methodName"] = methodName?.Trim(),
            ["parameters"] = JsonSerializer.SerializeToNode(normalized, YuzuhaToolJsonContext.Default.ListDynamicParameterInput) };
        return InvokeLegacyAsync(YuzuhaOperationIds.Generate, input, new(), default).GetAwaiter().GetResult();
    }

    [McpServerTool]
    [Description(
        "Executes one already-generated PML command inside the local AVEVA " +
        "host through Named Pipe RPC. This has host-side effects. " +
        "Call it only when the user explicitly asks to execute the command, " +
        "and do not retry automatically.")]
    public async Task<string> RunPmlCommand(
        [Description(
            "Complete PML command text, for example " +
            "!!TestAgent4(TRUE,2,'你好').")]
        string pmlCommand,
        CancellationToken cancellationToken)
    {
        var context = await SelectedContextAsync(cancellationToken);
        if (context == null) return "PML RPC failed: E3D_TARGET_NOT_SELECTED_OR_VERIFIED: Check get_connection_status; never retry automatically.";
        return await InvokeLegacyAsync(YuzuhaOperationIds.Execute,
            new JsonObject { ["pmlCommand"] = pmlCommand }, context, cancellationToken);
    }

    [McpServerTool]
    [Description(
        "Executes a PML expression whose result is stored in a global array " +
        "variable, then returns the whole array parsed into structured JSON " +
        "for AI consumption (no E3D command-line printing). Each item is " +
        "{depth, path, type, value, empty}: STRING values are unquoted, and " +
        "Unset/blank/empty-array values are normalized to value=null with " +
        "empty=true. A Summary block aggregates unset/blank/zero/emptyArray " +
        "counts and counts by type. includeEmpty=false filters empty items " +
        "out of Items (Summary still reflects the full set). Example: " +
        "pmlCommand !!ATestGetByce20260823(300,2,''), " +
        "globalVar PMLGLOBALARRFORRPC.")]
    public async Task<string> RunPmlCommandList(
        [Description(
            "Complete PML expression whose result is assigned to the global " +
            "array variable, for example !!ATestGetByce20260823(300,2,'').")]
        string pmlCommand,
        [Description(
            "Name of the global array variable without the !! prefix, for " +
            "example PMLGLOBALARRFORRPC.")]
        string globalVar,
        [Description(
            "Whether to delete the global array variable after reading it.")]
        bool deleteGlobalVar,
        [Description(
            "Include empty/unset items in Items. Default true; set false to " +
            "return only non-empty values.")]
        bool includeEmpty = true,
        CancellationToken cancellationToken = default)
    {
        var context = await SelectedContextAsync(cancellationToken);
        if (context == null) return "PML RPC failed: E3D_TARGET_NOT_SELECTED_OR_VERIFIED: Check get_connection_status; never retry automatically.";
        return await InvokeLegacyAsync(YuzuhaOperationIds.ReadArray,
            new JsonObject { ["pmlCommand"] = pmlCommand, ["globalVar"] = globalVar,
                ["deleteGlobalVar"] = deleteGlobalVar, ["includeEmpty"] = includeEmpty }, context, cancellationToken);
    }

    private async Task<OperationContext?> SelectedContextAsync(CancellationToken token)
    {
        var status = await _bridge.GetConnectionStatusAsync(token).ConfigureAwait(false);
        return status.TargetVerified
            ? new(status.ExpectedProcessId, status.ExpectedModel, AllowExecution: true) : null;
    }

    private async Task<string> InvokeLegacyAsync(string id, JsonObject input,
        OperationContext context, CancellationToken token)
    {
        var result = await _executor.InvokeAsync(id,
            JsonSerializer.SerializeToElement(input, YuzuhaToolJsonContext.Default.JsonObject), context, token)
            .ConfigureAwait(false);
        if (result.ExitCode == OperationExitCodes.Cancelled) throw new OperationCanceledException(token);
        if (result.Data is { } data)
            return id == YuzuhaOperationIds.Generate ? data.GetString()! : data.GetRawText();
        if (id == YuzuhaOperationIds.Select)
            return JsonSerializer.Serialize(new YuzuhaConnectionStatus
            { Error = "AVEVA_SESSION_SELECTION_FAILED: " + result.Error?.Message },
                YuzuhaJsonContext.Default.YuzuhaConnectionStatus);
        if (id == YuzuhaOperationIds.Generate) throw new ArgumentException(result.Error?.Message);
        return "PML RPC failed: " + result.Error?.Message +
            " (transport/connectivity or target verification failure; this does not prove the PML function is wrong. " +
            "Check get_connection_status or list_aveva_sessions; never retry automatically.)";
    }
}
