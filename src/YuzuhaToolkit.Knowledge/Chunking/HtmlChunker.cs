using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Knowledge.Chunking;

public sealed record HtmlChunk(
    string ChunkType,
    string Symbol,
    int? StartLine,
    int? EndLine,
    string Content);

public sealed record HtmlFileChunks(
    IReadOnlyList<HtmlChunk> Chunks,
    string Title,
    bool ParseError);

/// <summary>
///     WebHelp HTML section chunking ported from pml_knowledge_proto
///     (HelpParser / html_chunks.py). Sections start at h1–h6 or the
///     N#Heading classes used by the AVEVA WebWorks output; script, style,
///     noscript, and svg content is dropped; tables keep their cell text.
/// </summary>
public static partial class HtmlChunker
{
    private const int DefaultMaxChars = 5000;

    private static readonly HashSet<string> BlockTags = new(StringComparer.Ordinal)
    {
        "div", "p", "li", "tr", "td", "th", "pre", "br", "caption"
    };

    private static readonly HashSet<string> SkipTags = new(StringComparer.Ordinal)
    {
        "script", "style", "noscript", "svg"
    };

    private static readonly HashSet<string> HeadingTags = new(StringComparer.Ordinal)
    {
        "h1", "h2", "h3", "h4", "h5", "h6"
    };

    [GeneratedRegex(@"(?:^|\s)N\d+Heading(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingClassRegex();

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceRunRegex();

    [GeneratedRegex(@"\n\s*\n+", RegexOptions.CultureInvariant)]
    private static partial Regex BlankRunRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    public static HtmlFileChunks Chunk(string raw, string relativePath, string module)
    {
        var document = new HelpDocument();
        try
        {
            document.Parse(raw);
        }
        catch (Exception)
        {
            return new HtmlFileChunks(Array.Empty<HtmlChunk>(), string.Empty, true);
        }

        List<HtmlChunk> chunks = new();
        foreach (var section in document.Sections)
        {
            var cleanedLines = section.Content
                .Split('\n')
                .Select(line => line.Trim(' ', '|', '\t'))
                .Where(line => line.Length > 0);
            var cleaned = string.Join("\n", cleanedLines);
            if (cleaned.Length < 60)
                continue;

            var parts = TextWindows.SplitLongLines(
                cleaned.Split('\n'),
                section.StartLine,
                DefaultMaxChars - 500,
                2);
            for (var partNumber = 0; partNumber < parts.Count; partNumber++)
            {
                var part = parts[partNumber];
                var heading = section.Heading.Length > 0
                    ? section.Heading
                    : document.Title.Length > 0
                        ? document.Title
                        : Path.GetFileNameWithoutExtension(relativePath);
                var prefix = new List<string>
                {
                    "Source: AVEVA WebHelp",
                    $"Module: {module}",
                    $"Page: {document.Title}",
                    $"Section: {heading}",
                    $"File: {relativePath}"
                };
                if (partNumber > 0)
                    prefix.Add($"Part: {partNumber + 1}");

                chunks.Add(new HtmlChunk(
                    "html_section",
                    section.Heading,
                    section.StartLine,
                    null,
                    string.Join("\n", prefix.Concat(new[] { string.Empty, part.Body }))));
            }
        }

        return new HtmlFileChunks(chunks, document.Title, false);
    }

    /// <summary>SHA-256 of the UTF-8 text, used for global chunk dedup.</summary>
    public static string Sha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record HelpSection(string Heading, int StartLine, string Content);

    private sealed class HelpDocument
    {
        public string Title { get; private set; } = string.Empty;

        public List<HelpSection> Sections { get; } = new();

        private readonly List<string> _buffer = new();
        private readonly List<string> _headingBuffer = new();
        private string _currentHeading = string.Empty;
        private int _currentStart = 1;
        private bool _inTitle;
        private int _skipDepth;
        private int _headingDepth;
        private int _headingLine = 1;
        private int _line = 1;

        public void Parse(string raw)
        {
            var position = 0;
            while (position < raw.Length)
            {
                var open = raw.IndexOf('<', position);
                if (open < 0)
                {
                    HandleData(raw[position..]);
                    break;
                }

                if (open > position)
                    HandleData(raw[position..open]);

                if (raw.AsSpan(open).StartsWith("<!--".AsSpan(), StringComparison.Ordinal))
                {
                    var commentEnd = raw.IndexOf("-->", open + 4, StringComparison.Ordinal);
                    var jumpTo = commentEnd < 0 ? raw.Length : commentEnd + 3;
                    CountNewlines(raw, open, jumpTo);
                    position = jumpTo;
                    continue;
                }

                var close = raw.IndexOf('>', open + 1);
                if (close < 0)
                {
                    HandleData(raw[open..]);
                    break;
                }

                var tag = raw[(open + 1)..close];
                var closing = tag.StartsWith("/", StringComparison.Ordinal);
                if (closing)
                    HandleEndTag(tag[1..].Trim());
                else
                    HandleStartTag(tag);

                position = close + 1;
            }

            Flush();
            Title = WhitespaceRegex().Replace(Title, " ").Trim();
        }

        private void HandleStartTag(string tagText)
        {
            var name = tagText;
            string? classAttribute = null;
            string? imageSource = null;
            string? imageAlt = null;

            var space = tagText.IndexOfAny([' ', '\t', '\n', '\r']);
            if (space >= 0)
            {
                name = tagText[..space];
                classAttribute = ExtractAttribute(tagText, "class");
                imageSource = ExtractAttribute(tagText, "src");
                imageAlt = ExtractAttribute(tagText, "alt");
            }

            name = name.TrimEnd('/').ToLowerInvariant();
            CountNewlines(tagText, 0, tagText.Length);

            if (SkipTags.Contains(name))
            {
                _skipDepth++;
                return;
            }

            if (_skipDepth > 0)
                return;

            if (name == "title")
                _inTitle = true;

            var isHeading = HeadingTags.Contains(name) ||
                            (classAttribute is not null &&
                             HeadingClassRegex().IsMatch(classAttribute));
            if (isHeading)
            {
                Flush();
                _headingDepth++;
                _headingLine = _line;
                _headingBuffer.Clear();
            }
            else if (BlockTags.Contains(name))
            {
                _buffer.Add("\n");
            }

            if (name is "td" or "th")
                _buffer.Add(" | ");

            if (name == "img" && !string.IsNullOrEmpty(imageSource))
            {
                if (!string.IsNullOrEmpty(imageAlt))
                    _buffer.Add($" [Image: {imageAlt}] ");
            }
        }

        private void HandleEndTag(string rawName)
        {
            var name = rawName.Trim().ToLowerInvariant();

            if (SkipTags.Contains(name))
            {
                _skipDepth = Math.Max(0, _skipDepth - 1);
                return;
            }

            if (_skipDepth > 0)
                return;

            if (name == "title")
            {
                _inTitle = false;
                return;
            }

            if (_headingDepth > 0 && (name == "div" || HeadingTags.Contains(name)))
            {
                var heading = WhitespaceRegex()
                    .Replace(string.Concat(_headingBuffer), " ")
                    .Trim();
                if (heading.Length > 0)
                {
                    _currentHeading = heading;
                    _currentStart = _headingLine;
                }

                _headingDepth = 0;
                _headingBuffer.Clear();
            }
            else if (BlockTags.Contains(name))
            {
                _buffer.Add("\n");
            }
        }

        private void HandleData(string data)
        {
            if (data.Length == 0)
                return;

            var decoded = System.Net.WebUtility.HtmlDecode(data);
            CountNewlines(data, 0, data.Length);

            if (_skipDepth > 0)
                return;

            if (_inTitle)
                Title += decoded;

            if (_headingDepth > 0)
                _headingBuffer.Add(decoded);
            else
                _buffer.Add(decoded);
        }

        private void Flush()
        {
            var joined = string.Concat(_buffer);
            var content = SpaceRunRegex().Replace(joined, " ");
            content = BlankRunRegex().Replace(content, "\n\n").Trim();
            if (content.Length > 0)
                Sections.Add(new HelpSection(_currentHeading, _currentStart, content));

            _buffer.Clear();
        }

        private static string? ExtractAttribute(string tagText, string attribute)
        {
            var match = Regex.Match(
                tagText,
                $@"{attribute}\s*=\s*(""([^""]*)""|'([^']*)'|([^\s>]+))",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
                return null;

            return match.Groups[2].Success
                ? match.Groups[2].Value
                : match.Groups[3].Success
                    ? match.Groups[3].Value
                    : match.Groups[4].Value;
        }

        private void CountNewlines(string text, int start, int end)
        {
            for (var index = start; index < end; index++)
                if (text[index] == '\n')
                    _line++;
        }
    }
}
