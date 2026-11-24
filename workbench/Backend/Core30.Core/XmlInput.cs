using System.Xml;

namespace Core30.Core;

/// <summary>Creates XML reader settings for bounded local documents.</summary>
public static class XmlInput
{
    public static XmlReaderSettings CreateReaderSettings(long maxCharacters = 4 * 1024 * 1024) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = maxCharacters
    };
}
