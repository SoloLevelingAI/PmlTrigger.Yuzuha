using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Knowledge;

/// <summary>个人笔记的一个不可变修订，删除也记录为可恢复修订。</summary>
/// <param name="Id">笔记编号。</param>
/// <param name="Revision">修订序号。</param>
/// <param name="Title">标题。</param>
/// <param name="Content">正文。</param>
/// <param name="Context">来源与验证上下文。</param>
/// <param name="Deleted">是否已移入回收站。</param>
/// <param name="UpdatedUtc">保存时间。</param>
public sealed record PersonalNote(string Id, int Revision, string Title, string Content, string Context, bool Deleted, string UpdatedUtc);

/// <summary>知识库列表，错误逐库显示，不静默隐藏损坏文件。</summary>
/// <param name="Name">数据库名称。</param>
/// <param name="Role">来源类型。</param>
/// <param name="Bytes">数据库文件长度。</param>
/// <param name="Chunks">索引条目数。</param>
/// <param name="Status">校验状态。</param>
public sealed record KnowledgeLibraryRow(string Name, string Role, long Bytes, long Chunks, string Status);

/// <summary>已移入回收站的资料索引；外部原文始终保留。</summary>
/// <param name="Id">回收站编号。</param>
/// <param name="Name">原数据库名称。</param>
/// <param name="DeletedUtc">移除时间。</param>
public sealed record ArchivedKnowledge(string Id, string Name, string DeletedUtc);

/// <summary>管理记录的编译期 JSON 元数据。</summary>
[JsonSerializable(typeof(ArchivedKnowledge))]
internal partial class KnowledgeManagementJson : JsonSerializerContext { }

/// <summary>个人笔记的版本化编辑及资料索引的备份、回收与恢复。</summary>
public sealed partial class KnowledgeRepository
{
    private string PersonalDatabase => Path.Combine(DefaultDirectory, "personal.sqlite3");

    /// <summary>只读列出个人笔记最新修订；可选显示回收站，不创建空库。</summary>
    /// <param name="includeDeleted">是否同时列出已软删除的笔记。</param>
    public PersonalNote[] ListNotes(bool includeDeleted = false)
    {
        if (!File.Exists(PersonalDatabase)) return [];
        using var connection = OpenReadOnly(PersonalDatabase);
        return ReadNotes(connection, "SELECT id,revision,title,content,context,deleted,updated_utc FROM personal_notes n WHERE revision=(SELECT MAX(revision) FROM personal_notes p WHERE p.id=n.id) "
            + (includeDeleted ? "" : "AND deleted=0 ") + "ORDER BY updated_utc DESC");
    }

    /// <summary>按笔记编号读取完整历史，历史修订不会被编辑或删除。</summary>
    /// <param name="id">笔记的 GUID N 格式编号。</param>
    public PersonalNote[] NoteHistory(string id)
    {
        ValidateNoteId(id);
        if (!File.Exists(PersonalDatabase)) return [];
        using var connection = OpenReadOnly(PersonalDatabase);
        return ReadNotes(connection, "SELECT id,revision,title,content,context,deleted,updated_utc FROM personal_notes WHERE id=$id ORDER BY revision DESC", id);
    }

    /// <summary>新增或编辑个人笔记；核对预期修订后，在同一事务更新当前检索索引和修订记录。</summary>
    /// <param name="id">现有笔记编号；null 表示新建。</param>
    /// <param name="expectedRevision">编辑前的最新修订；新建为 0，过期时拒绝覆盖。</param>
    /// <param name="title">非空标题，最多 500 字符。</param>
    /// <param name="content">正文，最多 200 万字符。</param>
    /// <param name="context">来源与验证说明，最多 10000 字符。</param>
    /// <param name="deleted">true 保存软删除修订，false 保存正文或恢复修订。</param>
    public PersonalNote SaveNote(string? id, int expectedRevision, string title, string content, string context, bool deleted = false)
    {
        id ??= Guid.NewGuid().ToString("N"); ValidateNoteId(id);
        if (expectedRevision < 0 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content) || title.Length > 500 || content.Length > 2_000_000 || context.Length > 10000)
            throw new KnowledgeException("笔记需要标题和正文；标题最多 500 字符，正文最多 200 万字符，上下文最多 10000 字符。");
        PathRules.NoLinks(DefaultDirectory); Directory.CreateDirectory(DefaultDirectory); PathRules.NoLinks(PersonalDatabase);
        using var held = AcquireWriter(PersonalDatabase);
        if (!File.Exists(PersonalDatabase))
        {
            var stage = PersonalDatabase + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var initialize = OpenReadWrite(stage))
                    Execute(initialize, CreateSchemaSql + ";INSERT INTO meta VALUES ('schema_version','1'),('generator','YuzuhaToolkit.Knowledge'),('role','personal');"
                        + "CREATE TABLE personal_notes(id TEXT NOT NULL,revision INTEGER NOT NULL,title TEXT NOT NULL,content TEXT NOT NULL,context TEXT NOT NULL,deleted INTEGER NOT NULL,updated_utc TEXT NOT NULL,PRIMARY KEY(id,revision));");
                File.Move(stage, PersonalDatabase, false);
            }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        using var connection = OpenReadWrite(PersonalDatabase);
        using var transaction = connection.BeginTransaction();
        using var query = connection.CreateCommand(); query.Transaction = transaction;
        query.CommandText = "SELECT COALESCE(MAX(revision),0) FROM personal_notes WHERE id=$id"; query.Parameters.AddWithValue("$id", id);
        if (Convert.ToInt32(query.ExecuteScalar()) != expectedRevision) throw new KnowledgeException("笔记已被其他窗口修改，请刷新后再保存。");
        var note = new PersonalNote(id, expectedRevision + 1, title.Trim(), content, context, deleted, DateTime.UtcNow.ToString("o"));
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO personal_notes VALUES ($id,$revision,$title,$content,$context,$deleted,$utc);"
            + "DELETE FROM chunks_fts WHERE rowid IN (SELECT c.id FROM semantic_chunks c JOIN sources s ON c.source_id=s.id WHERE s.source_path=$path);"
            + "DELETE FROM call_refs WHERE chunk_id IN (SELECT c.id FROM semantic_chunks c JOIN sources s ON c.source_id=s.id WHERE s.source_path=$path);"
            + "DELETE FROM semantic_chunks WHERE source_id IN (SELECT id FROM sources WHERE source_path=$path); DELETE FROM sources WHERE source_path=$path;";
        command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$revision", note.Revision);
        command.Parameters.AddWithValue("$title", note.Title); command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$context", context); command.Parameters.AddWithValue("$deleted", deleted ? 1 : 0);
        command.Parameters.AddWithValue("$utc", note.UpdatedUtc); command.Parameters.AddWithValue("$path", "personal:" + id); command.ExecuteNonQuery();
        if (!deleted)
        {
            command.CommandText = "INSERT INTO sources(source_type,source_path,relative_path,module) VALUES ('personal',$path,$id,$context); SELECT last_insert_rowid();";
            var sourceId = Convert.ToInt64(command.ExecuteScalar());
            InsertChunk(connection, transaction, sourceId, "personal_note", note.Title, note.Title, null, null, context + "\n" + content, id, context, []);
        }
        transaction.Commit(); return note;
    }

    /// <summary>逐库列出条目、体积与错误；不修改索引或源文件。</summary>
    public KnowledgeLibraryRow[] ListLibraries()
    {
        if (!Directory.Exists(DefaultDirectory)) return [];
        return Directory.EnumerateFiles(DefaultDirectory, "*.sqlite3").Order(StringComparer.OrdinalIgnoreCase).Select(path =>
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var role = name.StartsWith("official-", StringComparison.OrdinalIgnoreCase) ? "official" : name is "project" or "experience" or "personal" ? name : "custom";
            try { var summary = Summarize(path, Path.ChangeExtension(path, ".manifest.json"), false); return new KnowledgeLibraryRow(name, role, new FileInfo(path).Length, summary.TotalChunks, "可读取"); }
            catch (Exception error) { return new KnowledgeLibraryRow(name, role, new FileInfo(path).Length, 0, "读取失败：" + error.Message); }
        }).ToArray();
    }

    /// <summary>按原登记来源重建自有/官方资料索引；先保留数据库备份，不修改原文。</summary>
    /// <param name="name">不含扩展名的资料库名，保留库不允许重建。</param>
    public KnowledgeBuildResult RebuildLibrary(string name)
    {
        var database = ManagedLibrary(name);
        using var held = AcquireWriter(database);
        var summary = Summarize(database, Path.ChangeExtension(database, ".manifest.json"), false);
        var backup = Path.Combine(DefaultDirectory, ".history", Guid.NewGuid().ToString("N"));
        PathRules.NoLinks(backup); Directory.CreateDirectory(backup);
        {
            if (File.Exists(database + "-wal") || File.Exists(database + "-shm")) throw new KnowledgeException("资料库正在使用，请关闭使用它的客户端后重试。");
            using var source = OpenReadOnly(database); using var target = OpenReadWrite(Path.Combine(backup, Path.GetFileName(database)));
            source.BackupDatabase(target);
        }
        return BuildUnderWriterLock(summary.Roots.FirstOrDefault(r => r.Kind == "pmllib")?.Path, summary.Roots.FirstOrDefault(r => r.Kind == "pmlui")?.Path,
            summary.Roots.FirstOrDefault(r => r.Kind == "webhelp")?.Path, name, DefaultDirectory, true, 0);
    }

    /// <summary>将资料库和来源清单移入本目录回收站，保留外部原文；个人笔记用逐条软删除。</summary>
    /// <param name="name">不含扩展名的资料库名。</param>
    public ArchivedKnowledge ArchiveLibrary(string name)
    {
        var database = ManagedLibrary(name);
        if (!File.Exists(database)) throw new KnowledgeException("资料库不存在。");
        using var held = AcquireWriter(database);
        if (File.Exists(database + "-wal") || File.Exists(database + "-shm")) throw new KnowledgeException("资料库正在使用，请关闭使用它的客户端后重试。");
        var entry = new ArchivedKnowledge(Guid.NewGuid().ToString("N"), name, DateTime.UtcNow.ToString("o"));
        var directory = PathRules.Inside(DefaultDirectory, ".trash/" + entry.Id); Directory.CreateDirectory(directory);
        var moved = new List<(string Source, string Target)>();
        try
        {
            foreach (var source in new[] { database, Path.ChangeExtension(database, ".manifest.json") }.Where(File.Exists))
            { PathRules.NoLinks(source); var target = Path.Combine(directory, Path.GetFileName(source)); File.Move(source, target); moved.Add((source, target)); }
            File.WriteAllText(Path.Combine(directory, "record.json"), JsonSerializer.Serialize(entry, KnowledgeManagementJson.Default.ArchivedKnowledge)); return entry;
        }
        catch { foreach (var move in moved.AsEnumerable().Reverse()) if (!File.Exists(move.Source)) File.Move(move.Target, move.Source); throw; }
    }

    /// <summary>列出完整回收站记录；损坏记录抛出错误而非静默丢失。</summary>
    public ArchivedKnowledge[] ArchivedLibraries()
    {
        var root = PathRules.Inside(DefaultDirectory, ".trash");
        return Directory.Exists(root) ? Directory.EnumerateDirectories(root).Select(directory =>
        { var file = PathRules.Inside(directory, "record.json"); return File.Exists(file) ? JsonSerializer.Deserialize(File.ReadAllText(file), KnowledgeManagementJson.Default.ArchivedKnowledge) : null; }).Where(row => row is not null).Cast<ArchivedKnowledge>().ToArray() : [];
    }

    /// <summary>恢复指定索引与清单；同名目标已存在时拒绝覆盖。</summary>
    /// <param name="id">回收站条目的 GUID N 格式编号。</param>
    public void RestoreLibrary(string id)
    {
        ValidateNoteId(id); var directory = PathRules.Inside(DefaultDirectory, ".trash/" + id);
        var entry = JsonSerializer.Deserialize(File.ReadAllText(PathRules.Inside(directory, "record.json")), KnowledgeManagementJson.Default.ArchivedKnowledge) ?? throw new KnowledgeException("无效回收站记录。");
        if (entry.Id != id) throw new KnowledgeException("回收站编号不匹配。");
        var database = ManagedLibrary(entry.Name); using var held = AcquireWriter(database);
        var pairs = new[] { entry.Name + ".sqlite3", entry.Name + ".manifest.json" }.Select(name => (Source: PathRules.Inside(directory, name), Target: PathRules.Inside(DefaultDirectory, name))).Where(pair => File.Exists(pair.Source)).ToArray();
        if (!pairs.Any(pair => pair.Source.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase)) || pairs.Any(pair => File.Exists(pair.Target))) throw new KnowledgeException("恢复目标已存在或回收站缺少数据库，未覆盖文件。");
        var moved = new List<(string Source, string Target)>();
        try { foreach (var pair in pairs) { File.Move(pair.Source, pair.Target); moved.Add(pair); } File.Delete(Path.Combine(directory, "record.json")); }
        catch { foreach (var pair in moved.AsEnumerable().Reverse()) if (!File.Exists(pair.Source)) File.Move(pair.Target, pair.Source); throw; }
    }

    private string ManagedLibrary(string name)
    {
        ValidateDbName(name);
        if (name.Equals("personal", StringComparison.OrdinalIgnoreCase) || name.Equals("experience", StringComparison.OrdinalIgnoreCase) || name.Equals("project", StringComparison.OrdinalIgnoreCase))
            throw new KnowledgeException("内置项目、经验和个人笔记库不能按资料库重建/删除；个人笔记请逐条管理。");
        return PathRules.Inside(DefaultDirectory, name + ".sqlite3");
    }
    private static void ValidateNoteId(string id) { if (!Guid.TryParseExact(id, "N", out _)) throw new KnowledgeException("笔记或回收站编号无效。"); }
    private static PersonalNote[] ReadNotes(SqliteConnection connection, string sql, string? id = null)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; if (id is not null) command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader(); var rows = new List<PersonalNote>();
        while (reader.Read()) rows.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5) != 0, reader.GetString(6)));
        return rows.ToArray();
    }
}
