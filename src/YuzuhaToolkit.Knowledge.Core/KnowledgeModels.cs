using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Knowledge;

/// <summary>知识库清单中记录的来源类别与根目录。</summary>
/// <param name="Kind">来源目录或条目的类别标识。</param>
/// <param name="Path">当前操作要求的本机文件或目录路径。</param>
public sealed record KnowledgeRootRecord(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path);

/// <summary>索引源文件的数量、字节和最新修改时间统计。</summary>
/// <param name="Kind">来源目录或条目的类别标识。</param>
/// <param name="Path">当前操作要求的本机文件或目录路径。</param>
/// <param name="FileCount">该来源中符合索引规则的文件数。</param>
/// <param name="TotalBytes">该来源中被统计文件的字节总数。</param>
/// <param name="MaxMtimeUtc">来源文件最近修改时间的最大 UTC 值。</param>
public sealed record KnowledgeRootStats(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("fileCount")] long FileCount,
    [property: JsonPropertyName("totalBytes")] long TotalBytes,
    [property: JsonPropertyName("maxMtimeUtc")] DateTime MaxMtimeUtc);

/// <summary>本机知识库的路径、版本、来源、计数和可选新鲜度报告。</summary>
/// <param name="Name">该来源、变量或对象的稳定名称。</param>
/// <param name="Database">所操作的本机 SQLite 文件路径。</param>
/// <param name="Manifest">与数据库对应的来源清单文件路径。</param>
/// <param name="SchemaVersion">索引表结构或清单的架构版本。</param>
/// <param name="Generator">生成该索引的工具标识。</param>
/// <param name="BuiltAtUtc">索引构建完成的 UTC 时间文本。</param>
/// <param name="TotalSources">被记录的源文件总数。</param>
/// <param name="TotalChunks">可检索片段总数。</param>
/// <param name="CallRefs">提取到的 PML 调用引用数量。</param>
/// <param name="FtsRows">全文检索表的行数。</param>
/// <param name="Roots">构建时登记的来源目录及角色。</param>
/// <param name="Freshness">与当前来源比较后的新鲜度报告。</param>
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

/// <summary>索引清单与当前来源目录比较后的总体状态和逐项明细。</summary>
/// <param name="Status">服务器返回的状态快照；没有对应记录时可为空。</param>
/// <param name="Details">按来源目录列出的校验明细。</param>
public sealed record KnowledgeFreshnessReport(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("details")]
    IReadOnlyList<KnowledgeRootFreshness> Details);

/// <summary>一个来源目录的索引时与当前文件统计对比。</summary>
/// <param name="Kind">来源目录或条目的类别标识。</param>
/// <param name="Path">当前操作要求的本机文件或目录路径。</param>
/// <param name="Status">服务器返回的状态快照；没有对应记录时可为空。</param>
/// <param name="RecordedFileCount">上次索引记录的源文件数。</param>
/// <param name="CurrentFileCount">当前来源目录的源文件数。</param>
/// <param name="RecordedTotalBytes">上次索引记录的字节总数。</param>
/// <param name="CurrentTotalBytes">当前来源目录的字节总数。</param>
public sealed record KnowledgeRootFreshness(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("recordedFileCount")] long? RecordedFileCount,
    [property: JsonPropertyName("currentFileCount")] long? CurrentFileCount,
    [property: JsonPropertyName("recordedTotalBytes")] long? RecordedTotalBytes,
    [property: JsonPropertyName("currentTotalBytes")] long? CurrentTotalBytes);

/// <summary>知识库构建结果、处理计数、耗时和警告。</summary>
/// <param name="Ok">此次操作是否完成其声明的校验或写入。</param>
/// <param name="Database">所操作的本机 SQLite 文件路径。</param>
/// <param name="Manifest">与数据库对应的来源清单文件路径。</param>
/// <param name="PmlFiles">本次处理的 PML 文件数。</param>
/// <param name="PmlChunks">从 PML 生成的片段数。</param>
/// <param name="HtmlFiles">本次处理的 HTML 帮助文件数。</param>
/// <param name="HtmlChunks">从 HTML 生成的片段数。</param>
/// <param name="HtmlDuplicateChunks">被去重的 HTML 片段数。</param>
/// <param name="SkippedBinaryFiles">识别为二进制并跳过的文件数。</param>
/// <param name="TotalSources">被记录的源文件总数。</param>
/// <param name="TotalChunks">可检索片段总数。</param>
/// <param name="CallRefs">提取到的 PML 调用引用数量。</param>
/// <param name="ElapsedSeconds">本次构建的总耗时秒数。</param>
/// <param name="Warnings">不阻止构建完成但需要审阅的诊断列表。</param>
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

/// <summary>SQLite 完整性、Yuzuha 架构与来源清单校验结果。</summary>
/// <param name="Ok">此次操作是否完成其声明的校验或写入。</param>
/// <param name="Database">所操作的本机 SQLite 文件路径。</param>
/// <param name="Exists">目标数据库文件是否存在。</param>
/// <param name="Integrity">SQLite 完整性检查返回的状态文本。</param>
/// <param name="SchemaOk">是否存在并符合预期的 Yuzuha 索引结构。</param>
/// <param name="SchemaVersion">索引表结构或清单的架构版本。</param>
/// <param name="Generator">生成该索引的工具标识。</param>
/// <param name="BuiltAtUtc">索引构建完成的 UTC 时间文本。</param>
/// <param name="TotalSources">被记录的源文件总数。</param>
/// <param name="TotalChunks">可检索片段总数。</param>
/// <param name="CallRefs">提取到的 PML 调用引用数量。</param>
/// <param name="FtsRows">全文检索表的行数。</param>
/// <param name="Roots">构建时登记的来源目录及角色。</param>
/// <param name="Note">结果边界、诊断或下一步建议的说明。</param>
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

/// <summary>融合排序后的知识片段命中、来源坐标与匹配依据。</summary>
/// <param name="Rank">融合排序后从 1 开始的名次。</param>
/// <param name="ChunkId">仅在指定数据库内部唯一的片段编号。</param>
/// <param name="Score">融合排序得分，用于同一次查询内比较。</param>
/// <param name="SourceType">pml 或 webhelp 等来源类型过滤或标识。</param>
/// <param name="Module">PML 模块或资料目录范围。</param>
/// <param name="ChunkType">函数、方法、窗体或 HTML 章节等片段类别。</param>
/// <param name="Symbol">从源码提取的函数、方法或主题符号。</param>
/// <param name="RelativePath">相对于该来源根目录的文件路径。</param>
/// <param name="StartLine">源文件中从 1 开始的首行行号。</param>
/// <param name="EndLine">源文件中从 1 开始的末行行号，包含该行。</param>
/// <param name="MatchedBy">产生该命中的查询变体与匹配原因。</param>
/// <param name="CallTargets">从片段正文提取的被调用符号。</param>
/// <param name="Excerpt">供检索结果预览的正文摘要。</param>
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

/// <summary>一次只读检索的数据库、查询计划、命中列表和说明。</summary>
/// <param name="Ok">此次操作是否完成其声明的校验或写入。</param>
/// <param name="Query">PML、英文或中文的任务描述或检索文本。</param>
/// <param name="Database">所操作的本机 SQLite 文件路径。</param>
/// <param name="Hits">按最终得分排序的检索命中集合。</param>
/// <param name="Plan">已经构造的安装计划或查询计划数据。</param>
/// <param name="Note">结果边界、诊断或下一步建议的说明。</param>
public sealed record KnowledgeSearchResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("hits")] IReadOnlyList<KnowledgeSearchHit> Hits,
    [property: JsonPropertyName("plan")]
    IReadOnlyList<QueryVariantDto> Plan,
    [property: JsonPropertyName("note")] string? Note);

/// <summary>对外输出的 FTS5 表达式、权重和生成原因。</summary>
/// <param name="Fts">已转义的 FTS5 查询表达式。</param>
/// <param name="Weight">该查询变体参与融合排序的权重。</param>
/// <param name="Reason">用户确认状态变化的原因或验证依据。</param>
public sealed record QueryVariantDto(
    [property: JsonPropertyName("fts")] string Fts,
    [property: JsonPropertyName("weight")] double Weight,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>片段全文、符号、来源路径、行范围和调用目标。</summary>
/// <param name="Ok">此次操作是否完成其声明的校验或写入。</param>
/// <param name="ChunkId">仅在指定数据库内部唯一的片段编号。</param>
/// <param name="Content">完整正文或待保存的经验内容。</param>
/// <param name="ChunkType">函数、方法、窗体或 HTML 章节等片段类别。</param>
/// <param name="Symbol">从源码提取的函数、方法或主题符号。</param>
/// <param name="Title">资料或经验的可读标题。</param>
/// <param name="StartLine">源文件中从 1 开始的首行行号。</param>
/// <param name="EndLine">源文件中从 1 开始的末行行号，包含该行。</param>
/// <param name="SourceType">pml 或 webhelp 等来源类型过滤或标识。</param>
/// <param name="Module">PML 模块或资料目录范围。</param>
/// <param name="RelativePath">相对于该来源根目录的文件路径。</param>
/// <param name="RecordedPath">索引时记录的源文件位置。</param>
/// <param name="ResolvedPath">当前可解析到的实际源文件位置，不存在时为空。</param>
/// <param name="CallTargets">从片段正文提取的被调用符号。</param>
/// <param name="Note">结果边界、诊断或下一步建议的说明。</param>
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

/// <summary>可序列化的知识服务错误及后续操作提示。</summary>
/// <param name="Ok">此次操作是否完成其声明的校验或写入。</param>
/// <param name="Error">当前查询、连接或处理失败的可读原因。</param>
/// <param name="Hint">解释错误后可采取的后续操作。</param>
public sealed record KnowledgeError(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("hint")] string? Hint);
