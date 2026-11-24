using System.Text.RegularExpressions;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed record InstalledLaunchModule(string Name, string Executable, string Source)
{
    public CoreData ToData(bool? defined = null) => new("module", new CoreField("name", Name),
        new CoreField("executable", Executable), new CoreField("source", Source),
        defined is null ? null : new CoreField("defined", defined));
}

/// <summary>Reads module arguments and their executable targets from the vendor's installed launcher.</summary>
internal static partial class InstalledLaunchModules
{
    public static InstalledLaunchModule[] Read(string product, string installation)
    {
        var path = Path.Combine(installation, product == "E3D" ? "launch.bat" : "marine.bat");
        try
        {
            var script = LocalText.Decode(FileContent.ReadBounded(path, 512 * 1024)).Content;
            return ModuleBranch().Matches(script)
                .Select(match => Describe(match.Groups["name"].Value, match.Groups["exe"].Value, path))
                .Append(product == "E3D" ? DescribeDraw(installation) : null)
                .Append(MonitorBranch().IsMatch(script) ? Describe("Monitor", "mon", path) : null)
                .OfType<InstalledLaunchModule>()
                .DistinctBy(module => module.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return []; }
    }

    private static InstalledLaunchModule? Describe(string name, string executable, string script)
    {
        var file = Path.Combine(Path.GetDirectoryName(script)!, executable + ".exe");
        return File.Exists(file) ? new(name, file, script) : null;
    }

    private static InstalledLaunchModule? DescribeDraw(string installation)
    {
        var addins = Path.Combine(installation, "DrawAddins.xml");
        return File.Exists(addins) ? Describe("Draw", "draw", addins) : null;
    }

    [GeneratedRegex("""^[ \t]*@?if /i not "%moduleArg%"=="(?<name>[A-Za-z0-9]+)" goto [^\r\n]+\r?\n[ \t]*set executable=(?<exe>[A-Za-z0-9_]+)[ \t]*\r?$""",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModuleBranch();

    [GeneratedRegex("""^[ \t]*@?if /i "%moduleArg%"=="Monitor" set moduleArg=[ \t]*\r?$""",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonitorBranch();
}
