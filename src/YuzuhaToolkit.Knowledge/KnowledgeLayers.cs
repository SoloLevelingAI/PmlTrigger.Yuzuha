using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Knowledge;

public sealed record LayerSearch(string Role, string Database, KnowledgeSearchResult? Result, string? Error);
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
        string? webHelpRoot, string dbName, string? dbDir, bool rebuild, long maxFilesPerRoot)
    {
        var name = string.IsNullOrWhiteSpace(dbName) ? "pml-knowledge" : dbName.Trim();
        ValidateDbName(name);
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
        if (role is not ("project" or "official"))
            throw new KnowledgeException("role must be project or official. Experience uses its own append tool.");
        ValidateDbName(name);
        return Build(pmlLibRoot, pmlUiRoot, webHelpRoot,
            role == "project" ? "project" : "official-" + name, null, rebuild, 0);
    }

    public KnowledgeBuildResult RefreshProject(string installRoot) => Build(
        Path.Combine(installRoot, "PMLLIB"), Path.Combine(installRoot, "PMLUI"),
        null, "project", null, true, 0);

    public List<LayerSearch> SearchLayers(string query, int topK)
    {
        var results = new List<LayerSearch>();
        if (!Directory.Exists(DefaultDirectory)) return results;
        foreach (var database in Directory.EnumerateFiles(DefaultDirectory, "*.sqlite3")
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(database);
            var role = name.Equals("project", StringComparison.OrdinalIgnoreCase) ? "project" :
                name.Equals("experience", StringComparison.OrdinalIgnoreCase) ? "experience" :
                name.StartsWith("official-", StringComparison.OrdinalIgnoreCase) ? "official" : "legacy";
            try { results.Add(new(role, database, Search(query, null, database, topK, null, null, null), null)); }
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
