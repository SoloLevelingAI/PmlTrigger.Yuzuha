namespace YuzuhaToolkit.Knowledge.Chunking;

/// <summary>One line-range slice of a source file.</summary>
/// <param name="StartLine">源文件中从 1 开始的首行行号。</param>
/// <param name="EndLine">源文件中从 1 开始的末行行号，包含该行。</param>
/// <param name="Body">该章节清洗后的完整正文。</param>
public sealed record TextSlice(int StartLine, int EndLine, string Body);

/// <summary>
///     Line-window helpers ported from the pml_knowledge_proto Python
///     implementation so both builders produce comparable chunks.
/// </summary>
public static class TextWindows
{
    /// <summary>
    ///     Trims trailing whitespace and collapses runs of blank lines to a
    ///     single blank line.
    /// </summary>
    /// <param name="lines">按源文件顺序排列的文本行。</param>
    public static string NormalizeLines(IEnumerable<string> lines)
    {
        List<string> output = new();
        var blank = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                if (!blank)
                    output.Add(string.Empty);
                blank = true;
            }
            else
            {
                output.Add(line);
                blank = false;
            }
        }

        return string.Join("\n", output).Trim();
    }

    /// <summary>
    ///     Splits lines into windows of at most <paramref name="maxChars"/>
    ///     characters, breaking only on line boundaries and overlapping by
    ///     <paramref name="overlapLines"/> lines.
    /// </summary>
    /// <param name="lines">按源文件顺序排列的文本行。</param>
    /// <param name="startLine">源文件中从 1 开始的首行行号。</param>
    /// <param name="maxChars">每个分片的目标最大字符数；单行不会被截断。</param>
    /// <param name="overlapLines">相邻分片重复保留的源行数。</param>
    public static List<TextSlice> SplitLongLines(
        IReadOnlyList<string> lines,
        int startLine,
        int maxChars,
        int overlapLines = 4)
    {
        List<TextSlice> chunks = new();
        var position = 0;
        while (position < lines.Count)
        {
            var end = position;
            var used = 0;
            while (end < lines.Count &&
                   (end == position || used + lines[end].Length + 1 <= maxChars))
            {
                used += lines[end].Length + 1;
                end++;
            }

            chunks.Add(new TextSlice(
                startLine + position,
                startLine + end - 1,
                NormalizeLines(lines.Skip(position).Take(end - position))));

            if (end >= lines.Count)
                break;

            position = Math.Max(position + 1, end - overlapLines);
        }

        return chunks;
    }
}
