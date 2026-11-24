using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Core30.Core;

internal static class XmlContracts
{
    internal static XDocument Read(Stream input)
    {
        using var reader = XmlReader.Create(input, XmlInput.CreateReaderSettings());
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    internal static void Validate(XDocument document, Stream schema, string contract, string rootName)
    {
        EnsureRoot(document, contract, rootName);
        using var reader = XmlReader.Create(schema, XmlInput.CreateReaderSettings());
        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(contract, reader);
        schemas.Compile();
        document.Validate(schemas, (source, error) => Reject(source, error, contract));
    }

    private static void EnsureRoot(XDocument document, string contract, string rootName)
    {
        if (document.Root?.Name != XName.Get(rootName, contract))
            throw new InvalidDataException($"XML 根元素须为 {contract} 的 {rootName}。");
    }

    private static void Reject(object? source, ValidationEventArgs error, string contract)
    {
        var position = source as IXmlLineInfo;
        throw new InvalidDataException(
            $"XML 不符合 {contract}（行 {position?.LineNumber ?? 0}，列 {position?.LinePosition ?? 0}）。",
            error.Exception);
    }
}
