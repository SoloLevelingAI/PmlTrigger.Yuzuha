using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Knowledge;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(KnowledgeDbSummary))]
[JsonSerializable(typeof(KnowledgeBuildResult))]
[JsonSerializable(typeof(KnowledgeCheckResult))]
[JsonSerializable(typeof(KnowledgeSearchResult))]
[JsonSerializable(typeof(KnowledgeChunkDetail))]
[JsonSerializable(typeof(KnowledgeError))]
[JsonSerializable(typeof(List<KnowledgeDbSummary>))]
[JsonSerializable(typeof(List<KnowledgeRootRecord>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class KnowledgeResponseJsonContext : JsonSerializerContext
{
}
