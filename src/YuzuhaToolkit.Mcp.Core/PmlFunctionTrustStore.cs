using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Mcp;

/// <summary>用户明确确认的 PML 函数信任状态、原因与时间。</summary>
public sealed class PmlFunctionTrustEntry
{
    /// <summary>经规范化的 PML 全局函数名称。</summary>
    [JsonPropertyName("functionName")] public string FunctionName { get; set; } = string.Empty;

    /// <summary>当前协议状态；枚举或字符串取值由所属契约限定。</summary>
    [JsonPropertyName("state")] public string State { get; set; } = "untrusted";

    /// <summary>状态变更或查询变体的可追踪原因。</summary>
    [JsonPropertyName("reason")] public string? Reason { get; set; }

    /// <summary>用户确认发生问题时保存的 PML 调用文本。</summary>
    [JsonPropertyName("failingCommand")] public string? FailingCommand { get; set; }

    /// <summary>该记录首次建立的 UTC 时间。</summary>
    [JsonPropertyName("createdAtUtc")] public DateTime CreatedAtUtc { get; set; }

    /// <summary>该记录最后变更的 UTC 时间。</summary>
    [JsonPropertyName("updatedAtUtc")] public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>本机函数信任文件的架构版本与记录集合。</summary>
internal sealed class PmlFunctionTrustFile
{
    /// <summary>本机状态文件的架构版本。</summary>
    [JsonPropertyName("schema")] public int Schema { get; set; } = 1;

    /// <summary>已保存的人工函数信任记录。</summary>
    [JsonPropertyName("functions")] public List<PmlFunctionTrustEntry> Functions { get; set; } = new();
}

/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PmlFunctionTrustFile))]
[JsonSerializable(typeof(PmlFunctionTrustListResponse))]
[JsonSerializable(typeof(PmlFunctionTrustMutationResponse))]
internal partial class PmlFunctionTrustJsonContext : JsonSerializerContext
{
}

/// <summary>函数信任文件位置、分类计数与现有记录。</summary>
public sealed class PmlFunctionTrustListResponse
{
    /// <summary>本机人工信任状态 JSON 文件位置。</summary>
    [JsonPropertyName("stateFile")] public string StateFile { get; set; } = string.Empty;

    /// <summary>用户明确标为不可信的函数数量。</summary>
    [JsonPropertyName("untrustedCount")] public int UntrustedCount { get; set; }

    /// <summary>用户明确恢复信任的函数数量。</summary>
    [JsonPropertyName("trustedCount")] public int TrustedCount { get; set; }

    /// <summary>已保存的人工函数信任记录。</summary>
    [JsonPropertyName("functions")] public List<PmlFunctionTrustEntry> Functions { get; set; } = new();
}

/// <summary>一次明确授权的信任状态变更及结果说明。</summary>
public sealed class PmlFunctionTrustMutationResponse
{
    /// <summary>此次操作是否完成其声明的校验或写入。</summary>
    [JsonPropertyName("ok")] public bool Ok { get; set; }

    /// <summary>经规范化的 PML 全局函数名称。</summary>
    [JsonPropertyName("functionName")] public string FunctionName { get; set; } = string.Empty;

    /// <summary>当前协议状态；枚举或字符串取值由所属契约限定。</summary>
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;

    /// <summary>本机人工信任状态 JSON 文件位置。</summary>
    [JsonPropertyName("stateFile")] public string StateFile { get; set; } = string.Empty;

    /// <summary>结果边界、诊断或下一步建议的说明。</summary>
    [JsonPropertyName("note")] public string? Note { get; set; }
}

/// <summary>
///     Persists the manual trust state of user-provided PML functions in a
///     JSON file under the managed installation root. A transport failure is
///     never recorded here; only a user-confirmed wrong answer moves a
///     function to <c>untrusted</c>, and only an explicit user instruction
///     restores or removes an entry.
/// </summary>
public sealed class PmlFunctionTrustStore
{
    private readonly object _gate = new();

    /// <summary>本机人工信任状态 JSON 文件位置。</summary>
    public string StateFile { get; }

    /// <summary>配置人工信任状态文件位置，不在构造时改写记录。</summary>
    public PmlFunctionTrustStore()
        : this(YuzuhaToolkitPaths.TrustStateFile())
    {
    }

    /// <summary>配置人工信任状态文件位置，不在构造时改写记录。</summary>
    /// <param name="stateFile">本机 JSON 信任文件的完整路径。</param>
    public PmlFunctionTrustStore(string stateFile)
    {
        StateFile = stateFile;
    }

    /// <summary>移除调用前缀与参数部分并转为规范大写函数名，不改变信任状态。</summary>
    /// <param name="input">需要规范化的调用方输入或安装请求对象。</param>
    public static string NormalizeFunctionName(string? input)
    {
        var name = (input ?? string.Empty).Trim();
        while (name.StartsWith("!", StringComparison.Ordinal))
            name = name[1..];
        name = name.Trim();

        var parentheses = name.IndexOf('(');
        if (parentheses >= 0)
            name = name[..parentheses].Trim();

        return name.ToUpperInvariant();
    }

    /// <summary>
    ///     Returns the untrusted warning text for the global function named by
    ///     the command's leading <c>!!Name</c>, or null when the function is
    ///     absent or not untrusted.
    /// </summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    public string? GetUntrustedWarning(string? pmlCommand)
    {
        var name = NormalizeFunctionName(ExtractLeadingFunction(pmlCommand));
        if (name.Length == 0)
            return null;

        lock (_gate)
        {
            var entry = LoadOrNull()?.Functions.FirstOrDefault(
                candidate => candidate.FunctionName == name &&
                             candidate.State == "untrusted");
            if (entry is null)
                return null;

            return "FUNCTION MARKED UNTRUSTED (user-confirmed): !!" +
                   entry.FunctionName +
                   " previously returned a wrong answer" +
                   (string.IsNullOrWhiteSpace(entry.Reason)
                       ? ""
                       : "; reason: " + entry.Reason) +
                   ". Confirm with the user before relying on it again; " +
                   "restore or remove the entry only on explicit user " +
                   "instruction via set_pml_function_trust.";
        }
    }

    /// <summary>只读列出本机人工函数信任记录及分类计数；损坏文件不被当作执行结果。</summary>
    public PmlFunctionTrustListResponse List()
    {
        lock (_gate)
        {
            var file = LoadOrNull() ?? new PmlFunctionTrustFile();
            return new PmlFunctionTrustListResponse
            {
                StateFile = StateFile,
                UntrustedCount = file.Functions.Count(f => f.State == "untrusted"),
                TrustedCount = file.Functions.Count(f => f.State == "trusted"),
                Functions = file.Functions
                    .OrderBy(f => f.FunctionName, StringComparer.Ordinal)
                    .ToList()
            };
        }
    }

    /// <summary>按用户明确指示设置或删除本机信任记录；通过临时文件替换保存结果。</summary>
    /// <param name="functionName">用户明确要求维护信任状态的 PML 全局函数名称。</param>
    /// <param name="state">由调用方明确指定的状态或原生枚举上下文。</param>
    /// <param name="reason">用户确认状态变化的原因或验证依据。</param>
    /// <param name="failingCommand">用户确认错误时保存的原始调用文本。</param>
    public PmlFunctionTrustMutationResponse Set(
        string functionName,
        string state,
        string? reason,
        string? failingCommand)
    {
        var normalizedName = NormalizeFunctionName(functionName);
        if (normalizedName.Length == 0)
            throw new ArgumentException(
                "functionName is required, for example MyFunc for !!MyFunc.",
                nameof(functionName));

        var normalizedState = state.Trim().ToLowerInvariant();
        lock (_gate)
        {
            var file = LoadOrNull() ?? new PmlFunctionTrustFile();
            var entry = file.Functions.FirstOrDefault(
                candidate => candidate.FunctionName == normalizedName);
            var nowUtc = DateTime.UtcNow;

            if (normalizedState == "remove")
            {
                if (entry is null)
                    return NoChange(normalizedName, "remove",
                        "No trust entry existed; nothing was removed.");
                file.Functions.Remove(entry);
                Save(file);
                return NoChange(normalizedName, "remove",
                    "Trust entry removed. The function is neither trusted " +
                    "nor untrusted again.");
            }

            if (normalizedState is not ("untrusted" or "trusted"))
                throw new ArgumentException(
                    "state must be one of: untrusted, trusted, remove.",
                    nameof(state));

            if (entry is null)
            {
                entry = new PmlFunctionTrustEntry
                {
                    FunctionName = normalizedName,
                    CreatedAtUtc = nowUtc
                };
                file.Functions.Add(entry);
            }

            entry.State = normalizedState;
            entry.Reason = string.IsNullOrWhiteSpace(reason) ? entry.Reason : reason.Trim();
            entry.FailingCommand = string.IsNullOrWhiteSpace(failingCommand)
                ? entry.FailingCommand
                : failingCommand.Trim();
            entry.UpdatedAtUtc = nowUtc;
            Save(file);

            return new PmlFunctionTrustMutationResponse
            {
                Ok = true,
                FunctionName = normalizedName,
                State = normalizedState,
                StateFile = StateFile,
                Note = normalizedState == "untrusted"
                    ? "Marked untrusted. Execution tools will warn about it " +
                      "until the user confirms a fix (state=trusted) or asks " +
                      "for removal (state=remove)."
                    : "Marked trusted again after a user-confirmed fix."
            };
        }
    }

    private PmlFunctionTrustMutationResponse NoChange(
        string functionName,
        string state,
        string note)
    {
        return new PmlFunctionTrustMutationResponse
        {
            Ok = true,
            FunctionName = functionName,
            State = state,
            StateFile = StateFile,
            Note = note
        };
    }

    private static string? ExtractLeadingFunction(string? pmlCommand)
    {
        if (string.IsNullOrWhiteSpace(pmlCommand))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            pmlCommand,
            @"^\s*!+\s*([A-Za-z_][A-Za-z0-9_]*)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private PmlFunctionTrustFile? LoadOrNull()
    {
        if (!File.Exists(StateFile))
            return null;

        try
        {
            var json = File.ReadAllText(StateFile);
            return JsonSerializer.Deserialize(
                json,
                PmlFunctionTrustJsonContext.Default.PmlFunctionTrustFile);
        }
        catch (Exception)
        {
            // A corrupt trust file must never block execution; report it via
            // the listing tool instead of throwing here.
            return new PmlFunctionTrustFile();
        }
    }

    private void Save(PmlFunctionTrustFile file)
    {
        var directory = Path.GetDirectoryName(StateFile);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = StateFile + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(file, PmlFunctionTrustJsonContext.Default.PmlFunctionTrustFile));
        File.Move(temporary, StateFile, overwrite: true);
    }
}
