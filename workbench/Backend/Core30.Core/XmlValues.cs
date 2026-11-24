using System.Xml.Linq;

namespace Core30.Core;

public static class XmlValues
{
    public static string AttributeText(XElement element, XName name) => (string?)element.Attribute(name) ?? "";

    public static IEnumerable<XElement> Children(XElement element, XName container, XName child) =>
        element.Element(container)?.Elements(child) ?? [];
}
