using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Knowledge;

public sealed record KnowledgeRootRecord(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path);

public sealed record KnowledgeRootStats(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("fileCount")] long FileCount,
    [property: JsonPropertyName("totalBytes")] long TotalBytes,
    [property: JsonPropertyName("maxMtimeUtc")] DateTime MaxMtimeUtc);

public sealed record KnowledgeDbSummary(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("manifest")] string? Manifest,
    [property: JsonPropertyName("schemaVersion")] string? SchemaVersion,
    [property: JsonPropertyName("generator")] string? Generator,
    [property: JsonPropertyName("builtAtUtc")] string? BuiltAtUtc,
    [property: JsonPropertyName("totalSources")] long TotalSources,
    [property: JsonPropertyName("totalChunks")] long TotalChunks,
    [property: JsonPropertyName("callRefs")] long CallRefs,
    [property: JsonPropertyName("ftsRows")] long FtsRows,
    [property: JsonPropertyName("roots")] IReadOnlyList<KnowledgeRootRecord> Roots,
    [property: JsonPropertyName("freshness")] KnowledgeFreshnessReport? Freshness);

public sealed record KnowledgeFreshnessReport(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("details")]
    IReadOnlyList<KnowledgeRootFreshness> Details);

public sealed record KnowledgeRootFreshness(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("recordedFileCount")] long? RecordedFileCount,
    [property: JsonPropertyName("currentFileCount")] long? CurrentFileCount,
    [property: JsonPropertyName("recordedTotalBytes")] long? RecordedTotalBytes,
    [property: JsonPropertyName("currentTotalBytes")] long? CurrentTotalBytes);

public sealed record KnowledgeBuildResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("manifest")] string Manifest,
    [property: JsonPropertyName("pmlFiles")] long PmlFiles,
    [property: JsonPropertyName("pmlChunks")] long PmlChunks,
    [property: JsonPropertyName("htmlFiles")] long HtmlFiles,
    [property: JsonPropertyName("htmlChunks")] long HtmlChunks,
    [property: JsonPropertyName("htmlDuplicateChunks")] long HtmlDuplicateChunks,
    [property: JsonPropertyName("skippedBinaryFiles")] long SkippedBinaryFiles,
    [property: JsonPropertyName("totalSources")] long TotalSources,
    [property: JsonPropertyName("totalChunks")] long TotalChunks,
    [property: JsonPropertyName("callRefs")] long CallRefs,
    [property: JsonPropertyName("elapsedSeconds")] double ElapsedSeconds,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);

public sealed record KnowledgeCheckResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("exists")] bool Exists,
    [property: JsonPropertyName("integrity")] string? Integrity,
    [property: JsonPropertyName("schemaOk")] bool? SchemaOk,
    [property: JsonPropertyName("schemaVersion")] string? SchemaVersion,
    [property: JsonPropertyName("generator")] string? Generator,
    [property: JsonPropertyName("builtAtUtc")] string? BuiltAtUtc,
    [property: JsonPropertyName("totalSources")] long TotalSources,
    [property: JsonPropertyName("totalChunks")] long TotalChunks,
    [property: JsonPropertyName("callRefs")] long CallRefs,
    [property: JsonPropertyName("ftsRows")] long FtsRows,
    [property: JsonPropertyName("roots")] IReadOnlyList<KnowledgeRootRecord> Roots,
    [property: JsonPropertyName("note")] string? Note);

public sealed record KnowledgeSearchHit(
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("chunkId")] long ChunkId,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("sourceType")] string SourceType,
    [property: JsonPropertyName("module")] string Module,
    [property: JsonPropertyName("chunkType")] string ChunkType,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("startLine")] int? StartLine,
    [property: JsonPropertyName("endLine")] int? EndLine,
    [property: JsonPropertyName("matchedBy")] IReadOnlyList<string> MatchedBy,
    [property: JsonPropertyName("callTargets")] IReadOnlyList<string> CallTargets,
    [property: JsonPropertyName("excerpt")] string Excerpt);

public sealed record KnowledgeSearchResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("hits")] IReadOnlyList<KnowledgeSearchHit> Hits,
    [property: JsonPropertyName("plan")]
    IReadOnlyList<QueryVariantDto> Plan,
    [property: JsonPropertyName("note")] string? Note);

public sealed record QueryVariantDto(
    [property: JsonPropertyName("fts")] string Fts,
    [property: JsonPropertyName("weight")] double Weight,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record KnowledgeChunkDetail(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("chunkId")] long ChunkId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("chunkType")] string ChunkType,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("startLine")] int? StartLine,
    [property: JsonPropertyName("endLine")] int? EndLine,
    [property: JsonPropertyName("sourceType")] string SourceType,
    [property: JsonPropertyName("module")] string Module,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("recordedPath")] string RecordedPath,
    [property: JsonPropertyName("resolvedPath")] string? ResolvedPath,
    [property: JsonPropertyName("callTargets")] IReadOnlyList<string> CallTargets,
    [property: JsonPropertyName("note")] string? Note);

public sealed record KnowledgeError(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("hint")] string? Hint);
