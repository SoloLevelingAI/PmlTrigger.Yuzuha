using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

internal interface IPhaytonFunctionClient
{
    IDisposable LockCatalog();
    PhaytonReceipt Run(string operation, object input, PhaytonTarget? target = null,
        string? permission = null, int timeout = 12);
}

/// <summary>Runs the installed CLI with a catalog and state confined to this toolbox workspace.</summary>
internal sealed class PhaytonFunctionClient(FeatureContext context, Func<string[]> findPrograms) : IPhaytonFunctionClient
{
    internal const string StatePath = ".core30/phayton";
    private readonly string program = FindProgram(findPrograms);

    public IDisposable LockCatalog()
    {
        var path = context.FilePath(StatePath + "/toolbox.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
    }

    public PhaytonReceipt Run(string operation, object input, PhaytonTarget? target = null,
        string? permission = null, int timeout = 12)
    {
        if ((operation is "pml.command.execute" or "pml.array.read" or "pml.undo.begin" or "pml.undo.end") &&
            PmlChannelMode.Read() == PmlChannelMode.ReadOnly)
            return PhaytonReceipt.Blocked(operation);

        var path = context.FilePath($"{StatePath}/request-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            using (var file = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(file, input);
            var start = Prepare(operation, path, target, permission, timeout);
            var (exit, output, error) = WindowsDetachedProcess.RunCaptured(start, TimeSpan.FromSeconds(timeout + 7));
            return PhaytonReceipt.Read(operation, exit, output, error);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            // A terminated CLI does not prove the Host did not receive its command.
            return PhaytonReceipt.Uncertain(operation, error.Message, permission == "--allow-execution");
        }
        finally { File.Delete(path); }
    }

    private ProcessStartInfo Prepare(string operation, string path, PhaytonTarget? target, string? permission, int timeout)
    {
        var start = new ProcessStartInfo(program) { WorkingDirectory = Path.GetDirectoryName(program)! };
        start.Environment["YUZUHA_STATE_DIR"] = context.FilePath(StatePath);
        start.Environment["YUZUHA_FUNCTION_REGISTRY"] = context.FilePath(StatePath + "/functions.json");
        start.Environment["YUZUHA_TRUST_FILE"] = context.FilePath(StatePath + "/trust.json");
        start.Environment["YUZUHA_USER_DIR"] = context.FilePath(StatePath + "/user");
        start.Environment["YUZUHA_KNOWLEDGE_DIR"] = context.FilePath(StatePath + "/knowledge");
        start.Environment["YUZUHA_WEBAPPS_DIR"] = context.FilePath(StatePath + "/webapps");
        string[] arguments = ["op", "invoke", operation, "--input-file", path, "--timeout-seconds",
            timeout.ToString(CultureInfo.InvariantCulture)];
        foreach (var argument in arguments.Concat(target?.Arguments() ?? []))
            start.ArgumentList.Add(argument);
        if (permission is not null)
            start.ArgumentList.Add(permission);
        return start;
    }

    private static string FindProgram(Func<string[]> findPrograms)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Phayton 函数调用需要 Windows。");
        return findPrograms() switch
        {
            [var program] => program,
            [] => throw new InvalidDataException("找不到已登记的 Phayton CLI。"),
            _ => throw new InvalidDataException("发现多个 Phayton CLI，无法确定本次调用使用的安装。")
        };
    }
}

internal sealed record PhaytonTarget(int Pid, long StartTicks, string Model)
{
    internal IEnumerable<string> Arguments()
    {
        yield return "--pid";
        yield return Pid.ToString(CultureInfo.InvariantCulture);
        yield return "--expected-start-time-utc-ticks";
        yield return StartTicks.ToString(CultureInfo.InvariantCulture);
        if (Model.Length != 0)
        {
            yield return "--expected-model";
            yield return Model;
        }
    }

    internal bool Matches(JsonElement status)
    {
        var host = status.Field("Host");
        return status.Boolean("Connected") && status.Boolean("TargetVerified")
            && status.Field("Compatibility").Boolean("Compatible")
            && host.Integer("ProcessId") == Pid && host.Integer("ProcessStartTimeUtcTicks") == StartTicks
            && host.String("Model") is { Length: > 0 } actual
            && (Model.Length == 0 || actual.Equals(Model, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record PhaytonReceipt(string Operation, int ExitCode, string Raw, string Stderr,
    bool Success, bool MayHaveExecuted, JsonElement Data, string Message)
{
    internal bool HostSucceeded => Success && Data.Boolean("Success") && Data.String("Code") == "OK";

    internal static PhaytonReceipt Read(string operation, int exit, string output, string stderr)
    {
        using var document = JsonDocument.Parse(output);
        var envelope = document.RootElement;
        if (envelope.String("operationId") != operation || envelope.Integer("exitCode") != exit)
            throw new InvalidDataException("Phayton 回执的操作或退出码不匹配。");
        var error = envelope.Field("error");
        return new(operation, exit, output, stderr, exit == 0 && envelope.Boolean("success"),
            error.Boolean("mayHaveExecuted"), envelope.Field("data").Clone(), error.String("message"));
    }

    internal static PhaytonReceipt Uncertain(string operation, string message, bool mayHaveExecuted)
    {
        var error = new { code = "TOOLBOX_TRANSPORT_FAILED", message, mayHaveExecuted };
        return new(operation, -1, JsonSerializer.Serialize(new { operationId = operation, success = false,
            exitCode = -1, error }), "", false, mayHaveExecuted, default, message);
    }

    internal static PhaytonReceipt Blocked(string operation)
    {
        var error = new { code = PmlChannelMode.DeniedCode, message = PmlChannelMode.DeniedMessage, mayHaveExecuted = false };
        return new(operation, 4, JsonSerializer.Serialize(new { operationId = operation, success = false, exitCode = 4, error }),
            "", false, false, default, PmlChannelMode.DeniedMessage);
    }

    internal CoreData Describe() => new("receipt", new CoreField("operation", Operation),
        new CoreField("exitCode", ExitCode), new CoreField("success", Success),
        new CoreField("mayHaveExecuted", MayHaveExecuted), Raw);
}

internal static class PhaytonJson
{
    internal static JsonElement Field(this JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) ? field : default;

    internal static string String(this JsonElement value, string name) =>
        value.Field(name) is { ValueKind: JsonValueKind.String } field ? field.GetString() ?? "" : "";

    internal static long Integer(this JsonElement value, string name) =>
        value.Field(name) is { ValueKind: JsonValueKind.Number } field && field.TryGetInt64(out var number) ? number : 0;

    internal static bool Boolean(this JsonElement value, string name) => value.Field(name).ValueKind == JsonValueKind.True;

    internal static IEnumerable<JsonElement> Items(this JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];
}
