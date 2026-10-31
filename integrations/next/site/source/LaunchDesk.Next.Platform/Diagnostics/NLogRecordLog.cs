using System.Collections.ObjectModel;
using System.Text.Json;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
using LaunchDesk.Next.Domain.Security;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
namespace LaunchDesk.Next.Platform.Diagnostics;

public sealed class NLogRecordLog : IRecordLog, IDisposable
{
    private readonly List<Record> records = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly LogFactory factory = new();
    private readonly FileTarget target;
    private long sequence;
    public bool HasDamagedHistory { get; }
    public NLogRecordLog(string path)
    {
        path = Path.GetFullPath(path);
        if (File.Exists(path))
        {
            var history = Read(path);
            records.AddRange(history.Records);
            HasDamagedHistory = history.DamagedTail;
            sequence = records.Count == 0 ? 0 : records.Max(record => record.Sequence);
            // Keep damaged bytes for inspection; isolate an unterminated tail before appending.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            if (stream.Length > 0)
            {
                stream.Seek(-1, SeekOrigin.End);
                if (stream.ReadByte() != '\n')
                {
                    stream.Seek(0, SeekOrigin.End);
                    stream.WriteByte((byte)'\n');
                    stream.Flush();
                }
            }
        }
        target = new FileTarget("evidence") { FileName = Path.GetFullPath(path), Layout = "${message}", KeepFileOpen = false, AutoFlush = true };
        var config = new LoggingConfiguration();
        config.AddRuleForAllLevels(target);
        factory.Configuration = config;
    }
    public IReadOnlyList<Record> Records { get { lock (records) return records.ToArray(); } }
    public async Task<bool> AppendAsync(Guid attemptId, Guid? runId, string stage, string outcome, IReadOnlyDictionary<string, string>? snapshot = null)
    {
        await gate.WaitAsync();
        try
        {
            var clean = new ReadOnlyDictionary<string, string>((snapshot ?? new Dictionary<string, string>()).Where(p => !SensitiveFields.ContainsSecret(p.Key)).ToDictionary(p => p.Key, p => p.Value));
            Record record; lock (records)
            {
                record = new(++sequence, DateTimeOffset.UtcNow, attemptId, runId, stage, outcome, clean);
                records.Add(record);
            }
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                target.WriteAsyncLogEvent(new AsyncLogEventInfo(new LogEventInfo(LogLevel.Info, "evidence", JsonSerializer.Serialize(record)), ex => completion.TrySetResult(ex is null)));
                if (!await completion.Task.WaitAsync(TimeSpan.FromSeconds(5))) return false;
                var flush = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                target.Flush(ex => flush.TrySetResult(ex is null));
                return await flush.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch { return false; }
        }
        finally { gate.Release(); }
    }
    public static (IReadOnlyList<Record> Records, bool DamagedTail) Read(string path)
    {
        var result = new List<Record>();
        var damaged = false;
        foreach (var line in File.ReadLines(path)) try
            {
                var record = JsonSerializer.Deserialize<Record>(line);
                if (record != null) result.Add(record with { Snapshot = new ReadOnlyDictionary<string, string>(record.Snapshot.Where(pair => !SensitiveFields.ContainsSecret(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value)) }); else damaged = true;
            }
            catch (JsonException) { damaged = true; }
        return (result, damaged);
    }
    public void Dispose() => factory.Dispose();
}
