using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

public sealed class DynamicParameterInput
{
    [Description(
        "Parameter type: string/str, bool/boolean, or " +
        "double/real/number.")]
    public string Type { get; set; } = string.Empty;

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
    public int Depth { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool Empty { get; set; }
}

internal sealed class PmlListSummary
{
    [JsonPropertyName("total")] public int Total { get; set; }

    [JsonPropertyName("unset")] public int Unset { get; set; }

    [JsonPropertyName("blank")] public int Blank { get; set; }

    [JsonPropertyName("zero")] public int Zero { get; set; }

    [JsonPropertyName("emptyArray")] public int EmptyArray { get; set; }

    [JsonPropertyName("hasValue")] public int HasValue { get; set; }

    [JsonPropertyName("byType")] public Dictionary<string, int> ByType { get; set; } = new();
}

internal sealed class PmlListToolResponse
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PmlCommand { get; set; }
    public PmlListSummary Summary { get; set; } = new();
    public int Count { get; set; }
    public List<PmlListItem> Items { get; set; } = new();
    public bool IncludeEmpty { get; set; }
    public int UnparsedCount { get; set; }
    public List<string> Unparsed { get; set; } = new();
    public string? RequestId { get; set; }
    public string? ServerRuntime { get; set; }
    public DateTime ServerTimeUtc { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DynamicParameterInput))]
[JsonSerializable(typeof(List<DynamicParameterInput>))]
[JsonSerializable(typeof(RunPmlCommandRequest))]
[JsonSerializable(typeof(RunPmlCommandResponse))]
[JsonSerializable(typeof(PmlListToolResponse))]
[JsonSerializable(typeof(YuzuhaConnectionStatus))]
internal partial class YuzuhaJsonContext : JsonSerializerContext
{
}

[McpServerToolType]
public sealed class PmlCallTools
{
    private readonly YuzuhaRpcBridge _bridge;

    public PmlCallTools(YuzuhaRpcBridge bridge)
    {
        _bridge = bridge;
    }

    [McpServerTool]
    [Description(
        "Returns the current AVEVA PML Host connection state, heartbeat " +
        "timestamps, latency, consecutive failures, and last error. " +
        "This tool has no host-side effects.")]
    public string GetConnectionStatus()
    {
        return JsonSerializer.Serialize(
            _bridge.GetConnectionStatus(),
            YuzuhaJsonContext.Default.YuzuhaConnectionStatus);
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
        try
        {
            var response =
                await _bridge.RunPmlCommandAsync(
                        pmlCommand,
                        cancellationToken)
                    .ConfigureAwait(false);

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
            return "PML RPC failed: " + exception.Message;
        }
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
        "pmlCommand !!YuzuhaReadCurrentElement(30,3,'member'), " +
        "globalVar PMLGLOBALARRFORRPC.")]
    public async Task<string> RunPmlCommandList(
        [Description(
            "Complete PML expression whose result is assigned to the global " +
            "array variable, for example !!YuzuhaReadCurrentElement(30,3,'member'). " +
            "The host performs any temporary global assignment; do not pass an assignment statement.")]
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
                ServerTimeUtc = response.ServerTimeUtc
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
            return "PML RPC failed: " + exception.Message;
        }
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
