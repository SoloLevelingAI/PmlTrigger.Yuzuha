using System.Collections.ObjectModel;
using System.Diagnostics;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

/// <summary>Find actual AVEVA monitors; bounded, read-only, and never traverse directory links.</summary>
public sealed class FindInstalledAveva(IFileSystem files) : IApplicationDiscovery
{
    public const string Version = "1.2.0";
    public static DiscoveryOptions DefaultOptions => new(
        [@"C:\Program Files (x86)\AVEVA", @"C:\Program Files\AVEVA", @"C:\AVEVA\Marine", @"C:\AVEVA\PDMS", @"C:\AVEVA\AM"],
        MaximumDepth: 5, MaximumFiles: 300000, TimeoutSeconds: 30,
        Presets: new Dictionary<string, string>
        {
            ["Product"] = "E3D",
            ["Module"] = "DESIGN",
            ["Mdb"] = "ALL"
        });

    public Task<DiscoveryReport> DiscoverAsync(DiscoveryOptions options, CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(options, cancellationToken), cancellationToken);

    private DiscoveryReport Scan(DiscoveryOptions options, CancellationToken ct)
    {
        if (options.MaximumDepth < 0 || options.MaximumFiles < 1 || options.TimeoutSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(options));

        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var diagnostics = new List<DiscoveryDiagnostic>();
        var found = new List<DiscoveryCandidate>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        var limited = false;

        foreach (var root in options.SearchRoots)
        {
            if (!files.DirectoryExists(root))
            {
                diagnostics.Add(new("RootMissing", root, "Search root does not exist."));
                continue;
            }
            var pending = new Stack<(string Path, int Depth)>();
            pending.Push((Path.GetFullPath(root), 0));
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (clock.Elapsed.TotalSeconds >= options.TimeoutSeconds)
                {
                    diagnostics.Add(new("Timeout", root, "Configured scan time limit reached."));
                    limited = true;
                    break;
                }
                var item = pending.Pop();
                if (!visited.Add(item.Path)) continue;
                try
                {
                    foreach (var file in files.EnumerateFiles(item.Path))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (++count > options.MaximumFiles)
                        {
                            diagnostics.Add(new("FileLimit", item.Path, "Configured file count limit reached."));
                            limited = true;
                            break;
                        }
                        if (!IsE3dMonitor(file) && !IsMarineMonitor(file) && !IsPdmsMonitor(file)) continue;
                        found.Add(Prefill(file, item.Path, options, ct));
                    }
                    if (limited) break;
                    if (item.Depth < options.MaximumDepth)
                        foreach (var directory in files.EnumerateDirectories(item.Path))
                            if (!files.IsReparsePoint(directory)) pending.Push((directory, item.Depth + 1));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(new("ReadFailed", item.Path, ex.Message));
                }
            }
            if (limited) break;
        }
        return new(Guid.NewGuid(), started, DateTimeOffset.UtcNow,
            found.OrderBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            diagnostics.ToArray(), Version);
    }

    private DiscoveryCandidate Prefill(string executable, string directory, DiscoveryOptions options, CancellationToken ct)
    {
        var sources = new Dictionary<string, string>
        {
            ["ExecutablePath"] = "Discovered",
            ["WorkingDirectory"] = "Discovered",
            ["Protocol"] = "Discovered",
            ["PasswordPolicy"] = "Preset"
        };
        string Preset(string key)
        {
            if (options.Presets is null || !options.Presets.TryGetValue(key, out var value)) return "";
            sources[key] = "Preset";
            return value;
        }
        var productName = IsMarineMonitor(executable) ? "AVEVA Marine" : IsPdmsMonitor(executable) ? "AVEVA PDMS" : "AVEVA Everything3D";
        var input = new LaunchInput
        {
            DisplayName = $"{productName} \u00B7 {Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}",
            ExecutablePath = executable,
            WorkingDirectory = directory,
            Protocol = EntryProtocol.Monitor,
            PasswordPolicy = PasswordPolicy.Prompt,
            Product = Preset("Product"),
            InitializationFile = Preset("InitializationFile"),
            Project = Preset("Project"),
            User = Preset("User"),
            Mdb = Preset("Mdb"),
            Module = Preset("Module"),
            LicenceServer = Preset("LicenceServer"),
            Sources = sources
        };
        var initialization = Path.Combine(directory, "launch.init");
        if (string.IsNullOrEmpty(input.InitializationFile) && files.FileExists(initialization))
        {
            input.InitializationFile = initialization;
            sources["InitializationFile"] = "Discovered";
        }
        if (int.TryParse(Preset("LicencePort"), out var port)) input.LicencePort = port;
        var environment = new FindAvevaProjectsAndModules(files).Execute(directory, options.ProjectModules, ct);
        var projectChoices = environment.Projects.Where(project => project.DirectoryExists && project.Origin.Kind == "Assignment")
            .GroupBy(project => project.Code, StringComparer.OrdinalIgnoreCase).ToArray();
        if (string.IsNullOrWhiteSpace(input.Project) && projectChoices.Length == 1)
        {
            input.Project = projectChoices[0].Key;
            sources["Project"] = "StaticCandidate";
        }
        var moduleChoices = environment.Modules.Where(module => module.Origin.Kind == "Assignment")
            .GroupBy(module => module.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (string.IsNullOrWhiteSpace(input.Module) && moduleChoices.Length == 1)
        {
            input.Module = moduleChoices[0].Key;
            sources["Module"] = "StaticCandidate";
        }
        return new(input.ConfigurationId, input.DisplayName, executable, input.Protocol,
            input.CreateSnapshot(Guid.Empty), new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(sources)), environment);
    }

    private static bool IsE3dMonitor(string path) =>
        Path.GetFileName(path).Equals("mon.exe", StringComparison.OrdinalIgnoreCase) &&
        path.Contains("Everything3D", StringComparison.OrdinalIgnoreCase);
    private static bool IsMarineMonitor(string path) =>
        Path.GetFileName(path).Equals("mon.exe", StringComparison.OrdinalIgnoreCase) &&
        (path.Contains("Marine", StringComparison.OrdinalIgnoreCase) || path.Split('\\', '/').Contains("AM", StringComparer.OrdinalIgnoreCase));
    private static bool IsPdmsMonitor(string path) =>
        Path.GetFileName(path).Equals("mon.exe", StringComparison.OrdinalIgnoreCase) &&
        path.Contains("PDMS", StringComparison.OrdinalIgnoreCase);
}
