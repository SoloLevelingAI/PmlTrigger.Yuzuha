using System.Text;
using System.Text.RegularExpressions;
using Core30.Core;

namespace Core30.PmlTwin;

internal interface IEvarsIntegration
{
    EvarsPatch Integrate(byte[] original, string evarsPath, string library, string encodingName);
    EvarsPatch PatchInstalled(string path, byte[] original, string? library, string? uiDirectory, string? product);
}

internal sealed record EvarsPatch(byte[] Bytes, bool YuzuhaRegistered, bool? LibraryAlreadyRegistered,
    bool? UiAlreadyRegistered = null, string[]? AppendedSettings = null);

/// <summary>Preserves EVARS and appends missing PML directories to their own search lists.</summary>
internal sealed class WindowsEvarsIntegration : IEvarsIntegration
{
    private const RegexOptions IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex YuzuhaRegistration = new(
        @"^[ \t]*@?rem[ \t]+>>>[ \t]+Yuzuha managed settings[ \t]*\r?$",
        IgnoreCase | RegexOptions.Multiline, MatchTimeout);
    private static readonly Regex YuzuhaPath = new(
        @"(?:^|[\\/])(?:Yuzuha(?:Toolkit)?|PmlTrigger\.Yuzuha)(?:[\\/]|$)|^%YUZUHA(?:_[a-z0-9_]+)?%[\\/]",
        IgnoreCase, MatchTimeout);

    public EvarsPatch Integrate(byte[] original, string evarsPath, string library, string encodingName)
    {
        var folder = Path.GetDirectoryName(evarsPath)!;
        var target = Path.GetFullPath(library, folder);
        var literal = $"%~dp0{library.Replace('/', '\\')}";
        return AppendLibrary(original, Decode(original, encodingName), folder, target, literal);
    }

    public EvarsPatch PatchInstalled(string path, byte[] original, string? library, string? uiDirectory, string? product)
    {
        var source = LocalText.Decode(original);
        var current = PmlEnvironment.Parse(path, source.Content, product);
        var libraryRegistered = IsRegistered(library, current.Roots);
        var uiRegistered = IsRegistered(uiDirectory, current.ActiveUiRoots);
        var commands = new[] { MissingSetting("PMLLIB", library, libraryRegistered), MissingSetting(current.UiVariable, uiDirectory, uiRegistered) }
            .OfType<string>().ToArray();
        var updated = commands.Length == 0 ? original : AppendSettings(original, source, commands);
        return new EvarsPatch(updated, YuzuhaRegistration.IsMatch(source.Content), libraryRegistered, uiRegistered, commands);
    }

    private static bool? IsRegistered(string? directory, PmlSearchRoot[] roots) =>
        directory is null ? null : roots.Any(root => PathToken(root.Path).Equals(PathToken(directory), StringComparison.OrdinalIgnoreCase));

    private static string? MissingSetting(string variable, string? directory, bool? registered) =>
        registered == false ? $"set {variable}=%{variable}%;{directory}" : null;

    internal static bool IsLegacyPath(string path) => YuzuhaPath.IsMatch(path.Trim());

    private static EvarsPatch AppendLibrary(byte[] original, LocalText source, string folder, string? library, string? literal)
    {
        var yuzuhaRegistered = YuzuhaRegistration.IsMatch(source.Content);
        if (library is null)
            return new EvarsPatch(original, yuzuhaRegistered, null);
        var present = HasLibrary(source.Content, folder, library);
        var updated = present ? original : AppendSettings(original, source, [$"set PMLLIB=%PMLLIB%;{literal}"]);
        return new EvarsPatch(updated, yuzuhaRegistered, present);
    }

    private static bool HasLibrary(string content, string folder, string library)
    {
        var absolute = PathToken(library);
        var offset = Path.GetRelativePath(folder, library);
        var relative = PathToken(offset == "." ? "%~dp0" : $"%~dp0{offset}");
        var present = false;
        using var lines = new StringReader(content);
        while (lines.ReadLine() is { } line)
        {
            if (PmlEnvironment.ReadSetting(line) is { } setting &&
                setting.Key.Equals("PMLLIB", StringComparison.OrdinalIgnoreCase))
                present = setting.Value.Split(';').Any(part => IsRegistration(part, absolute, relative, present));
        }
        return present;
    }

    private static bool IsRegistration(string part, string absolute, string relative, bool inherited)
    {
        var token = PathToken(part);
        return token.Equals(absolute, StringComparison.OrdinalIgnoreCase) ||
            token.Equals(relative, StringComparison.OrdinalIgnoreCase) ||
            inherited && token.Equals("%PMLLIB%", StringComparison.OrdinalIgnoreCase);
    }

    private static string PathToken(string value) => value.Trim().Replace('/', '\\').TrimEnd('\\');

    private static byte[] AppendSettings(byte[] original, LocalText source, string[] commands)
    {
        var newline = source.Content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var separator = source.Content.Length == 0 || source.Content.EndsWith('\n') ? "" : newline;
        return [.. original, .. source.Encoding.GetBytes(separator + string.Join(newline, commands) + newline)];
    }

    private static LocalText Decode(byte[] original, string encodingName)
    {
        if (encodingName != "gbk")
            return LocalText.Decode(original);
        var gbk = CodePagesEncodingProvider.Instance.GetEncoding(936, EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback)!;
        return new LocalText(gbk.GetString(original), gbk, 0);
    }
}
