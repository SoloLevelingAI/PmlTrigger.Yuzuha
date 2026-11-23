using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Core30.Core;

public sealed record WorkspaceRun(string RunId, string Feature, string Category, string Operation, string State,
    DateTimeOffset At, int? ProcessId, long? StartTicks, long? Milliseconds, string RelativePath, RunEvent[] Events)
{
    public string? EntryPoint => Events.LastOrDefault()?.EntryPoint;

    public bool Matches(string category, string state, string query) =>
        (category.Length == 0 || Category == category)
        && (state.Length == 0 || State == state)
        && (query.Length == 0 || RunId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Feature.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Operation.Contains(query, StringComparison.OrdinalIgnoreCase)
            || EntryPoint?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
            || ProcessId?.ToString(CultureInfo.InvariantCulture) == query);
}

public sealed record WorkspaceActivitySnapshot(WorkspaceRun[] Runs, int Inspected, bool HasOlderLogs);

/// <summary>Reads Core run records and verifies live PID identities without invoking a Feature.</summary>
public static class WorkspaceActivity
{
    private const int ScanLimit = 500;
    private const int LogLimit = 256 * 1024;
    private const string LogDirectory = ".core30/logs";

    public static WorkspaceActivitySnapshot Read(FeatureContext context)
    {
        var directory = context.FilePath(LogDirectory);
        if (!Directory.Exists(directory))
            return new WorkspaceActivitySnapshot([], 0, false);
        var files = Directory.EnumerateFiles(directory).Where(IsRunFile)
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(ScanLimit + 1).ToArray();
        var runs = files.Take(ScanLimit).Select(path => TryRead(context, Path.GetFileName(path)))
            .OfType<WorkspaceRun>().OrderByDescending(run => run.State == "running")
            .ThenByDescending(run => run.At).ToArray();
        return new WorkspaceActivitySnapshot(runs, Math.Min(files.Length, ScanLimit), files.Length > ScanLimit);
    }

    private static bool IsRunFile(string path) =>
        Path.GetExtension(path) is ".jsonl" or ".log"
        && Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _);

    private static WorkspaceRun? TryRead(FeatureContext context, string name)
    {
        try
        {
            var relative = $"{LogDirectory}/{name}";
            var path = context.FilePath(relative);
            var events = ReadEvents(path, Path.GetFileNameWithoutExtension(name));
            return events.Length == 0 ? null : Summarize(relative, events);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static RunEvent[] ReadEvents(string path, string runId)
    {
        using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        RequireSmallLog(input.Length);
        using var reader = new StreamReader(input);
        var events = new List<RunEvent>();
        var isJson = Path.GetExtension(path) == ".jsonl";
        while (reader.ReadLine() is { } line)
        {
            var entry = isJson ? ParseJson(line) : ParseText(line);
            if (entry?.Run == runId && entry.At != default
                && !string.IsNullOrEmpty(entry.Feature) && !string.IsNullOrEmpty(entry.Phase))
                events.Add(entry);
        }
        return events.ToArray();
    }

    private static void RequireSmallLog(long length)
    {
        if (length > LogLimit)
            throw new InvalidDataException("Run log is larger than the activity window.");
    }

    private static RunEvent? ParseJson(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<RunEvent>(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RunEvent? ParseText(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 5 || !DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            return null;
        return new RunEvent(at, parts[1], parts[2], parts[3], parts[4])
        {
            ProcessId = int.TryParse(parts.ElementAtOrDefault(5), out var id) ? id : null,
            StartTicks = long.TryParse(parts.ElementAtOrDefault(6), out var ticks) ? ticks : null,
            Category = parts.ElementAtOrDefault(7),
            Operation = parts.ElementAtOrDefault(8),
            EntryPoint = parts.ElementAtOrDefault(9)
        };
    }

    private static WorkspaceRun Summarize(string relative, RunEvent[] events)
    {
        var first = events[0];
        var last = events[^1];
        var state = GetState(last);
        var elapsed = Elapsed(first, last, state);
        return new WorkspaceRun(first.Run, first.Feature, last.Category ?? "unclassified",
            last.Operation ?? "request", state, first.At, last.ProcessId, last.StartTicks, elapsed, relative, events);
    }

    private static long? Elapsed(RunEvent first, RunEvent last, string state) => state switch
    {
        "running" => (long)Math.Max(0, (DateTimeOffset.UtcNow - first.At).TotalMilliseconds),
        "completed" or "failed" when first.Phase == "started" =>
            (long)Math.Max(0, (last.At - first.At).TotalMilliseconds),
        _ => null
    };

    private static string GetState(RunEvent entry) => entry.Phase switch
    {
        "completed" or "failed" => entry.Phase,
        "started" when IsSameProcess(entry.ProcessId, entry.StartTicks) => "running",
        _ => "unknown"
    };

    private static bool IsSameProcess(int? id, long? ticks)
    {
        if (id is not > 0 || ticks is not > 0)
            return false;
        try
        {
            using var process = Process.GetProcessById(id.Value);
            return process.StartTime.ToUniversalTime().Ticks == ticks.Value && !process.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
