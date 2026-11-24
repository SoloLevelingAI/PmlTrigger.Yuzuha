using System.Security.Cryptography;
using System.Xml;

namespace Core30.Core;

/// <summary>Shared limits and encoding helpers for workspace documents.</summary>
public static class FileContent
{
    public static byte[] ReadBounded(string path, int limit)
    {
        using var stream = File.OpenRead(path);
        var length = stream.Length;
        if (length > limit)
            throw new InvalidDataException($"Input exceeds {limit} bytes.");
        var bytes = new byte[(int)length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public static ReadOnlySpan<byte> StripUtf8Bom(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith("\uFEFF"u8) ? bytes[3..] : bytes;

    public static string XmlText(string value) => XmlConvert.VerifyXmlChars(value);

    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static void RequireMatchingSha256(string left, string right, string message)
    {
        if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(message);
    }
}
