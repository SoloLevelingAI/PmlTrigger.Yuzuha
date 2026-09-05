using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace YuzuhaToolkit.Mcp;

[McpServerToolType]
public sealed class PmlFunctionTrustTools
{
    private readonly PmlFunctionTrustStore _store;

    public PmlFunctionTrustTools(PmlFunctionTrustStore store)
    {
        _store = store;
    }

    [McpServerTool]
    [Description(
        "Lists the persisted trust state of user-provided PML functions. " +
        "untrusted means the user confirmed a wrong answer and the " +
        "execution tools warn before running it again. This list is advice, " +
        "never proof: a transport failure does not make a function " +
        "untrustworthy, and a missing function may simply not be loaded " +
        "yet. Restoring trust (fixed) or removing an entry (deleted) is " +
        "done only on an explicit user request.")]
    public string ListPmlFunctionTrust()
    {
        return JsonSerializer.Serialize(
            _store.List(),
            PmlFunctionTrustJsonContext.Default.PmlFunctionTrustListResponse);
    }

    [McpServerTool]
    [Description(
        "Updates the persisted trust state of one PML global function. " +
        "state=untrusted records a user-confirmed wrong answer (ask the " +
        "user first; never decide from local memory). state=trusted " +
        "restores a function after the user says it is fixed. state=remove " +
        "deletes the entry after the user says the function was deleted or " +
        "the record was a mistake.")]
    public string SetPmlFunctionTrust(
        [Description(
            "PML global function name with or without the !! prefix, for " +
            "example MyFunc.")]
        string functionName,
        [Description("One of: untrusted, trusted, remove.")]
        string state,
        [Description("Why the state changed, for example 'wrong results on " +
                     "!!MyFunc(TRUE,2)' or 'user confirmed the fix'.")]
        string? reason = null,
        [Description("Optional failing PML command text preserved for the " +
                     "record.")]
        string? failingCommand = null)
    {
        try
        {
            var response = _store.Set(
                functionName,
                state,
                reason,
                failingCommand);
            return JsonSerializer.Serialize(
                response,
                PmlFunctionTrustJsonContext.Default.PmlFunctionTrustMutationResponse);
        }
        catch (ArgumentException exception)
        {
            return JsonSerializer.Serialize(
                new PmlFunctionTrustMutationResponse
                {
                    Ok = false,
                    FunctionName = PmlFunctionTrustStore.NormalizeFunctionName(functionName),
                    State = state,
                    StateFile = _store.StateFile,
                    Note = exception.Message
                },
                PmlFunctionTrustJsonContext.Default.PmlFunctionTrustMutationResponse);
        }
    }
}
