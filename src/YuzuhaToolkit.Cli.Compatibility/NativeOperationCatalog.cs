using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;
namespace YuzuhaToolkit.Mcp;
/// <summary>既有 CLI 的组合目录；管理与通信实现依赖在此汇合，通信 Core 不依赖管理。</summary>
public static class NativeOperationCatalog
{
    /// <summary>保留原 ID/schema/退出码及隔离存储注入，通信能力委托独立核心。</summary>
    public static OperationExecutor Create(YuzuhaRpcBridge bridge, PmlFunctionTrustStore trust,
        Func<YuzuhaRpcBridge> createBridge, PmlFunctionRegistry? functions = null,
        KnowledgeRepository? knowledge = null, RuntimeJournal? journal = null, IEnumerable<OperationRegistration>? additional = null)
    {
        var communication = CommunicationOperationCatalog.Create(bridge, trust, createBridge, functions);
        var list = new List<OperationRegistration>();
        foreach (var descriptor in communication.Registry.Descriptors)
        { communication.Registry.TryGet(descriptor.Id, out var registration); list.Add(registration!); }
        var handler = new DelegateOperationHandler((id, input, _, token) => {
            token.ThrowIfCancellationRequested();
            try {
                string json;
                if (id == "workspace.pmllib.inspect") {
                    var settings = WorkspaceSettings.Load(); var directory = S(input, "directory") ?? settings.RegisteredPmlLib;
                    if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Specify directory or configure registeredPmlLib.");
                    json = JsonSerializer.Serialize(DirectoryInspector.Scan(directory, settings.WarnFiles, settings.WarnBytes), WorkspaceJsonContext.Default.DirectoryHealth);
                } else json = InvokeKnowledge(id, input, knowledge ?? new KnowledgeRepository());
                return Task.FromResult(ParseResult(id, json, false));
            }
            catch (OperationCanceledException) { throw; }
            catch (UnauthorizedAccessException error) { return Task.FromResult(OperationResult.Fail(id, 4, "ACCESS_DENIED", error.Message)); }
            catch (Exception error) when (error is ArgumentException or FormatException or InvalidDataException or KeyNotFoundException or NotSupportedException or KnowledgeException)
            { return Task.FromResult(OperationResult.Fail(id, 2, "INVALID_INPUT", error.Message)); }
            catch (Exception error) { return Task.FromResult(OperationResult.Fail(id, 7, "OPERATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
        });
        list.AddRange(Descriptors().Select(d => new OperationRegistration(d, handler)));
        if (additional is not null) list.AddRange(additional);
        return new(new OperationRegistry(list), journal is null ? null : new JournalObserver(journal));
    }
    private static string InvokeKnowledge(string id, JsonElement input, KnowledgeRepository repository)
    {
        var tools = new KnowledgeTools(repository);
        var layers = new KnowledgeLayerTools(repository);
        return id switch
        {
            "knowledge.databases.list" => tools.ListKnowledgeDatabases(B(input, "checkFreshness", true)),
            "knowledge.database.build" => tools.BuildKnowledgeDatabase(S(input, "pmlLibRoot"), S(input, "pmlUiRoot"), S(input, "webHelpRoot"), S(input, "dbName") ?? "pml-knowledge", S(input, "dbDir"), B(input, "rebuild"), L(input, "maxFilesPerRoot", 0)),
            "knowledge.database.check" => tools.CheckKnowledgeDatabase(S(input, "dbPath")!),
            "knowledge.search" => tools.SearchKnowledge(S(input, "query")!, S(input, "dbName"), S(input, "dbPath"), I(input, "topK", 8), S(input, "sourceType"), S(input, "module"), S(input, "chunkType")),
            "knowledge.chunk.read" => tools.GetKnowledgeChunk(L(input, "chunkId", 0), S(input, "dbName"), S(input, "dbPath")),
            "knowledge.source.register" => layers.RegisterKnowledgeSource(S(input, "role")!, S(input, "name")!, S(input, "pmlLibRoot"), S(input, "pmlUiRoot"), S(input, "webHelpRoot"), B(input, "rebuild")),
            "knowledge.layers.search" => layers.SearchKnowledgeLayers(S(input, "query")!, I(input, "topK", 5), B(input, "includeSupplemental")),
            "knowledge.experience.record" => layers.RecordLocalExperience(S(input, "title")!, S(input, "content")!, S(input, "context")!, S(input, "id")),
            _ => throw new ArgumentException("Unknown native operation: " + id)
        };
    }

    private static OperationResult ParseResult(string id, string json, bool dispatched)
    {
        if (json.StartsWith("PML RPC failed:", StringComparison.Ordinal))
        {
            var target = json.Contains("E3D_TARGET_", StringComparison.Ordinal);
            return OperationResult.Fail(id, target ? 5 : 7, target ? "TARGET_FAILED" : "RPC_FAILED", json, mayHaveExecuted: dispatched && !target);
        }
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.Clone();
        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in new[] { "ok", "Ok", "success", "Success" })
                if (data.TryGetProperty(field, out var ok) && ok.ValueKind == JsonValueKind.False)
                {
                    var message = S(data, "ErrorMessage") ?? S(data, "error") ?? S(data, "note") ?? "The underlying operation returned failure.";
                    return OperationResult.Fail(id, dispatched ? 6 : 2, dispatched ? "HOST_FAILED" : "OPERATION_REJECTED", message, data,
                        mayHaveExecuted: dispatched);
                }
        }
        return OperationResult.Ok(id, data);
    }

    private static string? S(JsonElement value, string key) => value.TryGetProperty(key, out var item) && item.ValueKind != JsonValueKind.Null ? item.GetString() : null;
    private static bool B(JsonElement value, string key, bool fallback = false) => value.TryGetProperty(key, out var item) ? item.GetBoolean() : fallback;
    private static int I(JsonElement value, string key, int fallback) => value.TryGetProperty(key, out var item) ? item.GetInt32() : fallback;
    private static long L(JsonElement value, string key, long fallback) => value.TryGetProperty(key, out var item) ? item.GetInt64() : fallback;

    private static OperationDescriptor D(string id, string legacy, string description, string properties = "", string required = "", bool target = false, bool execute = false, bool write = false)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
        return new(id, 1, description, schema.RootElement.Clone(), target, execute, write, LegacyTools: [legacy]);
    }

    private static IEnumerable<OperationDescriptor> Descriptors()
    {
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string optionalText = "{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"null\"}]}";
        const string boolean = "{\"type\":\"boolean\"}";
        var roots = "\"pmlLibRoot\":" + optionalText + ",\"pmlUiRoot\":" + optionalText + ",\"webHelpRoot\":" + optionalText;
        var database = "\"dbName\":" + optionalText + ",\"dbPath\":" + optionalText;
        yield return D("workspace.pmllib.inspect", "inspect_registered_pmllib", "Bounded read-only directory inspection; duplicates are candidates, not proven load conflicts.", "\"directory\":" + optionalText);
        yield return D("knowledge.databases.list", "list_knowledge_databases", "List existing local indices and optional source freshness without creating indices.", "\"checkFreshness\":" + boolean);
        yield return D("knowledge.database.build", "build_knowledge_database", "Build a user-selected local index; overwrite additionally requires rebuild=true.", roots + ",\"dbName\":" + text + ",\"dbDir\":" + optionalText + ",\"rebuild\":" + boolean + ",\"maxFilesPerRoot\":{\"type\":\"integer\",\"minimum\":0}", write: true);
        yield return D("knowledge.database.check", "check_knowledge_database", "Validate SQLite integrity, schema and recorded roots.", "\"dbPath\":" + text, "\"dbPath\"");
        yield return D("knowledge.search", "search_knowledge", "Search a selected local index with source/module/chunk filters.", "\"query\":" + text + "," + database + ",\"topK\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":50},\"sourceType\":" + optionalText + ",\"module\":" + optionalText + ",\"chunkType\":" + optionalText, "\"query\"");
        yield return D("knowledge.chunk.read", "get_knowledge_chunk", "Read complete chunk text and local source provenance from its database.", "\"chunkId\":{\"type\":\"integer\",\"minimum\":1}," + database, "\"chunkId\"");
        yield return D("knowledge.source.register", "register_knowledge_source", "Index an explicitly selected official/custom source; no automatic indexing during update.", "\"role\":" + text + ",\"name\":" + text + "," + roots + ",\"rebuild\":" + boolean, "\"role\",\"name\"", write: true);
        yield return D("knowledge.layers.search", "search_knowledge_layers", "Search embedded guides and optional local source layers using the existing retrieval policy.", "\"query\":" + text + ",\"topK\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":50},\"includeSupplemental\":" + boolean, "\"query\"");
        yield return D("knowledge.experience.record", "record_local_experience", "Append a local lesson with verification context; identical retries may use the same ID.", "\"title\":" + text + ",\"content\":" + text + ",\"context\":" + text + ",\"id\":" + optionalText, "\"title\",\"content\",\"context\"", write: true);
    }
    /// <summary>将原生操作的开始、拒绝、成功和未知结果关联到现有运行日志。</summary>
    private sealed class JournalObserver(RuntimeJournal journal) : IOperationObserver
    {
        /// <summary>记录脱敏输入与显式目标。</summary>
        public string Begin(string id, JsonElement input, OperationContext context) => journal.Begin(id, input.GetRawText(), context.ProcessId);
        /// <summary>保存原始结果的成功或失败语义，不推断业务结果。</summary>
        public void Complete(string correlationId, OperationResult result, OperationContext context) => journal.Complete(correlationId,
            result.Success ? "succeeded" : result.Error?.MayHaveExecuted == true ? "cancelled-unknown" : "failed",
            result.Success ? "Completed; see operation JSON for data." : result.Error?.Code + ": " + result.Error?.Message, context.ProcessId);
    }
}
