using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private const int RegistryLimit = 4 * 1024 * 1024;
    private const string FunctionRegistryPath = "state/functions.xml";
    private static readonly Regex Declaration = new(
        @"^[ \t]*define[ \t]+function[ \t]+!!([A-Za-z][A-Za-z0-9_]*)[ \t]*\(([^\r\n)]*)\)[ \t]*(?:is[ \t]+([A-Za-z][A-Za-z0-9_]*))?[ \t]*(?:--[^\r\n]*)?\r?$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex Parameter = new(
        @"^\s*!([A-Za-z][A-Za-z0-9_]*)\s+is\s+(STRING|REAL|BOOLEAN)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static CoreData RegisterPmlFunction(FeatureContext context, CoreData request)
    {
        var entry = ReadRegistration(context, request);
        StoreRegistration(context, entry);
        return new CoreData("result", new CoreField("registry", FunctionRegistryPath), entry);
    }

    private static CoreData ReadRegistration(FeatureContext context, CoreData request)
    {
        var relative = Value(request, "file");
        var bytes = ReadBounded(RequirePmlSource(context.FilePath(relative)), 1024 * 1024);
        return ParseRegistration(bytes, relative, Value(request, "name"));
    }

    // Port of the declaration and authorization defaults in PmlFunctionRegistry.Register.
    private static CoreData ParseRegistration(byte[] bytes, string relativePath, string name)
    {
        var source = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        var matches = Declaration.Matches(source).Cast<Match>().Where(match =>
            match.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var declaration = RequireSingleDeclaration(matches);
        var parameters = ParseParameters(declaration.Groups[2].Value);
        return new CoreData("function", new CoreField("name", declaration.Groups[1].Value),
            new CoreField("source", relativePath), new CoreField("sha256", Sha256(bytes)),
            new CoreField("returnType", declaration.Groups[3].Value.ToUpperInvariant()),
            new CoreField("allowInvoke", false), parameters);
    }

    private static CoreData[] ParseParameters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        var parameters = text.Split(',').Select(part => Parameter.Match(part)).ToArray();
        RequireUniqueParameters(parameters);
        return parameters.Select(parameter => new CoreData("parameter",
            new CoreField("name", parameter.Groups[1].Value),
            new CoreField("type", parameter.Groups[2].Value.ToUpperInvariant()))).ToArray();
    }

    private static Match RequireSingleDeclaration(Match[] matches) => matches.Length == 1 ? matches[0]
        : throw new InvalidDataException("Expected exactly one matching global PML function declaration.");

    private static void RequireUniqueParameters(Match[] parameters)
    {
        if (parameters.Any(parameter => !parameter.Success)
            || parameters.Select(parameter => parameter.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.Length)
            throw new InvalidDataException("PML parameters must use unique names and STRING, REAL or BOOLEAN.");
    }

    private static string RequirePmlSource(string path) =>
        string.Equals(Path.GetExtension(path), ".pmlfnc", StringComparison.OrdinalIgnoreCase) ? path
            : throw new InvalidDataException("Expected a .pmlfnc file.");

    private static void StoreRegistration(FeatureContext context, CoreData entry)
    {
        UpdateRegistry(context, registry => ReplaceRegistration(registry, CoreXml.Write(entry)));
    }

    private static XElement ReplaceRegistration(XElement registry, XElement entry)
    {
        var name = Value(entry, "name");
        var matches = registry.Elements("function").Where(item =>
            Value(item, "name").Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var existing = matches.FirstOrDefault();
        var replacement = new XElement(entry);
        replacement.SetAttributeValue("displayName", (string?)existing?.Attribute("displayName"));
        replacement.SetElementValue("note", (string?)existing?.Element("note"));
        matches.Remove();
        registry.Add(replacement);
        return replacement;
    }

    private static XElement UpdateRegistry(FeatureContext context, Func<XElement, XElement> change)
    {
        var state = context.FilePath(FunctionRegistryPath);
        var lockPath = context.FilePath("state/functions.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(state)!);
        using var gate = File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var registry = LoadRegistry(state);
        var entry = change(registry);
        SaveRegistry(state, registry);
        return entry;
    }

    private static XElement LoadRegistry(string state)
    {
        if (!File.Exists(state))
            return new XElement("registry", new XAttribute("version", 1));
        using var reader = XmlReader.Create(state, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = RegistryLimit
        });
        var registry = XElement.Load(reader);
        RequireRegistryVersion(registry);
        return registry;
    }

    private static void RequireRegistryVersion(XElement registry)
    {
        if (registry.Name != "registry" || Value(registry, "version") != "1")
            throw new InvalidDataException("Unsupported function registry version.");
    }

    private static void SaveRegistry(string state, XElement registry)
    {
        var directory = Path.GetDirectoryName(state)!;
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                registry.Save(output);
                RequireRegistrySize(output.Length);
            }
            File.Move(temporary, state, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void RequireRegistrySize(long length)
    {
        if (length > RegistryLimit)
            throw new InvalidDataException("Function registry exceeds its 4 MiB limit.");
    }
}
