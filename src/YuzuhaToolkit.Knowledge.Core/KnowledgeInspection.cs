using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Knowledge;

/// <summary>原始来源根与用户明确选择的本机替代根；不改写历史来源。</summary>
public sealed record KnowledgeSourceMapping(string RecordedRoot, string LocalRoot);
/// <summary>独立检查项，error 阻止启用，warning 保留降级检索。</summary>
public sealed record KnowledgeInspectionItem(string Code, string Status, string Message, long? Count = null);
/// <summary>逐文件内容证据；legacy 索引不把路径/长度摘要当作内容指纹。</summary>
public sealed record KnowledgeSourceEvidence(string RecordedPath, string? ResolvedPath, string Status,
    string? RecordedHash, string? CurrentHash);
/// <summary>保留数据库身份与局部片段编号的分层检查报告。</summary>
public sealed record KnowledgeInspectionReport(string Database, string DatabaseSha256, bool CanEnable,
    string? SchemaVersion, long Sources, long Chunks, KnowledgeInspectionItem[] Items,
    KnowledgeSourceEvidence[] SourceEvidence, long[] SampleChunkIds);

/// <summary>在私有一致快照上做深检查，原库只读；拒绝活动 journal/WAL 和写占用。</summary>
public static class KnowledgeInspector
{
    /// <summary>取得一致 SQLite 备份；目标必须不存在，不复制 WAL/SHM 或活动写入。</summary>
    public static string Snapshot(string database, string destination)
    {
        database = Path.GetFullPath(database); destination = Path.GetFullPath(destination);
        PathRules.NoLinks(database); PathRules.NoLinks(destination);
        if (File.Exists(destination)) throw new IOException("快照目标已存在。");
        RejectActive(database);
        // Windows denies a concurrent writer for the complete fingerprint/backup interval.
        using var lease = new FileStream(database, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (lease.Length > 8L * 1024 * 1024 * 1024) throw new IOException("数据库超过 8 GiB 检查预算。");
        using var writerLease = File.Exists(database + ".lock")
            ? new FileStream(database + ".lock", FileMode.Open, FileAccess.Read, FileShare.None) : null;
        RejectActive(database);
        var hash = Convert.ToHexString(SHA256.HashData(lease)).ToLowerInvariant();
        try
        {
            using var source = Open(database, SqliteOpenMode.ReadOnly);
            using var target = Open(destination, SqliteOpenMode.ReadWriteCreate);
            source.BackupDatabase(target);
            RejectActive(database);
            return hash;
        }
        catch { if (File.Exists(destination)) File.Delete(destination); throw; }
    }

    /// <summary>只读原库并在临时副本上检查 FTS 自校验；损坏库返回明确失败项。</summary>
    public static KnowledgeInspectionReport Inspect(string database, KnowledgeSourceMapping[]? mappings = null, string? manifest = null,
        CancellationToken cancellationToken = default)
    {
        database = Path.GetFullPath(database);
        var temp = Path.Combine(Path.GetTempPath(), "YuzuhaKnowledgeCheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = Path.Combine(temp, "check.sqlite3");
            var hash = Snapshot(database, snapshot);
            return WithManifest(InspectSnapshot(snapshot, database, hash, mappings ?? [], cancellationToken), manifest);
        }
        catch (Exception error) when (error is IOException or SqliteException or UnauthorizedAccessException)
        {
            return new(database, "", false, null, 0, 0,
                [new("snapshot", "error", Message(error))], [], []);
        }
        finally { Directory.Delete(temp, true); }
    }

    /// <summary>清单单独报告；缺省可降级，未来版本及计数矛盾阻止启用。</summary>
    public static KnowledgeInspectionReport WithManifest(KnowledgeInspectionReport report, string? manifest)
    {
        KnowledgeInspectionItem item;
        if (manifest is null) item = new("manifest", "warning", "未提供来源清单；数据库中的历史来源仍保留。");
        else
        {
            try
            {
                PathRules.NoLinks(manifest);
                using var file = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (file.Length > 16 * 1024 * 1024) throw new InvalidDataException("manifest 超过 16 MiB 预算。");
                using var document = JsonDocument.Parse(file); var root = document.RootElement;
                if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1)
                    throw new InvalidDataException("来源清单 schemaVersion 不受支持。");
                foreach (var (name, expected) in new[] { ("totalSources", report.Sources), ("totalChunks", report.Chunks), ("ftsRows", report.Chunks) })
                    if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || value.GetInt64() != expected)
                        throw new InvalidDataException("来源清单计数与数据库不一致：" + name);
                item = new("manifest", "ok", "已核对清单版本及来源/片段/FTS 计数。");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or FormatException or OverflowException)
            { item = new("manifest", "error", Message(error)); }
        }
        return report with { CanEnable = report.CanEnable && item.Status != "error", Items = [.. report.Items, item] };
    }

    /// <summary>仅对调用方的私有暂存快照运行检查；允许 FTS integrity-check 写指令。</summary>
    public static KnowledgeInspectionReport InspectSnapshot(string snapshot, string recordedDatabase,
        string databaseHash, KnowledgeSourceMapping[] mappings, CancellationToken cancellationToken = default)
    {
        var items = new List<KnowledgeInspectionItem>(); var sources = new List<KnowledgeSourceEvidence>();
        string? version = null; long sourceCount = 0, chunks = 0; long[] samples = [];
        SqliteConnection? connection = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection = Open(snapshot, SqliteOpenMode.ReadWrite);
            Check("sqlite.integrity", () => {
                using var command = connection.CreateCommand(); command.CommandText = "PRAGMA integrity_check";
                using var reader = command.ExecuteReader(); var errors = new List<string>();
                while (reader.Read()) if (reader.GetString(0) != "ok") errors.Add(reader.GetString(0));
                if (errors.Count > 0) throw new InvalidDataException(string.Join("; ", errors.Take(10)));
                return 0;
            });
            if (items.Any(i => i.Status == "error")) return Report();
            Check("schema.tables", () => {
                foreach (var (table, columns) in new[] {
                    ("meta", "key,value"), ("sources", "id,source_type,source_path,relative_path,module,content_hash"),
                    ("semantic_chunks", "id,source_id,content,chunk_type,symbol,title"),
                    ("call_refs", "id,chunk_id,target"), ("chunks_fts", "content,relative_path,module,chunk_type,symbol,title") })
                {
                    using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT " + columns + " FROM " + table + " LIMIT 0"; cmd.ExecuteNonQuery();
                }
                return 5;
            });
            if (items.Any(i => i.Status == "error")) return Report();
            version = Text("SELECT value FROM meta WHERE key='schema_version'");
            items.Add(new("schema.version", version == "1" ? "ok" : "error", "schema_version=" + version));
            if (version != "1") return Report();
            sourceCount = Number("SELECT COUNT(*) FROM sources"); chunks = Number("SELECT COUNT(*) FROM semantic_chunks");
            Check("rows.relationships", () => {
                var count = Number("SELECT COUNT(*) FROM semantic_chunks c LEFT JOIN sources s ON c.source_id=s.id WHERE s.id IS NULL")
                    + Number("SELECT COUNT(*) FROM call_refs r LEFT JOIN semantic_chunks c ON r.chunk_id=c.id WHERE c.id IS NULL");
                if (count > 0) throw new InvalidDataException("存在孤立来源或调用引用：" + count);
                return chunks;
            });
            Check("fts.rows", () => {
                var count = Number("SELECT COUNT(*) FROM chunks_fts");
                if (count != chunks) throw new InvalidDataException("FTS/片段行数不一致。");
                var mismatch = Number("SELECT COUNT(*) FROM semantic_chunks c JOIN sources s ON s.id=c.source_id LEFT JOIN chunks_fts f ON f.rowid=c.id WHERE f.rowid IS NULL OR f.content IS NOT c.content OR f.relative_path IS NOT s.relative_path OR COALESCE(f.module,'') != COALESCE(s.module,'') OR COALESCE(f.chunk_type,'') != COALESCE(c.chunk_type,'') OR COALESCE(f.symbol,'') != COALESCE(c.symbol,'') OR COALESCE(f.title,'') != COALESCE(c.title,'')");
                if (mismatch > 0) throw new InvalidDataException("FTS 与来源/片段正文不一致：" + mismatch);
                return count;
            });
            Check("fts.integrity", () => {
                using var cmd = connection.CreateCommand(); cmd.CommandText = "INSERT INTO chunks_fts(chunks_fts) VALUES ('integrity-check')"; cmd.ExecuteNonQuery(); return chunks;
            });
            Check("fts.query", () => {
                using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM chunks_fts WHERE chunks_fts MATCH $query";
                cmd.Parameters.AddWithValue("$query", "\"yuzuha\""); return Convert.ToInt64(cmd.ExecuteScalar());
            });
            Check("fts.samples", () =>
            {
                var candidates = new List<(long Id, string Content)>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT id,content FROM semantic_chunks ORDER BY id LIMIT 20";
                    using var reader = cmd.ExecuteReader(); while (reader.Read()) candidates.Add((reader.GetInt64(0), reader.GetString(1)));
                }
                var ids = new List<long>();
                foreach (var candidate in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var word = Regex.Match(candidate.Content, @"[\p{L}\p{N}]{2,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Value;
                    if (word.Length == 0) continue;
                    using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT rowid FROM chunks_fts WHERE chunks_fts MATCH $query AND rowid=$id";
                    cmd.Parameters.AddWithValue("$query", "\"" + word + "\""); cmd.Parameters.AddWithValue("$id", candidate.Id);
                    if (cmd.ExecuteScalar() is null) throw new InvalidDataException("抽样正文词无法从对应片段检索：" + candidate.Id);
                    ids.Add(candidate.Id); if (ids.Count == 5) break;
                }
                samples = ids.ToArray(); return samples.Length;
            });
            if (chunks > 0 && samples.Length == 0) items.Add(new("fts.samples.coverage", "warning", "片段没有可抽样的文字 token；未声称抽样命中通过。"));
            var contentHashes = Text("SELECT value FROM meta WHERE key='source_hash_algorithm'") == "sha256-bytes-v1";
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT source_path,content_hash FROM sources ORDER BY id";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (sources.Count >= 200000) throw new InvalidDataException("来源超过 200000 条检查预算。");
                    var recorded = reader.GetString(0); var expected = reader.IsDBNull(1) ? null : reader.GetString(1);
                    if (!Path.IsPathFullyQualified(recorded) || recorded.StartsWith(@"\\", StringComparison.Ordinal))
                    { sources.Add(new(recorded, null, "not-local-file", expected, null)); continue; }
                    var local = Resolve(recorded, mappings);
                    try
                    {
                        PathRules.NoLinks(local);
                        if (!File.Exists(local)) { sources.Add(new(recorded, null, "missing", expected, null)); continue; }
                        using var file = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var actual = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
                        sources.Add(new(recorded, local, !contentHashes ? "unverified-legacy-fingerprint" :
                            string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) ? "current" : "changed", expected, actual));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    { sources.Add(new(recorded, local, "unavailable", expected, null)); }
                }
            }
            var degraded = sources.Count(s => s.Status != "current" && s.Status != "not-local-file");
            items.Add(new("sources.content", degraded == 0 ? "ok" : "warning",
                contentHashes ? "逐文件 SHA256；缺源或变化不代表 SQLite 无法检索。" : "旧库未记录内容指纹，不能证明新鲜。", degraded));
        }
        catch (Exception error) when (error is SqliteException or InvalidDataException or IOException or ArgumentException)
        { items.Add(new("inspection", "error", Message(error))); }
        finally { connection?.Dispose(); }
        return Report();

        KnowledgeInspectionReport Report() => new(recordedDatabase, databaseHash,
            items.Count > 0 && items.All(i => i.Status != "error"), version, sourceCount, chunks, items.ToArray(), sources.ToArray(), samples);
        void Check(string code, Func<long> action)
        {
            try { items.Add(new(code, "ok", "检查通过。", action())); }
            catch (Exception error) when (error is SqliteException or InvalidDataException)
            { items.Add(new(code, "error", Message(error))); }
        }
        string Text(string sql) { using var cmd = connection!.CreateCommand(); cmd.CommandText = sql; return cmd.ExecuteScalar()?.ToString() ?? ""; }
        long Number(string sql) => long.Parse(Text(sql), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>只允许明确的本机根映射，最长前缀优先并保持相对路径。</summary>
    public static string Resolve(string recordedPath, KnowledgeSourceMapping[] mappings)
    {
        foreach (var map in mappings.OrderByDescending(m => m.RecordedRoot.Length))
        {
            var root = Path.GetFullPath(map.RecordedRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (recordedPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return PathRules.Inside(Path.GetFullPath(map.LocalRoot), Path.GetRelativePath(root, recordedPath));
        }
        return recordedPath;
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 1 }.ToString());
        try { connection.Open(); return connection; } catch { connection.Dispose(); throw; }
    }
    private static void RejectActive(string file)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            if (File.Exists(file + suffix)) throw new IOException("数据库有活动或待恢复的 " + suffix + "，请停止写入后重新计划。");
    }
    private static string Message(Exception error) => string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
}
