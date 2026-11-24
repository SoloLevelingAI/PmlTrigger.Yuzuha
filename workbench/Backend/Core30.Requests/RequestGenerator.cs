using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Core30.Requests;

[Generator(LanguageNames.CSharp)]
public sealed partial class RequestGenerator : IIncrementalGenerator
{
    private const string ManifestSuffix = "Requests.xml";
    private const string GeneratedCode = "[global::System.CodeDom.Compiler.GeneratedCode(\"Core30.Requests.RequestGenerator\", \"1.0\")]";
    private static readonly DiagnosticDescriptor InvalidInput = new(
        "PMT002", "Invalid Core request generation input", "{0}", "Contracts",
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputs = context.AdditionalTextsProvider
            .Where(file => IsManifest(file.Path) || file.Path.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            .Select((file, token) => (file.Path, Text: file.GetText(token)))
            .Collect();
        var accessibility = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.Core30ExposeRequestFactories", out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "public" : "internal");
        context.RegisterSourceOutput(inputs.Combine(accessibility), (source, data) => Generate(source, data.Left, data.Right));
    }

    private static bool IsManifest(string path) => path.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase);

    private static void Generate(SourceProductionContext context, ImmutableArray<(string Path, SourceText? Text)> inputs,
        string accessibility)
    {
        var manifests = inputs.Where(input => IsManifest(input.Path)).ToArray();
        if (manifests.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidInput, Location.None, $"No *{ManifestSuffix} input was supplied."));
            return;
        }

        var schemas = inputs.Where(input => !IsManifest(input.Path))
            .ToLookup(input => Path.GetFileName(input.Path), StringComparer.OrdinalIgnoreCase);
        var types = new HashSet<string>(StringComparer.Ordinal) { "CoreRequest" };
        foreach (var manifest in manifests)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (manifest.Text is null)
            {
                Report(context, manifest.Path, null, "The request manifest could not be read.");
                continue;
            }

            try
            {
                using var reader = ReadXml(manifest.Text);
                var root = XDocument.Load(reader, LoadOptions.SetLineInfo).Root!;
                if (root.Name != "requests" || root.Elements().Any(item => item.Name != "contract" && item.Name != "binding") || !root.HasElements)
                    throw Invalid(root, "A request manifest must contain <requests> with one or more <contract> or <binding> elements.");
                foreach (var definition in root.Elements())
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    var type = Name(definition, "type");
                    if (!types.Add(type))
                        throw Invalid(definition, $"Duplicate generated type '{type}'.");
                    var schemaName = Required(definition, "schema");
                    var schema = schemas[schemaName].ToArray();
                    if (schema.Length != 1 || schema[0].Text is null)
                        throw Invalid(definition, $"Expected one readable AdditionalFiles input for '{schemaName}'.");

                    var request = SchemaRequest(definition, ReadSchema(schemaName, schema[0].Text!));
                    var factory = definition.Name == "contract";
                    var source = factory
                        ? GenerateContract(definition, type, request, accessibility)
                        : GenerateBinding(definition, type, request);
                    context.AddSource(type + ".g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
            catch (InputException error)
            {
                Report(context, manifest.Path, manifest.Text, error.Message, error.Line, error.Column);
            }
            catch (XmlException error)
            {
                Report(context, manifest.Path, manifest.Text, error.Message, error.LineNumber, error.LinePosition);
            }
        }
    }

    private static XmlSchemaElement SchemaRequest(XElement location, XmlSchema schema) =>
        schema.Elements[new XmlQualifiedName("request", schema.TargetNamespace)] as XmlSchemaElement
        ?? throw Invalid(location, "The schema must expose a global request element.");

    private static string GenerateContract(XElement contract, string type, XmlSchemaElement request, string accessibility)
    {
        var ns = request.QualifiedName.Namespace;
        var parts = ns.Split(':');
        if (parts.Length != 4 || parts[0] != "urn" || parts[1] != "core30" || parts[2].Length == 0)
            throw Invalid(contract, $"Unsupported Core namespace '{ns}'.");
        var module = Required(contract, "module");
        var output = new StringBuilder("""
            // <auto-generated/>
            #nullable enable
            using System.Collections.Generic;
            using System.Xml.Linq;
            using Core30.Core;

            namespace Core30.Generated;

            """);
        output.Append('\n').AppendLine(GeneratedCode)
            .Append(accessibility).Append(" static class ").AppendLine(type)
            .AppendLine("{")
            .Append("    private static readonly XNamespace Contract = ").Append(Literal(ns)).AppendLine(";")
            .Append("    internal const string Feature = ").Append(Literal(parts[2])).AppendLine(";")
            .Append("    internal const string Module = ").Append(Literal(module)).AppendLine(";");

        var names = new HashSet<string>(StringComparer.Ordinal) { type, "Contract", "Feature", "Module" };
        foreach (var method in contract.Elements())
        {
            if (method.Name != "method")
                throw Invalid(method, "Only <method> elements are allowed inside <contract>.");
            var name = Name(method, "name");
            if (!names.Add(name))
                throw Invalid(method, $"Duplicate or reserved method name '{name}'.");
            GenerateMethod(output, method, name, request, accessibility);
        }
        if (names.Count == 4)
            throw Invalid(contract, "A contract must declare at least one method.");
        return output.AppendLine("}").ToString();
    }

    private static void GenerateMethod(StringBuilder output, XElement method, string name, XmlSchemaElement request,
        string accessibility)
    {
        var fragmentText = (string?)method.Attribute("fragment");
        if (fragmentText is not (null or "true" or "false"))
            throw Invalid(method, "The fragment attribute must be 'true' or 'false'.");
        var fragment = fragmentText == "true";
        var bodies = method.Elements().Where(item => item.Name != "param").ToArray();
        if (bodies.Length != 1 || bodies[0].Name.Namespace != XNamespace.None ||
            !fragment && bodies[0].Name != "request")
            throw Invalid(method, "A method must declare one unqualified XML template, rooted at <request> for full requests.");

        var parameters = new Dictionary<string, Parameter>(StringComparer.Ordinal);
        var signature = new List<string>();
        foreach (var parameter in method.Elements("param"))
        {
            var variable = Name(parameter, "name", requireUppercase: false);
            var binding = new Parameter(variable, Required(parameter, "type"), parameter);
            if (parameters.ContainsKey(variable))
                throw Invalid(parameter, $"Duplicate parameter '{variable}'.");
            parameters.Add(variable, binding);
            signature.Add(binding.Type + " @" + variable);
        }

        var body = bodies[0];
        var schemaElement = fragment ? FindFragment(request, body) : request;
        var expression = GenerateElement(body, schemaElement, parameters);
        var unused = parameters.Values.FirstOrDefault(parameter => !parameter.Used);
        if (unused is not null)
            throw Invalid(method, $"Parameter '{unused.Name}' is unused.");

        output.AppendLine().Append("    ").Append(accessibility).Append(" static ").Append(fragment ? "CoreData " : "CoreRequest ")
            .Append(name).Append('(').Append(string.Join(", ", signature)).AppendLine(") =>");
        if (fragment)
            output.Append("        ").Append(expression).AppendLine(";");
        else
            output.Append("        new CoreRequest(Feature, Module, ").Append(expression).AppendLine(");");
    }

    private static XmlReader ReadXml(SourceText text) => XmlReader.Create(new StringReader(text.ToString()),
        new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true });

    private static XmlSchema ReadSchema(string name, SourceText text)
    {
        try
        {
            using var reader = ReadXml(text);
            var schema = XmlSchema.Read(reader, null) ?? throw new XmlSchemaException("The input is empty.");
            if (schema.Includes.Count != 0)
                throw new XmlSchemaException("The schema must be self-contained.");
            var set = new XmlSchemaSet { XmlResolver = null };
            set.Add(schema);
            set.Compile();
            return schema;
        }
        catch (XmlSchemaException error)
        {
            throw new InputException($"Cannot load schema '{name}': {error.Message}");
        }
        catch (XmlException error)
        {
            throw new InputException($"Cannot load schema '{name}': {error.Message}");
        }
    }

    private static string Required(XElement node, string attribute) => (string?)node.Attribute(attribute) is { Length: > 0 } value
        && !string.IsNullOrWhiteSpace(value) ? value : throw Invalid(node, $"<{node.Name.LocalName}> requires a '{attribute}' value.");

    private static string Name(XElement node, string attribute, bool requireUppercase = true)
    {
        var name = Required(node, attribute);
        if (!AsciiLetter(name[0]) || name.Any(character => !AsciiLetter(character) &&
            character is not (>= '0' and <= '9' or '_')) ||
            requireUppercase && name[0] is not (>= 'A' and <= 'Z'))
            throw Invalid(node, $"Invalid C# identifier '{name}'.");
        return name;
    }

    private static bool AsciiLetter(char character) => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\u0085", "\\u0085")
        .Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029") + "\"";

    private static InputException Invalid(XObject node, string message) =>
        node is IXmlLineInfo line ? new InputException(message, line.LineNumber, line.LinePosition) : new InputException(message);

    private static void Report(SourceProductionContext context, string path, SourceText? text, string message,
        int line = 0, int column = 0)
    {
        var location = Location.None;
        if (text is not null && line > 0 && line <= text.Lines.Count)
        {
            var sourceLine = text.Lines[line - 1];
            var position = sourceLine.Start + Math.Min(Math.Max(0, column - 1), sourceLine.Span.Length);
            var span = new TextSpan(position, 0);
            location = Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
        }
        context.ReportDiagnostic(Diagnostic.Create(InvalidInput, location, message));
    }

    private sealed class InputException(string message, int line = 0, int column = 0) : Exception(message)
    {
        public int Line { get; } = line;
        public int Column { get; } = column;
    }
}
