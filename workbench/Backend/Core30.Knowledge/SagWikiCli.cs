using System.Diagnostics;
using System.Globalization;
using System.Text;
using Core30.Core;

namespace Core30.Knowledge;

internal sealed class SagWikiCli(FeatureContext home, ISagWikiProcess process)
{
    public string DataDirectory => home.FilePath("data");

    public string Run(string executable, IEnumerable<string> arguments, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = home.Root };
        start.Environment["SAG_DATA_DIR"] = DataDirectory;
        start.Environment["SAG_DESKTOP_DATA_DIR"] = DataDirectory;
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        return process.Run(start, timeout ?? TimeSpan.FromSeconds(105));
    }

    public string Index(string executable, string directory, string? extensions = null) =>
        Run(executable, extensions is null
            ? ["add", directory, "--json"]
            : ["add", directory, "--types", extensions, "--json"]);

    public string Search(string executable, string directory, string query, int limit, bool semantic = false)
    {
        string[] mode = semantic ? [] : ["-F", "-i"];
        return Run(executable, [.. mode, "-c", "--json", "--path", directory, "-m",
            limit.ToString(CultureInfo.InvariantCulture), "--timeout", "90", "--", query]);
    }

    public static string? FindInstalled(FeatureContext? home = null)
    {
        if (home is null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(appData, "AVEVA-Toolbox", "SAG-Wiki");
            if (Directory.Exists(path)) home = new FeatureContext(path);
        }
        if (home is not null && FindManaged(home) is { } managed) return managed;
        return ExternalPaths().FirstOrDefault(File.Exists);
    }

    internal static string? FindManaged(FeatureContext home)
    {
        if (ManagedVersion(home) is not { } version) return null;
        var executable = home.FilePath($"runtime/{version}/sag.exe");
        return File.Exists(executable) ? executable : null;
    }

    public static Version? ManagedVersion(FeatureContext home)
    {
        var pointer = home.FilePath("active-version.txt");
        return File.Exists(pointer) && Version.TryParse(Encoding.ASCII.GetString(FileContent.ReadBounded(pointer, 64)), out var version)
            ? version : null;
    }

    private static IEnumerable<string> ExternalPaths()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "sag.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SAG Wiki", "sag.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules",
            "@zleap-ai", "sag", "native", "win32-x64", "sag.exe");
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (Path.IsPathFullyQualified(entry.Trim('"')))
                yield return Path.Combine(entry.Trim('"'), "sag.exe");
    }
}
