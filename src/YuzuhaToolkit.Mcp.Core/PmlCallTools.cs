using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PmlTwin.Phayton;
using System.Text.Json.Nodes;

namespace YuzuhaToolkit.Mcp;

/// <summary>从 MCP JSON 输入读取的参数类型与标量值。</summary>
public sealed class DynamicParameterInput
{
    /// <summary>Parameter type: string/str, bool/boolean, or double/real/number.</summary>
    [Description(
        "Parameter type: string/str, bool/boolean, or " +
        "double/real/number.")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Parameter value. JSON strings, booleans, and numbers are accepted.</summary>
    [Description(
        "Parameter value. JSON strings, booleans, and numbers are accepted.")]
    public object? Value { get; set; }
}

/// <summary>
///     One normalized entry of the BFS list: STRING values are unquoted, and
///     Unset / blank / empty-array values are collapsed to Value=null with
///     Empty=true.
/// </summary>
public sealed class PmlListItem
{
    /// <summary>该条目在 BFS 展开结果中的层级。</summary>
    public int Depth { get; set; }
    /// <summary>当前条目、来源或文件的可追踪路径。</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>PML 数据类型名称或动态参数的类型别名。</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>当前参数或 PML 条目的标量值；空值表示未设置。</summary>
    public string? Value { get; set; }
    /// <summary>该条目是否代表 unset、空白或空数组。</summary>
    public bool Empty { get; set; }
}

/// <summary>BFS 列表的空值、零值和类型分布统计。</summary>
internal sealed class PmlListSummary
{
    /// <summary>未过滤的原始条目总数。</summary>
    [JsonPropertyName("total")] public int Total { get; set; }

    /// <summary>值未设置的条目数量。</summary>
    [JsonPropertyName("unset")] public int Unset { get; set; }

    /// <summary>空白字符串条目数量。</summary>
    [JsonPropertyName("blank")] public int Blank { get; set; }

    /// <summary>数值为零的条目数量。</summary>
    [JsonPropertyName("zero")] public int Zero { get; set; }

    /// <summary>空数组条目数量。</summary>
    [JsonPropertyName("emptyArray")] public int EmptyArray { get; set; }

    /// <summary>具有有效非空值的条目数量。</summary>
    [JsonPropertyName("hasValue")] public int HasValue { get; set; }

    /// <summary>按 PML 类型名称统计的条目数量。</summary>
    [JsonPropertyName("byType")] public Dictionary<string, int> ByType { get; set; } = new();
}

/// <summary>PML 列表调用的结果、过滤后条目、全量统计及不可解析行。</summary>
internal sealed class PmlListToolResponse
{
    /// <summary>保留执行桥的 MarkDbId 回执字段。</summary>
    public string? MarkDbId { get; set; }
    /// <summary>保留执行桥的 MarkDbStarted 回执字段。</summary>
    public bool MarkDbStarted { get; set; }
    /// <summary>保留执行桥的 MarkDbEnded 回执字段。</summary>
    public bool MarkDbEnded { get; set; }
    /// <summary>保留执行桥的 ExecutionStarted 回执字段。</summary>
    public bool ExecutionStarted { get; set; }
    /// <summary>保留执行桥的 MarkDbError 回执字段。</summary>
    public string? MarkDbError { get; set; }
    /// <summary>保留执行桥的 AuthorizationMode 回执字段。</summary>
    public string? AuthorizationMode { get; set; }
    /// <summary>保留执行桥的 ReadOnlyEvidence 回执字段。</summary>
    public string? ReadOnlyEvidence { get; set; }
    /// <summary>该次协议或业务操作报告的成功标志。</summary>
    public bool Success { get; set; }
    /// <summary>稳定的业务状态或失败代码。</summary>
    public string? Code { get; set; }
    /// <summary>执行失败的可读原因，成功时通常为空。</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>已生成或准备执行的完整 PML 文本。</summary>
    public string? PmlCommand { get; set; }
    /// <summary>过滤前的完整 BFS 类型与空值统计。</summary>
    public PmlListSummary Summary { get; set; } = new();
    /// <summary>当前返回集合的条目数量。</summary>
    public int Count { get; set; }
    /// <summary>经过请求过滤后返回的条目集合。</summary>
    public List<PmlListItem> Items { get; set; } = new();
    /// <summary>结果是否保留空值条目；统计仍基于完整数据。</summary>
    public bool IncludeEmpty { get; set; }
    /// <summary>未能按 BFS 格式解释的原始行数量。</summary>
    public int UnparsedCount { get; set; }
    /// <summary>未解析的原始行，供诊断而不丢弃来源数据。</summary>
    public List<string> Unparsed { get; set; } = new();
    /// <summary>用于关联一个请求与响应的编号。</summary>
    public string? RequestId { get; set; }
    /// <summary>处理请求的 Host 运行时标识。</summary>
    public string? ServerRuntime { get; set; }
    /// <summary>Host 生成响应时的 UTC 时间。</summary>
    public DateTime ServerTimeUtc { get; set; }

    /// <summary>由本机人工信任记录补充的提示，不作为传输失败的结论。</summary>
    [JsonPropertyName("FunctionTrustWarning")]
    public string? FunctionTrustWarning { get; set; }
}

/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RunPmlCommandResponse))]
[JsonSerializable(typeof(PmlListToolResponse))]
[JsonSerializable(typeof(YuzuhaConnectionStatus))]
[JsonSerializable(typeof(HostIdentityResponse))]
[JsonSerializable(typeof(AvevaSessionListResponse))]
[JsonSerializable(typeof(AvevaSessionCandidate))]
internal partial class YuzuhaJsonContext : JsonSerializerContext
{
}

/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<DynamicParameterInput>))]
[JsonSerializable(typeof(DynamicParameterInput))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonObject))]
internal partial class YuzuhaToolJsonContext : JsonSerializerContext
{
}

/// <summary>MCP 会话选择、身份核验、纯调用文本生成和显式 PML 执行入口。</summary>
[McpServerToolType]
public sealed class PmlCallTools
{
    private readonly YuzuhaRpcBridge _bridge;
    private readonly OperationExecutor _executor;

    /// <summary>复用会话桥与人工信任存储提供 MCP 工具，不在构造时执行模型操作。</summary>
    /// <param name="bridge">已选择会话的身份核验与 RPC 协调器。</param>
    /// <param name="trust">用户人工确认的函数信任记录存储。</param>
    public PmlCallTools(
        YuzuhaRpcBridge bridge,
        PmlFunctionTrustStore trust)
    {
        _bridge = bridge;
        _executor = YuzuhaOperationCatalog.Create(bridge, trust, bridge.CreateIndependent);
    }

    /// <summary>Discovers visible AVEVA E3D, PDMS, and Marine sessions without opening an RPC connection or executing PML. Returns window title, project, PID, process start time, expected PID-bound pipe, and whether that pipe was observed. Never selects a session automatically.</summary>
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

    /// <summary>Selects one PID returned by list_aveva_sessions, then connects only to its PID-bound pipe and verifies PID, process start time, pipe, and host-reported module. This does not execute PML. An optional expectedModel such as Design makes module matching mandatory.</summary>
    /// <param name="processId">Exact process ID returned by list_aveva_sessions. Do not guess.</param>
    /// <param name="expectedModel">Optional expected AVEVA module, for example Design or Draft. Leave null to accept and lock the module reported by the host.</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>Checks the explicitly selected AVEVA session without executing PML. TargetVerified is true only when PID, process start time, model, and PID-bound pipe all match. Returns E3D_TARGET_NOT_SELECTED until select_aveva_session succeeds.</summary>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>Generates a PML global-method call string from a method name and an ordered dynamic parameter array. This only builds text and does not execute PML. Example: methodName BatchCrtAnciForCheck with bool true followed by string 测试 returns !!BatchCrtAnciForCheck(TRUE,'测试').</summary>
    /// <param name="methodName">PML method name without the leading !! or parentheses.</param>
    /// <param name="parameters">Ordered dynamic parameters. Each item contains type and value. Use an empty array for a parameterless method.</param>
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

    /// <summary>按索引窗口读取已有全局数组，返回预算、截断和续读位置；不重放生成表达式。</summary>
    [McpServerTool,Description("Read a bounded index window of an existing global PML array. Requires Host bounded-array protocol v1; older Hosts are rejected before PML execution. Does not evaluate a producer expression or delete the source array. maxItems 1..1000, maxCharacters 1..262144 UTF-16 characters excluding JSON; an oversized first row fails. IndexUpperBound is MAXINDEX, not the count of set items. Sparse slots retain native displays or report a read failure. Separate calls are not a frozen snapshot; no automatic retry.")]
    public async Task<string> ReadPmlArray(string globalVar,int startIndex=1,int maxItems=100,int maxCharacters=32768,CancellationToken cancellationToken=default)
    {
        try
        {
            var response=await _bridge.ReadPmlArrayAsync(globalVar,startIndex,maxItems,maxCharacters,cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(response,YuzuhaJsonContext.Default.RunPmlCommandResponse);
        }
        catch (Exception error) when(error is ArgumentException or NotSupportedException ||
            error is InvalidOperationException && error.Message.StartsWith("E3D_TARGET_",StringComparison.Ordinal))
        {
            throw new McpException(error.Message);
        }
    }

    /// <summary>通过已核验 Host 原生执行 PML；开发任务授权覆盖相关连续操作，不逐条重复审批，超时不自动重试。</summary>
    /// <param name="pmlCommand">Complete PML command text, for example !!TestAgent4(TRUE,2,'你好').</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
    [McpServerTool]
    [Description(
        "Executes one already-generated PML command inside the local AVEVA " +
        "host through Named Pipe RPC. This has host-side effects. " +
        "Use within the user's authorized development task without requesting " +
        "separate approval for every related command, " +
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

    /// <summary>Executes a PML expression whose result is stored in a global array variable, then returns the whole array parsed into structured JSON for AI consumption (no E3D command-line printing). Each item is {depth, path, type, value, empty}: STRING values are unquoted, and Unset/blank/empty-array values are normalized to value=null with empty=true. A Summary block aggregates unset/blank/zero/emptyArray counts and counts by type. includeEmpty=false filters empty items out of Items (Summary still reflects the full set). Example: pmlCommand !!ATestGetByce20260823(300,2,''), globalVar PMLGLOBALARRFORRPC.</summary>
    /// <param name="pmlCommand">Complete PML expression whose result is assigned to the global array variable, for example !!ATestGetByce20260823(300,2,'').</param>
    /// <param name="globalVar">Name of the global array variable without the !! prefix, for example PMLGLOBALARRFORRPC.</param>
    /// <param name="deleteGlobalVar">Whether to delete the global array variable after reading it.</param>
    /// <param name="includeEmpty">Include empty/unset items in Items. Default true; set false to return only non-empty values.</param>
    /// <param name="cancellationToken">取消等待或传输；远端已经开始的命令可能继续执行，不能据此自动重试。</param>
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

    /// <summary>通过共享桥核验注册调用，保留授权模式和自动 MARKDB 回执。</summary>
    public async Task<string> RunRegisteredFunctionAsync(PmlFunctionRegistry registry, string name, List<DynamicParameterInput> parameters, CancellationToken token)
    {
        try
        {
            var policy = _bridge.ReadAuthorization();
            if (policy.Mode == "Conservative") throw new UnauthorizedAccessException("PML_CONSERVATIVE_MODE: registration only; no PML dispatch.");
            registry.PrepareInvocation(name, parameters, policy.Mode == "Development" && policy.BypassRules.Contains("function-permission"));
            if (policy.Mode == "Development" && _bridge.SelectedProcessId == null)
            {
                if (policy.ExpiresUtc <= DateTimeOffset.UtcNow) throw new UnauthorizedAccessException("PML_ACTIVE_DEVELOPMENT_GRANT_REQUIRED");
                await _bridge.SelectSessionAsync(policy.ProcessId, policy.Model, token).ConfigureAwait(false);
            }
            return JsonSerializer.Serialize(await _bridge.RunRegisteredFunctionAsync(registry, name, parameters, token).ConfigureAwait(false), YuzuhaJsonContext.Default.RunPmlCommandResponse);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or InvalidDataException or ArgumentException or KeyNotFoundException or InvalidOperationException or NotSupportedException)
        { throw new McpException(error.Message); }
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
