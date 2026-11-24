using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core30.Core;

namespace Core30.PmlTwin;

public sealed record PmlLiveSession(int Pid, long StartTicks, string Model, string Product, string Project, string HostVersion)
{
    public bool EnforcesReadOnly { get; init; }
    public bool SupportsReviewedFunctions { get; init; }
    public bool ReviewedFunctionLibraryReady { get; init; }
    public string ExecutablePath { get; init; } = "";
    public string LoadedHostDll { get; init; } = "";
    public override string ToString() => string.IsNullOrWhiteSpace(Project)
        ? $"{Product} · {Model} · PID {Pid}" : $"{Product} · {Project} / {Model} · PID {Pid}";
}

public sealed record PmlSessionDiscovery(IReadOnlyList<PmlLiveSession> Verified, int PendingHostCount);

public sealed record PmlSessionContext(PmlLiveSession Session, string Product, string Version, string Project,
    string Mdb, string Module, IReadOnlyList<string> Libraries)
{
    public bool ContainsLibraryPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Libraries.Any(library =>
        {
            var relative = Path.GetRelativePath(library, fullPath);
            return !Path.IsPathRooted(relative) && relative != ".." &&
                !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        });
    }

    public bool SameEngineeringContext(PmlSessionContext other) => new[] { Product, Version, Project, Mdb, Module }
        .SequenceEqual(new[] { other.Product, other.Version, other.Project, other.Mdb, other.Module }, StringComparer.OrdinalIgnoreCase);
}

public sealed record PmlHostReceipt(string Operation, int ExitCode, bool Success, bool MayHaveExecuted, string Output, string Error);
public sealed record PmlCallbackRestore(string Control, string Status);
public sealed class PmlHostOperationException(string message, bool mayHaveExecuted = false) : IOException(message)
{
    public bool MayHaveExecuted { get; } = mayHaveExecuted;
}

/// <summary>Uses the installed native PmlTwin Host for session-scoped workbench operations.</summary>
public sealed class PmlWorkbenchHost
{
    private const string Execute = "--allow-execution";
    private const string StatusOperation = "aveva.session.status";
    private const string Recorder = "YuzuhaChooseFormToTrack";
    private static readonly Regex NamePattern = new(@"\A[A-Za-z][A-Za-z0-9_]{0,63}\z", RegexOptions.CultureInvariant);
    private readonly FeatureContext _workspace;
    private readonly IPhaytonFunctionClient _client;
    private readonly Action<PmlHostReceipt>? _log;

    public PmlWorkbenchHost(string workspace, Action<PmlHostReceipt>? log = null)
    {
        _workspace = new FeatureContext(workspace);
        _client = new PhaytonFunctionClient(_workspace, RegisteredPhaytonCli.FindWorkbenchPrograms);
        _log = log;
    }

    internal PmlWorkbenchHost(FeatureContext workspace, IPhaytonFunctionClient client)
    {
        _workspace = workspace;
        _client = client;
    }

    public IReadOnlyList<PmlLiveSession> Sessions() => DiscoverSessions().Verified;

    public PmlSessionDiscovery DiscoverSessions()
    {
        var result = Require(Send("aveva.sessions.list", new { }));
        var sessions = new List<PmlLiveSession>();
        var pending = 0;
        foreach (var row in result.Field("Sessions").Items())
        {
            var pid = row.Integer("ProcessId");
            var ticks = row.Integer("ProcessStartTimeUtcTicks");
            if (pid is <= 0 or > int.MaxValue || ticks <= 0 || ticks > DateTime.MaxValue.Ticks)
                continue;
            pending++;
            if (!row.Boolean("PipeDetected"))
                continue;
            var target = new PhaytonTarget((int)pid, ticks, "");
            var status = Send(StatusOperation, new { }, target, timeout: 4);
            if (!status.Success || !target.Matches(status.Data))
                continue;
            var host = status.Data.Field("Host");
            var model = host.String("Model");
            if (!model.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                sessions.Add(new((int)pid, ticks, model, row.String("Product"), row.String("Project"), host.String("HostVersion"))
                {
                    EnforcesReadOnly = host.Integer("PmlChannelModeVersion") >= 1,
                    SupportsReviewedFunctions = host.Integer("PmlChannelModeVersion") >= 2,
                    ReviewedFunctionLibraryReady = host.Boolean("ReviewedFunctionLibraryReady"),
                    ExecutablePath = row.String("ExecutablePath"),
                    LoadedHostDll = ReadLoadedHostDll((int)pid, ticks, host.String("HostFramework"))
                });
                pending--;
            }
        }
        return new(sessions, pending);
    }

    private static string ReadLoadedHostDll(int pid, long startTicks, string framework)
    {
        var dllName = PmlHostConfiguration.DllFileName(framework);
        if (dllName.Length == 0 || !OperatingSystem.IsWindows()) return "";

        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.StartTime.ToUniversalTime().Ticks != startTicks) return "";
            var paths = process.Modules.Cast<ProcessModule>()
                .Where(module => module.ModuleName.Equals(dllName, StringComparison.OrdinalIgnoreCase))
                .Select(module => module.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            return paths.Length == 1 ? paths[0] : "";
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return "";
        }
    }

    public PmlSessionContext Context(PmlLiveSession session)
    {
        var target = Target(session);
        var status = Verify(target);
        var identity = new AvevaLoginSession(_client).ReadIdentity(target, status);
        var fields = identity.Child("session");
        if (identity.Field("state")?.Value != "observed" || fields is null)
            throw new PmlHostOperationException(identity.Child("message")?.Value ?? "无法读取实际项目、MDB 和模块。");
        var rows = ReadMacro(target, """
            var !path evar PMLLIB
            !!RESULT.append('[0] PmlLib<STRING> ' + !path)
            """);
        var pathList = rows.SingleOrDefault(row => row.Name == "PmlLib").Value ?? "";
        var libraries = pathList.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Trim('"')).Where(Path.IsPathFullyQualified)
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (libraries.Length == 0) throw new InvalidDataException("会话没有返回可用的 PMLLIB 目录。");
        string Field(string name) => fields.Field(name)?.Value ?? "";
        return new(session with { HostVersion = status.Data.Field("Host").String("HostVersion") },
            Field("product"), Field("version"), Field("project"), Field("mdb"), Field("module"), libraries);
    }

    public IReadOnlyList<string> ShownForms(PmlLiveSession session)
    {
        var target = Target(session);
        Verify(target);
        var query = Require(Send("pml.forms.list", new { shownOnly = true, maxItems = 1000, maxCharacters = 262144 }, target, Execute),
            host: true).Field("FormQueryResult");
        if (query.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Host 缺少结构化窗体清单。");
        if (query.Boolean("Truncated"))
            throw new InvalidDataException("可见窗体列表超出读取上限，请先收起无关窗体。");
        return query.Field("Items").Items().Select(item => item.String("Name").TrimStart('!'))
            .Where(name => NamePattern.IsMatch(name) && !name.Equals(Recorder, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void Rehash(PmlLiveSession session)
    {
        var target = Target(session);
        Verify(target);
        Require(Send("pml.command.execute", new { pmlCommand = "PML REHASH ALL" }, target, Execute, timeout: 60), host: true);
    }

    public string StartRecording(PmlLiveSession session, string formName)
    {
        var form = FormName(formName);
        if (!ShownForms(session).Contains(form, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("所选窗体已不可见，请重新读取会话窗体。");
        var rows = ReadMacro(Target(session), $$"""
            !already = false
            if Defined(!!YuzuhaFormRecorder) then
              !forms = !!YuzuhaFormRecorder.TrackedForms()
              !already = !forms.find('{{form}}').size() gt 0
            endif
            if !already then
              !!RESULT.append('[0] State<STRING> AlreadyRecording')
              return
            endif
            show !!YuzuhaChooseFormToTrack
            !!YuzuhaChooseFormToTrack.filterGadGet('{{form}}','DECORATED')
            if Defined(!!YuzuhaFormRecorder) then
              !forms = !!YuzuhaFormRecorder.TrackedForms()
              if !forms.find('{{form}}').size() gt 0 then
                !!RESULT.append('[0] State<STRING> Recording')
                return
              endif
            endif
            !!RESULT.append('[0] State<STRING> NoCallbacks')
            """, changesRecording: true);
        var state = rows.SingleOrDefault(row => row.Name == "State").Value;
        return state is "Recording" or "AlreadyRecording" or "NoCallbacks"
            ? state : throw new PmlHostOperationException("Host 未返回明确的录制状态。", mayHaveExecuted: true);
    }

    public bool IsRecording(PmlLiveSession session, string formName)
    {
        var form = FormName(formName);
        var rows = ReadMacro(Target(session), $$"""
            if Defined(!!YuzuhaFormRecorder) then
              !forms = !!YuzuhaFormRecorder.TrackedForms()
              if !forms.find('{{form}}').size() gt 0 then
                !!RESULT.append('[0] State<STRING> Recording')
                return
              endif
            endif
            !!RESULT.append('[0] State<STRING> Idle')
            """);
        return rows.SingleOrDefault(row => row.Name == "State").Value switch
        {
            "Recording" => true,
            "Idle" => false,
            _ => throw new InvalidDataException("无法核对窗体录制状态。")
        };
    }

    public IReadOnlyList<PmlCallbackRestore> StopRecording(PmlLiveSession session, string formName)
    {
        var form = FormName(formName);
        var rows = ReadMacro(Target(session), $$"""
            if Defined(!!YuzuhaFormRecorder) then
              !results = !!YuzuhaFormRecorder.Restore('{{form}}')
              do !row values !results
                !!RESULT.append('[0] ' + !row[1] + '<STRING> ' + !row[2])
              enddo
            endif
            """, changesRecording: true);
        return rows.Select(row => new PmlCallbackRestore(row.Name, row.Value)).ToArray();
    }

    private (string Name, string Value)[] ReadMacro(PhaytonTarget target, string body, bool changesRecording = false)
    {
        Verify(target);
        var token = Guid.NewGuid().ToString("N");
        var global = "Core30Workbench" + token;
        var path = _workspace.FilePath($"{PhaytonFunctionClient.StatePath}/workbench-{token}.pmlmac");
        if (path.IndexOfAny(['$', '\r', '\n']) >= 0)
            throw new InvalidDataException("工作区路径不能含有 PML 展开符或换行。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"!!{global} = object array()\n{body.Replace("!!RESULT", $"!!{global}", StringComparison.Ordinal)}\n",
            new UTF8Encoding(false));

        var keepMacro = false;
        try
        {
            var command = Send("pml.command.execute", new { pmlCommand = "$M '" + path.Replace("'", "''") + "'" }, target, Execute);
            keepMacro = !command.HostSucceeded && (command.MayHaveExecuted ||
                changesRecording && command.Data.Field("Success").ValueKind == JsonValueKind.False);
            if (keepMacro && changesRecording)
                throw new PmlHostOperationException("录制命令的结果需通过当前窗体状态核对。", mayHaveExecuted: true);
            Require(command, host: true);
            var read = Send("pml.array.read", new
            {
                pmlCommand = $"!!{global}", globalVar = global, deleteGlobalVar = true, includeEmpty = true
            }, target, Execute);
            if (!read.HostSucceeded || read.Data.Field("Items").ValueKind != JsonValueKind.Array ||
                read.Data.Field("UnparsedCount").ValueKind != JsonValueKind.Number || read.Data.Integer("UnparsedCount") != 0)
                throw new PmlHostOperationException("Host 未返回完整的结构化结果，请核对实际会话状态。", changesRecording);
            return read.Data.Field("Items").Items().Select(item => (item.String("Path"), item.String("Value"))).ToArray();
        }
        finally
        {
            if (!keepMacro) File.Delete(path);
        }
    }

    private PhaytonReceipt Verify(PhaytonTarget target)
    {
        var status = Send(StatusOperation, new { }, target, timeout: 4);
        if (!status.Success || !target.Matches(status.Data))
            throw new PmlHostOperationException("所选 Host 的 PID、启动时间、模块或协议已变化，请重新选择会话。");
        return status;
    }

    private PhaytonReceipt Send(string operation, object input, PhaytonTarget? target = null, string? permission = null, int timeout = 12)
    {
        var result = _client.Run(operation, input, target, permission, timeout);
        var looksLikeHostResult = result.Data.Field("Success").ValueKind is JsonValueKind.True or JsonValueKind.False;
        _log?.Invoke(new(operation, result.ExitCode, looksLikeHostResult ? result.HostSucceeded : result.Success,
            result.MayHaveExecuted, result.Raw, result.Stderr));
        return result;
    }

    private static JsonElement Require(PhaytonReceipt receipt, bool host = false)
    {
        if (host ? receipt.HostSucceeded : receipt.Success) return receipt.Data;
        var detail = receipt.Data.String("ErrorMessage");
        if (detail.Length == 0) detail = receipt.Message;
        throw new PmlHostOperationException(detail.Length == 0 ? $"{receipt.Operation} 失败，请查看执行日志。" : detail,
            receipt.MayHaveExecuted);
    }

    private static PhaytonTarget Target(PmlLiveSession session) => session.Pid > 0 && session.StartTicks > 0 &&
        session.Model.Length > 0 && !session.Model.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
        ? new(session.Pid, session.StartTicks, session.Model) : throw new InvalidDataException("请选择已核验的 AVEVA 会话。");

    private static string FormName(string name) => NamePattern.IsMatch(name) && !name.Equals(Recorder, StringComparison.OrdinalIgnoreCase)
        ? name.ToUpperInvariant() : throw new InvalidDataException("请选择普通原生窗体。");

}
