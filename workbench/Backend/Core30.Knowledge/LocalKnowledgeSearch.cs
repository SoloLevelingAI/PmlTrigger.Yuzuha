using System.Xml;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.Knowledge;

/// <summary>Searches source-backed document sections and the PmlTwin function catalog together.</summary>
internal sealed class LocalKnowledgeSearch(ImplementationCatalog<string, KnowledgeTextReader> readers)
{
    private const int ScanLimit = 14_000;

    public CoreData Run(FeatureContext context, CoreData request)
    {
        var roots = ReadRoots(context, request);
        var query = ((string?)request.Field("query") ?? "").Trim().TrimStart('!');
        var limit = (int?)request.Field("limit") ?? 20;
        var documents = SearchDocuments(roots, query, limit);
        var inventory = context.Input?.FunctionInventory ?? ReadFunctionReceipt(context, (string?)request.Field("functions"));
        var functions = SearchFunctionReceipt(inventory, query).Take(limit).ToArray();
        return new CoreData("result", new CoreField("examined", documents.Examined),
            new CoreField("skipped", documents.Skipped), new CoreField("matches", documents.Matches),
            new CoreField("truncated", documents.Truncated),
            new CoreData("roots", roots.Select(path => new CoreData("root", new CoreField("path", path)))),
            new CoreData("documents", documents.Hits.Select(hit => hit.Describe())), new CoreData("functions", functions));
    }

    private static string[] ReadRoots(FeatureContext context, CoreData request)
    {
        var configured = request.Children().Select(root => (string)root.Field("path")!).ToArray();
        var paths = configured.Length == 0 ? ["knowledge"] : configured;
        return paths.Select(path => LocalSource.Resolve(context, path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private DocumentResults SearchDocuments(string[] roots, string query, int limit)
    {
        var files = roots.SelectMany(EnumerateRoot).Distinct(StringComparer.OrdinalIgnoreCase).Take(ScanLimit + 1).ToArray();
        var results = new DocumentResults(limit);
        foreach (var file in files.Take(ScanLimit))
            results.Read(file, query, readers.Require(Path.GetExtension(file)));
        results.Truncated = files.Length > ScanLimit || results.Matches > limit;
        return results;
    }

    private IEnumerable<string> EnumerateRoot(string root)
    {
        if (File.Exists(root))
            return Supports(root) ? [root] : [];
        if (!Directory.Exists(root))
            return [];
        var directory = new FeatureContext(root);
        return Directory.EnumerateFiles(directory.Root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, MaxRecursionDepth = 24,
            AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true
        }).Where(Supports);
    }

    private bool Supports(string path) => readers.Find(Path.GetExtension(path)) is not null;

    private static CoreData? ReadFunctionReceipt(FeatureContext context, string? receipt)
    {
        if (receipt is null)
            return null;
        using var input = XmlReader.Create(context.FilePath(receipt), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024
        });
        return CoreXml.Read(XElement.Load(input));
    }

    private static IEnumerable<CoreData> SearchFunctionReceipt(CoreData? run, string query)
    {
        if (run is null)
            return [];
        return FunctionRows(run).Select(KnowledgeFunction).Where(row => row.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
            || ((string?)row.Field("name") ?? "").Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<CoreData> FunctionRows(CoreData run)
    {
        if (run.Name != "run" || (string?)run.Field("feature") != "P03" || (string?)run.Field("version") != "v5"
            || (string?)run.Field("status") != "ok" || run.Child("result")?.Child("environment") is null)
            throw new InvalidDataException("需要成功的 P03 v5 函数库回执。");
        return run.Child("result")!.Children("function");
    }

    private static CoreData KnowledgeFunction(CoreData row) => new("function", row.Fields(),
        (string?)row.Field("encryption") == "plain" ? row.Children() : []);

    private sealed record Hit(string Path, KnowledgeSection Section, int Score, string Excerpt)
    {
        public CoreData Describe() => new("document", new CoreField("source", Path),
            new CoreField("title", Safe(Section.Title, 260)), new CoreField("anchor", Safe(Section.Anchor, 260)),
            new CoreField("format", System.IO.Path.GetExtension(Path).TrimStart('.').ToLowerInvariant()),
            new CoreData("snippet", Safe(Excerpt, 300)));
    }

    private sealed class DocumentResults(int limit)
    {
        private readonly PriorityQueue<Hit, (int Score, int Order)> _hits = new();
        public IEnumerable<Hit> Hits => _hits.UnorderedItems.OrderByDescending(row => row.Priority.Score)
            .ThenByDescending(row => row.Priority.Order).Select(row => row.Element);
        public int Examined { get; private set; }
        public int Skipped { get; private set; }
        public int Matches { get; private set; }
        public bool Truncated { get; set; }

        public void Read(string path, string query, KnowledgeTextReader reader)
        {
            Examined++;
            try
            {
                foreach (var section in reader(path))
                    Offer(path, section, query);
            }
            catch (Exception error) when (CoreRunner.IsOperationalError(error)) { Skipped++; }
        }

        private void Offer(string path, KnowledgeSection section, string query)
        {
            var hit = MatchSection(path, section, query);
            if (hit is null)
                return;
            _hits.Enqueue(hit, (hit.Score, -Matches++));
            TrimQueue();
        }

        private void TrimQueue()
        {
            if (_hits.Count > limit)
                _hits.Dequeue();
        }

        private static Hit? MatchSection(string path, KnowledgeSection section, string query)
        {
            var position = section.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            var titleHit = section.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(path).Contains(query, StringComparison.OrdinalIgnoreCase);
            if (!titleHit && position < 0)
                return null;
            var score = titleHit ? 2 : 1;
            var start = Math.Max(0, position - 65);
            return new Hit(path, section, score, section.Text.Substring(start, Math.Min(245, section.Text.Length - start)));
        }
    }

    private static string Safe(string text, int length) => new(text.Take(length).Where(XmlConvert.IsXmlChar).ToArray());
}
