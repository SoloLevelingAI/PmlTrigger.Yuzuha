using System.Text.Json;
using YuzuhaToolkit.Builtins;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Knowledge;

public sealed record LayerSearch(string Role, string Database, KnowledgeSearchResult? Result, string? Error, IReadOnlyList<BuiltinGuide>? Guides = null);
public sealed record ExperienceResult(string Id, string Database, string Title);

[JsonSerializable(typeof(List<LayerSearch>))]
[JsonSerializable(typeof(ExperienceResult))]
[JsonSerializable(typeof(string))]
internal partial class LayerJsonContext : JsonSerializerContext { }

public sealed partial class KnowledgeRepository
{
    // Every writer, including another MCP process, uses the same per-DB lock.
    private static FileStream AcquireWriter(string database) =>
        new(database + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    public KnowledgeBuildResult Build(string? pmlLibRoot, string? pmlUiRoot,
        string? webHelpRoot, string dbName, string? dbDir, bool rebuild, long maxFilesPerRoot, bool packageRefresh = false)
    {
        var name = string.IsNullOrWhiteSpace(dbName) ? "pml-knowledge" : dbName.Trim();
        ValidateDbName(name);
        if (name.Equals("project", StringComparison.OrdinalIgnoreCase) && !packageRefresh)
            throw new KnowledgeException("project is reserved for package refresh. Use a custom database name.");
        if (name.Equals("experience", StringComparison.OrdinalIgnoreCase))
            throw new KnowledgeException("The experience database is append-only; use record_local_experience.");
        var directory = Path.GetFullPath(string.IsNullOrWhiteSpace(dbDir) ? DefaultDirectory : dbDir);
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, name + ".sqlite3");
        var manifest = Path.Combine(directory, name + ".manifest.json");
        using var writerLock = AcquireWriter(database);
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

    public KnowledgeBuildResult RefreshProject(string installRoot) => Build(
        Path.Combine(installRoot, "PMLLIB"), Path.Combine(installRoot, "PMLUI"),
        null, "project", null, true, 0, packageRefresh: true);

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
