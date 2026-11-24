using System.Text;
using System.Text.RegularExpressions;

namespace Core30.Core;

public static class PmlSourceHeader
{
    private static readonly Regex PublishedPml = new(
        @"^--<(?<algorithm>\d{3})(?:-\d+)?>--[ \t]+Published PML\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public static string ReadAlgorithm(ReadOnlySpan<byte> bytes)
    {
        var prefix = bytes[..Math.Min(bytes.Length, 512)];
        var text = prefix.StartsWith(new byte[] { 0xff, 0xfe }) ? Encoding.Unicode.GetString(prefix[2..])
            : prefix.StartsWith(new byte[] { 0xfe, 0xff }) ? Encoding.BigEndianUnicode.GetString(prefix[2..])
            : Encoding.ASCII.GetString(FileContent.StripUtf8Bom(prefix));
        return PublishedPml.Match(text.Split(['\r', '\n'], 2)[0]).Groups["algorithm"].Value;
    }
}
