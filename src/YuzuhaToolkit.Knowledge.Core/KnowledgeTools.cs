using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace YuzuhaToolkit.Knowledge;

/// <summary>MCP 知识库生命周期与只读检索入口，序列化仓库结果并保留失败说明。</summary>
[McpServerToolType]
public sealed class KnowledgeTools
{
    private readonly KnowledgeRepository _repository;

    /// <summary>绑定本机知识仓库供 MCP 工具复用，不在构造时创建索引。</summary>
    /// <param name="repository">封装本机索引、校验、检索与经验持久化的仓库。</param>
    public KnowledgeTools(KnowledgeRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Lists the knowledge databases found in the local knowledge directory with their source roots, row counts, and a freshness comparison against those roots. When no database exists, or one reports changed or root-missing, ask the user whether to build or rebuild locally (build_knowledge_database), copy a database from a colleague and validate it (check_knowledge_database), or skip. Never build or rebuild without the user's consent.</summary>
    /// <param name="checkFreshness">Also compare each database against its source roots. Default true; set false for a fast listing.</param>
    [McpServerTool]
    [Description(
        "Lists the knowledge databases found in the local knowledge " +
        "directory with their source roots, row counts, and a freshness " +
        "comparison against those roots. When no database exists, or one " +
        "reports changed or root-missing, ask the user whether to build or " +
        "rebuild locally (build_knowledge_database), copy a database from a " +
        "colleague and validate it (check_knowledge_database), or skip. Never " +
        "build or rebuild without the user's consent.")]
    public string ListKnowledgeDatabases(
        [Description(
            "Also compare each database against its source roots. Default " +
            "true; set false for a fast listing.")]
        bool checkFreshness = true)
    {
        try
        {
            return JsonSerializer.Serialize(
                _repository.List(checkFreshness),
                KnowledgeResponseJsonContext.Default.ListKnowledgeDbSummary);
        }
        catch (Exception exception)
        {
            return Error(exception, "Run check_knowledge_database on each database.");
        }
    }

    /// <summary>Builds or fully rebuilds one local knowledge database (SQLite + FTS5) from directories the user owns: the package PMLLIB, PMLUI, and optionally an AVEVA WebHelp directory. Copyright: the built database contains AVEVA-derived and user content; keep it local and never commit, ship, or publish it. The tool refuses to overwrite an existing database unless rebuild=true — confirm with the user first. Use a distinct dbName per data set so different sources never mix.</summary>
    /// <param name="pmlLibRoot">Absolute path of the PMLLIB directory to index, for example D:\PmlTrigger.Yuzuha\PMLLIB.</param>
    /// <param name="pmlUiRoot">Absolute path of the PMLUI directory to index.</param>
    /// <param name="webHelpRoot">Absolute path of an AVEVA WebHelp root to index, if present. This can add tens of thousands of pages and take minutes.</param>
    /// <param name="dbName">Database name without extension. Default 'pml-knowledge'. Different names keep databases separate.</param>
    /// <param name="dbDir">Override the output directory. Default is the knowledge directory inside the managed installation.</param>
    /// <param name="rebuild">true to delete and rebuild an existing database of the same name. Only pass true after the user explicitly confirmed the rebuild.</param>
    /// <param name="maxFilesPerRoot">Optional safety cap on files indexed per root; 0 means no limit.</param>
    [McpServerTool]
    [Description(
        "Builds or fully rebuilds one local knowledge database (SQLite + " +
        "FTS5) from directories the user owns: the package PMLLIB, PMLUI, " +
        "and optionally an AVEVA WebHelp directory. Copyright: the built " +
        "database contains AVEVA-derived and user content; keep it local " +
        "and never commit, ship, or publish it. The tool refuses to " +
        "overwrite an existing database unless rebuild=true — confirm with " +
        "the user first. Use a distinct dbName per data set so different " +
        "sources never mix.")]
    public string BuildKnowledgeDatabase(
        [Description(
            "Absolute path of the PMLLIB directory to index, for example " +
            "D:\\PmlTrigger.Yuzuha\\PMLLIB.")]
        string? pmlLibRoot = null,
        [Description("Absolute path of the PMLUI directory to index.")]
        string? pmlUiRoot = null,
        [Description(
            "Absolute path of an AVEVA WebHelp root to index, if present. " +
            "This can add tens of thousands of pages and take minutes.")]
        string? webHelpRoot = null,
        [Description(
            "Database name without extension. Default 'pml-knowledge'. " +
            "Different names keep databases separate.")]
        string dbName = "pml-knowledge",
        [Description(
            "Override the output directory. Default is the knowledge " +
            "directory inside the managed installation.")]
        string? dbDir = null,
        [Description(
            "true to delete and rebuild an existing database of the same " +
            "name. Only pass true after the user explicitly confirmed the " +
            "rebuild.")]
        bool rebuild = false,
        [Description(
            "Optional safety cap on files indexed per root; 0 means no " +
            "limit.")]
        long maxFilesPerRoot = 0)
    {
        try
        {
            return JsonSerializer.Serialize(
                _repository.Build(
                    pmlLibRoot,
                    pmlUiRoot,
                    webHelpRoot,
                    dbName,
                    dbDir,
                    rebuild,
                    maxFilesPerRoot),
                KnowledgeResponseJsonContext.Default.KnowledgeBuildResult);
        }
        catch (Exception exception)
        {
            return Error(exception,
                "If the database already existed, ask the user whether to " +
                "rebuild it (rebuild=true) or copy it aside first.");
        }
    }

    /// <summary>Validates one knowledge database without executing anything: SQLite integrity, Yuzuha schema presence, row counts, and the recorded source roots. Use it before first use of a database copied from someone else, and to explain to the user whether the copy is usable.</summary>
    /// <param name="dbPath">Absolute path of the .sqlite3 database file to validate.</param>
    [McpServerTool]
    [Description(
        "Validates one knowledge database without executing anything: " +
        "SQLite integrity, Yuzuha schema presence, row counts, and the " +
        "recorded source roots. Use it before first use of a database " +
        "copied from someone else, and to explain to the user whether the " +
        "copy is usable.")]
    public string CheckKnowledgeDatabase(
        [Description(
            "Absolute path of the .sqlite3 database file to validate.")]
        string dbPath)
    {
        try
        {
            return JsonSerializer.Serialize(
                _repository.Check(dbPath),
                KnowledgeResponseJsonContext.Default.KnowledgeCheckResult);
        }
        catch (Exception exception)
        {
            return Error(exception, null);
        }
    }

    /// <summary>Deterministic read-only FTS5 search over one knowledge database. Multiple query variants (raw terms, exact phrase, domain terms, and create-intent patterns) are fused with weighted reciprocal rank. Prefer PML or English technical terms; Chinese task wording is also understood. Select a specific database with dbName (in the knowledge directory) or dbPath (anywhere).</summary>
    /// <param name="query">Free-text query, for example 'create bolt sube extr' or 'define method callback'.</param>
    /// <param name="dbName">Database name in the knowledge directory, without extension. Default 'pml-knowledge'. Ignored when dbPath is given.</param>
    /// <param name="dbPath">Absolute path to a .sqlite3 knowledge database.</param>
    /// <param name="topK">Maximum number of hits, 1-50. Default 8.</param>
    /// <param name="sourceType">Filter by source type: pml or webhelp.</param>
    /// <param name="module">Filter by module, for example Addins or Traversal.</param>
    /// <param name="chunkType">Filter by chunk type, for example pml_function, pml_form, pml_macro_section, or html_section.</param>
    [McpServerTool]
    [Description(
        "Deterministic read-only FTS5 search over one knowledge database. " +
        "Multiple query variants (raw terms, exact phrase, domain terms, " +
        "and create-intent patterns) are fused with weighted reciprocal " +
        "rank. Prefer PML or English technical terms; Chinese task wording " +
        "is also understood. Select a specific database with dbName (in " +
        "the knowledge directory) or dbPath (anywhere).")]
    public string SearchKnowledge(
        [Description("Free-text query, for example 'create bolt sube extr' " +
                     "or 'define method callback'.")]
        string query,
        [Description(
            "Database name in the knowledge directory, without extension. " +
            "Default 'pml-knowledge'. Ignored when dbPath is given.")]
        string? dbName = null,
        [Description("Absolute path to a .sqlite3 knowledge database.")]
        string? dbPath = null,
        [Description("Maximum number of hits, 1-50. Default 8.")]
        int topK = 8,
        [Description("Filter by source type: pml or webhelp.")]
        string? sourceType = null,
        [Description("Filter by module, for example Addins or Traversal.")]
        string? module = null,
        [Description(
            "Filter by chunk type, for example pml_function, pml_form, " +
            "pml_macro_section, or html_section.")]
        string? chunkType = null)
    {
        try
        {
            return JsonSerializer.Serialize(
                _repository.Search(
                    query,
                    dbName,
                    dbPath,
                    topK,
                    sourceType,
                    module,
                    chunkType),
                KnowledgeResponseJsonContext.Default.KnowledgeSearchResult);
        }
        catch (Exception exception)
        {
            return Error(exception,
                "Use list_knowledge_databases to see which databases exist; ask " +
                "the user before building one.");
        }
    }

    /// <summary>Returns the full text of one chunk plus the resolved local source file path when that file exists on this machine. Use it after SearchKnowledge to read the complete PML function or WebHelp section behind a promising hit.</summary>
    /// <param name="chunkId">Chunk id returned by SearchKnowledge.</param>
    /// <param name="dbName">Database name in the knowledge directory, without extension. Default 'pml-knowledge'. Ignored when dbPath is given.</param>
    /// <param name="dbPath">Absolute path to a .sqlite3 knowledge database.</param>
    [McpServerTool]
    [Description(
        "Returns the full text of one chunk plus the resolved local source " +
        "file path when that file exists on this machine. Use it after " +
        "SearchKnowledge to read the complete PML function or WebHelp " +
        "section behind a promising hit.")]
    public string GetKnowledgeChunk(
        [Description("Chunk id returned by SearchKnowledge.")]
        long chunkId,
        [Description(
            "Database name in the knowledge directory, without extension. " +
            "Default 'pml-knowledge'. Ignored when dbPath is given.")]
        string? dbName = null,
        [Description("Absolute path to a .sqlite3 knowledge database.")]
        string? dbPath = null)
    {
        try
        {
            return JsonSerializer.Serialize(
                _repository.GetChunk(chunkId, dbName, dbPath),
                KnowledgeResponseJsonContext.Default.KnowledgeChunkDetail);
        }
        catch (Exception exception)
        {
            return Error(exception, null);
        }
    }

    private static string Error(Exception exception, string? hint)
    {
        return JsonSerializer.Serialize(
            new KnowledgeError(false, exception.Message, hint),
            KnowledgeResponseJsonContext.Default.KnowledgeError);
    }
}
