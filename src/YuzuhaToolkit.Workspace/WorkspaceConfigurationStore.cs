using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YuzuhaToolkit.Workspace;

/// <summary>兼容 workspace.json 的整文档修订存储，使用互斥文件、提交日志与历史快照。</summary>
public sealed class WorkspaceConfigurationStore : IConfigurationStore<WorkspaceSettings>
{
    private readonly string directory;
    private readonly string file;
    private readonly string history;
    private readonly Action<string>? commitCheckpoint;

    /// <summary>选择状态目录；可注入提交检查点验证崩溃恢复，不改变生产提交顺序。</summary>
    public WorkspaceConfigurationStore(string? stateDirectory = null, Action<string>? commitCheckpoint = null)
    {
        directory = Path.GetFullPath(stateDirectory ?? WorkspaceSettings.StateDirectory);
        file = Path.Combine(directory, "workspace.json");
        history = Path.Combine(directory, "workspace.history");
        this.commitCheckpoint = commitCheckpoint;
    }

    /// <summary>读取文件快照，恢复已替换文件但未完成历史记录的中断提交。</summary>
    public ConfigSnapshot<WorkspaceSettings> Read()
    {
        using var lease = Acquire(); Recover();
        return ReadCore();
    }

    /// <summary>先冻结历史和日志，再替换当前文件；每次提交都有独立修订。</summary>
    public ConfigCommit<WorkspaceSettings> Commit(WorkspaceSettings candidate, string expectedRevision, ConfigurationChangeReason reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(reason);
        if (string.IsNullOrWhiteSpace(reason.Reason) || reason.Reason.Length > 1000)
            throw new ArgumentException("请提供不超过 1000 字符的修改原因。");
        using var lease = Acquire(); Recover();
        var current = ReadCore();
        if (current.Revision != expectedRevision) throw new ConfigurationConflictException(current.Revision);
        Validate(candidate);
        var next = Clone(candidate);
        next.ConfigurationRevision = Guid.NewGuid().ToString("N");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next, WorkspaceJsonContext.Default.WorkspaceSettings);
        var revision = Hash(bytes);
        var changes = Difference(current.Value, next);
        ArchiveCurrent(current);
        var entry = new ConfigHistoryEntry(revision, current.Revision, DateTimeOffset.UtcNow,
            reason.Reason, reason.ProfileId, reason.RestoredFrom, "application");
        WriteAtomic(SnapshotPath(revision), bytes);
        WriteEntry(Path.Combine(history, "pending.json"), entry);
        commitCheckpoint?.Invoke("prepared");
        WriteAtomic(file, bytes);
        commitCheckpoint?.Invoke("replaced");
        WriteEntry(EntryPath(revision), entry);
        File.Delete(Path.Combine(history, "pending.json"));
        var saved = ReadCore();
        candidate.LoadedRevision = saved.Revision;
        candidate.PersistenceDirectory = directory;
        candidate.ConfigurationRevision = saved.Value.ConfigurationRevision;
        return new(saved, changes, "已保存；启动配置将在下次启动使用，当前 AVEVA 会话不受影响。");
    }

    /// <summary>返回已完成提交及首次接入的原文件快照，不把未提交暂存当作历史。</summary>
    public ConfigHistoryEntry[] History(int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit), "历史条数需为 1 到 500。");
        using var lease = Acquire(); Recover(); ArchiveCurrent(ReadCore());
        if (!Directory.Exists(history)) return [];
        return Directory.EnumerateFiles(history, "*.meta.json").Select(ReadEntry)
            .OrderByDescending(e => e.SavedUtc).ThenByDescending(e => e.Revision, StringComparer.Ordinal).Take(limit).ToArray();
    }

    /// <summary>校验历史快照哈希后读取，不将历史内容替换当前文件。</summary>
    public ConfigSnapshot<WorkspaceSettings> ReadRevision(string revision)
    {
        CheckRevision(revision);
        using var lease = Acquire(); Recover(); ArchiveCurrent(ReadCore());
        var entry = EntryPath(revision);
        if (!File.Exists(entry)) throw new FileNotFoundException("未找到已提交的配置修订。", entry);
        var bytes = File.ReadAllBytes(SnapshotPath(revision));
        if (Hash(bytes) != revision) throw new InvalidDataException("配置历史快照校验失败。");
        return Snapshot(bytes, revision, true);
    }

    /// <summary>生成保持扩展字段的独立工作区对象。</summary>
    internal static WorkspaceSettings Clone(WorkspaceSettings settings) => JsonSerializer.Deserialize(
        JsonSerializer.SerializeToUtf8Bytes(settings, WorkspaceJsonContext.Default.WorkspaceSettings),
        WorkspaceJsonContext.Default.WorkspaceSettings)!;

    /// <summary>字段级 JSON Pointer 差异，忽略仅用于区分提交的内部标记。</summary>
    internal static ConfigChange[] Difference(WorkspaceSettings before, WorkspaceSettings after)
    {
        var left = JsonSerializer.SerializeToNode(before, WorkspaceJsonContext.Default.WorkspaceSettings);
        var right = JsonSerializer.SerializeToNode(after, WorkspaceJsonContext.Default.WorkspaceSettings);
        var changes = new List<ConfigChange>();
        Compare(left, right, "", changes);
        return changes.Where(c => c.Path != "/configurationRevision").ToArray();
    }

    private static void Compare(JsonNode? before, JsonNode? after, string path, List<ConfigChange> changes)
    {
        if (JsonNode.DeepEquals(before, after)) return;
        if (before is JsonObject left && after is JsonObject right)
        {
            foreach (var key in left.Select(p => p.Key).Union(right.Select(p => p.Key), StringComparer.Ordinal).Order(StringComparer.Ordinal))
                Compare(left[key], right[key], path + "/" + key.Replace("~", "~0").Replace("/", "~1"), changes);
        }
        else if (before is JsonArray leftArray && after is JsonArray rightArray && leftArray.Count == rightArray.Count)
        {
            for (var i = 0; i < leftArray.Count; i++) Compare(leftArray[i], rightArray[i], path + "/" + i, changes);
        }
        else changes.Add(new(path, before?.ToJsonString(), after?.ToJsonString()));
    }

    private FileStream Acquire()
    {
        Directory.CreateDirectory(directory);
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(Path.Combine(directory, "workspace.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (watch.Elapsed < TimeSpan.FromSeconds(5)) { Thread.Sleep(25); }
        }
    }

    private ConfigSnapshot<WorkspaceSettings> ReadCore()
    {
        if (!File.Exists(file))
        {
            var empty = new WorkspaceSettings { PersistenceDirectory = directory };
            return new(empty, "missing", 1, file, false);
        }
        var bytes = File.ReadAllBytes(file);
        return Snapshot(bytes, Hash(bytes), true);
    }

    private ConfigSnapshot<WorkspaceSettings> Snapshot(byte[] bytes, string revision, bool exists)
    {
        using var document = JsonDocument.Parse(bytes);
        RejectDuplicates(document.RootElement);
        var value = JsonSerializer.Deserialize(bytes, WorkspaceJsonContext.Default.WorkspaceSettings)
            ?? throw new InvalidDataException("工作区配置为空。");
        Validate(value);
        value.LoadedRevision = revision; value.PersistenceDirectory = directory;
        return new(value, revision, value.SchemaVersion, file, exists);
    }

    private static void Validate(WorkspaceSettings value)
    {
        if (value.SchemaVersion != 1) throw new InvalidDataException("不支持的工作区格式版本；未修改文件。");
        if (value.WarnFiles < 1 || value.WarnBytes < 1) throw new ArgumentException("目录提醒阈值必须为正数。");
        if (value.Profiles is null || value.Profiles.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id)) ||
            value.Profiles.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != value.Profiles.Count)
            throw new InvalidDataException("启动配置 ID 为空或重复。");
    }

    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject())
            {
                if (!keys.Add(field.Name)) throw new InvalidDataException("配置含重复字段：" + field.Name);
                RejectDuplicates(field.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicates(item);
    }

    private void ArchiveCurrent(ConfigSnapshot<WorkspaceSettings> current)
    {
        if (!current.Exists || File.Exists(EntryPath(current.Revision))) return;
        var bytes = File.ReadAllBytes(file);
        if (Hash(bytes) != current.Revision) throw new ConfigurationConflictException(Hash(bytes));
        WriteAtomic(SnapshotPath(current.Revision), bytes);
        WriteEntry(EntryPath(current.Revision), new(current.Revision, "", DateTimeOffset.UtcNow,
            "接入已有或外部修改的配置快照", null, null, "external-snapshot"));
    }

    private void Recover()
    {
        var pending = Path.Combine(history, "pending.json");
        if (!File.Exists(pending)) return;
        var entry = ReadEntry(pending);
        CheckRevision(entry.Revision);
        var current = File.Exists(file) ? Hash(File.ReadAllBytes(file)) : "missing";
        if (current == entry.Revision)
        {
            var bytes = File.ReadAllBytes(SnapshotPath(entry.Revision));
            if (Hash(bytes) != entry.Revision) throw new InvalidDataException("中断提交快照损坏，无法恢复历史。");
            WriteEntry(EntryPath(entry.Revision), entry);
        }
        else if (current != entry.ParentRevision)
            throw new InvalidDataException("中断提交后配置又被外部修改；保留日志和快照，请先检查 workspace.history/pending.json。");
        File.Delete(pending);
    }

    private string SnapshotPath(string revision) { CheckRevision(revision); return Path.Combine(history, revision + ".json"); }
    private string EntryPath(string revision) { CheckRevision(revision); return Path.Combine(history, revision + ".meta.json"); }
    private static void CheckRevision(string revision)
    {
        if (revision.Length != 64 || revision.Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("无效修订标识。");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static ConfigHistoryEntry ReadEntry(string path) => JsonSerializer.Deserialize(File.ReadAllBytes(path),
        LaunchLifecycleJsonContext.Default.ConfigHistoryEntry) ?? throw new InvalidDataException("历史元数据为空。");
    private static void WriteEntry(string path, ConfigHistoryEntry entry) => WriteAtomic(path,
        JsonSerializer.SerializeToUtf8Bytes(entry, LaunchLifecycleJsonContext.Default.ConfigHistoryEntry));
    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
