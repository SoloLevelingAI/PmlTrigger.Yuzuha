using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Core30.Core;

/// <summary>Translates Core data at the XML interface, including XML files supplied by other tools.</summary>
public static class CoreXml
{
    public static CoreData Read(XElement input) => new(input.Name,
        input.Attributes().Where(field => !field.IsNamespaceDeclaration).Select(field => new CoreField(field.Name, field.Value)),
        input.Nodes().Select<XNode, object?>(node => node switch
        {
            XElement child => Read(child),
            XText text => text.Value,
            _ => null
        }));

    public static XElement Write(CoreData input) => new(input.Name,
        input.Fields().Select(field => new XAttribute(field.Name, field.Value)),
        input.Body.Select(item => item is CoreData child ? (object)Write(child) : item));

    public static XElement WriteReceipt<T>(CoreReceipt<T> receipt, Func<T, XElement> writeResult) =>
        new("run", new XAttribute("feature", receipt.Feature), new XAttribute("version", receipt.Version),
            new XAttribute("status", "ok"), new XAttribute("configuration", receipt.Configuration),
            new XAttribute("log", receipt.Log), writeResult(receipt.Result));

    internal static void Validate(CoreData data, Stream schema, string contract)
    {
        if (data.Name != XName.Get("request", contract))
            throw new InvalidDataException($"请求须使用 {contract} 契约。");
        using var input = XmlReader.Create(schema, XmlInput.CreateReaderSettings());
        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(contract, input);
        schemas.Compile();
        var names = new NameTable();
        var validator = new XmlSchemaValidator(names, schemas, new XmlNamespaceManager(names), XmlSchemaValidationFlags.None);
        validator.ValidationEventHandler += (_, error) =>
            throw new InvalidDataException($"参数不符合 {contract}。", error.Exception);
        validator.Initialize();
        ValidateSection(validator, data);
        validator.EndValidation();
    }

    private static void ValidateSection(XmlSchemaValidator validator, CoreData data)
    {
        validator.ValidateElement(data.Name.LocalName, data.Name.NamespaceName, null);
        foreach (var field in data.Fields())
            validator.ValidateAttribute(field.Name.LocalName, field.Name.NamespaceName, XmlConvert.VerifyXmlChars(field.Value), null);
        validator.ValidateEndOfAttributes(null);
        foreach (var item in data.Body)
        {
            if (item is CoreData child)
                ValidateSection(validator, child);
            else
                validator.ValidateText(XmlConvert.VerifyXmlChars((string)item));
        }
        validator.ValidateEndElement(null);
    }
}
