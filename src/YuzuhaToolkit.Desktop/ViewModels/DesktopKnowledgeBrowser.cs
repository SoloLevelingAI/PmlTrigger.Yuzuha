using YuzuhaToolkit.Knowledge;

namespace YuzuhaToolkit.Desktop;

/// <summary>GUI 复用现有分层知识仓库，不要求打开另一个 MCP 客户端。</summary>
/// <param name="directory">明确选择的本机知识库目录。</param>
internal sealed class DesktopKnowledgeBrowser(string directory)
{
    private readonly KnowledgeRepository repository = new(directory);
    /// <summary>在本窗口搜索完整内置指南及用户选择的本机资料。</summary>
    /// <param name="query">用户输入的检索词，空值列出内置指南。</param>
    /// <param name="includeSupplemental">是否同时搜索已存在的补充知识库。</param>
    internal KnowledgeBrowseRow[] Search(string query, bool includeSupplemental)
    {
        var rows = new List<KnowledgeBrowseRow>();
        foreach (var layer in repository.SearchLayers(query, 20, includeSupplemental))
        {
            if (layer.Error is not null) rows.Add(new(layer.Role, layer.Database, "读取失败", null, null, "", layer.Error));
            foreach (var guide in layer.Guides ?? []) rows.Add(new("builtin", "", guide.Title, guide.Id, null, "随版本内置", guide.Content));
            foreach (var hit in layer.Result?.Hits ?? []) rows.Add(new(layer.Role, layer.Database, string.IsNullOrEmpty(hit.Symbol) ? hit.RelativePath : hit.Symbol, null, hit.ChunkId, hit.RelativePath + ":" + hit.StartLine, hit.Excerpt));
        }
        return rows.ToArray();
    }
    /// <summary>按数据库路径与片段 ID 联合定位正文，不把相关性当验证证据。</summary>
    /// <param name="row">包含数据库与片段身份的同次命中。</param>
    internal KnowledgeBrowseDetail Read(KnowledgeBrowseRow row)
    {
        if (row.GuideId is not null) return new(row.Excerpt, "docs/builtin/" + row.GuideId + ".md", 1, "随版本提供的使用指南；非本次实测证据");
        if (row.ChunkId is null) return new(row.Excerpt, row.Database, null, "读取失败");
        var detail = repository.GetChunk(row.ChunkId.Value, null, row.Database);
        return new(detail.Content, detail.ResolvedPath ?? detail.RecordedPath, detail.StartLine, "未关联实测证据");
    }
}

/// <summary>检索命中保留来源角色、数据库及片段身份。</summary>
/// <param name="Role">builtin、project、official、experience 或 custom。</param>
/// <param name="Database">本机数据库路径；内置指南为空。</param>
/// <param name="Title">符号或资料标题。</param>
/// <param name="GuideId">完整内置指南 ID。</param>
/// <param name="ChunkId">仅在指定数据库内唯一的片段编号。</param>
/// <param name="Location">源码相对位置与行号。</param>
/// <param name="Excerpt">摘要、完整内置正文或明确错误。</param>
internal sealed record KnowledgeBrowseRow(string Role, string Database, string Title, string? GuideId, long? ChunkId, string Location, string Excerpt);
/// <summary>GUI 正文和来源坐标；证据状态与相关性分别表达。</summary>
/// <param name="Content">完整正文。</param>
/// <param name="SourcePath">来源文件或内置文档逻辑路径。</param>
/// <param name="StartLine">可取得时的源码首行。</param>
/// <param name="Verification">实测证据关联状态。</param>
internal sealed record KnowledgeBrowseDetail(string Content, string SourcePath, int? StartLine, string Verification);
