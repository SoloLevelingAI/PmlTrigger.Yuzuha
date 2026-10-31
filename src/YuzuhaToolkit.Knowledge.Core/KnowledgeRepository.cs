using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using YuzuhaToolkit.Knowledge.Chunking;
using YuzuhaToolkit.Knowledge.Search;

namespace YuzuhaToolkit.Knowledge;

/// <summary>
///     Local knowledge database lifecycle: build (rebuild) the SQLite/FTS5
///     database from PMLLIB / PMLUI / WebHelp directories the user owns,
///     search it read-only, and validate databases copied from elsewhere.
///     Databases are never created or overwritten silently.
/// </summary>
public sealed partial class KnowledgeRepository
{
    private const string GeneratorName = "YuzuhaToolkit.Knowledge";
    private const int SchemaVersion = 1;

    /// <summary>经安装布局或显式环境覆盖解析的本机知识库目录。</summary>
    public string DefaultDirectory { get; }

    /// <summary>解析本机知识库默认目录，不在构造时建立或覆盖数据库。</summary>
    public KnowledgeRepository()
    {
        DefaultDirectory = ResolveDefaultDirectory();
    }
    /// <summary>使用调用方明确选择的知识库目录，不创建或重建数据库。</summary>
    /// <param name="directory">现有安装目录或用户在 GUI 中明确选择的本机目录。</param>
    public KnowledgeRepository(string directory) => DefaultDirectory = Path.GetFullPath(directory);

    private static string ResolveDefaultDirectory() => YuzuhaToolkit.Workspace.UserDataPaths.KnowledgeDirectory();

    /// <summary>从显式路径或经校验的数据库名称解析本机文件位置，不创建文件。</summary>
    /// <param name="dbName">经校验的不带扩展名的数据库名称。</param>
    /// <param name="dbPath">显式数据库路径；提供时优先于名称解析。</param>
    public string ResolveDatabasePath(string? dbName, string? dbPath)
    {
        if (!string.IsNullOrWhiteSpace(dbPath))
            return Path.GetFullPath(dbPath.Trim());

        var name = string.IsNullOrWhiteSpace(dbName) ? "pml-knowledge" : dbName.Trim();
        ValidateDbName(name);
        return Path.Combine(DefaultDirectory, name + ".sqlite3");
    }

    private static void ValidateDbName(string name)
    {
        if (!Regex.IsMatch(name, @"^[\w][\w.\-]*$"))
            throw new KnowledgeException(
                $"dbName '{name}' may contain only letters, digits, dot, dash, " +
                "and underscore.");
    }

    /// <summary>列出本机知识库并按选项比较来源新鲜度，不自动构建索引。</summary>
    /// <param name="checkFreshness">是否将清单统计与当前源目录比较。</param>
    public IReadOnlyList<KnowledgeDbSummary> List(bool checkFreshness)
    {
        List<KnowledgeDbSummary> summaries = new();
        if (!Directory.Exists(DefaultDirectory))
            return summaries;

        foreach (var database in Directory
                     .EnumerateFiles(DefaultDirectory, "*.sqlite3")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(
                Path.GetDirectoryName(database)!,
                Path.GetFileNameWithoutExtension(database) + ".manifest.json");
            try
            {
                summaries.Add(Summarize(database, manifest, checkFreshness));
            }
            catch (KnowledgeException)
            {
                // Unreadable files are reported via check, not by failing the
                // whole listing.
            }
        }

        return summaries;
    }

    /// <summary>读取数据库计数和清单，按选项检查来源目录是否发生变化。</summary>
    /// <param name="database">所操作的本机 SQLite 文件路径。</param>
    /// <param name="manifestPath">对应数据库的来源清单 JSON 路径。</param>
    /// <param name="checkFreshness">是否将清单统计与当前源目录比较。</param>
    public KnowledgeDbSummary Summarize(
        string database,
        string manifestPath,
        bool checkFreshness)
    {
        using var connection = OpenReadOnly(database);
        var (schemaVersion, generator, builtAtUtc, roots) = ReadMeta(connection);
        var (sources, chunks, callRefs, ftsRows) = ReadCounts(connection);
        KnowledgeFreshnessReport? freshness = null;
        if (checkFreshness)
            freshness = CheckFreshness(roots, manifestPath);
        return new KnowledgeDbSummary(
            Path.GetFileNameWithoutExtension(database),
            database,
            File.Exists(manifestPath) ? manifestPath : null,
            schemaVersion,
            generator,
            builtAtUtc,
            sources,
            chunks,
            callRefs,
            ftsRows,
            roots,
            freshness);
    }

    /// <summary>只读检查 SQLite 完整性、Yuzuha 表结构、版本和来源清单。</summary>
    /// <param name="dbPath">显式数据库路径；提供时优先于名称解析。</param>
    public KnowledgeCheckResult Check(string dbPath)
    {
        dbPath = Path.GetFullPath(dbPath);
        if (!File.Exists(dbPath))
            return new KnowledgeCheckResult(
                false,
                dbPath,
                false,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                0,
                Array.Empty<KnowledgeRootRecord>(),
                "Database not found. If it was copied from someone else, " +
                "verify the file extension is .sqlite3 and the path is exact.");

        using var connection = OpenReadOnly(dbPath);
        var integrity = Scalar(connection, "PRAGMA integrity_check");
        var schemaOk = IsSchemaPresent(connection);
        string? schemaVersion = null;
        string? generator = null;
        string? builtAtUtc = null;
        List<KnowledgeRootRecord> roots = new();
        if (schemaOk)
        {
            var (version, generatorValue, builtAt, metaRoots) = ReadMeta(connection);
            schemaVersion = version;
            generator = generatorValue;
            builtAtUtc = builtAt;
            roots = metaRoots;
        }

        var (sources, chunks, callRefs, ftsRows) =
            schemaOk ? ReadCounts(connection) : (0L, 0L, 0L, 0L);
        return new KnowledgeCheckResult(
            integrity == "ok" && schemaOk && schemaVersion == SchemaVersion.ToString(CultureInfo.InvariantCulture),
            dbPath,
            true,
            integrity,
            schemaOk,
            schemaVersion,
            generator,
            builtAtUtc,
            sources,
            chunks,
            callRefs,
            ftsRows,
            roots,
            schemaOk
                ? schemaVersion == SchemaVersion.ToString(CultureInfo.InvariantCulture) ? null : "Unsupported schema version: " + schemaVersion
                : "The tables of a Yuzuha knowledge database are missing; " +
                  "this file is not a compatible knowledge base.");
    }

    private KnowledgeBuildResult BuildCore(
        string? pmlLibRoot,
        string? pmlUiRoot,
        string? webHelpRoot,
        string dbName,
        string? dbDir,
        bool rebuild,
        long maxFilesPerRoot, CancellationToken cancellationToken = default)
    {
        var requested = new List<(string Kind, string? Path)>
        {
            ("pmllib", pmlLibRoot),
            ("pmlui", pmlUiRoot),
            ("webhelp", webHelpRoot)
        };
        var roots = requested
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .Select(entry => (entry.Kind, Path: Path.GetFullPath(entry.Path!.Trim())))
            .ToList();
        if (roots.Count == 0)
            throw new KnowledgeException(
                "At least one source root is required: pmlLibRoot, pmlUiRoot, " +
                "or webHelpRoot.");
        foreach (var (_, path) in roots)
            if (!Directory.Exists(path))
                throw new KnowledgeException($"Source root does not exist: {path}");

        var directory = string.IsNullOrWhiteSpace(dbDir)
            ? DefaultDirectory
            : Path.GetFullPath(dbDir.Trim());
        Directory.CreateDirectory(directory);
        var name = string.IsNullOrWhiteSpace(dbName) ? "pml-knowledge" : dbName.Trim();
        ValidateDbName(name);
        var database = Path.Combine(directory, name + ".sqlite3");
        var manifest = Path.Combine(directory, name + ".manifest.json");

        if (File.Exists(database) && !rebuild)
            throw new KnowledgeException(
                $"Database already exists: {database}. Rebuilding erases and " +
                "replaces it, so ask the user to confirm first, then call " +
                "again with rebuild=true. To keep both, choose another dbName.");

        var stopwatch = Stopwatch.StartNew();
        foreach (var stale in new[]
                 {
                     database,
                     database + "-wal",
                     database + "-shm",
                     manifest
                 })
            if (File.Exists(stale))
                File.Delete(stale);

        List<KnowledgeRootStats> rootStats = new();
        long pmlFiles = 0;
        long pmlChunks = 0;
        long htmlFiles = 0;
        long htmlChunks = 0;
        long htmlDuplicates = 0;
        long skippedBinaries = 0;
        List<string> warnings = new();

        using (var connection = OpenReadWrite(database))
        {
            Execute(connection, "PRAGMA journal_mode=WAL");
            Execute(connection, "PRAGMA synchronous=NORMAL");
            Execute(connection, CreateSchemaSql);
            var rootsJson = JsonSerializer.Serialize(
                roots.Select(entry => new KnowledgeRootRecord(entry.Kind, entry.Path))
                    .ToList(),
                KnowledgeResponseJsonContext.Default.ListKnowledgeRootRecord);
            Execute(
                connection,
                "INSERT INTO meta(key, value) VALUES ('schema_version', '" +
                SchemaVersion.ToString(CultureInfo.InvariantCulture) + "');" +
                "INSERT INTO meta(key, value) VALUES ('generator', '" +
                GeneratorName + "');" +
                "INSERT INTO meta(key, value) VALUES ('source_hash_algorithm', 'sha256-bytes-v1');" +
                "INSERT INTO meta(key, value) VALUES ('built_at_utc', '" +
                DateTime.UtcNow.ToString("o") + "');" +
                "INSERT INTO meta(key, value) VALUES ('roots_json', '" +
                rootsJson.Replace("'", "''") + "');");

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var (kind, path) in roots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var files = EnumerateSourceFiles(path, kind).ToList();
                    rootStats.Add(CollectStats(kind, path, files));
                    if (maxFilesPerRoot > 0 && files.Count > maxFilesPerRoot)
                        warnings.Add(
                            $"Root {path} contains more than maxFilesPerRoot " +
                            $"({maxFilesPerRoot}) files; the extra files were " +
                            "not indexed.");

                    HashSet<string> globalHtmlHashes = new(StringComparer.Ordinal);
                    var processed = 0;
                    foreach (var file in files)
                    {
                        processed++;
                        if (maxFilesPerRoot > 0 && processed > maxFilesPerRoot)
                            break;

                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            // Keep the parsed bytes and their recorded content fingerprint in the same read lease.
                            using var sourceLease = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var text = ReadTextBestEffort(file, ref skippedBinaries);
                            if (text is null)
                                continue;
                            var relative = Path.GetRelativePath(path, file);

                            if (kind == "webhelp")
                            {
                                var module = DeriveWebHelpModule(relative);
                                var parsed = HtmlChunker.Chunk(text, relative, module);
                                List<HtmlChunk> filtered = new();
                                foreach (var chunk in parsed.Chunks)
                                {
                                    var digest = HtmlChunker.Sha256Hex(chunk.Content);
                                    if (globalHtmlHashes.Add(digest))
                                        filtered.Add(chunk);
                                    else
                                        htmlDuplicates++;
                                }

                                if (filtered.Count > 0)
                                {
                                    Dictionary<string, string> metadata = new()
                                    {
                                        ["title"] = parsed.Title
                                    };
                                    var sourceId = InsertSource(
                                        connection,
                                        transaction,
                                        "webhelp",
                                        file,
                                        relative,
                                        module,
                                        "webworks_html",
                                        metadata,
                                        "medium");
                                    foreach (var chunk in filtered)
                                        InsertChunk(
                                            connection,
                                            transaction,
                                            sourceId,
                                            chunk.ChunkType,
                                            chunk.Symbol,
                                            string.Empty,
                                            chunk.StartLine,
                                            null,
                                            chunk.Content,
                                            relative,
                                            module,
                                            Array.Empty<string>());
                                    htmlFiles++;
                                    htmlChunks += filtered.Count;
                                }
                            }
                            else
                            {
                                var parser = ClassifyParser(file);
                                var parsed = PmlChunker.Chunk(text, relative, parser);
                                var module = relative
                                    .Split(Path.DirectorySeparatorChar)[0];
                                var sourceId = InsertSource(
                                    connection,
                                    transaction,
                                    "pml",
                                    file,
                                    relative,
                                    module,
                                    parser,
                                    parsed.Metadata,
                                    "high");
                                foreach (var chunk in parsed.Chunks)
                                {
                                    InsertChunk(
                                        connection,
                                        transaction,
                                        sourceId,
                                        chunk.ChunkType,
                                        chunk.Symbol,
                                        string.Empty,
                                        chunk.StartLine,
                                        chunk.EndLine,
                                        chunk.Content,
                                        relative,
                                        module,
                                        chunk.CallTargets);
                                }

                                pmlFiles++;
                                pmlChunks += parsed.Chunks.Count;
                            }
                        }
                        catch (IOException)
                        {
                            warnings.Add($"Unreadable file skipped: {file}");
                        }
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        long totalSources;
        long totalChunks;
        long callRefs;
        long ftsRows;
        using (var connection = OpenReadWrite(database))
        {
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
            Execute(connection, "PRAGMA journal_mode=DELETE");
            totalSources = ScalarLong(connection, "SELECT COUNT(*) FROM sources");
            totalChunks = ScalarLong(connection, "SELECT COUNT(*) FROM semantic_chunks");
            callRefs = ScalarLong(connection, "SELECT COUNT(*) FROM call_refs");
            ftsRows = ScalarLong(connection, "SELECT COUNT(*) FROM chunks_fts");
        }

        WriteManifest(
            manifest,
            database,
            rootStats,
            pmlFiles,
            pmlChunks,
            htmlFiles,
            htmlChunks,
            htmlDuplicates,
            skippedBinaries,
            totalSources,
            totalChunks,
            callRefs,
            ftsRows);

        stopwatch.Stop();
        return new KnowledgeBuildResult(
            true,
            database,
            manifest,
            pmlFiles,
            pmlChunks,
            htmlFiles,
            htmlChunks,
            htmlDuplicates,
            skippedBinaries,
            totalSources,
            totalChunks,
            callRefs,
            Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
            warnings);
    }

    /// <summary>规划 FTS5 变体并执行只读检索，以加权倒数排名融合命中。</summary>
    /// <param name="query">PML、英文或中文的任务描述或检索文本。</param>
    /// <param name="dbName">经校验的不带扩展名的数据库名称。</param>
    /// <param name="dbPath">显式数据库路径；提供时优先于名称解析。</param>
    /// <param name="topK">返回命中总数上限，内部限制到工具支持范围。</param>
    /// <param name="sourceType">pml 或 webhelp 等来源类型过滤或标识。</param>
    /// <param name="module">PML 模块或资料目录范围。</param>
    /// <param name="chunkType">函数、方法、窗体或 HTML 章节等片段类别。</param>
    public KnowledgeSearchResult Search(
        string query,
        string? dbName,
        string? dbPath,
        int topK,
        string? sourceType,
        string? module,
        string? chunkType)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new KnowledgeException("query must not be empty.");
        var database = ResolveDatabasePath(dbName, dbPath);
        if (!File.Exists(database))
            throw new KnowledgeException(
                $"Knowledge database not found: {database}. Run " +
                "list_knowledge_databases to see what exists; ask the user whether " +
                "to build one now, or copy and validate one with " +
                "check_knowledge_database.");

        topK = Math.Clamp(topK, 1, 50);
        var plan = QueryPlanner.Plan(query);
        var createIntent = QueryPlanner.CreateIntentRegex().IsMatch(query);
        var docIntent = QueryPlanner.DocIntentRegex().IsMatch(query);
        var codeIntent = QueryPlanner.CodeIntentRegex().IsMatch(query);

        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(sourceType))
            filters.Add("s.source_type = $sourceType");
        if (!string.IsNullOrWhiteSpace(module))
            filters.Add("lower(s.module) = lower($module)");
        if (!string.IsNullOrWhiteSpace(chunkType))
            filters.Add("lower(c.chunk_type) = lower($chunkType)");
        var whereTail = filters.Count > 0 ? " AND " + string.Join(" AND ", filters) : "";
        var sql =
            "SELECT c.id, c.content, c.chunk_type, c.symbol, c.start_line, " +
            "c.end_line, s.source_type, s.module, s.relative_path " +
            "FROM chunks_fts " +
            "JOIN semantic_chunks c ON c.id = chunks_fts.rowid " +
            "JOIN sources s ON s.id = c.source_id " +
            "WHERE chunks_fts MATCH $match" + whereTail + " " +
            "ORDER BY bm25(chunks_fts) LIMIT $limit";

        var candidateLimit = Math.Max(30, topK * 6);
        Dictionary<long, double> scores = new();
        Dictionary<long, List<string>> reasons = new();
        Dictionary<long, SearchRow> rowsById = new();

        using (var connection = OpenReadOnly(database))
        {
            foreach (var variant in plan)
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("$match", variant.FtsExpression);
                command.Parameters.AddWithValue("$limit", candidateLimit);
                if (filters.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(sourceType))
                        command.Parameters.AddWithValue("$sourceType", sourceType);
                    if (!string.IsNullOrWhiteSpace(module))
                        command.Parameters.AddWithValue("$module", module);
                    if (!string.IsNullOrWhiteSpace(chunkType))
                        command.Parameters.AddWithValue("$chunkType", chunkType);
                }

                try
                {
                    using var reader = command.ExecuteReader();
                    var position = 0;
                    while (reader.Read())
                    {
                        position++;
                        var row = new SearchRow(
                            reader.GetInt64(0),
                            reader.GetString(1),
                            reader.IsDBNull(2) ? "" : reader.GetString(2),
                            reader.IsDBNull(3) ? "" : reader.GetString(3),
                            reader.IsDBNull(4) ? null : reader.GetInt64(4),
                            reader.IsDBNull(5) ? null : reader.GetInt64(5),
                            reader.IsDBNull(6) ? "" : reader.GetString(6),
                            reader.IsDBNull(7) ? "" : reader.GetString(7),
                            reader.IsDBNull(8) ? "" : reader.GetString(8));
                        scores[row.Id] = scores.GetValueOrDefault(row.Id) +
                                         variant.Weight / (20.0 + position);
                        if (!reasons.TryGetValue(row.Id, out var reasonList))
                            reasons[row.Id] = reasonList = new List<string>();
                        reasonList.Add(variant.Reason);
                        rowsById[row.Id] = row;
                    }
                }
                catch (SqliteException)
                {
                    // An FTS syntax error in one variant must not kill the
                    // other variants (parity with the Python prototype).
                }
            }

            foreach (var id in rowsById.Keys)
            {
                var row = rowsById[id];
                if (createIntent && row.SourceType == "pml")
                    scores[id] *= 1.18;
                if (createIntent &&
                    row.ChunkType is "pml_function" or "pml_method" or "pml_macro_section")
                    scores[id] *= 1.12;
                if (docIntent && row.SourceType == "webhelp")
                    scores[id] *= 1.15;
                if (codeIntent && row.SourceType == "pml")
                    scores[id] *= 1.15;
                if (codeIntent &&
                    row.ChunkType is "pml_form" or "pml_function" or "pml_method" or
                        "pml_macro_section")
                    scores[id] *= 1.08;
                if (row.Symbol.Length > 0)
                    scores[id] *= 1.03;
            }
        }

        var ranked = scores.Keys
            .OrderByDescending(id => scores[id])
            .ThenBy(id => id)
            .ToList();

        // Cover each requested creation operation at least once before
        // filling the remaining slots.
        var requiredReasons = QueryPlanner.Dedupe(plan
                .Where(variant =>
                    variant.Reason.StartsWith("创建模式:", StringComparison.Ordinal) ||
                    variant.Reason.Contains("候选:", StringComparison.Ordinal) ||
                    variant.Reason.StartsWith("完整轮廓:", StringComparison.Ordinal))
                .Select(variant => variant.Reason))
            .ToList();
        List<long> ordered = new();
        if (ranked.Count > 0)
            ordered.Add(ranked[0]);
        foreach (var required in requiredReasons)
        {
            if (ordered.Count >= topK ||
                ordered.Any(id => reasons[id].Contains(required)))
                continue;
            var candidate = ranked.FirstOrDefault(
                id => !ordered.Contains(id) && reasons[id].Contains(required));
            if (candidate != 0)
                ordered.Add(candidate);
        }

        ordered.AddRange(ranked.Where(id => !ordered.Contains(id)));
        ordered = ordered.Take(topK).ToList();

        Dictionary<long, List<string>> callMap = new();
        if (ordered.Count > 0)
            using (var connection = OpenReadOnly(database))
            {
                var marks = string.Join(
                    ",",
                    ordered.Select((_, index) => $"$id{index}"));
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT chunk_id, target FROM call_refs " +
                    $"WHERE chunk_id IN ({marks}) ORDER BY chunk_id, target";
                for (var index = 0; index < ordered.Count; index++)
                    command.Parameters.AddWithValue($"$id{index}", ordered[index]);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt64(0);
                    if (!callMap.TryGetValue(id, out var targets))
                        callMap[id] = targets = new List<string>();
                    targets.Add(reader.GetString(1));
                }
            }

        var terms = QueryPlanner.Dedupe(plan
                .Select(variant => variant.Reason.Contains(':')
                    ? variant.Reason[(variant.Reason.IndexOf(':') + 1)..]
                    : variant.Reason))
            .ToList();
        List<KnowledgeSearchHit> hits = new();
        var rank = 0;
        foreach (var id in ordered)
        {
            rank++;
            var row = rowsById[id];
            hits.Add(new KnowledgeSearchHit(
                rank,
                id,
                Math.Round(scores[id], 6),
                row.SourceType,
                row.Module,
                row.ChunkType,
                row.Symbol,
                row.RelativePath,
                row.StartLine is null ? null : (int?)checked((int)row.StartLine.Value),
                row.EndLine is null ? null : (int?)checked((int)row.EndLine.Value),
                QueryPlanner.Dedupe(reasons[id]),
                callMap.GetValueOrDefault(id) ?? (IReadOnlyList<string>)Array.Empty<string>(),
                QueryPlanner.CompactExcerpt(row.Content, terms)));
        }

        return new KnowledgeSearchResult(
            true,
            query,
            database,
            hits,
            plan.Select(variant => new QueryVariantDto(
                    variant.FtsExpression,
                    variant.Weight,
                    variant.Reason))
                .ToList(),
            null);
    }

    /// <summary>按数据库内片段编号读取完整正文、来源坐标与已记录路径。</summary>
    /// <param name="chunkId">仅在指定数据库内部唯一的片段编号。</param>
    /// <param name="dbName">经校验的不带扩展名的数据库名称。</param>
    /// <param name="dbPath">显式数据库路径；提供时优先于名称解析。</param>
    public KnowledgeChunkDetail GetChunk(long chunkId, string? dbName, string? dbPath)
    {
        var database = ResolveDatabasePath(dbName, dbPath);
        if (!File.Exists(database))
            throw new KnowledgeException($"Knowledge database not found: {database}");

        using var connection = OpenReadOnly(database);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT c.content, c.chunk_type, c.symbol, c.title, c.start_line, " +
            "c.end_line, s.source_type, s.module, s.relative_path, s.source_path " +
            "FROM semantic_chunks c JOIN sources s ON s.id = c.source_id " +
            "WHERE c.id = $id";
        command.Parameters.AddWithValue("$id", chunkId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new KnowledgeException($"Chunk {chunkId} was not found in {database}.");

        var content = reader.GetString(0);
        var chunkType = reader.IsDBNull(1) ? "" : reader.GetString(1);
        var symbol = reader.IsDBNull(2) ? "" : reader.GetString(2);
        var title = reader.IsDBNull(3) ? "" : reader.GetString(3);
        var startLine = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4);
        var endLine = reader.IsDBNull(5) ? null : (long?)reader.GetInt64(5);
        var sourceType = reader.IsDBNull(6) ? "" : reader.GetString(6);
        var module = reader.IsDBNull(7) ? "" : reader.GetString(7);
        var relativePath = reader.IsDBNull(8) ? "" : reader.GetString(8);
        var recordedPath = reader.IsDBNull(9) ? "" : reader.GetString(9);

        List<string> callTargets = new();
        using (var refs = connection.CreateCommand())
        {
            refs.CommandText =
                "SELECT target FROM call_refs WHERE chunk_id = $id ORDER BY target";
            refs.Parameters.AddWithValue("$id", chunkId);
            using var refReader = refs.ExecuteReader();
            while (refReader.Read())
                callTargets.Add(refReader.GetString(0));
        }

        string? resolvedPath = null;
        var (_, _, _, roots) = ReadMeta(connection);
        foreach (var root in roots)
        {
            var candidate = Path.Combine(root.Path, relativePath);
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                break;
            }
        }

        if (resolvedPath is null && File.Exists(recordedPath))
            resolvedPath = recordedPath;

        return new KnowledgeChunkDetail(
            true,
            chunkId,
            content,
            chunkType,
            symbol,
            title,
            startLine is null ? null : (int?)checked((int)startLine.Value),
            endLine is null ? null : (int?)checked((int)endLine.Value),
            sourceType,
            module,
            relativePath,
            recordedPath,
            resolvedPath,
            callTargets,
            resolvedPath is null
                ? "The source file is not present on this machine (typical " +
                  "for a database copied from someone else); the content " +
                  "above is self-contained."
                : null);
    }

    private SqliteConnection OpenReadOnly(string database)
    {
        if (!File.Exists(database))
            throw new KnowledgeException($"Knowledge database not found: {database}");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private SqliteConnection OpenReadWrite(string database)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string text)
    {
        using var command = connection.CreateCommand();
        command.CommandText = text;
        command.ExecuteNonQuery();
    }

    private static string Scalar(SqliteConnection connection, string text)
    {
        using var command = connection.CreateCommand();
        command.CommandText = text;
        return command.ExecuteScalar()?.ToString() ?? string.Empty;
    }

    private static long ScalarLong(SqliteConnection connection, string text)
    {
        using var command = connection.CreateCommand();
        command.CommandText = text;
        var value = command.ExecuteScalar();
        return value is null ? 0 : Convert.ToInt64(value);
    }

    private static bool IsSchemaPresent(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
            "('sources','semantic_chunks','call_refs','chunks_fts')";
        return ScalarLong(connection, command.CommandText) == 4;
    }

    private (string?, string?, string?, List<KnowledgeRootRecord>) ReadMeta(
        SqliteConnection connection)
    {
        if (!IsSchemaPresent(connection))
            throw new KnowledgeException(
                "This file is not a Yuzuha knowledge database (required " +
                "tables are missing).");

        Dictionary<string, string> meta = new(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT key, value FROM meta";
            try
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    meta[reader.GetString(0)] = reader.GetString(1);
            }
            catch (SqliteException)
            {
                // Databases built by the Python prototype have no meta table.
            }
        }

        List<KnowledgeRootRecord> roots = new();
        if (meta.TryGetValue("roots_json", out var rootsJson) &&
            !string.IsNullOrWhiteSpace(rootsJson))
        {
            try
            {
                using var document = JsonDocument.Parse(rootsJson);
                foreach (var element in document.RootElement.EnumerateArray())
                    roots.Add(new KnowledgeRootRecord(
                        element.GetProperty("kind").GetString() ?? "",
                        element.GetProperty("path").GetString() ?? ""));
            }
            catch (JsonException)
            {
            }
        }

        return (
            meta.GetValueOrDefault("schema_version"),
            meta.GetValueOrDefault("generator"),
            meta.GetValueOrDefault("built_at_utc"),
            roots);
    }

    private (long Sources, long Chunks, long CallRefs, long FtsRows) ReadCounts(
        SqliteConnection connection)
    {
        return (
            ScalarLong(connection, "SELECT COUNT(*) FROM sources"),
            ScalarLong(connection, "SELECT COUNT(*) FROM semantic_chunks"),
            ScalarLong(connection, "SELECT COUNT(*) FROM call_refs"),
            ScalarLong(connection, "SELECT COUNT(*) FROM chunks_fts"));
    }

    private KnowledgeFreshnessReport? CheckFreshness(
        IReadOnlyList<KnowledgeRootRecord> roots,
        string manifestPath)
    {
        if (roots.Count == 0)
            return null;

        Dictionary<string, long> recordedCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> recordedBytes = new(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(manifestPath))
        {
            try
            {
                using var document =
                    JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (document.RootElement.TryGetProperty("roots", out var manifestRoots))
                    foreach (var element in manifestRoots.EnumerateArray())
                    {
                        var path = element.TryGetProperty("path", out var pathElement)
                            ? pathElement.GetString() ?? ""
                            : "";
                        recordedCounts[path] =
                            element.TryGetProperty("fileCount", out var countElement)
                                ? countElement.GetInt64()
                                : -1;
                        recordedBytes[path] =
                            element.TryGetProperty("totalBytes", out var bytesElement)
                                ? bytesElement.GetInt64()
                                : -1;
                    }
            }
            catch (JsonException)
            {
            }
        }

        List<KnowledgeRootFreshness> details = new();
        var overall = "fresh";
        foreach (var root in roots)
        {
            string status;
            KnowledgeRootFreshness detail;
            if (!Directory.Exists(root.Path))
            {
                status = "root-missing";
                detail = new KnowledgeRootFreshness(
                    root.Kind,
                    root.Path,
                    status,
                    recordedCounts.GetValueOrDefault(root.Path),
                    null,
                    recordedBytes.GetValueOrDefault(root.Path),
                    null);
            }
            else
            {
                var current = CollectStats(
                    root.Kind,
                    root.Path,
                    EnumerateSourceFiles(root.Path, root.Kind).ToList());
                var recordedCount = recordedCounts.GetValueOrDefault(root.Path, -1);
                var recordedByteTotal = recordedBytes.GetValueOrDefault(root.Path, -1);
                if (recordedCount < 0)
                    status = "unknown";
                else if (current.FileCount == recordedCount &&
                         current.TotalBytes == recordedByteTotal)
                    status = "fresh";
                else
                    status = "changed";
                detail = new KnowledgeRootFreshness(
                    root.Kind,
                    root.Path,
                    status,
                    recordedCount < 0 ? null : recordedCount,
                    current.FileCount,
                    recordedByteTotal < 0 ? null : recordedByteTotal,
                    current.TotalBytes);
            }

            details.Add(detail);
            if (status != "fresh" && overall == "fresh")
                overall = status;
            else if (status == "changed" && overall == "root-missing")
                overall = "changed";
        }

        return new KnowledgeFreshnessReport(overall, details);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root, string kind) => EnumerateIndexFiles(root, kind);

    /// <summary>建库与冻结来源计划共用的文件选择规则；无法读取或链接目录均明确拒绝。</summary>
    public static IEnumerable<string> EnumerateIndexFiles(string root, string kind)
    {
        var pending = new Stack<string>(); pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
        {
            var directory = pending.Pop(); YuzuhaToolkit.Workspace.PathRules.NoLinks(directory);
            foreach (var child in Directory.EnumerateDirectories(directory))
            { YuzuhaToolkit.Workspace.PathRules.NoLinks(child); pending.Push(child); }
            foreach (var file in Directory.EnumerateFiles(directory))
            {
            YuzuhaToolkit.Workspace.PathRules.NoLinks(file);
            var name = Path.GetFileName(file);
            if (name.Equals("version.dat", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("pml.index", StringComparison.OrdinalIgnoreCase))
                continue;

            if (kind == "webhelp")
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is ".html" or ".htm")
                    yield return file;
                continue;
            }

            if (IsBinaryExtension(file))
                continue;

            yield return file;
            }
        }
    }

    private static bool IsBinaryExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is
            ".png" or ".gif" or ".jpg" or ".jpeg" or ".bmp" or ".ico" or
            ".zip" or ".cab" or ".msi" or ".jar" or ".7z" or ".gz" or
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or
            ".pdf" or ".chm" or ".hxs" or ".dll" or ".exe" or ".lib" or ".pdb" or
            ".js" or ".css" or ".woff" or ".woff2" or ".ttf" or ".eot" or
            ".mp4" or ".avi" or ".mov" or ".wav" or ".db" or ".sqlite3";
    }

    private static string ClassifyParser(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pmlfnc" => "pml_function",
            ".pmlobj" => "pml_object",
            ".pmlfrm" => "pml_form",
            _ => "legacy_pml"
        };
    }

    private static string? ReadTextBestEffort(string path, ref long skippedBinaries)
    {
        const int sampleSize = 8192;
        using (var stream = File.OpenRead(path))
        {
            var sample = new byte[Math.Min(sampleSize, stream.Length)];
            var read = stream.Read(sample, 0, sample.Length);
            if (sample.AsSpan(0, read).Contains((byte)0))
            {
                skippedBinaries++;
                return null;
            }

            var control = 0;
            for (var index = 0; index < read; index++)
            {
                var value = sample[index];
                if (value < 9 || (value > 13 && value < 32) || value == 127)
                    control++;
            }

            if (read > 0 && control / (double)read > 0.10)
            {
                skippedBinaries++;
                return null;
            }
        }

        // UTF-8 with replacement matches the Python prototype's
        // read_text(encoding='utf-8', errors='replace').
        return File.ReadAllText(path, new UTF8Encoding(false, false));
    }

    private static string DeriveWebHelpModule(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar);
        if (parts.Length > 1 &&
            parts[0].StartsWith("aveva ", StringComparison.OrdinalIgnoreCase))
            return parts[1];

        return parts[0];
    }

    private static KnowledgeRootStats CollectStats(
        string kind,
        string root,
        IReadOnlyList<string> files)
    {
        long count = 0;
        long bytes = 0;
        DateTime maxMtime = DateTime.MinValue;
        foreach (var file in files)
        {
            count++;
            try
            {
                var info = new FileInfo(file);
                bytes += info.Length;
                if (info.LastWriteTimeUtc > maxMtime)
                    maxMtime = info.LastWriteTimeUtc;
            }
            catch (IOException)
            {
            }
        }

        return new KnowledgeRootStats(kind, root, count, bytes, maxMtime);
    }

    private static long InsertSource(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceType,
        string path,
        string relativePath,
        string module,
        string parser,
        IReadOnlyDictionary<string, string> metadata,
        string priority)
    {
        var size = new FileInfo(path).Length;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO sources(source_type, source_path, relative_path, module, " +
            "extension, size, parser, metadata_json, content_hash) " +
            "VALUES ($sourceType, $sourcePath, $relativePath, $module, $extension, " +
            "$size, $parser, $metadataJson, $contentHash); " +
            "SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$sourceType", sourceType);
        command.Parameters.AddWithValue("$sourcePath", path);
        command.Parameters.AddWithValue("$relativePath", relativePath);
        command.Parameters.AddWithValue("$module", module);
        command.Parameters.AddWithValue(
            "$extension",
            Path.GetExtension(path).ToLowerInvariant());
        command.Parameters.AddWithValue("$size", size);
        command.Parameters.AddWithValue("$parser", parser);
        command.Parameters.AddWithValue(
            "$metadataJson",
            JsonSerializer.Serialize(metadata, KnowledgeResponseJsonContext.Default.DictionaryStringString));
        command.Parameters.AddWithValue(
            "$contentHash",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void InsertChunk(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long sourceId,
        string chunkType,
        string symbol,
        string title,
        int? startLine,
        int? endLine,
        string content,
        string relativePath,
        string module,
        IReadOnlyList<string> callTargets)
    {
        long chunkId;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO semantic_chunks(source_id, chunk_type, symbol, title, " +
                "start_line, end_line, content, content_hash, embedding_priority) " +
                "VALUES ($sourceId, $chunkType, $symbol, $title, $startLine, " +
                "$endLine, $content, $contentHash, $priority); " +
                "SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$sourceId", sourceId);
            command.Parameters.AddWithValue("$chunkType", chunkType);
            command.Parameters.AddWithValue("$symbol", symbol);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue(
                "$startLine",
                startLine.HasValue ? (object)startLine.Value : DBNull.Value);
            command.Parameters.AddWithValue(
                "$endLine",
                endLine.HasValue ? (object)endLine.Value : DBNull.Value);
            command.Parameters.AddWithValue("$content", content);
            command.Parameters.AddWithValue("$contentHash", HtmlChunker.Sha256Hex(content));
            command.Parameters.AddWithValue("$priority", "high");
            chunkId = Convert.ToInt64(command.ExecuteScalar());
        }

        using (var fts = connection.CreateCommand())
        {
            fts.Transaction = transaction;
            fts.CommandText =
                "INSERT INTO chunks_fts(rowid, content, relative_path, module, " +
                "chunk_type, symbol, title) " +
                "VALUES ($rowid, $content, $relativePath, $module, $chunkType, " +
                "$symbol, $title)";
            fts.Parameters.AddWithValue("$rowid", chunkId);
            fts.Parameters.AddWithValue("$content", content);
            fts.Parameters.AddWithValue("$relativePath", relativePath);
            fts.Parameters.AddWithValue("$module", module);
            fts.Parameters.AddWithValue("$chunkType", chunkType);
            fts.Parameters.AddWithValue("$symbol", symbol);
            fts.Parameters.AddWithValue("$title", title);
            fts.ExecuteNonQuery();
        }

        foreach (var target in callTargets)
            using (var reference = connection.CreateCommand())
            {
                reference.Transaction = transaction;
                reference.CommandText =
                    "INSERT INTO call_refs(chunk_id, target) VALUES ($chunkId, $target)";
                reference.Parameters.AddWithValue("$chunkId", chunkId);
                reference.Parameters.AddWithValue("$target", target);
                reference.ExecuteNonQuery();
            }
    }

    private static void WriteManifest(
        string manifestPath,
        string database,
        IReadOnlyList<KnowledgeRootStats> roots,
        long pmlFiles,
        long pmlChunks,
        long htmlFiles,
        long htmlChunks,
        long htmlDuplicates,
        long skippedBinaries,
        long totalSources,
        long totalChunks,
        long callRefs,
        long ftsRows)
    {
        using var stream = File.Create(manifestPath);
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteString(writer, "database", database);
        WriteNumber(writer, "schemaVersion", SchemaVersion);
        WriteString(writer, "generator", GeneratorName);
        WriteString(writer, "createdAtUtc", DateTime.UtcNow.ToString("o"));
        writer.WritePropertyName("roots");
        writer.WriteStartArray();
        foreach (var stat in roots)
        {
            writer.WriteStartObject();
            WriteString(writer, "kind", stat.Kind);
            WriteString(writer, "path", stat.Path);
            WriteNumber(writer, "fileCount", stat.FileCount);
            WriteNumber(writer, "totalBytes", stat.TotalBytes);
            WriteString(writer, "maxMtimeUtc", stat.MaxMtimeUtc.ToString("o"));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteNumber(writer, "pmlFiles", pmlFiles);
        WriteNumber(writer, "pmlChunks", pmlChunks);
        WriteNumber(writer, "htmlFiles", htmlFiles);
        WriteNumber(writer, "htmlChunks", htmlChunks);
        WriteNumber(writer, "htmlDuplicateChunks", htmlDuplicates);
        WriteNumber(writer, "skippedBinaryFiles", skippedBinaries);
        WriteNumber(writer, "totalSources", totalSources);
        WriteNumber(writer, "totalChunks", totalChunks);
        WriteNumber(writer, "callRefs", callRefs);
        WriteNumber(writer, "ftsRows", ftsRows);
        writer.WriteEndObject();
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string value)
    {
        writer.WritePropertyName(name);
        writer.WriteStringValue(value);
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, long value)
    {
        writer.WritePropertyName(name);
        writer.WriteNumberValue(value);
    }

    private const string CreateSchemaSql =
        "CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);" +
        "CREATE TABLE sources(" +
        "id INTEGER PRIMARY KEY, source_type TEXT NOT NULL, " +
        "source_path TEXT NOT NULL UNIQUE, relative_path TEXT NOT NULL, " +
        "module TEXT, extension TEXT, size INTEGER, parser TEXT, " +
        "metadata_json TEXT, content_hash TEXT);" +
        "CREATE TABLE semantic_chunks(" +
        "id INTEGER PRIMARY KEY, source_id INTEGER NOT NULL REFERENCES sources(id), " +
        "chunk_type TEXT NOT NULL, symbol TEXT, title TEXT, start_line INTEGER, " +
        "end_line INTEGER, content TEXT NOT NULL, content_hash TEXT NOT NULL, " +
        "embedding_priority TEXT NOT NULL);" +
        "CREATE TABLE call_refs(" +
        "id INTEGER PRIMARY KEY, chunk_id INTEGER NOT NULL REFERENCES semantic_chunks(id), " +
        "target TEXT NOT NULL);" +
        "CREATE INDEX idx_chunks_source ON semantic_chunks(source_id);" +
        "CREATE INDEX idx_chunks_symbol ON semantic_chunks(symbol);" +
        "CREATE INDEX idx_call_target ON call_refs(target);" +
        "CREATE VIRTUAL TABLE chunks_fts USING fts5(content, relative_path, module, " +
        "chunk_type, symbol, title)";

    /// <summary>从 SQLite 读取、用于加权融合排序的中间命中行。</summary>
    /// <param name="Id">来源或记录的稳定编号；重用规则由该操作约定。</param>
    /// <param name="Content">完整正文或待保存的经验内容。</param>
    /// <param name="ChunkType">函数、方法、窗体或 HTML 章节等片段类别。</param>
    /// <param name="Symbol">从源码提取的函数、方法或主题符号。</param>
    /// <param name="StartLine">源文件中从 1 开始的首行行号。</param>
    /// <param name="EndLine">源文件中从 1 开始的末行行号，包含该行。</param>
    /// <param name="SourceType">pml 或 webhelp 等来源类型过滤或标识。</param>
    /// <param name="Module">PML 模块或资料目录范围。</param>
    /// <param name="RelativePath">相对于该来源根目录的文件路径。</param>
    private sealed record SearchRow(
        long Id,
        string Content,
        string ChunkType,
        string Symbol,
        long? StartLine,
        long? EndLine,
        string SourceType,
        string Module,
        string RelativePath);
}

/// <summary>知识库名称、清单、索引或读取校验失败。</summary>
public sealed class KnowledgeException : Exception
{
    /// <summary>保存本机知识生命周期校验失败的原因。</summary>
    /// <param name="message">向调用方解释失败或状态的可读信息。</param>
    public KnowledgeException(string message) : base(message)
    {
    }
}
