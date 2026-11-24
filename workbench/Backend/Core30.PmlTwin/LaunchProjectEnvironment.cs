using System.Collections;
using System.Text.RegularExpressions;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed record LaunchProjectSource(string Code, string Directory, string SystemDatabase, string Source,
    bool Registered, bool Found);

/// <summary>Reads project assignments and local SYSTEM database locations without executing startup scripts.</summary>
internal sealed partial class LaunchProjectEnvironment(LaunchCatalogSources sources, IReadOnlyDictionary<string, string> environment)
{
    private static readonly EnumerationOptions LocalChildren = new() { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
    private readonly Dictionary<string, string> _variables = new(environment, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LaunchProjectSource> _registered = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _scripts = new(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string> ReadEnvironment() => Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
        .Where(entry => entry.Key is string key && IsProjectVariable(key))
        .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.OrdinalIgnoreCase);

    private static bool IsProjectVariable(string name) => name.Equals("projects_dir", StringComparison.OrdinalIgnoreCase)
        || ProjectVariable().IsMatch(name);

    public LaunchProjectSource[] Read(string product, string installation)
    {
        foreach (var (name, path) in environment.Where(item => ProjectVariable().IsMatch(item.Key)))
            RememberProject(name[..3], path, "Windows 环境变量");
        var primary = Path.Combine(installation, product == "E3D" ? "evars.init" : "evars.bat");
        var fallback = Path.Combine(installation, "evars.bat");
        ReadScript(sources.Observe(primary).Exists ? primary : fallback, 0);
        ReadProjectRoot();
        return _registered.Values.OrderBy(project => project.Code, StringComparer.Ordinal).ToArray();
    }

    private void ReadScript(string path, int depth)
    {
        if (depth > 6 || _scripts.Count >= 128 || !_scripts.Add(path) || !sources.Observe(path).Exists)
            return;
        try
        {
            using var lines = new StringReader(LocalText.Decode(FileContent.ReadBounded(path, 512 * 1024)).Content);
            for (var line = lines.ReadLine(); line is not null; line = lines.ReadLine())
                ReadLine(line, path, depth);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { }
    }

    private void ReadLine(string line, string script, int depth)
    {
        var assignment = SetLine().Match(line);
        if (assignment.Success)
        {
            SetVariable(assignment.Groups[1].Value, assignment.Groups[2].Value, script);
            return;
        }
        var call = CallLine().Match(line);
        if (call.Success && ResolvePath(Expand(call.Groups[1].Success ? call.Groups[1].Value : call.Groups[2].Value, script),
                Path.GetDirectoryName(script)) is { } include)
            ReadScript(include, depth + 1);
    }

    private void SetVariable(string name, string value, string script)
    {
        var expanded = Expand(value, script);
        _variables[name] = expanded;
        if (ProjectVariable().IsMatch(name))
            RememberProject(name[..3], expanded, script);
    }

    private string Expand(string value, string script) => PercentVariable().Replace(value.Trim().Trim('"')
        .Replace("%~dp0", Path.GetDirectoryName(script)! + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
        match => _variables.GetValueOrDefault(match.Groups[1].Value) ?? Environment.GetEnvironmentVariable(match.Groups[1].Value) ?? match.Value);

    private void RememberProject(string code, string path, string source)
    {
        var key = code.ToUpperInvariant();
        if (ResolvePath(path) is { } directory)
            _registered[key] = Describe(key, directory, source, registered: true);
    }

    private void ReadProjectRoot()
    {
        if (ResolvePath(_variables.GetValueOrDefault("projects_dir", "")) is not { } root)
            return;
        foreach (var directory in Children(root).SelectMany(child => new[] { child }.Concat(Children(child))).Take(500))
        {
            var match = ProjectVariable().Match(Path.GetFileName(directory));
            if (match.Success)
                AddUnregistered(match.Groups[1].Value.ToUpperInvariant(), directory, root);
        }
    }

    private string[] Children(string directory)
    {
        if (!sources.Observe(directory, directory: true).Exists)
            return [];
        try { return Directory.EnumerateDirectories(directory, "*", LocalChildren).Take(160).ToArray(); }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return []; }
    }

    private void AddUnregistered(string code, string directory, string root)
    {
        if (_registered.ContainsKey(code))
            return;
        var project = Describe(code, directory, root, registered: false);
        if (project.Found)
            _registered.Add(code, project);
    }

    private static LaunchProjectSource Describe(string code, string directory, string source, bool registered)
    {
        var system = Path.Combine(directory, code.ToLowerInvariant() + "sys");
        return new(code, directory, system, source, registered, File.Exists(system));
    }

    private static string? ResolvePath(string path, string? parent = null)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('%') || path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            return null;
        try
        {
            return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : parent is null ? null : Path.GetFullPath(path, parent);
        }
        catch (ArgumentException) { return null; }
    }

    [GeneratedRegex(@"^([a-z0-9]{3})000$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProjectVariable();
    [GeneratedRegex("^\\s*@?set\\s+\"?([a-z_][a-z0-9_]*)\\s*=\\s*(.*?)\"?\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SetLine();
    [GeneratedRegex("^\\s*@?(?:if\\s+exist\\s+(?:\"[^\"]*\"|\\S+)\\s+)?call\\s+(?:\"([^\"]+\\.bat)\"|(\\S+\\.bat))(?=\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CallLine();
    [GeneratedRegex(@"%([a-z_][a-z0-9_]*)%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PercentVariable();
}
