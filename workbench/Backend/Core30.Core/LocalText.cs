using System.Text;

namespace Core30.Core;

/// <summary>Decodes local AVEVA text without losing a file's original byte order mark.</summary>
public sealed record LocalText(string Content, Encoding Encoding, int PreambleLength)
{
    public static LocalText Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe }))
            return Read(bytes, new UnicodeEncoding(false, false, true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff }))
            return Read(bytes, new UnicodeEncoding(true, false, true), 2);
        try
        {
            return Read(bytes, new UTF8Encoding(false, true), bytes.AsSpan().StartsWith("\uFEFF"u8) ? 3 : 0);
        }
        catch (DecoderFallbackException)
        {
            var gbk = CodePagesEncodingProvider.Instance.GetEncoding(936, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback)!;
            return Read(bytes, gbk, 0);
        }
    }

    public byte[] Encode(byte[] original, string updated) =>
        [.. original.AsSpan(0, PreambleLength), .. Encoding.GetBytes(updated)];

    private static LocalText Read(byte[] bytes, Encoding encoding, int start) =>
        new(encoding.GetString(bytes.AsSpan(start)), encoding, start);
}
