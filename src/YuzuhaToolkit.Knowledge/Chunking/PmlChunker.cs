using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Knowledge.Chunking;

/// <summary>带符号、行范围和调用目标的 PML 语法片段。</summary>
/// <param name="ChunkType">函数、方法、窗体或 HTML 章节等片段类别。</param>
/// <param name="Symbol">从源码提取的函数、方法或主题符号。</param>
/// <param name="StartLine">源文件中从 1 开始的首行行号。</param>
/// <param name="EndLine">源文件中从 1 开始的末行行号，包含该行。</param>
/// <param name="Content">完整正文或待保存的经验内容。</param>
/// <param name="CallTargets">从片段正文提取的被调用符号。</param>
public sealed record PmlChunk(
    string ChunkType,
    string Symbol,
    int StartLine,
    int EndLine,
    string Content,
    IReadOnlyList<string> CallTargets);

/// <summary>一个 PML 文件的语法片段及头部元数据。</summary>
/// <param name="Chunks">保留源顺序及行范围的正文片段集合。</param>
/// <param name="Metadata">从 PML 文件头提取的描述性字段。</param>
public sealed record PmlFileChunks(
    IReadOnlyList<PmlChunk> Chunks,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
///     Syntax-aware PML chunking ported from pml_knowledge_proto
///     (build_semantic_chunks.py). Blocks are cut on define/setup boundaries,
///     long blocks are split only on line boundaries, and licence banners are
///     reduced to header metadata instead of being repeated in every chunk.
/// </summary>
public static partial class PmlChunker
{
    private const int DefaultMaxChars = 6000;

    [GeneratedRegex(
        @"^\s*(define\s+(?<dtype>function|method|object)\s+(?<dname>[^\s(]+)(?<args>\([^)]*\))?" +
        @"|setup\s+(?<stype>form|object)\s+(?<sname>[^\s]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockStartRegex();

    [GeneratedRegex(@"^\s*endfunction\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndFunctionRegex();

    [GeneratedRegex(@"^\s*endmethod\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndMethodRegex();

    [GeneratedRegex(@"^\s*(?:endobject|end)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndObjectRegex();

    [GeneratedRegex(
        @"^\s*--\s*(File|Type|Group|Keyword|Module|Description|Author|Created):\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex HeaderFieldRegex();

    [GeneratedRegex(
        @"(!![A-Za-z_]\w*(?:\.[A-Za-z_]\w*)?|!this\.[A-Za-z_]\w*)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CallRefRegex();

    private static readonly Dictionary<string, Regex> BlockEndRegexes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["function"] = EndFunctionRegex(),
            ["method"] = EndMethodRegex(),
            ["object"] = EndObjectRegex()
        };

    /// <summary>按原始语法或标题边界切分源码，保留行范围与可追踪的来源信息。</summary>
    /// <param name="text">待切分、解析或规范化的原始正文。</param>
    /// <param name="relativePath">相对于该来源根目录的文件路径。</param>
    /// <param name="parser">PML 文件种类所选用的语法切分策略。</param>
    public static PmlFileChunks Chunk(string text, string relativePath, string parser)
    {
        var lines = text.Split('\n');
        var metadata = ExtractHeader(text);

        var formAwareParser = parser is "pml_form" or "pml_object" or "legacy_pml";
        List<(int index, string blockType, string symbol, string signature)> starts = new();
        for (var index = 0; index < lines.Length; index++)
        {
            var match = BlockStartRegex().Match(lines[index]);
            if (!match.Success)
                continue;

            // A function may dynamically create a form; that is part of the
            // function body unless this parser already understands blocks.
            var setupType = match.Groups["stype"].Value;
            if (setupType.Length > 0 && !formAwareParser)
                continue;

            var blockType = match.Groups["dtype"].Success
                ? match.Groups["dtype"].Value.ToLowerInvariant()
                : setupType.Length > 0 ? setupType.ToLowerInvariant() : "block";
            var symbol = (match.Groups["dname"].Success
                    ? match.Groups["dname"].Value
                    : match.Groups["sname"].Value)
                .Trim();
            starts.Add((index, blockType, symbol, lines[index].Trim()));
        }

        List<PmlChunk> emitted = new();
        if (starts.Count > 0 && starts[0].index > 0)
        {
            metadata.TryGetValue("description", out var description);
            metadata.TryGetValue("type", out var typeValue);
            metadata.TryGetValue("module", out var moduleValue);
            metadata.TryGetValue("keyword", out var keywordValue);
            var compact = string.Join("\n", new[]
                {
                    $"File: {relativePath}",
                    $"Type: {typeValue ?? parser}",
                    $"Module: {moduleValue ?? string.Empty}",
                    $"Keyword: {keywordValue ?? string.Empty}",
                    $"Description: {description ?? string.Empty}"
                }
                .Where(field => field.Split(':', 2)[1].Trim().Length > 0));

            if (compact.Length > 0)
                emitted.Add(new PmlChunk(
                    "pml_overview",
                    string.Empty,
                    1,
                    starts[0].index,
                    compact,
                    Array.Empty<string>()));
        }

        for (var index = 0; index < starts.Count; index++)
        {
            var (start, blockType, symbol, signature) = starts[index];
            var nextStart = index + 1 < starts.Count
                ? starts[index + 1].index
                : lines.Length;
            var end = nextStart;
            if (BlockEndRegexes.TryGetValue(blockType, out var endRegex))
                for (var scan = start + 1; scan < nextStart; scan++)
                    if (endRegex.IsMatch(lines[scan]))
                    {
                        end = scan + 1;
                        break;
                    }

            var blockLines = lines[start..end];
            var calls = CallRefRegex().Matches(string.Join("\n", blockLines))
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(call => call, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prefix = new[]
            {
                "Source: PML",
                $"File: {relativePath}",
                $"Kind: {blockType}",
                $"Symbol: {symbol}",
                $"Signature: {signature}"
            };
            var room = Math.Max(
                1000,
                DefaultMaxChars - prefix.Sum(field => field.Length + 1));

            var parts = TextWindows.SplitLongLines(
                blockLines,
                start + 1,
                room);
            for (var partNumber = 0; partNumber < parts.Count; partNumber++)
            {
                var part = parts[partNumber];
                var partLabel = part.Body.Length >= room * 0.8
                    ? new[] { $"Part: {partNumber + 1}" }
                    : Array.Empty<string>();
                var content = string.Join(
                    "\n",
                    prefix.Concat(partLabel).Concat(new[] { string.Empty, part.Body }));
                emitted.Add(new PmlChunk(
                    $"pml_{blockType}",
                    symbol,
                    part.StartLine,
                    part.EndLine,
                    content,
                    calls));
            }
        }

        if (starts.Count == 0)
        {
            // Old macros and command files: preserve header + line sections.
            var parts = TextWindows.SplitLongLines(
                lines,
                1,
                DefaultMaxChars - 200);
            for (var partNumber = 0; partNumber < parts.Count; partNumber++)
            {
                var part = parts[partNumber];
                if (part.Body.Trim().Length < 20)
                    continue;
                var content =
                    $"Source: PML\nFile: {relativePath}\nKind: {parser}\nPart: {partNumber + 1}\n\n{part.Body}";
                var calls = CallRefRegex().Matches(part.Body)
                    .Select(match => match.Groups[1].Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(call => call, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                emitted.Add(new PmlChunk(
                    "pml_macro_section",
                    string.Empty,
                    part.StartLine,
                    part.EndLine,
                    content,
                    calls));
            }
        }

        return new PmlFileChunks(emitted, metadata);
    }

    private static Dictionary<string, string> ExtractHeader(string text)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        var probe = text.Length > 20000 ? text[..20000] : text;
        foreach (Match match in HeaderFieldRegex().Matches(probe))
        {
            var value = match.Groups[2].Value.Trim();
            if (value.Length > 0 &&
                !metadata.ContainsKey(match.Groups[1].Value.ToLowerInvariant()))
                metadata[match.Groups[1].Value.ToLowerInvariant()] = value;
        }

        return metadata;
    }
}
