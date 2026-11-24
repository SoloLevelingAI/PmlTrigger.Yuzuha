using System.Text.Json;

namespace Core30.Core;

public sealed record RunEvent(DateTimeOffset At, string Run, string Feature, string Phase, string Detail)
{
    public int? ProcessId { get; init; }
    public long? StartTicks { get; init; }
    public string? Category { get; init; }
    public string? Operation { get; init; }
    public string? EntryPoint { get; init; }
}

public interface IRunLog
{
    string RelativePath { get; }
    void Record(RunEvent entry);
}

public interface IRunLogProvider
{
    string Name { get; }
    IRunLog Open(FeatureContext context, string runId);
}

public sealed class JsonLinesRunLogProvider : IRunLogProvider
{
    public string Name => "jsonl";

    public IRunLog Open(FeatureContext context, string runId) =>
        new FileRunLog(context, $".core30/logs/{runId}.jsonl", entry => JsonSerializer.Serialize(entry));
}

public sealed class TextRunLogProvider : IRunLogProvider
{
    public string Name => "text";

    public IRunLog Open(FeatureContext context, string runId) =>
        new FileRunLog(context, $".core30/logs/{runId}.log",
            entry => FormattableString.Invariant(
                $"{entry.At:O}\t{entry.Run}\t{entry.Feature}\t{entry.Phase}\t{entry.Detail}\t{entry.ProcessId}\t{entry.StartTicks}\t{entry.Category}\t{entry.Operation}\t{entry.EntryPoint}"));
}

public sealed class FileRunLog(
    FeatureContext context,
    string relativePath,
    Func<RunEvent, string> format) : IRunLog
{
    public string RelativePath => relativePath;

    public void Record(RunEvent entry)
    {
        var path = context.FilePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = format(entry) + Environment.NewLine;
        File.AppendAllText(path, line);
    }
}
