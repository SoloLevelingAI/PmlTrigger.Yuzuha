using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Core30.Requests;

public sealed partial class RequestGenerator
{
    private static readonly XName Insert = XName.Get("insert", "urn:core30:request-generator");

    private static string GenerateElement(XElement template, XmlSchemaElement schema,
        Dictionary<string, Parameter> parameters)
    {
        if (template.Name.Namespace != XNamespace.None || schema.QualifiedName.Namespace.Length == 0)
            throw Invalid(template, "Template elements must be unqualified and correspond to a qualified schema element.");

        var arguments = new List<string> { "Contract + " + Literal(template.Name.LocalName) };
        var type = schema.ElementSchemaType as XmlSchemaComplexType;
        foreach (var attribute in template.Attributes().Where(item => !item.IsNamespaceDeclaration))
        {
            var name = new XmlQualifiedName(attribute.Name.LocalName, attribute.Name.NamespaceName);
            if (FindAttribute(type, name) is not { } declaration)
                throw Invalid(attribute, $"'{name}' is not a supported attribute on <{template.Name.LocalName}>.");
            arguments.Add(GenerateAttribute(attribute, declaration, parameters));
        }
        if (type is not null)
        {
            foreach (XmlSchemaAttribute attribute in type.AttributeUses.Values)
            {
                var name = XName.Get(attribute.QualifiedName.Name, attribute.QualifiedName.Namespace);
                if (attribute.Use == XmlSchemaUse.Required && template.Attribute(name) is null)
                    throw Invalid(template, $"<{template.Name.LocalName}> requires attribute '{name}'.");
            }
        }

        var children = ChildElements(type?.ContentTypeParticle).ToArray();
        foreach (var content in template.Nodes())
        {
            if (content is XElement child && child.Name == Insert)
            {
                arguments.Add(GenerateInsertion(child, children, parameters));
            }
            else if (content is XElement element)
            {
                var elementSchema = FindChild(element, element.Name.LocalName, children);
                arguments.Add(GenerateElement(element, elementSchema, parameters));
            }
            else if (content is XText text && !string.IsNullOrWhiteSpace(text.Value))
            {
                if (schema.ElementSchemaType?.Datatype is not { } datatype)
                    throw Invalid(text, $"<{template.Name.LocalName}> does not accept text.");
                arguments.Add(GenerateValue(text, text.Value, datatype, parameters, required: true));
            }
            else if (content is not (XComment or XText))
            {
                throw Invalid(content, "Template content can contain only child elements and scalar text.");
            }
        }
        return "new CoreData(" + string.Join(", ", arguments) + ")";
    }

    private static XmlSchemaAttribute? FindAttribute(XmlSchemaComplexType? type, XmlQualifiedName name) =>
        name.Namespace.Length == 0 && type?.AttributeUses[name] is XmlSchemaAttribute { Use: not XmlSchemaUse.Prohibited } attribute
            ? attribute : null;

    private static string GenerateAttribute(XAttribute attribute, XmlSchemaAttribute declaration,
        Dictionary<string, Parameter> parameters)
    {
        var value = GenerateValue(attribute, attribute.Value, declaration.AttributeSchemaType?.Datatype,
            parameters, declaration.Use == XmlSchemaUse.Required);
        if (declaration.FixedValue is { } fixedValue && fixedValue != attribute.Value)
            throw Invalid(attribute, $"Attribute '{attribute.Name}' must use the fixed value '{fixedValue}'.");
        var field = "new CoreField(" + Literal(attribute.Name.LocalName) + ", " + value + ")";
        return TryParameter(attribute, attribute.Value, parameters) is { IsOptional: true } parameter
            ? "(@" + parameter.Name + " is null ? null : " + field + ")"
            : field;
    }

    private static string GenerateInsertion(XElement insert, XmlSchemaElement[] children,
        Dictionary<string, Parameter> parameters)
    {
        var name = Required(insert, "param");
        var element = FindChild(insert, Required(insert, "element"), children);
        var parameter = FindParameter(insert, name, parameters);
        if (parameter.Type is not ("CoreData" or "CoreData?" or "IEnumerable<CoreData>"))
            throw Invalid(insert, $"Insertion parameter '{name}' must be CoreData or a sequence of sections.");
        if (parameter.Type == "IEnumerable<CoreData>" && (element.MinOccurs != 0 || element.MaxOccurs <= 1))
            throw Invalid(insert, $"An element sequence requires a repeating, optional schema element. '{element.Name}' does not qualify.");
        if (parameter.IsOptional && element.MinOccurs != 0)
            throw Invalid(insert, $"Nullable insertion for required element '{element.Name}' is unsupported.");
        if (insert.HasElements || !string.IsNullOrWhiteSpace(insert.Value))
            throw Invalid(insert, "The insertion marker must be empty.");
        return "@" + name;
    }

    private static string GenerateValue(XObject location, string raw, XmlSchemaDatatype? datatype,
        Dictionary<string, Parameter> parameters, bool required)
    {
        if (TryParameter(location, raw, parameters) is { } parameter)
        {
            if (parameter.Type is "CoreData" or "CoreData?" or "IEnumerable<CoreData>")
                throw Invalid(location, $"Parameter '{parameter.Name}' must be scalar here.");
            if (required && parameter.IsOptional)
                throw Invalid(location, $"Nullable parameter '{parameter.Name}' cannot supply a required XML value.");
            if (!MatchesDatatype(parameter.Type, datatype?.TypeCode))
                throw Invalid(location, $"Parameter '{parameter.Name}' is not compatible with the XML datatype.");
            return "@" + parameter.Name;
        }

        try
        {
            var names = new NameTable();
            datatype?.ParseValue(raw, names, new XmlNamespaceManager(names));
        }
        catch (Exception error) when (error is XmlSchemaException or FormatException or OverflowException)
        {
            throw Invalid(location, $"Literal '{raw}' cannot be used here. {error.Message}");
        }
        return Literal(raw);
    }

    private static bool MatchesDatatype(string type, XmlTypeCode? code)
    {
        return type switch
        {
            "bool" or "bool?" => code == XmlTypeCode.Boolean,
            "int" or "int?" or "long" or "long?" => code is XmlTypeCode.Decimal or XmlTypeCode.Integer or
                XmlTypeCode.Int or XmlTypeCode.Long or XmlTypeCode.Short or XmlTypeCode.Byte or
                XmlTypeCode.UnsignedInt or XmlTypeCode.UnsignedLong or XmlTypeCode.UnsignedShort or XmlTypeCode.UnsignedByte or
                XmlTypeCode.PositiveInteger or XmlTypeCode.NonNegativeInteger or XmlTypeCode.NegativeInteger or XmlTypeCode.NonPositiveInteger,
            _ => true
        };
    }

    private static Parameter? TryParameter(XObject node, string value, Dictionary<string, Parameter> parameters) =>
        value.StartsWith("$", StringComparison.Ordinal) ? FindParameter(node, value.Substring(1), parameters) : null;

    private static Parameter FindParameter(XObject node, string name, Dictionary<string, Parameter> parameters)
    {
        if (!parameters.TryGetValue(name, out var parameter))
            throw Invalid(node, $"Unknown parameter '{name}'.");
        parameter.Used = true;
        return parameter;
    }

    private static XmlSchemaElement FindChild(XObject location, string name, IEnumerable<XmlSchemaElement> children) =>
        children.FirstOrDefault(item => item.QualifiedName.Name == name)
        ?? throw Invalid(location, $"Element '{name}' cannot be used here.");

    private static XmlSchemaElement FindFragment(XmlSchemaElement root, XElement template)
    {
        var pending = new Queue<XmlSchemaElement>();
        var visited = new HashSet<XmlSchemaElement>();
        pending.Enqueue(root);
        while (pending.Count != 0)
        {
            var element = pending.Dequeue();
            if (!visited.Add(element))
                continue;
            if (element != root && element.QualifiedName.Name == template.Name.LocalName)
                return element;
            if (element.ElementSchemaType is XmlSchemaComplexType type)
            {
                foreach (var child in ChildElements(type.ContentTypeParticle))
                    pending.Enqueue(child);
            }
        }
        throw Invalid(template, $"'{template.Name.LocalName}' is not an element in this request schema.");
    }

    private static IEnumerable<XmlSchemaElement> ChildElements(XmlSchemaParticle? particle)
    {
        if (particle is XmlSchemaElement element)
            yield return element;
        if (particle is XmlSchemaGroupBase group)
        {
            foreach (var item in group.Items.OfType<XmlSchemaParticle>())
            {
                foreach (var child in ChildElements(item))
                    yield return child;
            }
        }
    }

    private sealed class Parameter
    {
        public Parameter(string name, string type, XElement location)
        {
            if (type is not ("string" or "string?" or "bool" or "bool?" or "int" or "int?" or "long" or "long?"
                or "CoreData" or "CoreData?" or "IEnumerable<CoreData>"))
                throw Invalid(location, $"Unsupported parameter type '{type}'.");
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public string Type { get; }
        public bool IsOptional => Type.EndsWith("?", StringComparison.Ordinal);
        public bool Used { get; set; }
    }
}
