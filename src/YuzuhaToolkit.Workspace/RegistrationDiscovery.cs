using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace YuzuhaToolkit.Workspace;

/// <summary>从现有环境配置找到的目录或产品入口；检测不等于替用户更改注册。</summary>
/// <param name="Path">目录或程序绝对路径。</param>
/// <param name="Source">来源环境变量、注册表安装项或 EVAR 文件。</param>
/// <param name="Kind">pmllib 或 application。</param>
public sealed record RegistrationCandidate(string Path, string Source, string Kind);

/// <summary>只读发现本机产品入口与 Evar 中的注册目录；不执行厂商脚本。</summary>
public static partial class RegistrationDiscovery
{
    /// <summary>读取环境及有限的卸载项安装位置，检查对应 EVAR 文件的字面 PMLLIB 路径。</summary>
    public static RegistrationCandidate[] Discover()
    {
        var candidates = new List<RegistrationCandidate>();
        void Pml(string value, string source)
        {
            foreach (var part in value.Split(';'))
            {
                var expanded = Environment.ExpandEnvironmentVariables(part.Trim().Trim('"'));
                if (!Path.IsPathFullyQualified(expanded) || expanded.Contains('%') || !Directory.Exists(expanded)) continue;
                candidates.Add(new(Path.GetFullPath(expanded), source, "pmllib"));
            }
        }
        Pml(Environment.GetEnvironmentVariable("PMLLIB") ?? "", "当前进程 PMLLIB");
        if (OperatingSystem.IsWindows())
        {
            var applications = FindApplications(ProductRoots().Concat(new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AVEVA"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AVEVA"), @"C:\AVEVA", @"D:\AVEVA" }));
            candidates.AddRange(applications);
            foreach (var root in applications.Select(candidate => Path.GetDirectoryName(candidate.Path)!).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var name in new[] { "evars.init", "evar.init", "evars.bat", "evar.bat" })
                {
                    var file = Path.Combine(root, name);
                    try
                    {
                        if (!File.Exists(file) || new FileInfo(file).Length > 2097152) continue;
                        foreach (Match match in PmlAssignment().Matches(File.ReadAllText(file))) Pml(match.Groups[1].Value.TrimEnd('"'), file);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
                }
            }
        }
        return candidates.DistinctBy(candidate => (candidate.Path.ToUpperInvariant(), candidate.Kind)).ToArray();
    }

    // Adapted from LaunchDesk.Core/AppDiscovery.cs: bounded, read-only traversal;
    // legacy Marine InstallLocation points above OH12.1.SP4, not at its entry point.
    /// <summary>在限定根目录、深度与目录数内发现真实程序入口，不执行厂商脚本。</summary>
    /// <param name="roots">注册表安装位置或已确认的厂商根目录候选。</param>
    internal static RegistrationCandidate[] FindApplications(IEnumerable<string> roots)
    {
        var found = new List<RegistrationCandidate>(); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Path, int Depth)>();
        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)) queue.Enqueue((Path.GetFullPath(root), 0));
        while (queue.Count > 0 && visited.Count < 10000)
        {
            var (directory, depth) = queue.Dequeue(); if (!visited.Add(directory)) continue;
            try
            {
                var batch = new[] { "marine.bat", "marine.cmd", "pdms.bat", "pdms.cmd" }.Where(name => File.Exists(Path.Combine(directory, name))).ToArray();
                foreach (var name in batch) found.Add(new(Path.Combine(directory, name), "安装目录只读发现 · " + directory, "application"));
                var monitor = Path.Combine(directory, "mon.exe");
                if (batch.Length == 0 && File.Exists(monitor) && (File.Exists(Path.Combine(directory, "launch.init")) || directory.Contains("Everything3D", StringComparison.OrdinalIgnoreCase) || directory.Contains("PDMS", StringComparison.OrdinalIgnoreCase)))
                    found.Add(new(monitor, "安装目录只读发现 · " + directory, "application"));
                if (depth < 4) foreach (var child in Directory.EnumerateDirectories(directory))
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) queue.Enqueue((Path.GetFullPath(child), depth + 1));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        return found.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> ProductRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine }) foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames().Take(4000))
                {
                    using var entry = uninstall.OpenSubKey(name); var display = entry?.GetValue("DisplayName") as string ?? "";
                    if (!display.Contains("AVEVA", StringComparison.OrdinalIgnoreCase) && !display.Contains("PDMS", StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry?.GetValue("InstallLocation") is string location && Path.IsPathFullyQualified(location) && Directory.Exists(location)) roots.Add(Path.GetFullPath(location));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        return roots;
    }

    [GeneratedRegex("^\\s*@?set\\s+\"?pmllib=(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex PmlAssignment();
}
