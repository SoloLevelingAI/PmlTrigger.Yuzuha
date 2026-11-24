using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace Core30.PmlTwin;

/// <summary>Collects Windows location hints without launching or modifying AVEVA.</summary>
internal sealed class WindowsAvevaEnvironmentInventory : IAvevaEnvironmentInventory
{
    private static readonly string[] InstallVariables =
        ["EVARTHREED", "AVEVA_DESIGN_EXE", "AVEVA_ROOT", "PDMSEXE", "PDMS_EXE", "MARINE_EXE"];
    private static readonly HashSet<string> ProductProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "mon", "des", "design", "pdms", "marine", "e3d", "adm", "draft", "draw",
        "marodes", "marhdes", "marodra", "mardra", "e3ddesign"
    };

    public EnvironmentClues Collect(string mode)
    {
        if (mode == "custom")
            return new EnvironmentClues([], [], 0, []);
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Windows 环境扫描需要 Windows，指定目录可使用 custom 模式。");
        return CollectWindows(mode);
    }

    [SupportedOSPlatform("windows")]
    internal static bool IsInstalledSdkLocation(string directory, AvevaProduct product)
    {
        var roots = new List<ScanRoot>();
        ReadUninstallRoots(roots, []);
        return roots.Any(root => root.Product == product &&
            string.Equals(root.Path, Path.TrimEndingDirectorySeparator(directory), StringComparison.OrdinalIgnoreCase))
            || IsStandardVendorLocation(directory, product);
    }

    private static bool IsStandardVendorLocation(string directory, AvevaProduct product)
    {
        var roots = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => Environment.GetFolderPath(folder)).Where(Path.IsPathFullyQualified)
            .Select(path => Path.Combine(path, "AVEVA")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (product == AvevaProduct.AM)
            roots.Add(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "AVEVA", "Marine"));
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (!current.Exists || current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;
            if (roots.Contains(current.FullName))
                return AvevaInstallationEvidence.IsAvevaVendor(FileVersionInfo.GetVersionInfo(Path.Combine(directory, "mon.exe")));
        }
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static EnvironmentClues CollectWindows(string mode)
    {
        var issues = new List<ScanIssue>();
        var roots = new List<ScanRoot>();
        var views = ReadUninstallRoots(roots, issues);
        ReadEnvironmentRoots(roots, issues);
        var processes = ReadProcesses(issues);
        roots.AddRange(processes.Select(process => new ScanRoot(Path.GetDirectoryName(process.Executable)!, "process", process.Product)));
        var disks = FixedDisks();
        roots.AddRange(DefaultLocations(disks));
        if (mode == "fixed-drives")
            roots.AddRange(disks.Select(path => new ScanRoot(path, "fixed-drive")));
        return new EnvironmentClues(roots.ToArray(), processes, views, issues.ToArray());
    }

    private static string[] FixedDisks() => DriveInfo.GetDrives()
        .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
        .Select(drive => drive.RootDirectory.FullName).ToArray();

    private static IEnumerable<ScanRoot> DefaultLocations(string[] disks) =>
        disks.SelectMany(path => new[]
        {
            Path.Combine(path, "AVEVA"), Path.Combine(path, "Program Files", "AVEVA"),
            Path.Combine(path, "Program Files (x86)", "AVEVA")
        }).Concat(new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AVEVA"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AVEVA")
        }).Where(Path.IsPathFullyQualified).Select(path => new ScanRoot(Path.TrimEndingDirectorySeparator(path), "default"));

    [SupportedOSPlatform("windows")]
    private static int ReadUninstallRoots(List<ScanRoot> roots, List<ScanIssue> issues)
    {
        var count = 0;
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            count += ReadRegistryView(hive, view, roots, issues);
        return count;
    }

    [SupportedOSPlatform("windows")]
    private static int ReadRegistryView(RegistryHive hive, RegistryView view, List<ScanRoot> roots, List<ScanIssue> issues)
    {
        var source = $"registry:{hive}/{view}";
        try
        {
            using var registry = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is not null)
                ReadInstallLocations(uninstall, source, roots);
            return 1;
        }
        catch (Exception error) when (IsSourceError(error))
        {
            issues.Add(new ScanIssue(source, "", error.GetType().Name));
            return 0;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReadInstallLocations(RegistryKey uninstall, string source, List<ScanRoot> roots)
    {
        foreach (var name in uninstall.GetSubKeyNames())
        {
            using var entry = uninstall.OpenSubKey(name);
            var product = AvevaInstallationEvidence.ProductFromLabel(entry?.GetValue("DisplayName") as string);
            if (product is null)
                continue;
            var location = NormalizeLocalPath(entry?.GetValue("InstallLocation") as string);
            if (location is not null)
                roots.Add(new ScanRoot(location, source, product, entry?.GetValue("DisplayVersion") as string));
        }
    }

    private static void ReadEnvironmentRoots(List<ScanRoot> roots, List<ScanIssue> issues)
    {
        foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        foreach (var variable in InstallVariables)
            ReadEnvironmentRoot(roots, issues, target, variable);
    }

    private static void ReadEnvironmentRoot(List<ScanRoot> roots, List<ScanIssue> issues, EnvironmentVariableTarget target, string variable)
    {
        var source = $"environment:{target}/{variable}";
        try
        {
            var path = NormalizeLocalPath(Environment.GetEnvironmentVariable(variable, target));
            if (path is not null)
                roots.Add(new ScanRoot(path, source));
        }
        catch (Exception error) when (IsSourceError(error))
        {
            issues.Add(new ScanIssue(source, "", error.GetType().Name));
        }
    }

    private static AvevaProcess[] ReadProcesses(List<ScanIssue> issues)
    {
        var results = new List<AvevaProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var candidate = InspectProcess(process, issues);
                if (candidate is not null)
                    results.Add(candidate);
            }
        }
        return results.OrderBy(process => process.ProcessId).ToArray();
    }

    private static AvevaProcess? InspectProcess(Process process, List<ScanIssue> issues)
    {
        try
        {
            return ProductProcesses.Contains(process.ProcessName) ? IdentifyProcess(process) : null;
        }
        catch (Exception error) when (IsSourceError(error) || error is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            issues.Add(new ScanIssue("process", $"PID {process.Id}", error.GetType().Name));
            return null;
        }
    }

    private static AvevaProcess? IdentifyProcess(Process process)
    {
        var binary = process.MainModule?.FileVersionInfo;
        var path = NormalizeLocalPath(binary?.FileName);
        if (path is null || !AvevaInstallationEvidence.IsAvevaVendor(binary))
            return null;
        var product = AvevaInstallationEvidence.ProductFromLabel(binary?.ProductName)
            ?? AvevaInstallationEvidence.ProductFromDirectory(Path.GetDirectoryName(path)!);
        return product is { } family
            ? new AvevaProcess(process.Id, process.StartTime.ToUniversalTime().Ticks, family, path) : null;
    }

    internal static string? NormalizeLocalPath(string? value)
    {
        var path = Environment.ExpandEnvironmentVariables(value?.Trim().Trim('"') ?? "");
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(path))
            return null;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsSourceError(Exception error) => error is IOException or UnauthorizedAccessException or SecurityException
        or ArgumentException;
}
