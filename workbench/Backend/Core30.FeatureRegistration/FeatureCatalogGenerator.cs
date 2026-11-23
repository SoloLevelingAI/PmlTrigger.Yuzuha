using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Core30.FeatureRegistration;

/// <summary>Builds ordered feature entries from module metadata and verifies their embedded schema inputs.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class FeatureCatalogGenerator : IIncrementalGenerator
{
    private const string ManifestName = "FeatureCatalog.xml";
    private const string SchemaNamespace = "http://www.w3.org/2001/XMLSchema";
    private static readonly DiagnosticDescriptor MissingCatalog = new(
        "C30F001", "Feature catalog input",
        "Provide exactly one FeatureCatalog.xml as an AdditionalFiles input; found {0}",
        "Feature Catalog", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidCatalog = new(
        "C30F002", "Invalid feature catalog",
        "{0}", "Feature Catalog", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputs = context.AdditionalTextsProvider
            .Where(file => IsManifest(file.Path) || file.Path.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            .Select((file, token) => (file.Path, Text: file.GetText(token)))
            .Collect();
        var assemblyName = context.CompilationProvider.Select((compilation, _) => compilation.AssemblyName);
        context.RegisterSourceOutput(inputs.Combine(assemblyName),
            (output, pair) => Generate(output, pair.Left, pair.Right));
    }

    private static bool IsManifest(string path) =>
        string.Equals(Path.GetFileName(path), ManifestName, StringComparison.OrdinalIgnoreCase);

    private static void Generate(SourceProductionContext context,
        ImmutableArray<(string Path, SourceText? Text)> inputs, string? assemblyName)
    {
        var catalogs = inputs.Where(file => IsManifest(file.Path)).ToArray();
        if (catalogs.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingCatalog, Location.None, catalogs.Length));
            return;
        }

        var (path, text) = catalogs[0];
        try
        {
            if (assemblyName is null || assemblyName.Split('.').Any(part => !IsIdentifier(part)))
                throw new XmlException("The module assembly name must be a C# namespace.");

            var document = ReadXml(text);
            if (document.Root is not { } root || root.Name != "features" || root.HasAttributes)
                throw Invalid(document.Root, "Expected a <features> root without attributes.");

            var schemas = inputs.Where(file => !IsManifest(file.Path))
                .ToLookup(input => Path.GetFileName(input.Path), StringComparer.OrdinalIgnoreCase);
            var entries = ReadEntries(root, schemas);
            context.AddSource("FeatureCatalog.g.cs", SourceText.From(WriteCatalog(assemblyName, entries), Encoding.UTF8));
        }
        catch (XmlException error)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidCatalog,
                At(path, text, error.LineNumber, error.LinePosition), error.Message));
        }
    }

    private static List<Entry> ReadEntries(XElement root,
        ILookup<string, (string Path, SourceText? Text)> schemas)
    {
        var entries = new List<Entry>();
        var handlers = new HashSet<string>(StringComparer.Ordinal);
        var contracts = new HashSet<string>(StringComparer.Ordinal);
        var dispatchers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.Elements())
        {
            if (element.Name != "feature" || !string.IsNullOrWhiteSpace(element.Value))
                throw Invalid(element, "Expected a <feature> entry.");

            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.Namespace != XNamespace.None ||
                    attribute.Name.LocalName is not ("handler" or "id" or "version" or "title" or
                        "activity" or "operationAttribute" or "operationDefault" or "dispatch"))
                {
                    throw Invalid(attribute, $"Unsupported feature attribute '{attribute.Name}'.");
                }
            }

            var id = Require(element, "id");
            var version = Require(element, "version");
            var title = Require(element, "title");
            var handler = Require(element, "handler");
            var activity = Optional(element, "activity");
            var operation = Optional(element, "operationAttribute");
            var fallback = Optional(element, "operationDefault");
            var dispatch = Optional(element, "dispatch");
            if (element.HasElements != (dispatch is not null))
                throw Invalid(element, "Declare dispatch and its command bindings together.");
            if (dispatch is not null && (!IsDispatcherName(dispatch) || !dispatchers.Add(dispatch)))
                throw Invalid(element, "Dispatch must be a unique Class.Method name in this module.");
            if (id.Length != 3 || id[0] is < 'A' or > 'Z' || !IsDigit(id[1]) || !IsDigit(id[2]))
                throw Invalid(element, $"Invalid feature identifier '{id}'. Expected one capital letter and two digits.");
            if (version.Length < 2 || version[0] != 'v' || version[1] == '0' || version.Skip(1).Any(c => !IsDigit(c)))
                throw Invalid(element, $"Invalid feature version '{version}'. Expected v followed by a positive integer.");
            if (!IsIdentifier(handler) || handler[0] is < 'a' or > 'z' || !handlers.Add(handler))
                throw Invalid(element, $"Feature handler '{handler}' must be a unique camelCase parameter name.");
            if (!contracts.Add(id + ":" + version))
                throw Invalid(element, $"Feature {id} {version} is registered more than once.");
            if ((operation is null) != (fallback is null))
                throw Invalid(element, "Specify operationAttribute and operationDefault together.");
            if (operation is not null)
                VerifyOperationName(element, operation);

            VerifySchema(element, schemas, id, version, operation);
            entries.Add(new Entry(id, version, title, handler, activity, operation, fallback, dispatch is null ? null : element));
        }

        if (entries.Count == 0 || root.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
            throw Invalid(root, "A feature catalog must contain one or more entries.");
        return entries;
    }

    private static void VerifyOperationName(XElement element, string operation)
    {
        try { XmlConvert.VerifyNCName(operation); }
        catch (XmlException) { throw Invalid(element, $"Invalid request operation attribute '{operation}'."); }
    }

    private static void VerifySchema(XElement element, ILookup<string, (string Path, SourceText? Text)> schemas,
        string id, string version, string? operation)
    {
        var name = version == "v1" ? $"{id}.xsd" : $"{id}.{version}.xsd";
        var matches = schemas[name].ToArray();
        if (matches.Length != 1)
            throw Invalid(element, $"Feature {id} {version} needs exactly one {name} input; found {matches.Length}.");

        XElement? schema;
        try { schema = ReadXml(matches[0].Text).Root; }
        catch (XmlException error) { throw Invalid(element, $"Cannot read schema {name}: {error.Message}"); }
        XNamespace xsd = SchemaNamespace;
        var expectedNamespace = $"urn:core30:{id}:{version}";
        if (schema?.Name != xsd + "schema" || (string?)schema.Attribute("targetNamespace") != expectedNamespace)
            throw Invalid(element, $"Schema {name} must declare targetNamespace '{expectedNamespace}'.");
        var request = schema.Elements(xsd + "element").FirstOrDefault(node => (string?)node.Attribute("name") == "request")
            ?.Element(xsd + "complexType");
        if (operation is not null && request?.Elements(xsd + "attribute")
            .Any(node => (string?)node.Attribute("name") == operation) != true)
        {
            throw Invalid(element, $"Schema {name} does not declare '{operation}' on its request.");
        }
        if (element.HasElements)
            VerifyDispatch(element, request?.Element(xsd + "choice"), xsd);
    }

    private static void VerifyDispatch(XElement feature, XElement? choice, XNamespace xsd)
    {
        var commands = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in feature.Elements())
            if (command.Name.Namespace != XNamespace.None || command.HasElements || command.Attributes().Count() != 1 ||
                !IsIdentifier(Require(command, "handler")) || !commands.Add(command.Name.LocalName))
                throw Invalid(command, "Each command needs a unique element name and one C# handler.");

        var names = choice?.Elements().Where(item => item.Name != xsd + "annotation")
            .Select(item => item.Name == xsd + "element" && IsSingle(item) ? (string?)item.Attribute("name") : null).ToArray();
        if (choice is null || !IsSingle(choice) || names!.Length != commands.Count || !commands.SetEquals(names!))
            throw Invalid(feature, "Dispatch must bind every single-occurrence command in the schema's request choice.");
    }

    private static bool IsSingle(XElement item) =>
        ((string?)item.Attribute("minOccurs") is null or "1") && ((string?)item.Attribute("maxOccurs") is null or "1");

    private static bool IsDispatcherName(string dispatcher)
    {
        var parts = dispatcher.Split('.');
        return parts.Length == 2 && parts.All(IsIdentifier);
    }

    private static XDocument ReadXml(SourceText? text)
    {
        if (text is null)
            throw new XmlException("The additional XML file could not be read.");
        using var source = new StringReader(text.ToString());
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static string WriteCatalog(string assemblyName, List<Entry> entries)
    {
        const string handlerType = "Func<FeatureContext, CoreData, CoreData>";
        var output = new StringBuilder("// <auto-generated/>\n#nullable enable\nusing System;\nusing System.Collections.Generic;\n");
        if (entries.Any(entry => entry.Dispatch is not null))
            output.Append("using System.Linq;\n");
        output.Append("using System.Xml.Linq;\nusing Core30.Core;\n\n");
        output.Append("namespace ").Append(string.Join(".", assemblyName.Split('.').Select(part => "@" + part)))
            .Append(";\n\ninternal static class FeatureCatalog\n{\n    internal static IEnumerable<IFeature> Bind(\n");
        output.Append(string.Join(",\n", entries.Select(entry => $"        {handlerType} @{entry.Handler}")))
            .Append(")\n    {\n        var assembly = typeof(FeatureCatalog).Assembly;\n");

        foreach (var entry in entries)
        {
            output.Append("        yield return new TypedFeature(").Append(Literal(entry.Id)).Append(", ")
                .Append(Literal(entry.Title)).Append(", assembly, @").Append(entry.Handler).Append(", ")
                .Append(Literal(entry.Version)).Append(')');
            if (entry.Activity is not null || entry.Operation is not null)
            {
                output.Append("\n        {\n");
                if (entry.Activity is not null)
                    output.Append("            ActivityCategory = ").Append(Literal(entry.Activity)).Append(",\n");
                if (entry.Operation is not null)
                    output.Append("            OperationName = static request => (string?)request.Field(")
                        .Append(Literal(entry.Operation)).Append(") ?? ").Append(Literal(entry.Fallback!)).Append(",\n");
                output.Append("        }");
            }
            output.Append(";\n");
        }

        output.Append("    }\n}\n");
        var dispatchers = entries.Select(entry => entry.Dispatch).OfType<XElement>()
            .GroupBy(element => Require(element, "dispatch").Split('.')[0], StringComparer.Ordinal);
        foreach (var dispatcher in dispatchers)
        {
            output.Append("\npartial class @").Append(dispatcher.Key).Append("\n{\n");
            foreach (var entry in dispatcher)
            {
                var method = Require(entry, "dispatch").Split('.')[1];
                output.Append("    public CoreData @").Append(method).Append("(FeatureContext context, CoreData request)\n")
                    .Append("    {\n        var command = request.Children().Single();\n        return command.Name.LocalName switch\n        {\n");
                foreach (var command in entry.Elements())
                    output.Append("            ").Append(Literal(command.Name.LocalName)).Append(" => @")
                        .Append(Require(command, "handler")).Append("(context, command),\n");
                output.Append("            _ => throw new global::System.IO.InvalidDataException(\"未登记请求操作。\")\n        };\n    }\n\n");
            }
            output.Append("}\n");
        }
        return output.ToString();
    }

    private static string Literal(string value)
    {
        var result = new StringBuilder("\"");
        foreach (var character in value)
        {
            if (character is '\\' or '"')
                result.Append('\\').Append(character);
            else if (char.IsControl(character) || character is '\u2028' or '\u2029')
                result.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            else
                result.Append(character);
        }
        return result.Append('"').ToString();
    }

    private static string Require(XElement element, XName name) =>
        Optional(element, name) ?? throw Invalid(element, $"Missing required feature attribute '{name}'.");

    private static string? Optional(XElement element, XName name)
    {
        if (element.Attribute(name) is not { } attribute)
            return null;
        if (string.IsNullOrWhiteSpace(attribute.Value))
            throw Invalid(attribute, $"Feature attribute '{name}' cannot be empty.");
        return attribute.Value;
    }

    private static bool IsIdentifier(string value) =>
        value.Length != 0 && (IsLetter(value[0]) || value[0] == '_') &&
        value.All(c => IsLetter(c) || IsDigit(c) || c == '_');

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
    private static bool IsLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static XmlException Invalid(XObject? element, string message)
    {
        var location = element as IXmlLineInfo;
        return new XmlException(message, null, location?.LineNumber ?? 0, location?.LinePosition ?? 0);
    }

    private static Location At(string path, SourceText? text, int line, int column)
    {
        if (text is null || line < 1 || line > text.Lines.Count)
            return Location.None;
        var target = text.Lines[line - 1];
        var offset = Math.Min(Math.Max(0, column - 1), target.Span.Length);
        var span = new TextSpan(target.Start + offset, 0);
        return Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
    }

    private sealed class Entry(string id, string version, string title, string handler,
        string? activity, string? operation, string? fallback, XElement? dispatch)
    {
        internal string Id { get; } = id;
        internal string Version { get; } = version;
        internal string Title { get; } = title;
        internal string Handler { get; } = handler;
        internal string? Activity { get; } = activity;
        internal string? Operation { get; } = operation;
        internal string? Fallback { get; } = fallback;
        internal XElement? Dispatch { get; } = dispatch;
    }
}
