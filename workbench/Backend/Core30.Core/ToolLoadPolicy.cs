using System.Xml;
using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Reads the workspace switch file before the host activates any tool.</summary>
internal sealed class ToolLoadPolicy(IReadOnlyDictionary<string, bool> switches, string source)
{
    public const string RelativePath = ".core30/tools.xml";
    private const string V1 = "urn:core30:tools:v1";
    private const string V2 = "urn:core30:tools:v2";

    public string Source => source;

    public bool IsEnabled(ToolRegistration tool) => switches.GetValueOrDefault(tool.Id, tool.EnabledByDefault);

    public static ToolLoadPolicy Load(FeatureContext context)
    {
        var (stream, source) = Open(context);
        using var input = stream;
        var document = XmlContracts.Read(input);
        return ReadPolicy(document, source);
    }

    public static LoadConfigurationChange Preview(FeatureContext context, ToolRegistration tool, bool enabled,
        LoadConfigurationChange.VerifiedSource? manifest)
    {
        var target = context.FilePath(RelativePath);
        var original = LoadConfigurationChange.ReadOptional(target);
        using var input = original is null ? EmbeddedFile.Open(typeof(ToolLoadPolicy).Assembly, "tools.xml")
            : new MemoryStream(original);
        var document = XmlContracts.Read(input);
        var before = ReadPolicy(document, RelativePath).IsEnabled(tool);
        var proposed = original;
        if (before != enabled)
        {
            SetSwitch(document, tool.Id, enabled, original is null);
            Validate(document);
            using var output = new MemoryStream();
            document.Save(output);
            proposed = output.ToArray();
        }
        return new(LoadConfigurationKind.ToolboxModule, tool.Id, before, enabled,
            context, RelativePath, original, proposed, manifest);
    }

    private static ToolLoadPolicy ReadPolicy(XDocument document, string source)
    {
        Validate(document);
        var contract = document.Root!.Name.NamespaceName;
        var switches = document.Root.Elements().ToDictionary(element => ReadId(element, contract),
            ReadSwitch, StringComparer.Ordinal);
        return new ToolLoadPolicy(switches, source);
    }

    private static void Validate(XDocument document)
    {
        var contract = document.Root!.Name.NamespaceName;
        using var schema = EmbeddedFile.Open(typeof(ToolLoadPolicy).Assembly, SchemaName(contract));
        XmlContracts.Validate(document, schema, contract, "tools");
    }

    private static void SetSwitch(XDocument document, string id, bool enabled, bool embedded)
    {
        var root = document.Root!;
        var contract = root.Name.Namespace;
        if (contract == V1 && !embedded && root.Elements().FirstOrDefault(node => node.Name.LocalName == id) is { } legacy)
        {
            legacy.SetAttributeValue("enabled", enabled);
            return;
        }

        XNamespace current = V2;
        if (contract == V1)
        {
            root = new XElement(current + "tools", root.Nodes().Select(node => node is XElement item
                ? new XElement(current + "tool", new XAttribute("id", item.Name.LocalName),
                    new XAttribute("enabled", item.Attribute("enabled")!.Value))
                : node));
            document.Root!.ReplaceWith(root);
        }
        var tool = root.Elements(current + "tool").SingleOrDefault(element => (string?)element.Attribute("id") == id);
        if (tool is null)
        {
            tool = new XElement(current + "tool", new XAttribute("id", id));
            root.Add(tool);
        }
        tool.SetAttributeValue("enabled", enabled);
    }

    private static (Stream Input, string Source) Open(FeatureContext context)
    {
        var path = context.FilePath(RelativePath);
        try
        {
            var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return (input, RelativePath);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return (EmbeddedFile.Open(typeof(ToolLoadPolicy).Assembly, "tools.xml"), "embedded-default");
        }
    }

    private static string SchemaName(string contract) => contract switch
    {
        V1 => "tools.xsd",
        V2 => "tools.v2.xsd",
        _ => throw new InvalidDataException($"工具配置 {RelativePath} 须使用 {V1} 或 {V2}。")
    };

    private static string ReadId(XElement element, string contract) =>
        contract == V1 ? element.Name.LocalName : element.Attribute("id")!.Value;

    private static bool ReadSwitch(XElement element) => XmlConvert.ToBoolean(element.Attribute("enabled")!.Value.Trim());
}
