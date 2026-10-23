using System.Text.Json;
using YuzuhaToolkit.Builtins;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Knowledge;

/// <summary>按来源角色分组的知识结果或完整内置指南。</summary>
/// <param name="Role">内置、项目、官方、经验或自定义的来源角色。</param>
/// <param name="Database">所操作的本机 SQLite 文件路径。</param>
/// <param name="Result">已经完成的发现或查询结果。</param>
/// <param name="Error">当前查询、连接或处理失败的可读原因。</param>
/// <param name="Guides">在无需 SQLite 时返回的完整内置指南。</param>
public sealed record LayerSearch(string Role, string Database, KnowledgeSearchResult? Result, string? Error, IReadOnlyList<BuiltinGuide>? Guides = null);
/// <summary>本机经验追加操作的结果、编号与存储位置。</summary>
/// <param name="Id">来源或记录的稳定编号；重用规则由该操作约定。</param>
/// <param name="Database">所操作的本机 SQLite 文件路径。</param>
/// <param name="Title">资料或经验的可读标题。</param>
public sealed record ExperienceResult(string Id, string Database, string Title);

/// <summary>为对应协议模型提供编译期 JSON 类型元数据，支持 Native AOT。</summary>
[JsonSerializable(typeof(List<LayerSearch>))]
[JsonSerializable(typeof(ExperienceResult))]
[JsonSerializable(typeof(string))]
internal partial class LayerJsonContext : JsonSerializerContext { }

/// <summary>维护本机知识源角色、内置优先检索与独立经验存储的仓库扩展。</summary>
public sealed partial class KnowledgeRepository
{
    // Every writer, including another MCP process, uses the same per-DB lock.
    private static FileStream AcquireWriter(string database) =>
        new(database + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    /// <summary>从用户明确选择的本机目录建立 SQLite/FTS5；已有库仅在显式 rebuild 时重建。</summary>
    /// <param name="pmlLibRoot">用户明确选择的本机 PMLLIB 来源目录。</param>
    /// <param name="pmlUiRoot">用户明确选择的本机 PMLUI 来源目录。</param>
    /// <param name="webHelpRoot">用户明确选择的本机 AVEVA 帮助目录。</param>
    /// <param name="dbName">经校验的不带扩展名的数据库名称。</param>
    /// <param name="dbDir">数据库输出目录；省略时使用配置的本机目录。</param>
    /// <param name="rebuild">是否明确允许覆盖重建；调用方必须先取得用户授权。</param>
    /// <param name="maxFilesPerRoot">每个来源目录的文件处理上限；0 表示不限。</param>
    /// <param name="packageRefresh">此次刷新是否来自包升级，而非用户资料重建。</param>
    public KnowledgeBuildResult Build(string? pmlLibRoot, string? pmlUiRoot,
        string? webHelpRoot, string dbName, string? dbDir, bool rebuild, long maxFilesPerRoot, bool packageRefresh = false)
    {
        var name = string.IsNullOrWhiteSpace(dbName) ? "pml-knowledge" : dbName.Trim();
        ValidateDbName(name);
        if (name.Equals("project", StringComparison.OrdinalIgnoreCase) && !packageRefresh)
            throw new KnowledgeException("project is reserved for package refresh. Use a custom database name.");
        if (name.Equals("experience", StringComparison.OrdinalIgnoreCase))
            throw new KnowledgeException("The experience database is append-only; use record_local_experience.");
        if (name.Equals("personal", StringComparison.OrdinalIgnoreCase))
            throw new KnowledgeException("personal is reserved for versioned notes; use the note editor.");
        var directory = Path.GetFullPath(string.IsNullOrWhiteSpace(dbDir) ? DefaultDirectory : dbDir);
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, name + ".sqlite3");
        var manifest = Path.Combine(directory, name + ".manifest.json");
        using var writerLock = AcquireWriter(database);
        return BuildUnderWriterLock(pmlLibRoot, pmlUiRoot, webHelpRoot, name, directory, rebuild, maxFilesPerRoot);
    }

    private KnowledgeBuildResult BuildUnderWriterLock(string? pmlLibRoot, string? pmlUiRoot, string? webHelpRoot,
        string name, string directory, bool rebuild, long maxFilesPerRoot)
    {
        var database = Path.Combine(directory, name + ".sqlite3");
        var manifest = Path.Combine(directory, name + ".manifest.json");
        if (File.Exists(database) && !rebuild)
            throw new KnowledgeException("Database exists. Explicitly request rebuild or use another name.");
        var stage = Path.Combine(directory, ".build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var built = BuildCore(pmlLibRoot, pmlUiRoot, webHelpRoot, name, stage, false, maxFilesPerRoot);
            if (!Check(built.Database).Ok)
                throw new KnowledgeException("Staged database failed validation; the previous database was retained.");
            // The database is authoritative. The freshness sidecar is only advisory.
            var json = File.ReadAllText(built.Manifest).Replace(
                JsonSerializer.Serialize(built.Database, LayerJsonContext.Default.String),
                JsonSerializer.Serialize(database, LayerJsonContext.Default.String));
            File.WriteAllText(built.Manifest, json);
            File.Move(built.Database, database, overwrite: rebuild);
            File.Move(built.Manifest, manifest, overwrite: true);
            return built with { Database = database, Manifest = manifest };
        }
        finally { Directory.Delete(stage, recursive: true); }
    }

    /// <summary>注册用户选择的自定义或官方资料；保持内置指南优先并隔离各来源数据库。</summary>
    /// <param name="role">内置、项目、官方、经验或自定义的来源角色。</param>
    /// <param name="name">该来源、变量或对象的稳定名称。</param>
    /// <param name="pmlLibRoot">用户明确选择的本机 PMLLIB 来源目录。</param>
    /// <param name="pmlUiRoot">用户明确选择的本机 PMLUI 来源目录。</param>
    /// <param name="webHelpRoot">用户明确选择的本机 AVEVA 帮助目录。</param>
    /// <param name="rebuild">是否明确允许覆盖重建；调用方必须先取得用户授权。</param>
    public KnowledgeBuildResult RegisterSource(string role, string name,
        string? pmlLibRoot, string? pmlUiRoot, string? webHelpRoot, bool rebuild)
    {
        role = role.Trim().ToLowerInvariant();
        if (role is not ("project" or "custom" or "official"))
            throw new KnowledgeException("role must be custom or official (project is a compatibility alias for custom). Experience uses its own append tool.");
        ValidateDbName(name);
        return Build(pmlLibRoot, pmlUiRoot, webHelpRoot,
            role == "official" ? "official-" + name : "custom-" + name, null, rebuild, 0);
    }

    /// <summary>刷新包内项目资料索引，保留官方资料、用户经验与自定义来源。</summary>
    /// <param name="installRoot">用户明确指定的安装根目录。</param>
    public KnowledgeBuildResult RefreshProject(string installRoot) => Build(
        Path.Combine(installRoot, "PMLLIB"), Path.Combine(installRoot, "PMLUI"),
        null, "project", null, true, 0, packageRefresh: true);

    /// <summary>优先返回匹配的完整内置指南；按选项补充或回退到分层只读检索。</summary>
    /// <param name="query">PML、英文或中文的任务描述或检索文本。</param>
    /// <param name="topK">返回命中总数上限，内部限制到工具支持范围。</param>
    /// <param name="includeSupplemental">是否在内置指南匹配后仍加入可选补充来源。</param>
    public List<LayerSearch> SearchLayers(string query, int topK, bool includeSupplemental = false)
    {
        topK = Math.Clamp(topK, 1, 20);
        var results = new List<LayerSearch>();
        var guides = BuiltinGuideCatalog.Find(query, topK);
        if (guides.Count > 0)
        {
            results.Add(new("builtin", "", null, null, guides));
            if (!includeSupplemental) return results;
        }
        if (!Directory.Exists(DefaultDirectory)) return results;
        var remaining = topK;
        foreach (var database in Directory.EnumerateFiles(DefaultDirectory, "*.sqlite3")
                     .OrderBy(p => Path.GetFileNameWithoutExtension(p).Equals("project", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (remaining == 0) break;
            var name = Path.GetFileNameWithoutExtension(database);
            var role = name.Equals("project", StringComparison.OrdinalIgnoreCase) ? "project" :
                name.Equals("experience", StringComparison.OrdinalIgnoreCase) ? "experience" :
                name.Equals("personal", StringComparison.OrdinalIgnoreCase) ? "personal" :
                name.StartsWith("official-", StringComparison.OrdinalIgnoreCase) ? "official" : "custom";
            try {
                var result = Search(query, null, database, remaining, null, null, null);
                remaining -= result.Hits.Count;
                results.Add(new(role, database, result with { Note =
                    "Supplemental reference only. Does not replace a built-in method; a custom replacement requires an explicit user decision." }, null));
            }
            catch (Exception ex) { results.Add(new(role, database, null, ex.Message)); }
        }
        return results;
    }

    /// <summary>按需初始化独立的本机经验库，不重建已有经验。</summary>
    public void EnsureExperience()
    {
        Directory.CreateDirectory(DefaultDirectory);
        var database = Path.Combine(DefaultDirectory, "experience.sqlite3");
        using var writerLock = AcquireWriter(database);
        if (File.Exists(database))
        {
            if (!Check(database).Ok) throw new KnowledgeException("Experience database is invalid; retained without replacement.");
            return;
        }
        var temporary = database + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var connection = OpenReadWrite(temporary))
            {
                Execute(connection, CreateSchemaSql);
                Execute(connection, "INSERT INTO meta VALUES ('schema_version','1'),('generator','YuzuhaToolkit.Knowledge'),('role','experience');");
            }
            File.Move(temporary, database);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>追加用户授权的经验与验证上下文；同编号同内容可重复确认，修正使用新编号。</summary>
    /// <param name="title">资料或经验的可读标题。</param>
    /// <param name="content">完整正文或待保存的经验内容。</param>
    /// <param name="context">产品版本、模块、工程背景及已经取得的验证证据。</param>
    /// <param name="id">来源或记录的稳定编号；重用规则由该操作约定。</param>
    public ExperienceResult RecordExperience(string title, string content, string context, string? id)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            throw new KnowledgeException("title and content are required.");
        EnsureExperience();
        id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
        ValidateDbName(id);
        var database = Path.Combine(DefaultDirectory, "experience.sqlite3");
        using var writerLock = AcquireWriter(database);
        using var connection = OpenReadWrite(database);
        using var transaction = connection.BeginTransaction();
        // An explicit id makes retries idempotent; never overwrite previous experience.
        using var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = "SELECT c.title,c.content FROM sources s JOIN semantic_chunks c ON c.source_id=s.id WHERE s.source_path=$path";
        lookup.Parameters.AddWithValue("$path", "experience:" + id);
        using (var reader = lookup.ExecuteReader())
        {
            if (reader.Read())
            {
                if (reader.GetString(0) != title || reader.GetString(1) != context + "\n" + content)
                    throw new KnowledgeException("Experience id already exists with different content; use a new id for a correction.");
                return new(id, database, title);
            }
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO sources(source_type,source_path,relative_path,module,metadata_json) VALUES ('experience',$path,$id,$context,$meta); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$path", "experience:" + id);
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$context", context);
        command.Parameters.AddWithValue("$meta", JsonSerializer.Serialize(
            new Dictionary<string,string> { ["recordedAtUtc"] = DateTime.UtcNow.ToString("o") },
            KnowledgeResponseJsonContext.Default.DictionaryStringString));
        var sourceId = Convert.ToInt64(command.ExecuteScalar());
        InsertChunk(connection, transaction, sourceId, "local_experience", title, title,
            null, null, context + "\n" + content, id, context, Array.Empty<string>());
        transaction.Commit();
        return new(id, database, title);
    }
}
