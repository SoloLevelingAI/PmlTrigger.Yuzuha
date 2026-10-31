using System.Text.Json;
using Microsoft.Data.Sqlite;
using PmlTwin.Phayton;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>GUI/管理 CLI 共用的版本资料计划、导入、深检查与明确身份检索。</summary>
public static class KnowledgeOperations
{
    /// <summary>建立知识管理操作；构造注册表不会建库或连接 AVEVA。</summary>
    public static IEnumerable<OperationRegistration> Registrations(string? knowledgeDirectory = null, ManagerPlanStore? plans = null)
    {
        var store = plans ?? new ManagerPlanStore();
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string mapping = "{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"recordedRoot\":{\"type\":\"string\",\"minLength\":1},\"localRoot\":{\"type\":\"string\",\"minLength\":1}},\"required\":[\"recordedRoot\",\"localRoot\"]}}";
        var product = "\"product\":" + text + ",\"version\":" + text;
        yield return Entry("knowledge.import.preview", "Freeze selected source content or inspect an existing SQLite before version-isolated import.",
            product + ",\"role\":{\"type\":\"string\",\"enum\":[\"official\",\"custom\"]},\"name\":" + text +
            ",\"database\":" + text + ",\"manifest\":" + text + ",\"pmlLib\":" + text + ",\"pmlUi\":" + text + ",\"webHelp\":" + text + ",\"mappings\":" + mapping,
            "\"product\",\"version\",\"role\",\"name\"", false);
        yield return Entry("knowledge.import.apply", "Build or consistently snapshot a frozen import, validate, and atomically publish without replacing another library.",
            "\"planId\":" + text + ",\"sha256\":" + text, "\"planId\",\"sha256\"", true);
        yield return Entry("knowledge.inspect", "Read-only original database check using a private SQLite snapshot; returns separate structural, FTS and source evidence.",
            "\"database\":" + text + ",\"manifest\":" + text + ",\"mappings\":" + mapping, "\"database\"", false);
        yield return Entry("knowledge.libraries.list", "List already published product/version libraries without rebuilding or importing.", product, "", false);
        yield return Entry("knowledge.library.search", "Search one explicitly identified product/version library; results carry library identity and chunk IDs.",
            product + ",\"name\":" + text + ",\"query\":" + text + ",\"topK\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":50},\"checkSources\":{\"type\":\"boolean\"}", "\"product\",\"version\",\"name\",\"query\"", false);
        yield return Entry("knowledge.library.chunk", "Read a chunk with its database identity, historical source and explicit local mapping.",
            product + ",\"name\":" + text + ",\"chunkId\":{\"type\":\"integer\",\"minimum\":1}", "\"product\",\"version\",\"name\",\"chunkId\"", false);

        OperationRegistration Entry(string id, string description, string properties, string required, bool write)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, description, schema.RootElement.Clone(), RequiresWrite: write),
                new DelegateOperationHandler((operation, input, _, token) => {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var root = knowledgeDirectory ?? UserDataPaths.KnowledgeDirectory();
                        JsonElement result;
                        switch (operation)
                        {
                            case "knowledge.import.preview":
                                var request = input.Deserialize(ManagerJsonContext.Default.KnowledgeImportRequest) ?? throw new ArgumentException("缺少导入请求。");
                                var plan = new KnowledgeImportService(root, cancellationToken: token).Preview(request);
                                var reference = store.Save("knowledge.import", plan, ManagerJsonContext.Default.KnowledgeImportPlan,
                                    JsonSerializer.SerializeToElement(plan, ManagerJsonContext.Default.KnowledgeImportPlan));
                                result = JsonSerializer.SerializeToElement(reference, ManagerJsonContext.Default.ManagerPlanReference); break;
                            case "knowledge.import.apply":
                                var frozen = store.Read("knowledge.import", S(input, "planId")!, S(input, "sha256")!, ManagerJsonContext.Default.KnowledgeImportPlan);
                                result = JsonSerializer.SerializeToElement(new KnowledgeImportService(root, cancellationToken: token).Apply(frozen), ManagerJsonContext.Default.KnowledgeImportReceipt); break;
                            case "knowledge.inspect":
                                var mappings = input.TryGetProperty("mappings", out var map) ? map.Deserialize(ManagerJsonContext.Default.KnowledgeSourceMappingArray) : [];
                                result = JsonSerializer.SerializeToElement(KnowledgeInspector.Inspect(S(input, "database")!, mappings, S(input, "manifest"), token), KnowledgeLibraryJson.Default.KnowledgeInspectionReport); break;
                            case "knowledge.libraries.list":
                                result = JsonSerializer.SerializeToElement(KnowledgeLibraries.List(root, S(input, "product"), S(input, "version")), KnowledgeLibraryJson.Default.RegisteredKnowledgeLibraryArray); break;
                            default:
                                var library = KnowledgeLibraries.List(root, S(input, "product"), S(input, "version"))
                                    .SingleOrDefault(l => l.Registration.Name == S(input, "name")) ?? throw new ArgumentException("找不到所选产品版本资料库。");
                                using (var file = new FileStream(library.Database, FileMode.Open, FileAccess.Read, FileShare.Read))
                                    if (!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)).Equals(library.Registration.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                                        throw new IOException("资料库内容与启用时身份不一致，请重新检查并另名导入。");
                                if (operation == "knowledge.library.search")
                                {
                                    var check = input.TryGetProperty("checkSources", out var requestedCheck) && requestedCheck.GetBoolean();
                                    var report = check ? KnowledgeInspector.Inspect(library.Database, library.Registration.Mappings, cancellationToken: token) : null;
                                    if (report is { CanEnable: false }) throw new InvalidDataException("已发布资料库深检查未通过。");
                                    var search = new KnowledgeRepository(root).Search(S(input, "query")!, null, library.Database,
                                        input.TryGetProperty("topK", out var top) ? top.GetInt32() : 8, null, null, null);
                                    result = JsonSerializer.SerializeToElement(new ManagerKnowledgeSearch(library,
                                        search, library.Registration.Mappings, check ? "checked-now" : "not-rechecked",
                                        report), ManagerJsonContext.Default.ManagerKnowledgeSearch);
                                }
                                else
                                {
                                    var chunk = new KnowledgeRepository(root).GetChunk(input.GetProperty("chunkId").GetInt64(), null, library.Database);
                                    if (Path.IsPathFullyQualified(chunk.RecordedPath))
                                    {
                                        var mapped = KnowledgeInspector.Resolve(chunk.RecordedPath, library.Registration.Mappings);
                                        chunk = chunk with { ResolvedPath = File.Exists(mapped) ? mapped : null };
                                    }
                                    result = JsonSerializer.SerializeToElement(new ManagerKnowledgeChunk(library, chunk), ManagerJsonContext.Default.ManagerKnowledgeChunk);
                                }
                                break;
                        }
                        return Task.FromResult(OperationResult.Ok(operation, result));
                    }
                    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or KnowledgeException or SqliteException)
                    { return Task.FromResult(OperationResult.Fail(operation, error is ArgumentException or JsonException or InvalidDataException ? 2 : 7,
                        "KNOWLEDGE_OPERATION_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                }));
        }
    }
    private static string? S(JsonElement input, string key) => input.TryGetProperty(key, out var value) ? value.GetString() : null;
}

/// <summary>检索身份、原始来源坐标与映射一起返回，不把 chunk ID 当作全局 ID。</summary>
public sealed record ManagerKnowledgeSearch(RegisteredKnowledgeLibrary Library, KnowledgeSearchResult Result,
    KnowledgeSourceMapping[] Mappings, string SourceEvidenceState, KnowledgeInspectionReport? Inspection);
/// <summary>数据库身份和库内片段编号一起构成完整引用。</summary>
public sealed record ManagerKnowledgeChunk(RegisteredKnowledgeLibrary Library, KnowledgeChunkDetail Chunk);
