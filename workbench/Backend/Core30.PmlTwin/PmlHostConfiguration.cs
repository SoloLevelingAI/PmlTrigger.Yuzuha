namespace Core30.PmlTwin;

public sealed record PmlSearchPaths(string ProgramDirectory, string[] Libraries, string[] Interfaces);

/// <summary>Resolves the Host registered by an EVARS file without executing the script.</summary>
public sealed record PmlHostConfiguration(string Profile, string Framework, string DllPath)
{
    public static PmlHostConfiguration Inspect(string evarsPath, string product, string release)
    {
        var environment = InstalledEvarsChange.Inspect(evarsPath, product);
        var (profile, framework) = ProfileFor(product, release);
        if (profile.Length == 0) return new(profile, framework, "");

        if (environment.ProgramDirectory.Length > 0)
        {
            var hasLibrary = environment.Roots.Any(root => root.State == "available" &&
                File.Exists(Path.Combine(root.Path, "Bootstrap", "YuzuhaResolveRuntimePath.pmlfnc")));
            var hasInterface = environment.ActiveUiRoots.Any(root => root.State == "available" &&
                File.Exists(Path.Combine(root.Path, "des", "Addins", "YuzuhaAddin")));
            return hasLibrary && hasInterface ? AtProgramDirectory(environment.ProgramDirectory, product, release) : new(profile, framework, "");
        }

        // Older EVARS registers sibling PML directories with a common program root.
        var libraries = environment.Roots.Select(root => PackageRoot(root, "PMLLIB"))
            .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var root in environment.ActiveUiRoots.Select(root => PackageRoot(root, "PMLUI")).OfType<string>())
        {
            if (!libraries.Contains(root)) continue;
            var candidate = AtProgramDirectory(root, product, release);
            if (candidate.DllPath.Length > 0) return candidate;
        }

        return new(profile, framework, "");
    }

    public static PmlSearchPaths ReadSearchPaths(string evarsPath, string product)
    {
        var environment = InstalledEvarsChange.Inspect(evarsPath, product);
        return new(environment.ProgramDirectory, environment.Roots.Where(root => root.State != "unresolved").Select(root => root.Path).ToArray(),
            environment.ActiveUiRoots.Where(root => root.State != "unresolved").Select(root => root.Path).ToArray());
    }

    public static PmlHostConfiguration AtProgramDirectory(string directory, string product, string release)
    {
        var (profile, framework) = ProfileFor(product, release);
        if (profile.Length == 0 || !Path.IsPathFullyQualified(directory)) return new(profile, framework, "");
        var dll = Path.Combine(directory, "runtime", "profiles", profile, framework, DllFileName(framework));
        return new(profile, framework, File.Exists(dll) ? dll : "");
    }

    private static (string Profile, string Framework) ProfileFor(string product, string release) => product switch
        {
            "AM" or "PDMS" => (product, "net35"),
            "E3D" when IsRelease(release, "3.1") => ("E3D3.1.6", "net48"),
            "E3D" when IsRelease(release, "2.1") || IsRelease(release, "2.10") => ("E3D2.1", "net48"),
            _ => ("", "")
        };

    internal static string DllFileName(string framework)
    {
        if (framework.Equals("net35", StringComparison.OrdinalIgnoreCase)) return "YuzuhaToolkit.PmlHost.Net35.dll";
        return framework.Equals("net48", StringComparison.OrdinalIgnoreCase) ? "YuzuhaToolkit.PmlHost.Net48.dll" : "";
    }

    private static string? PackageRoot(PmlSearchRoot root, string leaf) =>
        root.State == "available" && Path.GetFileName(root.Path).Equals(leaf, StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(root.Path) : null;

    private static bool IsRelease(string release, string family) =>
        release.Equals(family, StringComparison.OrdinalIgnoreCase) ||
        release.StartsWith(family + ".", StringComparison.OrdinalIgnoreCase);
}
