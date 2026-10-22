using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Pml;

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
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(JsonElement))]
internal partial class YuzuhaToolJsonContext : JsonSerializerContext
{
}

/// <summary>MCP 会话选择、身份核验、纯调用文本生成和显式 PML 执行入口。</summary>
[McpServerToolType]
public sealed class PmlCallTools
{
    private readonly YuzuhaRpcBridge _bridge;
    private readonly PmlFunctionTrustStore _trust;

    /// <summary>复用会话桥与人工信任存储提供 MCP 工具，不在构造时执行模型操作。</summary>
    /// <param name="bridge">已选择会话的身份核验与 RPC 协调器。</param>
    /// <param name="trust">用户人工确认的函数信任记录存储。</param>
    public PmlCallTools(
        YuzuhaRpcBridge bridge,
        PmlFunctionTrustStore trust)
    {
        _bridge = bridge;
        _trust = trust;
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
        return JsonSerializer.Serialize(
            _bridge.DiscoverSessions(),
            YuzuhaJsonContext.Default.AvevaSessionListResponse);
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
        try
        {
            var status = await _bridge.SelectSessionAsync(
                    processId,
                    expectedModel,
                    cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Serialize(
                status,
                YuzuhaJsonContext.Default.YuzuhaConnectionStatus);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(
                new YuzuhaConnectionStatus
                {
                    Error = "AVEVA_SESSION_SELECTION_FAILED: " +
                            exception.Message
                },
                YuzuhaJsonContext.Default.YuzuhaConnectionStatus);
        }
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
        var status = await _bridge.GetConnectionStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Serialize(
            status,
            YuzuhaJsonContext.Default.YuzuhaConnectionStatus);
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
        if (parameters == null || parameters.Count == 0)
            return PmlMethodSignatureGenerator.Build(
                methodName);

        var values =
            new DynamicParameter[parameters.Count];

        for (var index = 0; index < parameters.Count; index++)
        {
            var input = parameters[index]
                        ?? throw new ArgumentException(
                            "Parameter at index " + index + " is null.",
                            nameof(parameters));

            values[index] = new DynamicParameter(
                input.Type,
                NormalizeJsonValue(input.Value));
        }

        return PmlMethodSignatureGenerator.Build(methodName, values);
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
        try
        {
            var response =
                await _bridge.RunPmlCommandAsync(
                        pmlCommand,
                        cancellationToken)
                    .ConfigureAwait(false);

            response.FunctionTrustWarning =
                _trust.GetUntrustedWarning(pmlCommand);

            return JsonSerializer.Serialize(
                response,
                YuzuhaJsonContext.Default.RunPmlCommandResponse);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TransportFailure(pmlCommand, exception);
        }
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
        try
        {
            var response =
                await _bridge.RunPmlCommandListAsync(
                        pmlCommand,
                        globalVar,
                        deleteGlobalVar,
                        cancellationToken)
                    .ConfigureAwait(false);

            List<PmlListItem> items = new();
            List<string> unparsed = new();

            if (response.ResultList != null)
                foreach (var raw in response.ResultList)
                    if (TryParsePmlListItem(raw, out var item))
                        items.Add(item!);
                    else
                        unparsed.Add(raw);

            // L2: aggregate over the full set (before includeEmpty filtering).
            PmlListSummary summary = new()
            {
                Total = items.Count,
                Unset = items.Count(i => i.Empty && i.Type != "STRING" && i.Type != "ARRAY"),
                Blank = items.Count(i => i.Empty && i.Type == "STRING"),
                Zero = items.Count(i => !i.Empty && i.Type == "REAL" && IsZeroValue(i.Value)),
                EmptyArray = items.Count(i => i.Empty && i.Type == "ARRAY"),
                HasValue = items.Count(i => !i.Empty),
                ByType = items
                    .GroupBy(i => i.Type)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            // L3: optional filtering of empty items.
            var shown = includeEmpty
                ? items
                : items.Where(i => !i.Empty).ToList();

            PmlListToolResponse toolResponse = new()
            {
                Success = response.Success,
                Code = response.Code,
                ErrorMessage = response.ErrorMessage,
                PmlCommand = response.PmlCommand,
                Summary = summary,
                Count = shown.Count,
                Items = shown,
                IncludeEmpty = includeEmpty,
                UnparsedCount = unparsed.Count,
                Unparsed = unparsed,
                RequestId = response.RequestId,
                ServerRuntime = response.ServerRuntime,
                ServerTimeUtc = response.ServerTimeUtc,
                FunctionTrustWarning = _trust.GetUntrustedWarning(pmlCommand)
            };

            return JsonSerializer.Serialize(
                toolResponse,
                YuzuhaJsonContext.Default.PmlListToolResponse);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TransportFailure(pmlCommand, exception);
        }
    }

    /// <summary>
    ///     Formats a transport-level failure. The leading "PML RPC failed:"
    ///     marker stays stable for downstream parsing; the trailing triage
    ///     note keeps agents from treating a dead pipe or an unloaded host as
    ///     evidence that the user's PML function itself is wrong.
    /// </summary>
    private static string TransportFailure(
        string pmlCommand,
        Exception exception)
    {
        return "PML RPC failed: " + exception.Message +
               " (transport/connectivity failure — this does not prove the " +
               "PML function is wrong. Check get_connection_status or " +
               "list_aveva_sessions, confirm with the user whether the host " +
               "is loaded, and never retry automatically.)";
    }

    /// <summary>
    ///     Parses one ResultList item from the PML string format
    ///     "&lt;STRING&gt; '[depth] path &lt;TYPE&gt; value'" into a PmlListItem.
    ///     Path may be empty (element-level rows). Applies L1 normalization:
    ///     STRING values are unquoted; Unset / blank / empty-array values become
    ///     Value=null with Empty=true.
    /// </summary>
    private static bool TryParsePmlListItem(string raw, out PmlListItem? parsed)
    {
        parsed = null;
        if (string.IsNullOrEmpty(raw)) return false;

        var text = raw;
        const string wrapperPrefix = "<STRING> '";
        if (text.StartsWith(wrapperPrefix, StringComparison.Ordinal) &&
            text.EndsWith("'", StringComparison.Ordinal))
            text = text.Substring(
                wrapperPrefix.Length,
                text.Length - wrapperPrefix.Length - 1);

        var match = Regex.Match(
            text,
            @"^\[(\d+)\]\s*(.*?)\s*<([A-Za-z0-9_]+)>\s*(.*)$");

        if (!match.Success) return false;

        var type = match.Groups[3].Value;
        var rawValue = match.Groups[4].Value.Trim();

        PmlListItem item = new()
        {
            Depth = int.Parse(match.Groups[1].Value),
            Path = match.Groups[2].Value.Trim(),
            Type = type,
            Value = rawValue,
            Empty = false
        };

        // L1: normalize empty / unset / blank values.
        if (type == "STRING")
        {
            var unquoted = UnquotePmlString(rawValue);
            if (string.IsNullOrWhiteSpace(unquoted))
            {
                item.Value = null;
                item.Empty = true;
            }
            else
            {
                item.Value = unquoted;
            }
        }
        else if (rawValue.Equals("Unset", StringComparison.OrdinalIgnoreCase))
        {
            item.Value = null;
            item.Empty = true;
        }
        else if (type == "ARRAY" &&
                 Regex.IsMatch(rawValue, @"^0\s+Elements?$", RegexOptions.IgnoreCase))
        {
            item.Value = null;
            item.Empty = true;
        }

        parsed = item;
        return true;
    }

    /// <summary>
    ///     Strips one surrounding PML single-quote pair from a string display.
    /// </summary>
    private static string UnquotePmlString(string value)
    {
        if (value.Length >= 2 &&
            value[0] == '\'' &&
            value[value.Length - 1] == '\'')
            return value.Substring(1, value.Length - 2);

        return value;
    }

    /// <summary>
    ///     True when a REAL display like "0", "0mm", or "0pascal" is numerically
    ///     zero (unit suffix ignored).
    /// </summary>
    private static bool IsZeroValue(string? value)
    {
        if (value == null) return false;

        var match = Regex.Match(value, @"^-?\d+(\.\d+)?");
        if (!match.Success) return false;

        return double.TryParse(
            match.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number) && number == 0d;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement element) return value;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                throw new FormatException(
                    "Parameter value must be a JSON string, boolean, " +
                    "number, or null.");
        }
    }
}
