using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    [JsonPropertyName("FunctionTrustWarning")]
    public string? FunctionTrustWarning { get; set; }
}

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

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<DynamicParameterInput>))]
[JsonSerializable(typeof(DynamicParameterInput))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonObject))]
internal partial class YuzuhaToolJsonContext : JsonSerializerContext
{
}
