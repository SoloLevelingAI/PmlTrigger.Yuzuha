using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core30.Core;

namespace Core30.Knowledge;

public enum SagSourceKind { Html, Pml, Snippets }

public sealed record SagSource(string Directory, SagSourceKind Kind)
{
    public string Name => Kind == SagSourceKind.Snippets ? "我的切片" : Path.GetFileName(Path.TrimEndingDirectorySeparator(Directory));
    public string Format => Kind switch { SagSourceKind.Html => "HTML", SagSourceKind.Pml => "PML", _ => "Markdown" };
}

public sealed record SagIndexState(SagSource Source, string State, int Files, string Error);
public sealed record SagImportResult(SagIndexState Index, int Skipped);
public sealed record SagSearchHit(SagSource Source, string RelativePath, string Title, int Line, string Snippet);
public sealed record SagSearchResult(IReadOnlyList<SagSearchHit> Hits, bool Truncated);

/// <summary>Indexes visible HTML and plain PML in the workbench's private SAG data directory.</summary>
public sealed class SagKnowledgeLibrary
{
    private const int SourceFileLimit = 4 * 1024 * 1024;
    private const int ResultLimit = 35;
    private static readonly HashSet<string> PmlExtensions = new(
        [".pml", ".pmlfnc", ".pmlfrm", ".pmlobj", ".pmlcmd", ".pmlmac", ".pmldat"], StringComparer.OrdinalIgnoreCase);
    private static readonly EnumerationOptions Tree = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true
    };
    private readonly FeatureContext _home;
    private readonly SagWikiCli _cli;
    private readonly string _executable;

    public SagKnowledgeLibrary(string workspace, string executable)
    {
        _executable = FindExecutable(executable) ?? throw new FileNotFoundException("请选择本机已有的 sag.exe。");
        var context = new FeatureContext(Directory.CreateDirectory(workspace).FullName);
        _home = new FeatureContext(Directory.CreateDirectory(context.FilePath(".core30/sag-knowledge")).FullName);
        _cli = new SagWikiCli(_home, new SagWikiProcess());
    }

    public static string? FindExecutable(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return SagWikiCli.FindInstalled();
        var path = LocalSource.RequireAbsolutePath(configured);
        return Path.GetFileName(path).Equals("sag.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }

    public string ReadVersion()
    {
        var version = _cli.Run(_executable, ["--version"], TimeSpan.FromSeconds(8));
        if (!version.StartsWith("sag ", StringComparison.OrdinalIgnoreCase) || version.Length > 80)
            throw new InvalidDataException("所选程序未返回可识别的 SAG CLI 版本。");
        return version;
    }

    public IReadOnlyList<SagIndexState> List(IReadOnlyList<SagSource> sources)
    {
        if (sources.Count == 0) return [];
        using var gate = OpenGate();
        using var catalog = ReadCatalog();
        return sources.Select(source => FindIndex(catalog.RootElement, source, CorpusPath(source))).ToArray();
    }

    public SagImportResult Import(string directory, SagSourceKind kind)
    {
        using var gate = OpenGate();
        var origin = new FeatureContext(Path.TrimEndingDirectorySeparator(LocalSource.RequireAbsolutePath(directory)));
        if (IsInside(origin.Root, _home.Root) || IsInside(_home.Root, origin.Root))
            throw new InvalidDataException("请选择 SAG 索引工作区之外的原始资料目录。");
        var source = new SagSource(origin.Root, kind);
        var corpus = new FeatureContext(Directory.CreateDirectory(CorpusPath(source)).FullName);
        var skipped = PrepareCorpus(origin, corpus, kind);
        Exception? addError = null;
        try { _cli.Index(_executable, corpus.Root, "txt"); }
        catch (Exception error) when (error is IOException or TimeoutException) { addError = error; }
        var state = WaitForIndex(source, corpus.Root);
        if (state.State == "missing")
            throw new IOException(addError?.Message ?? "SAG 没有登记此目录。", addError);
        return new(state, skipped);
    }

    public SagSearchResult Search(SagSource source, string query)
    {
        query = query.Trim();
        if (query.Length is 0 or > 300 || query.Any(char.IsControl))
            throw new InvalidDataException("请输入 1 到 300 个字符的单行关键词。");
        using var gate = OpenGate();
        var corpus = new FeatureContext(CorpusPath(source));
        using var reply = JsonDocument.Parse(_cli.Search(_executable, corpus.Root, query, ResultLimit));
        var hits = new List<SagSearchHit>();
        foreach (var row in reply.RootElement.GetProperty("results").EnumerateArray())
        {
            var file = NormalizeCliPath(Text(row, "absolute_path"));
            if (file.Length == 0 || !IsInside(corpus.Root, file)) continue;
            var relative = Path.GetRelativePath(corpus.Root, file);
            if (!relative.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || !File.Exists(corpus.FilePath(relative))) continue;
            var original = relative[..^4];
            if (!Supports(original, source.Kind)) continue;
            var line = source.Kind == SagSourceKind.Pml ? Number(row, "start_line") : 0;
            var title = source.Kind is SagSourceKind.Html or SagSourceKind.Snippets ? ReadTitle(file) : null;
            hits.Add(new(source, original, string.IsNullOrWhiteSpace(title) ? Path.GetFileName(original) : title, line,
                Excerpt(Text(row, "content"), query)));
        }
        var truncated = reply.RootElement.TryGetProperty("truncated", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new(hits, truncated);
    }

    public IReadOnlyList<SagSearchHit> Browse(SagSource source)
    {
        if (source.Kind == SagSourceKind.Snippets)
        {
            var notes = new FeatureContext(LocalSource.RequireAbsolutePath(source.Directory));
            return Directory.EnumerateFiles(notes.Root, "*.md", SearchOption.TopDirectoryOnly)
                .Select(path => new SagSearchHit(source, Path.GetFileName(path), ReadTitle(path), 0, ""))
                .OrderBy(hit => hit.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        using var gate = OpenGate();
        var root = CorpusPath(source);
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*.txt", Tree)
            .Select(path => Path.GetRelativePath(root, path)[..^4])
            .Where(path => Supports(path, source.Kind))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new SagSearchHit(source, path, Path.GetFileName(path), 0, "")).ToArray();
    }

    private static string ReadTitle(string path) =>
        File.ReadLines(path).FirstOrDefault()?.TrimStart('#', ' ', '\t') ?? Path.GetFileName(path);

    public static string OriginalPath(SagSearchHit hit)
    {
        var source = new FeatureContext(LocalSource.RequireAbsolutePath(hit.Source.Directory));
        var path = source.FilePath(hit.RelativePath);
        if (!Supports(path, hit.Source.Kind) || !File.Exists(path))
            throw new FileNotFoundException("找不到原文件，请核对目录或重新导入。");
        return path;
    }

    public static string ReadPmlOriginal(SagSearchHit hit)
    {
        if (hit.Source.Kind != SagSourceKind.Pml)
            throw new InvalidDataException("请选择 PML 源文件。");
        return ReadPlainPml(OriginalPath(hit)) ?? throw new InvalidDataException("该 PML 文件已加密或不是可读文本。");
    }

    public static string ReadHtmlOriginal(SagSearchHit hit)
    {
        if (hit.Source.Kind != SagSourceKind.Html)
            throw new InvalidDataException("请选择 HTML 教程。");
        return KnowledgeTextExtraction.ReadSearchableHtml(OriginalPath(hit), SourceFileLimit);
    }

    private static int PrepareCorpus(FeatureContext origin, FeatureContext corpus, SagSourceKind kind)
    {
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        foreach (var file in Directory.EnumerateFiles(origin.Root, "*", Tree).Where(path => Supports(path, kind)))
        {
            var relative = Path.GetRelativePath(origin.Root, file);
            var text = TryReadSource(origin.FilePath(relative), kind);
            if (string.IsNullOrWhiteSpace(text))
            {
                skipped++;
                continue;
            }
            var output = corpus.FilePath(relative + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var pending = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(pending, text);
                File.Move(pending, output, overwrite: true);
            }
            finally { File.Delete(pending); }
            included.Add(output);
        }
        foreach (var file in Directory.EnumerateFiles(corpus.Root, "*.txt", Tree))
            if (!included.Contains(file)) File.Delete(corpus.FilePath(Path.GetRelativePath(corpus.Root, file)));
        if (included.Count == 0)
            throw new InvalidDataException(kind switch
            {
                SagSourceKind.Html => "所选目录没有可检索的 HTML 正文。",
                SagSourceKind.Pml => "所选目录没有可读的未加密 PML。",
                _ => "先写入一条切片，再建立索引。"
            });
        return skipped;
    }

    private static string? TryReadSource(string file, SagSourceKind kind)
    {
        try
        {
            return kind switch
            {
                SagSourceKind.Html => KnowledgeTextExtraction.ReadSearchableHtml(file, SourceFileLimit),
                SagSourceKind.Pml => ReadPlainPml(file),
                _ => LocalText.Decode(FileContent.ReadBounded(file, SourceFileLimit)).Content
            };
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error) || error is RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static string? ReadPlainPml(string path)
    {
        var bytes = FileContent.ReadBounded(path, SourceFileLimit);
        if (PmlSourceHeader.ReadAlgorithm(bytes) is not ("" or "000")) return null;
        var text = LocalText.Decode(bytes).Content;
        return text.Any(character => char.IsControl(character) && character is not ('\t' or '\r' or '\n' or '\f'))
            ? null : text;
    }

    private JsonDocument ReadCatalog() => JsonDocument.Parse(_cli.Run(_executable, ["list", "--json"], TimeSpan.FromSeconds(10)));

    private SagIndexState WaitForIndex(SagSource source, string root)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            using var catalog = ReadCatalog();
            var state = FindIndex(catalog.RootElement, source, root);
            if (state.State is "ready" or "error" or "failed" || timer.Elapsed >= TimeSpan.FromSeconds(16))
                return state;
            Thread.Sleep(450);
        }
    }

    private static SagIndexState FindIndex(JsonElement catalog, SagSource source, string root)
    {
        foreach (var entry in catalog.EnumerateArray())
            if (LocalSource.SamePath(root, NormalizeCliPath(Text(entry, "path"))))
                return new(source, Text(entry, "status"), Number(entry, "count"), Text(entry, "error"));
        return new(source, "missing", 0, "");
    }

    private string CorpusPath(SagSource source)
    {
        var key = $"{source.Kind}\n{LocalSource.RequireAbsolutePath(source.Directory).ToUpperInvariant()}";
        return _home.FilePath("sources/" + FileContent.Sha256(Encoding.UTF8.GetBytes(key)));
    }

    private FileStream OpenGate() => File.Open(_home.FilePath("index.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

    private static bool Supports(string path, SagSourceKind kind) => kind switch
    {
        SagSourceKind.Html => Path.GetExtension(path).ToLowerInvariant() is ".html" or ".htm",
        SagSourceKind.Pml => PmlExtensions.Contains(Path.GetExtension(path)),
        SagSourceKind.Snippets => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static bool IsInside(string root, string path) =>
        LocalSource.SamePath(root, path) || path.StartsWith(
            Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCliPath(string path)
    {
        var plain = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
        return Path.IsPathFullyQualified(plain) && !plain.StartsWith(@"\\", StringComparison.Ordinal) ? Path.GetFullPath(plain) : "";
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static int Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    private static string Excerpt(string text, string query)
    {
        text = text.Trim();
        if (text.Length <= 330) return text;
        var start = Math.Max(0, text.IndexOf(query, StringComparison.OrdinalIgnoreCase) - 65);
        var end = Math.Min(start + 330, text.Length);
        return (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
    }
}
