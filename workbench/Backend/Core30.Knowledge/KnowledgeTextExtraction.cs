using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.Knowledge;

internal sealed record KnowledgeSection(string Title, string Anchor, string Text);

internal delegate IEnumerable<KnowledgeSection> KnowledgeTextReader(string path);

internal static class KnowledgeTextExtraction
{
    internal const int Limit = 1024 * 1024;
    private const RegexOptions HtmlOptions = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly Regex Hidden = new(@"<!--.*?-->|<(script|style|template|head)\b[^>]*>.*?</\1\s*>", HtmlOptions, Timeout);
    private static readonly Regex Title = new(@"<title\b[^>]*>(?<text>.*?)</title\s*>", HtmlOptions, Timeout);
    private static readonly Regex Headings = new(@"<h(?<level>[1-6])\b(?<attrs>[^>]*)>(?<text>.*?)</h\k<level>\s*>", HtmlOptions, Timeout);
    private static readonly Regex Anchor = new("""\bid\s*=\s*(?:"(?<id>[^"]+)"|'(?<id>[^']+)')""", HtmlOptions, Timeout);
    private static readonly Regex Breaks = new(@"</(?:p|li|pre|div|tr|td|h[1-6])\s*>|<br\s*/?>", HtmlOptions, Timeout);
    private static readonly Regex Tags = new("""<[/!?]?[a-zA-Z](?:"[^"]*"|'[^']*'|[^"'<>])*?>""", HtmlOptions, Timeout);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant, Timeout);

    public static IEnumerable<KnowledgeSection> ReadPlainText(string path) =>
        [new KnowledgeSection(Path.GetFileNameWithoutExtension(path), "", ReadText(path))];

    public static IEnumerable<KnowledgeSection> ReadHtmlDocument(string path) =>
        ReadHtml(Path.GetFileNameWithoutExtension(path), ReadText(path));

    internal static string ReadSearchableHtml(string path, int limit)
    {
        var html = LocalText.Decode(ReadBounded(path, limit)).Content;
        var body = Spaces.Replace(Hidden.Replace(html, ""), " ");
        var paragraphs = Breaks.Replace(body, "\n").Split('\n').Select(Clean).Where(line => line.Length > 0);
        var visible = string.Join("\n", paragraphs);
        return visible.Length == 0 ? "" : ReadTitle(html, Path.GetFileNameWithoutExtension(path)) + "\n" + visible;
    }

    public static IEnumerable<KnowledgeSection> ReadWordDocument(string path) =>
        [new KnowledgeSection(Path.GetFileNameWithoutExtension(path), "", ReadWord(path))];

    private static string ReadText(string path) => LocalText.Decode(ReadBounded(path, Limit)).Content;

    private static IEnumerable<KnowledgeSection> ReadHtml(string fallback, string html)
    {
        var title = ReadTitle(html, fallback);
        var body = Hidden.Replace(html, "");
        var headings = Headings.Matches(body).Cast<Match>().Take(256).ToArray();
        return headings.Length == 0
            ? [new KnowledgeSection(title, "", Clean(body))]
            : SplitHtmlSections(body, headings, title);
    }

    private static string ReadTitle(string html, string fallback)
    {
        var title = Clean(Title.Match(html).Groups["text"].Value);
        return title.Length == 0 ? fallback : title;
    }

    private static IEnumerable<KnowledgeSection> SplitHtmlSections(string body, Match[] headings, string title)
    {
        var introduction = Clean(body[..headings[0].Index]);
        if (introduction.Length != 0)
            yield return new KnowledgeSection(title, "", introduction);
        for (var index = 0; index < headings.Length; index++)
        {
            var heading = headings[index];
            var end = index + 1 == headings.Length ? body.Length : headings[index + 1].Index;
            yield return new KnowledgeSection(Clean(heading.Groups["text"].Value),
                Anchor.Match(heading.Groups["attrs"].Value).Groups["id"].Value, Clean(body[heading.Index..end]));
        }
    }

    private static string Clean(string html) =>
        Spaces.Replace(WebUtility.HtmlDecode(Tags.Replace(Breaks.Replace(html, " "), " ")), " ").Trim();

    private static string ReadWord(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX 缺少正文。");
        if (document.Length > Limit)
            throw new InvalidDataException("DOCX 正文超过读取预算。");
        using var stream = document.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = Limit
        });
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join("\n", XElement.Load(reader).Descendants(word + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(word + "t").Select(node => node.Value))));
    }
}
