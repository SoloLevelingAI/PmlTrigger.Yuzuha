using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.Knowledge;

public static partial class KnowledgeFeatures
{
    private static readonly IKnowledgeSource KnowledgeSource = new WorkspaceKnowledgeSource();

    /// <summary>Opens a workspace catalog and searches or reads its source documents.</summary>
    private static CoreData QueryWorkspaceKnowledge(FeatureContext context, CoreData request)
    {
        var command = request.Children().Single();
        var catalogPath = Value(command, "catalog");
        var catalog = KnowledgeSource.Open(context, catalogPath);
        var entries = command.Name.LocalName == "read"
            ? ReadKnowledgeEntry(context, catalog, Value(command, "id"))
            : SearchKnowledge(context, catalog, command);
        return new CoreData("result", new CoreField("mode", command.Name.LocalName),
            new CoreField("catalog", catalogPath), new CoreField("library", catalog.Title),
            new CoreField("product", catalog.Product), new CoreField("productVersion", catalog.ProductVersion),
            new CoreField("count", entries.Length), entries);
    }

    private static CoreData[] ReadKnowledgeEntry(FeatureContext context, KnowledgeCatalog catalog, string id)
    {
        var entry = catalog.Entries.SingleOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("本地知识库中没有该条目。");
        var document = KnowledgeSource.Read(context, entry.Path);
        return [DescribeKnowledgeEntry(entry, document, new CoreData("content", document.Content))];
    }

    private static CoreData[] SearchKnowledge(FeatureContext context, KnowledgeCatalog catalog, CoreData command)
    {
        var query = Value(command, "query").Trim();
        var kind = Value(command, "kind");
        var limit = (int?)command.Field("limit") ?? 10;
        return catalog.Entries.Where(entry => MatchesKind(entry, kind))
            .Select(entry => MatchKnowledge(context, entry, query))
            .OfType<CoreData>().Take(limit).ToArray();
    }

    private static bool MatchesKind(KnowledgeEntry entry, string kind) => kind.Length == 0 || entry.Kind == kind;

    private static CoreData? MatchKnowledge(FeatureContext context, KnowledgeEntry entry, string query)
    {
        var document = KnowledgeSource.Read(context, entry.Path);
        var index = document.Content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        var metadata = string.Join(" ", new[] { entry.Id, entry.Title }.Concat(entry.Tags));
        if (index < 0 && !metadata.Contains(query, StringComparison.OrdinalIgnoreCase))
            return null;
        var start = Math.Max(0, index - 28);
        var snippet = SafeSnippet(document.Content, start);
        return DescribeKnowledgeEntry(entry, document, new CoreData("snippet", snippet));
    }

    private static string SafeSnippet(string content, int start)
    {
        var end = Math.Min(content.Length, start + 120);
        if (start < content.Length && char.IsLowSurrogate(content[start]))
            start--;
        if (end < content.Length && char.IsLowSurrogate(content[end]))
            end++;
        return content[start..end].Trim();
    }

    private static CoreData DescribeKnowledgeEntry(KnowledgeEntry entry, KnowledgeDocument document, CoreData text) =>
        new("entry", new CoreField("id", entry.Id), new CoreField("title", entry.Title),
            new CoreField("kind", entry.Kind), new CoreField("path", entry.Path),
            new CoreField("sha256", document.Sha256), entry.Tags.Select(tag => new CoreData("tag", tag)), text);

    private interface IKnowledgeSource
    {
        KnowledgeCatalog Open(FeatureContext context, string catalog);
        KnowledgeDocument Read(FeatureContext context, string path);
    }

    private sealed class WorkspaceKnowledgeSource : IKnowledgeSource
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        public KnowledgeCatalog Open(FeatureContext context, string catalog)
        {
            var bytes = ReadBounded(context.FilePath(catalog), 128 * 1024);
            var parsed = JsonSerializer.Deserialize<KnowledgeCatalog>(StripUtf8Bom(bytes), Json);
            return RequireKnowledgeCatalog(parsed);
        }

        public KnowledgeDocument Read(FeatureContext context, string path)
        {
            var source = context.FilePath(RequireKnowledgePath(path));
            var bytes = ReadBounded(source, 256 * 1024);
            var content = new UTF8Encoding(false, true).GetString(StripUtf8Bom(bytes));
            return new KnowledgeDocument(XmlText(content), Sha256(bytes));
        }
    }

    private static KnowledgeCatalog RequireKnowledgeCatalog(KnowledgeCatalog? catalog)
    {
        if (catalog is not { SchemaVersion: 1, Entries: { Length: <= 64 } }
            || !HasKnowledgeText(catalog.Title) || !HasKnowledgeText(catalog.Product)
            || !HasKnowledgeText(catalog.ProductVersion) || catalog.Entries.Any(InvalidKnowledgeEntry)
            || catalog.Entries.Select(entry => entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != catalog.Entries.Length)
            throw new InvalidDataException("知识目录须为版本 1，最多 64 条，条目 ID 唯一且元数据完整。");
        return catalog;
    }

    private static bool InvalidKnowledgeEntry(KnowledgeEntry? entry) =>
        entry is null || !HasKnowledgeText(entry.Id) || !HasKnowledgeText(entry.Title)
        || entry.Kind is not ("document" or "note") || !HasKnowledgeText(entry.Path)
        || entry.Tags is not { Length: <= 16 } || entry.Tags.Any(tag => !HasKnowledgeText(tag));

    private static bool HasKnowledgeText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && XmlText(value).Length <= 260;

    private static string RequireKnowledgePath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".md" or ".txt" ? path
            : throw new InvalidDataException("知识条目的来源须为工作区中的 UTF-8 Markdown 或文本文件。");

    private sealed record KnowledgeCatalog(int SchemaVersion, string Title, string Product,
        string ProductVersion, KnowledgeEntry[] Entries);
    private sealed record KnowledgeEntry(string Id, string Title, string Kind, string Path, string[] Tags);
    private sealed record KnowledgeDocument(string Content, string Sha256);
}
