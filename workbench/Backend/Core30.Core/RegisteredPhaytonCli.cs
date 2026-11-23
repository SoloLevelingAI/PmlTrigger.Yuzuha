using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace Core30.Core;

/// <summary>Finds local Phayton CLI paths from the current user's installation registration.</summary>
public static class RegisteredPhaytonCli
{
    public static string[] FindPrograms() => OperatingSystem.IsWindows() ? FindWindowsPrograms() : [];

    public static string[] FindWorkbenchPrograms()
    {
        if (FindInstalledWorkbenchRoot() is { } root && ProgramIn(root) is { } installed) return [installed];
        var bundled = Path.Combine(AppContext.BaseDirectory, "phayton", "cli", "PmlTwin.Phayton.Cli.exe");
        if (File.Exists(bundled)) return [bundled];
        try
        {
            var registered = FindPrograms();
            if (registered.Length > 0) return registered;
        }
        catch (InvalidDataException error) when (error.InnerException is SecurityException) { }
        return FindStandardUserProgram() is { } standard ? [standard] : [];
    }

    public static string? FindInstalledWorkbenchRoot()
    {
        var desktop = new DirectoryInfo(AppContext.BaseDirectory);
        if (desktop.Parent is not { Parent: { } installation } runtime ||
            !desktop.Name.Equals("desktop", StringComparison.OrdinalIgnoreCase) ||
            !runtime.Name.Equals("runtime", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(installation.FullName, ".setup-payload.json"))) return null;
        return ProgramIn(installation.FullName) is not null ? installation.FullName : null;
    }

    public static string? FindStandardUserProgram()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
        var profileLocal = Path.IsPathFullyQualified(profile) ? Path.Combine(profile, "AppData", "Local") : "";
        return new[] { local, profileLocal }.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal))
            .Select(path => ProgramIn(Path.Combine(path, "YuzuhaToolkit", "PmlTrigger.Yuzuha"))).FirstOrDefault(path => path is not null);
    }

    [SupportedOSPlatform("windows")]
    private static string[] FindWindowsPrograms()
    {
        try
        {
            using var installations = Registry.CurrentUser.OpenSubKey(@"Software\YuzuhaToolkit\Installations");
            return installations?.GetSubKeyNames().Select(name => ReadProgram(installations, name)).OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        }
        catch (SecurityException error) { throw new InvalidDataException("无法读取当前用户的 Phayton 安装登记。", error); }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadProgram(RegistryKey installations, string name)
    {
        using var entry = installations.OpenSubKey(name);
        if (entry?.GetValue("InstallLocation") is not string directory || !Path.IsPathFullyQualified(directory)
            || directory.StartsWith("\\\\", StringComparison.Ordinal))
            return null;
        return ProgramIn(directory);
    }

    private static string? ProgramIn(string directory)
    {
        var program = Path.Combine(directory, "runtime", "cli", "PmlTwin.Phayton.Cli.exe");
        return File.Exists(program) ? program : null;
    }
}
