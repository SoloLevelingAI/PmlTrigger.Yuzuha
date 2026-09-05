using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Mcp;

public sealed class PmlFunctionTrustEntry
{
    [JsonPropertyName("functionName")] public string FunctionName { get; set; } = string.Empty;

    [JsonPropertyName("state")] public string State { get; set; } = "untrusted";

    [JsonPropertyName("reason")] public string? Reason { get; set; }

    [JsonPropertyName("failingCommand")] public string? FailingCommand { get; set; }

    [JsonPropertyName("createdAtUtc")] public DateTime CreatedAtUtc { get; set; }

    [JsonPropertyName("updatedAtUtc")] public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class PmlFunctionTrustFile
{
    [JsonPropertyName("schema")] public int Schema { get; set; } = 1;

    [JsonPropertyName("functions")] public List<PmlFunctionTrustEntry> Functions { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PmlFunctionTrustFile))]
[JsonSerializable(typeof(PmlFunctionTrustListResponse))]
[JsonSerializable(typeof(PmlFunctionTrustMutationResponse))]
internal partial class PmlFunctionTrustJsonContext : JsonSerializerContext
{
}

public sealed class PmlFunctionTrustListResponse
{
    [JsonPropertyName("stateFile")] public string StateFile { get; set; } = string.Empty;

    [JsonPropertyName("untrustedCount")] public int UntrustedCount { get; set; }

    [JsonPropertyName("trustedCount")] public int TrustedCount { get; set; }

    [JsonPropertyName("functions")] public List<PmlFunctionTrustEntry> Functions { get; set; } = new();
}

public sealed class PmlFunctionTrustMutationResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }

    [JsonPropertyName("functionName")] public string FunctionName { get; set; } = string.Empty;

    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;

    [JsonPropertyName("stateFile")] public string StateFile { get; set; } = string.Empty;

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

    public string StateFile { get; }

    public PmlFunctionTrustStore()
        : this(YuzuhaToolkitPaths.TrustStateFile())
    {
    }

    public PmlFunctionTrustStore(string stateFile)
    {
        StateFile = stateFile;
    }

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
