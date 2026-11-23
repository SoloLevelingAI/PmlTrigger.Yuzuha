using System.Xml.Linq;
using Core30.Core;

namespace Core30.Knowledge;

public static partial class KnowledgeFeatures
{
    /// <summary>Matches guide titles and aliases, then reads the packaged text.</summary>
    private static CoreData SearchBuiltInGuides(FeatureContext context, CoreData request)
    {
        var guides = FindGuides(Value(request, "query").Trim(), (int?)request.Field("limit") ?? 20);
        return new CoreData("result", new CoreField("count", guides.Count),
            guides.Select(guide => new CoreData("guide",
                new CoreField("id", guide.Id), new CoreField("title", guide.Title),
                new CoreField("priority", guide.Priority), new CoreData("content", guide.Content))));
    }

    private static readonly GuideIndex[] GuideIndexes =
    [
        new("read-current-element", "查询当前元素 / Read current element", 10, "当前元素 CE current element"),
        new("read-dbref", "查询数据库引用 / Read DBREF", 20, "DBREF DB reference 引用"),
        new("read-global", "查询全局对象 / Read global", 30, "global 全局 数组"),
        new("connection", "核对连接证据 / Connection evidence", 40, "connection session 会话 身份"),
        new("environment-setup", "准备工作区 / Workspace setup", 50, "setup environment 配置"),
        new("execute", "登记与执行边界 / Registration and execution", 60, "execute register PML 登记 执行"),
        new("overview", "Core 使用导览 / Core overview", 70, "overview help 导览"),
    ];

    private static IReadOnlyList<Guide> FindGuides(string query, int limit) =>
        GuideIndexes.Where(guide => MatchesGuide(guide, query)).Take(limit).Select(ReadGuide).ToArray();

    private static bool MatchesGuide(GuideIndex index, string query) =>
        $"{index.Id} {index.Title} {index.Aliases}".Contains(query, StringComparison.OrdinalIgnoreCase);

    private static Guide ReadGuide(GuideIndex index)
    {
        using var input = typeof(KnowledgeFeatures).Assembly.GetManifestResourceStream($"Core30.Guide.{index.Id}.md")
            ?? throw new InvalidDataException($"Guide {index.Id} is missing from the build.");
        using var reader = new StreamReader(input);
        return new Guide(index.Id, index.Title, index.Priority, reader.ReadToEnd());
    }

    private sealed record GuideIndex(string Id, string Title, int Priority, string Aliases);
    private sealed record Guide(string Id, string Title, int Priority, string Content);
}
