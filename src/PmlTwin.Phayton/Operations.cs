using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PmlTwin.Phayton;

// IDs and contract versions are public API; incompatible changes need a new version/ID.
/// <summary>共享操作的稳定标识、契约版本、输入结构及目标和执行要求。</summary>
public sealed record OperationDescriptor(
    string Id, int ContractVersion, string Description, JsonElement InputSchema,
    bool RequiresPid = false, bool RequiresExecution = false);

/// <summary>本次调用显式指定的目标 PID、模块和执行许可。</summary>
public sealed record OperationContext(
    int? ProcessId = null, string? ExpectedModel = null, bool AllowExecution = false);

/// <summary>操作错误代码、可读原因及远端可能已执行的标记。</summary>
public sealed record OperationError(string Code, string Message, bool MayHaveExecuted = false);

/// <summary>操作的成功状态、退出码、原始数据与错误信息。</summary>
public sealed record OperationResult(
    string OperationId, bool Success, int ExitCode, JsonElement? Data = null,
    OperationError? Error = null, int SchemaVersion = 1)
{
    /// <summary>创建保留原始 JSON 数据的成功操作结果。</summary>
    public static OperationResult Ok(string id, JsonElement data) => new(id, true, 0, data);
    /// <summary>创建带明确错误代码和可能执行状态的失败结果。</summary>
    public static OperationResult Fail(string id, int exitCode, string code, string message,
        JsonElement? data = null, bool mayHaveExecuted = false) =>
        new(id, false, exitCode, data, new(code, message, mayHaveExecuted));
}

/// <summary>CLI 参数、授权、目标、Host、传输及取消错误的稳定退出码。</summary>
public static class OperationExitCodes
{
    /// <summary>定义各类操作失败的稳定数值退出码。</summary>
    public const int InvalidInput = 2, UnknownOperation = 3, ExecutionDenied = 4,
        TargetFailed = 5, HostFailed = 6, TransportFailed = 7, Cancelled = 8, InternalError = 9;
}

/// <summary>接收已经过契约和显式授权检查的操作请求。</summary>
public interface IOperationHandler
{
    /// <summary>执行一次操作并保留失败或未知状态；不自动重试。</summary>
    Task<OperationResult> InvokeAsync(string id, JsonElement input,
        OperationContext context, CancellationToken cancellationToken);
}

/// <summary>使用委托实现操作处理接口，复用相同调度与取消路径。</summary>
public sealed class DelegateOperationHandler(
    Func<string, JsonElement, OperationContext, CancellationToken, Task<OperationResult>> invoke)
    : IOperationHandler
{
    /// <summary>执行一次操作并保留失败或未知状态；不自动重试。</summary>
    public Task<OperationResult> InvokeAsync(string id, JsonElement input,
        OperationContext context, CancellationToken cancellationToken) =>
        invoke(id, input, context, cancellationToken);
}

/// <summary>将操作描述和实际处理器关联成注册项。</summary>
public sealed record OperationRegistration(OperationDescriptor Descriptor, IOperationHandler Handler);

/// <summary>校验并保存唯一的操作注册项，提供稳定排序的描述目录。</summary>
public sealed class OperationRegistry
{
    private readonly IReadOnlyDictionary<string, OperationRegistration> _operations;
    /// <summary>按稳定操作标识排序的只读描述集合。</summary>
    public IReadOnlyList<OperationDescriptor> Descriptors { get; }

    /// <summary>校验并保存唯一的操作注册项，提供稳定排序的描述目录。</summary>
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

    /// <summary>按完整、区分大小写的操作标识读取已注册处理器。</summary>
    public bool TryGet(string id, out OperationRegistration? registration) =>
        _operations.TryGetValue(id, out registration);
}

/// <summary>在调用处理器前统一验证参数、显式目标和执行许可。</summary>
public sealed class OperationExecutor(OperationRegistry registry)
{
    /// <summary>本执行器使用的操作注册表。</summary>
    public OperationRegistry Registry { get; } = registry;

    /// <summary>执行一次操作并保留失败或未知状态；不自动重试。</summary>
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

/// <summary>共享操作契约和结果的 AOT 友好 JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(OperationDescriptor))]
[JsonSerializable(typeof(OperationDescriptor[]))]
[JsonSerializable(typeof(OperationResult))]
[JsonSerializable(typeof(string))]
public partial class OperationJsonContext : JsonSerializerContext;
