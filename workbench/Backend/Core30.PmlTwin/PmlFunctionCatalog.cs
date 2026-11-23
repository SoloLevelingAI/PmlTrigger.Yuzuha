using System.Text;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    /// <summary>Lists the local registry or updates annotations against a known entry revision.</summary>
    private static CoreData QueryPmlFunctionCatalog(FeatureContext context, CoreData request)
    {
        var command = request.Children().Single();
        return command.Name.LocalName switch
        {
            "list" => ListFunctions(context, command),
            "unregister" => UnregisterFunction(context, command),
            _ => EditFunction(context, command)
        };
    }

    private static CoreData UnregisterFunction(FeatureContext context, CoreData command)
    {
        var entry = UpdateRegistry(context, registry =>
        {
            var row = RequireFunctionRevision(registry, Value(command, "name"), Value(command, "revision"));
            row.Remove();
            return row;
        });
        return new CoreData("result", new CoreField("removed", Value(entry, "name")),
            new CoreField("source", Value(entry, "source")));
    }

    private static CoreData ListFunctions(FeatureContext context, CoreData command)
    {
        var registry = LoadRegistry(context.FilePath(FunctionRegistryPath));
        var query = Value(command, "query").Trim();
        var limit = (int?)command.Field("limit") ?? 100;
        var items = registry.Elements("function").Where(entry => MatchesFunction(entry, query))
            .OrderBy(entry => Value(entry, "name"), StringComparer.OrdinalIgnoreCase).Take(limit + 1).ToArray();
        return new CoreData("result", new CoreField("count", Math.Min(items.Length, limit)),
            new CoreField("truncated", items.Length > limit), items.Take(limit).Select(DescribeFunction));
    }

    private static bool MatchesFunction(XElement entry, string query) =>
        new[] { Value(entry, "name"), Value(entry, "source"), Value(entry, "displayName"), (string?)entry.Element("note") ?? "" }
            .Any(text => text.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static CoreData EditFunction(FeatureContext context, CoreData command)
    {
        var entry = UpdateRegistry(context, registry => UpdateFunctionNotes(registry, command));
        return new CoreData("result", DescribeFunction(entry));
    }

    private static XElement UpdateFunctionNotes(XElement registry, CoreData command)
    {
        var entry = RequireFunctionRevision(registry, Value(command, "name"), Value(command, "revision"));
        entry.SetAttributeValue("displayName", Value(command, "displayName").Trim());
        entry.SetElementValue("note", command.Children().Single().Value);
        return entry;
    }

    private static XElement RequireFunctionRevision(XElement registry, string name, string revision)
    {
        var entry = FindFunction(registry, name) ?? throw new InvalidDataException("该函数已不在本地登记表。");
        if (FunctionRevision(entry) != revision)
            throw new InvalidDataException("登记表条目已更新，请刷新后再编辑。");
        return entry;
    }

    private static XElement? FindFunction(XElement registry, string name) =>
        registry.Elements("function").SingleOrDefault(entry => Value(entry, "name").Equals(name, StringComparison.OrdinalIgnoreCase));

    private static CoreData DescribeFunction(XElement entry)
    {
        var data = CoreXml.Read(entry);
        return new(data.Name, new CoreField("revision", FunctionRevision(entry)), data.Fields(), data.Children());
    }

    private static string FunctionRevision(XElement entry) =>
        Sha256(Encoding.UTF8.GetBytes(entry.ToString(SaveOptions.DisableFormatting)));
}
