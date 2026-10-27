using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace YuzuhaToolkit.Workspace;

/// <summary>一次工具调用的开始或结束记录；参数有界，返回值只保留状态摘要。</summary>
/// <param name="TimeUtc">事件 UTC 时间。</param>
/// <param name="Service">服务名称。</param>
/// <param name="ProcessId">提供工具的进程编号。</param>
/// <param name="CallId">本次工具调用关联编号。</param>
/// <param name="Tool">工具名称。</param>
/// <param name="Phase">started、succeeded、failed 或 cancelled-unknown。</param>
/// <param name="Milliseconds">从开始到结束的耗时。</param>
/// <param name="TargetPid">选定的 AVEVA PID；空值表示尚未选择。</param>
/// <param name="Arguments">脱敏后的有界参数 JSON 文本。</param>
/// <param name="Summary">结果摘要或错误，不包含完整元素返回数组。</param>
public sealed record ActivityEntry(DateTimeOffset TimeUtc, string Service, int ProcessId, string CallId,
    string Tool, string Phase, long Milliseconds, int? TargetPid, string Arguments, string Summary)
{
    /// <summary>中文工作台显示的北京时间，原始日志继续保存 UTC。</summary>
    public string DisplayTime => TimeUtc.ToOffset(TimeSpan.FromHours(8)).ToString("HH:mm:ss");
    /// <summary>工作台显示的工具事件名称。</summary>
    public string PhaseLabel => Phase switch { "started" => "开始", "succeeded" => "完成", "failed" => "失败", "cancelled-unknown" => "已取消 · 结果待核验", _ => Phase };
}

/// <summary>单个服务进程的心跳及正在执行的工具；不代表 AI 的内部思考状态。</summary>
/// <param name="Service">服务名称。</param>
/// <param name="ProcessId">本机服务 PID。</param>
/// <param name="ProcessStartTicks">进程启动 UTC ticks。</param>
/// <param name="UpdatedUtc">最近一次心跳。</param>
/// <param name="State">idle、running、stopped 或进程观察状态。</param>
/// <param name="ActiveTools">当前正在执行的工具。</param>
/// <param name="TargetPid">当前选定的 AVEVA PID。</param>
/// <param name="LastOutcome">最近完成工具的结果。</param>
/// <param name="LoggingError">本机日志写入故障。</param>
/// <param name="Directory">本进程日志目录。</param>
public sealed record RuntimeStatus(string Service, int ProcessId, long ProcessStartTicks, DateTimeOffset UpdatedUtc,
    string State, string[] ActiveTools, int? TargetPid, string LastOutcome, string? LoggingError, string Directory)
{
    /// <summary>供原生列表显示的当前工具集合。</summary>
    public string ActiveSummary => string.Join(", ", ActiveTools);
    /// <summary>工作台显示的进程运行状态。</summary>
    public string StateLabel => State switch { "idle" => "空闲", "running" => "执行中", "stopped" => "已停止", "exited" => "进程已退出", "exited-result-unknown" => "已退出 · 结果待核验", "heartbeat-stale" => "心跳过期", _ => State };
    /// <summary>优先显示日志故障，否则显示最近调用结果。</summary>
    public string OutcomeSummary => LoggingError is { Length: > 0 } ? "日志故障：" + LoggingError : LastOutcome;
}

/// <summary>持续写入滚动 JSONL 与原子心跳快照，供原生启动台读取全部 MCP 进程。</summary>
public sealed class RuntimeJournal : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (string Tool, long Started)> _active = [];
    private readonly Timer _timer;
    private readonly string _service;
    private readonly string _directory;
    private readonly long _processStart;
    private readonly int _maxLogBytes;
    private int _index;
    private bool _disposed;
    private string _lastOutcome = "尚无工具调用";
    private string? _appendError;
    private string? _statusError;
    private int? _targetPid;

    /// <summary>为当前进程建立独立状态目录；不写 stdout，不调用 AVEVA。</summary>
    /// <param name="service">显示在统一状态台中的服务名称。</param>
    /// <param name="stateDirectory">可选的隔离本机状态目录。</param>
    /// <param name="maxLogBytes">单个日志段预算，默认 1 MiB，每进程最多保留八段。</param>
    public RuntimeJournal(string service, string? stateDirectory = null, int maxLogBytes = 1048576)
    {
        _service = service; _maxLogBytes = Math.Max(1024, maxLogBytes);
        using var process = Process.GetCurrentProcess(); _processStart = process.StartTime.ToUniversalTime().Ticks;
        _directory = Path.Combine(stateDirectory ?? UserDataPaths.StateDirectory, "Activity", Environment.ProcessId + "-" + Guid.NewGuid().ToString("N"));
        _timer = new Timer(_ => WriteStatus(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        WriteStatus();
    }

    /// <summary>记录一次开始事件并返回调用编号；日志失败会写 stderr 并在快照中显示。</summary>
    /// <param name="tool">MCP 工具名或桌面操作名。</param>
    /// <param name="arguments">原始参数对象 JSON；密码、token 等命名字段会被替换。</param>
    /// <param name="targetPid">当前 AVEVA 目标 PID。</param>
    public string Begin(string tool, string arguments, int? targetPid = null)
    {
        lock (_gate)
        {
            var id = Guid.NewGuid().ToString("N"); _active.Add(id, (tool, Stopwatch.GetTimestamp())); _targetPid = targetPid;
            Append(new(DateTimeOffset.UtcNow, _service, Environment.ProcessId, id, tool, "started", 0, targetPid, RedactArguments(arguments), ""));
            WriteStatus(); return id;
        }
    }

    /// <summary>结束关联调用；取消标记为结果待核验，绝不触发重试或模型回滚。</summary>
    /// <param name="callId">Begin 返回的关联编号。</param>
    /// <param name="phase">succeeded、failed 或 cancelled-unknown。</param>
    /// <param name="summary">有界结果摘要。</param>
    /// <param name="targetPid">工具完成时实际选定的 AVEVA PID。</param>
    public void Complete(string callId, string phase, string summary, int? targetPid = null)
    {
        lock (_gate)
        {
            if (!_active.Remove(callId, out var call)) return;
            _targetPid = targetPid; _lastOutcome = call.Tool + " · " + phase;
            Append(new(DateTimeOffset.UtcNow, _service, Environment.ProcessId, callId, call.Tool, phase,
                (long)Stopwatch.GetElapsedTime(call.Started).TotalMilliseconds, targetPid, "", Bound(summary, 2048)));
            WriteStatus();
        }
    }

    /// <summary>当前进程的工具运行状态。</summary>
    public RuntimeStatus Snapshot()
    {
        lock (_gate) return new(_service, Environment.ProcessId, _processStart, DateTimeOffset.UtcNow,
            _disposed ? "stopped" : _active.Count > 0 ? "running" : "idle", _active.Values.Select(p => p.Tool).ToArray(), _targetPid, _lastOutcome, _appendError ?? _statusError, _directory);
    }

    /// <summary>替换命名秘密字段并限制日志大小；任意 PML 字符串仍按开发代码记录。</summary>
    /// <param name="arguments">参数 JSON 文本。</param>
    public static string RedactArguments(string arguments)
    {
        if (arguments.Length > 1048576) return "[参数超过日志预算，未记录正文]";
        try
        {
            using var document = JsonDocument.Parse(arguments);
            using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
            void Write(JsonElement value, int depth)
            {
                if (depth > 32) { writer.WriteStringValue("[depth limit]"); return; }
                if (value.ValueKind == JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        if (new[] { "password", "passwd", "token", "secret", "authorization", "api_key", "apikey" }.Any(secret => property.Name.Contains(secret, StringComparison.OrdinalIgnoreCase))) writer.WriteStringValue("[redacted]");
                        else Write(property.Value, depth + 1);
                    }
                    writer.WriteEndObject();
                }
                else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray().Take(500)) Write(item, depth + 1); writer.WriteEndArray(); }
                else if (value.ValueKind == JsonValueKind.String) writer.WriteStringValue(Bound(value.GetString() ?? "", 8192));
                else value.WriteTo(writer);
            }
            Write(document.RootElement, 0); writer.Flush(); return Bound(Encoding.UTF8.GetString(stream.ToArray()), 16384);
        }
        catch (JsonException) { return "[参数不是有效 JSON，未记录正文]"; }
    }

    private static string Bound(string value, int length) => value.Length > length ? value[..length] + "…[truncated]" : value;
    private void Append(ActivityEntry entry) => Persist(() =>
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, $"events-{_index:D6}.jsonl");
        if (File.Exists(file) && new FileInfo(file).Length >= _maxLogBytes) file = Path.Combine(_directory, $"events-{++_index:D6}.jsonl");
        File.AppendAllText(file, JsonSerializer.Serialize(entry, WorkspaceJsonContext.Default.ActivityEntry).Replace("\r", "").Replace("\n", "") + "\n", new UTF8Encoding(false));
        foreach (var expired in Directory.GetFiles(_directory, "events-*.jsonl").OrderDescending().Skip(8)) File.Delete(expired);
    });
    private void WriteStatus()
    {
        lock (_gate) Persist(() =>
        {
            Directory.CreateDirectory(_directory); var file = Path.Combine(_directory, "status.json"); var temporary = file + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Snapshot(), WorkspaceJsonContext.Default.RuntimeStatus)); File.Move(temporary, file, true);
        }, status: true);
    }
    private void Persist(Action write, bool status = false)
    {
        try { write(); if (status) _statusError = null; else _appendError = null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if ((status ? _statusError : _appendError) != error.Message) Console.Error.WriteLine("Yuzuha 日志写入失败：" + error.Message);
            if (status) _statusError = error.Message; else _appendError = error.Message;
        }
    }

    /// <summary>停止心跳；若调用尚未结束，会在最终快照中保留待核验状态。</summary>
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _timer.Dispose(); WriteStatus(); } }
}

/// <summary>读取各进程的原子快照和有界近期调用，用于本机状态台。</summary>
public static class RuntimeReader
{
    /// <summary>读取最近的状态文件，并以 PID 和启动时间确认进程仍存在。</summary>
    /// <param name="stateDirectory">测试或便携环境使用的状态根目录。</param>
    public static RuntimeStatus[] Statuses(string? stateDirectory = null)
    {
        var root = Path.Combine(stateDirectory ?? UserDataPaths.StateDirectory, "Activity");
        if (!Directory.Exists(root)) return [];
        var results = new List<RuntimeStatus>();
        foreach (var directory in Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(100))
        {
            try
            {
                var file = Path.Combine(directory, "status.json"); if (!File.Exists(file) || new FileInfo(file).Length > 65536) continue;
                var status = JsonSerializer.Deserialize(File.ReadAllText(file), WorkspaceJsonContext.Default.RuntimeStatus); if (status is null) continue;
                var alive = false;
                try { using var process = Process.GetProcessById(status.ProcessId); alive = process.StartTime.ToUniversalTime().Ticks == status.ProcessStartTicks; }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                results.Add(status with { State = !alive ? status.ActiveTools.Length > 0 ? "exited-result-unknown" : "exited" : status.State != "stopped" && DateTimeOffset.UtcNow - status.UpdatedUtc > TimeSpan.FromSeconds(15) ? "heartbeat-stale" : status.State });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return results.OrderByDescending(status => status.UpdatedUtc).ToArray();
    }

    /// <summary>读取最多 200 条最近事件，最多检查最近 16 个日志段，每段最多约 2 MiB。</summary>
    /// <param name="limit">返回记录数，1 到 200。</param>
    /// <param name="stateDirectory">状态根目录。</param>
    public static ActivityEntry[] Recent(int limit = 100, string? stateDirectory = null)
    {
        var entries = new List<ActivityEntry>();
        foreach (var file in Statuses(stateDirectory).Take(16).SelectMany(status => Directory.Exists(status.Directory) ? Directory.GetFiles(status.Directory, "events-*.jsonl") : []).OrderByDescending(File.GetLastWriteTimeUtc).Take(16))
        {
            try
            {
                if (new FileInfo(file).Length > 2097152) continue;
                foreach (var line in File.ReadLines(file))
                {
                    try { var entry = JsonSerializer.Deserialize(line, WorkspaceJsonContext.Default.ActivityEntry); if (entry is not null) entries.Add(entry); }
                    catch (JsonException) { /* A final partial line from an interrupted process is not a completed call. */ }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        return entries.OrderByDescending(entry => entry.TimeUtc).Take(Math.Clamp(limit, 1, 200)).ToArray();
    }
}
