using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Knowledge.Search;

/// <summary>可重现的 FTS5 查询变体及排序权重。</summary>
/// <param name="FtsExpression">已转义的 FTS5 查询表达式。</param>
/// <param name="Weight">该查询变体参与融合排序的权重。</param>
/// <param name="Reason">用户确认状态变化的原因或验证依据。</param>
public sealed record QueryVariant(string FtsExpression, double Weight, string Reason);

/// <summary>
///     Deterministic FTS5 query planning ported from pml_knowledge_proto
///     (rag_search.py). Every variant is inspectable and reproducible; the
///     small domain-term table bridges Chinese task wording to the English
///     and PML vocabulary of the corpus.
/// </summary>
public static partial class QueryPlanner
{
    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_.!]*|[\u4e00-\u9fff]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^[\u4e00-\u9fff]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CjkOnlyRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    /// <summary>返回识别创建意图的编译期正则表达式。</summary>
    [GeneratedRegex(@"创建|新建|建立|生成|建一个|下建|怎么建|如何建|create|new",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex CreateIntentRegex();

    /// <summary>返回识别解释或资料意图的编译期正则表达式。</summary>
    [GeneratedRegex(@"什么是|含义|说明|文档|属性|reference|manual|explain",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex DocIntentRegex();

    /// <summary>返回识别实现与代码意图的编译期正则表达式。</summary>
    [GeneratedRegex(@"实现|源码|代码|示例|怎么建|如何建|create|new|implementation|source",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex CodeIntentRegex();

    /// <summary>判断检索语句是否匹配一个领域词组的纯谓词。</summary>
    /// <param name="query">PML、英文或中文的任务描述或检索文本。</param>
    private delegate bool TermPredicate(string query);

    private static readonly (TermPredicate Match, string[] Terms)[] DomainTerms =
    {
        (q => q.Contains("螺栓") || Regex.IsMatch(q, "bolt", RegexOptions.IgnoreCase),
            new[] { "bolt", "SBOL", "MBOL" }),
        (q => q.Contains("子设备") || Regex.IsMatch(q, "sube", RegexOptions.IgnoreCase),
            new[] { "SUBE" }),
        (q => q.Contains("设备") || Regex.IsMatch(q, "equipment|equi", RegexOptions.IgnoreCase),
            new[] { "EQUI", "EQUIP" }),
        (q => q.Contains("圆柱") || q.Contains("柱体") ||
              Regex.IsMatch(q, "cylinder|cyli", RegexOptions.IgnoreCase),
            new[] { "CYLI", "DIA", "HEI" }),
        (q => q.Contains("方盒") || q.Contains("方块") ||
              Regex.IsMatch(q, "box", RegexOptions.IgnoreCase),
            new[] { "BOX", "XLEN", "YLEN", "ZLEN" }),
        (q => q.Contains("六角") || q.Contains("多边形") || q.Contains("拉伸") || q.Contains("挤出") ||
              Regex.IsMatch(q, @"hexagon|polygon|extrusion|\bextr\b", RegexOptions.IgnoreCase),
            new[] { "EXTR", "LOOP", "VERT", "HEI" }),
        (q => q.Contains("表单") || Regex.IsMatch(q, "form", RegexOptions.IgnoreCase),
            new[] { "FORM", "setup form" }),
        (q => q.Contains("回调") || Regex.IsMatch(q, "callback", RegexOptions.IgnoreCase),
            new[] { "callback" }),
        (q => q.Contains("方法") || Regex.IsMatch(q, "method", RegexOptions.IgnoreCase),
            new[] { "method", "define method" }),
        (q => q.Contains("函数") || Regex.IsMatch(q, "function", RegexOptions.IgnoreCase),
            new[] { "function", "define function" }),
        (q => q.Contains("当前元素") || Regex.IsMatch(q, @"current element|\bce\b", RegexOptions.IgnoreCase),
            new[] { "CE", "DBREF" }),
        (q => q.Contains("调用者") || q.Contains("调用关系") ||
              Regex.IsMatch(q, @"callers?|callees?", RegexOptions.IgnoreCase),
            new[] { "CALL" })
    };

    /// <summary>从原始术语、中文领域词和创建意图生成去重的确定性 FTS5 查询计划。</summary>
    /// <param name="query">PML、英文或中文的任务描述或检索文本。</param>
    public static List<QueryVariant> Plan(string query)
    {
        var rawTokens = WordRegex()
            .Matches(query)
            .Select(match => match.Value)
            .Where(token => !CjkOnlyRegex().IsMatch(token))
            .ToList();

        List<string[]> domainGroups = new();
        foreach (var (match, terms) in DomainTerms)
            if (match(query))
                domainGroups.Add(terms);

        List<QueryVariant> variants = new();
        var rawDeduped = Dedupe(rawTokens).ToList();
        if (rawDeduped.Count > 0)
        {
            variants.Add(new QueryVariant(
                string.Join(" AND ", rawDeduped.Select(QuoteFts)),
                1.25,
                "原始术语"));
            if (rawDeduped.Count > 1)
                variants.Add(new QueryVariant(
                    QuoteFts(string.Join(" ", rawDeduped)),
                    1.75,
                    "原始精确短语"));
        }

        foreach (var terms in domainGroups)
        {
            var expression = string.Join(" OR ", terms.Select(QuoteFts));
            if (terms.Length > 1)
                expression = $"({expression})";
            variants.Add(new QueryVariant(expression, 0.85, $"领域词:{string.Join("/", terms)}"));
        }

        var domain = Dedupe(domainGroups.SelectMany(terms => terms)).ToList();
        if (CreateIntentRegex().IsMatch(query))
        {
            foreach (var pmlType in new[] { "SUBE", "EQUI", "CYLI", "BOX", "EXTR" })
                if (domain.Contains(pmlType, StringComparer.OrdinalIgnoreCase))
                    variants.Add(new QueryVariant(
                        $"{QuoteFts("NEW")} AND {QuoteFts(pmlType)}",
                        1.55,
                        $"创建模式:NEW {pmlType}"));

            if (domain.Any(term => term.Equals("EQUIP", StringComparison.OrdinalIgnoreCase)))
                variants.Add(new QueryVariant("\"NEW\" AND \"EQUIP\"", 1.55, "创建模式:NEW EQUIP"));

            if (domain.Any(term => term.Equals("EXTR", StringComparison.OrdinalIgnoreCase)))
            {
                variants.Add(new QueryVariant(
                    "\"NEW\" AND \"EXTR\" AND \"LOOP\"", 1.90, "创建模式:NEW EXTR LOOP"));
                variants.Add(new QueryVariant(
                    "\"NEW\" AND \"EXTR\" AND \"LOOP\" AND \"VERT\"",
                    2.10,
                    "创建模式:NEW EXTR LOOP VERT"));
                variants.Add(new QueryVariant(
                    "\"NEW EXTR\" AND \"NEW LOOP\" AND \"NEW VERT\"",
                    2.60,
                    "完整轮廓:NEW EXTR/LOOP/VERT"));
            }

            if (domain.Any(term => term.Equals("bolt", StringComparison.OrdinalIgnoreCase)))
            {
                variants.Add(new QueryVariant("\"NEW\" AND \"SUBE\"", 1.20, "螺栓几何候选:SUBE"));
                variants.Add(new QueryVariant("\"NEW\" AND \"CYLI\"", 1.05, "螺栓几何候选:CYLI"));
                variants.Add(new QueryVariant("\"NEW\" AND \"EXTR\"", 1.20, "螺栓六角头候选:EXTR"));
            }
        }

        if (domain.Any(term => term.Equals("callback", StringComparison.OrdinalIgnoreCase)) &&
            domain.Any(term => term.Equals("method", StringComparison.OrdinalIgnoreCase)))
            variants.Add(new QueryVariant("\"callback\" AND \"method\"", 1.45, "表单回调模式"));

        if (variants.Count == 0)
            variants.Add(new QueryVariant(QuoteFts(query.Trim()), 1.0, "原始查询"));

        Dictionary<string, QueryVariant> unique = new(StringComparer.Ordinal);
        foreach (var variant in variants)
            if (!unique.TryGetValue(variant.FtsExpression, out var existing) ||
                variant.Weight > existing.Weight)
                unique[variant.FtsExpression] = variant;

        return unique.Values.ToList();
    }

    /// <summary>把用户术语作为 FTS5 字面量加双引号，并转义其中的双引号。</summary>
    /// <param name="term">需要作为 FTS5 字面量处理的单个术语。</param>
    public static string QuoteFts(string term)
    {
        return "\"" + term.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>保留原始顺序并按不区分大小写规则去重查询术语。</summary>
    /// <param name="items">按输入顺序排列、需要规范化的条目。</param>
    public static List<string> Dedupe(IEnumerable<string> items)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> result = new();
        foreach (var item in items)
            if (item.Length > 0 && seen.Add(item))
                result.Add(item);

        return result;
    }

    /// <summary>
    ///     Excerpt centered on the first matched term, at most
    ///     <paramref name="limit"/> characters, ported from
    ///     compact_excerpt().
    /// </summary>
    /// <param name="content">完整正文或待保存的经验内容。</param>
    /// <param name="terms">按输入顺序排列的查询术语集合。</param>
    /// <param name="limit">返回数量上限，内部会限制到有效范围。</param>
    public static string CompactExcerpt(string content, IEnumerable<string> terms, int limit = 520)
    {
        var flat = WhitespaceRegex().Replace(content, " ").Trim();
        var positions = terms
            .Where(term => term.Length > 0)
            .Select(term => flat.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(position => position >= 0)
            .ToList();
        var start = Math.Max(0, (positions.Count > 0 ? positions.Min() : 0) - 100);
        var excerpt = flat.Substring(start, Math.Min(limit, flat.Length - start));
        if (start > 0)
            excerpt = "…" + excerpt;
        if (start + limit < flat.Length)
            excerpt += "…";
        return excerpt;
    }
}
