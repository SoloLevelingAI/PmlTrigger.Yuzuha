using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PmlTwin.Phayton;

// IDs and contract versions are public API; incompatible changes need a new version/ID.
public sealed record OperationDescriptor(
    string Id, int ContractVersion, string Description, JsonElement InputSchema,
    bool RequiresPid = false, bool RequiresExecution = false);

public sealed record OperationContext(
    int? ProcessId = null, string? ExpectedModel = null, bool AllowExecution = false);

public sealed record OperationError(string Code, string Message, bool MayHaveExecuted = false);

public sealed record OperationResult(
    string OperationId, bool Success, int ExitCode, JsonElement? Data = null,
    OperationError? Error = null, int SchemaVersion = 1)
{
    public static OperationResult Ok(string id, JsonElement data) => new(id, true, 0, data);
    public static OperationResult Fail(string id, int exitCode, string code, string message,
        JsonElement? data = null, bool mayHaveExecuted = false) =>
        new(id, false, exitCode, data, new(code, message, mayHaveExecuted));
}

public static class OperationExitCodes
{
    public const int InvalidInput = 2, UnknownOperation = 3, ExecutionDenied = 4,
        TargetFailed = 5, HostFailed = 6, TransportFailed = 7, Cancelled = 8, InternalError = 9;
}

public interface IOperationHandler
{
    Task<OperationResult> InvokeAsync(string id, JsonElement input,
        OperationContext context, CancellationToken cancellationToken);
}

public sealed class DelegateOperationHandler(
    Func<string, JsonElement, OperationContext, CancellationToken, Task<OperationResult>> invoke)
    : IOperationHandler
{
    public Task<OperationResult> InvokeAsync(string id, JsonElement input,
        OperationContext context, CancellationToken cancellationToken) =>
        invoke(id, input, context, cancellationToken);
}

public sealed record OperationRegistration(OperationDescriptor Descriptor, IOperationHandler Handler);

public sealed class OperationRegistry
{
    private readonly IReadOnlyDictionary<string, OperationRegistration> _operations;
    public IReadOnlyList<OperationDescriptor> Descriptors { get; }

    public OperationRegistry(IEnumerable<OperationRegistration> registrations)
    {
        var entries = new Dictionary<string, OperationRegistration>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            var descriptor = registration.Descriptor;
            if (!Regex.IsMatch(descriptor.Id, @"^[a-z][a-z0-9]*(\.[a-z][a-z0-9-]*)+$") ||
                descriptor.ContractVersion < 1 || string.IsNullOrWhiteSpace(descriptor.Description) ||
                descriptor.InputSchema.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Invalid operation descriptor: " + descriptor.Id);
            ArgumentNullException.ThrowIfNull(registration.Handler);
            var copy = descriptor with { InputSchema = descriptor.InputSchema.Clone() };
            if (!entries.TryAdd(copy.Id, new(copy, registration.Handler)))
                throw new ArgumentException("Duplicate operation ID: " + copy.Id);
        }
        _operations = entries;
        Descriptors = Array.AsReadOnly(entries.Values.Select(e => e.Descriptor)
            .OrderBy(d => d.Id, StringComparer.Ordinal).ToArray());
    }

    public bool TryGet(string id, out OperationRegistration? registration) =>
        _operations.TryGetValue(id, out registration);
}

public sealed class OperationExecutor(OperationRegistry registry)
{
    public OperationRegistry Registry { get; } = registry;

    public async Task<OperationResult> InvokeAsync(string id, JsonElement input,
        OperationContext context, CancellationToken cancellationToken = default)
    {
        if (!Registry.TryGet(id, out var registration))
            return OperationResult.Fail(id, 3, "UNKNOWN_OPERATION", "Unknown operation ID: " + id);
        var error = InputContract.Validate(input, registration!.Descriptor.InputSchema);
        if (error != null)
            return OperationResult.Fail(id, 2, "INVALID_INPUT", error);
        if (context.ProcessId is <= 0 || (registration.Descriptor.RequiresPid && context.ProcessId == null))
            return OperationResult.Fail(id, 5, "PID_REQUIRED", "Specify a positive AVEVA PID explicitly.");
        if (registration.Descriptor.RequiresExecution && !context.AllowExecution)
            return OperationResult.Fail(id, 4, "EXECUTION_NOT_AUTHORIZED",
                "Execution requires explicit authorization. No PML was sent.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await registration.Handler.InvokeAsync(id, input.Clone(), context, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail(id, 8, "CANCELLED", "Cancelled; never retry execution automatically.",
                mayHaveExecuted: registration.Descriptor.RequiresExecution);
        }
        catch (Exception exception)
        {
            return OperationResult.Fail(id, 9, "INTERNAL_ERROR", exception.Message,
                mayHaveExecuted: registration.Descriptor.RequiresExecution);
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(OperationDescriptor))]
[JsonSerializable(typeof(OperationDescriptor[]))]
[JsonSerializable(typeof(OperationResult))]
[JsonSerializable(typeof(string))]
public partial class OperationJsonContext : JsonSerializerContext;
