using System.IO.Enumeration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core30.Core;

public sealed record RibbonSearchRules(string[]? IncludeDirectories = null, string[]? ExcludeDirectories = null,
    string? NameContains = null)
{
    public static bool MatchesName(string name, string? keyword) =>
        string.IsNullOrWhiteSpace(keyword) || name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);

    public static RibbonSearchRules FromText(string include, string exclude, string name) =>
        new(DirectoriesFromText(include), DirectoriesFromText(exclude), name.Trim());

    private static string[] DirectoriesFromText(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record RibbonSearchRoot(string Directory, string Origin, bool Recursive = false);

public sealed record RibbonFileCandidate(string Path, string Origin)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public enum RibbonFileKind { All, Uic, Manifest }

public sealed record RibbonFileFilter(string Name = "", string Directory = "", RibbonFileKind Kind = RibbonFileKind.All)
{
    public bool Matches(string path)
    {
        var extension = Path.GetExtension(path);
        if (Kind == RibbonFileKind.Uic && !extension.Equals(".uic", StringComparison.OrdinalIgnoreCase)
            || Kind == RibbonFileKind.Manifest && !extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            return false;
        var directory = Directory.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return RibbonSearchRules.MatchesName(Path.GetFileName(path), Name)
            && (directory.Length == 0 || Path.GetDirectoryName(path)?.Contains(directory, StringComparison.OrdinalIgnoreCase) == true);
    }
}

public sealed record RibbonSearchTruncation(RibbonSearchRoot Root, bool Uics, bool Manifests);
public sealed record RibbonSearchUnavailable(RibbonSearchRoot Root, string Reason);

public sealed record RibbonSearchResult(RibbonFileCandidate[] Uics, RibbonFileCandidate[] Manifests)
{
    public IReadOnlyList<RibbonSearchTruncation> TruncatedSources { get; init; } = [];
    public IReadOnlyList<RibbonSearchUnavailable> UnavailableSources { get; init; } = [];
    public IReadOnlyList<string> ExcludedDirectories { get; init; } = [];
}

/// <summary>Shared automatic discovery for the desktop Ribbon selectors.</summary>
public sealed class RibbonFileDiscovery
{
    public const string RulesPath = ".core30/ribbon-search.json";
    private const int RuleLimit = 64 * 1024;
    private const int PerRootLimit = 300;
    private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
    private readonly string[] _includes;
    private readonly string[] _excludes;

    public RibbonFileDiscovery(string workspace) : this(workspace, Read(workspace)) { }

    public RibbonFileDiscovery(string workspace, RibbonSearchRules rules)
    {
        Workspace = new FeatureContext(workspace).Root;
        Rules = rules;
        _includes = Directories(Rules.IncludeDirectories, Workspace);
        _excludes = Directories(Rules.ExcludeDirectories, Workspace);
    }

    public string Workspace { get; }
    public RibbonSearchRules Rules { get; }
    public bool IsRestricted => _includes.Length != 0;

    public RibbonSearchResult Find(IEnumerable<RibbonSearchRoot> installations) => Find(installations, null);

    public RibbonSearchResult Find(IEnumerable<RibbonSearchRoot> installations, Action<RibbonFileCandidate>? manifestFound)
    {
        IEnumerable<RibbonSearchRoot> roots = IsRestricted
            ? _includes.Select(path => new RibbonSearchRoot(path, "指定目录", Recursive: true))
            : installations.Prepend(new RibbonSearchRoot(Workspace, "工作区", Recursive: true));
        var uics = new List<RibbonFileCandidate>();
        var manifests = new List<RibbonFileCandidate>();
        var truncated = new List<RibbonSearchTruncation>();
        var unavailable = new List<RibbonSearchUnavailable>();
        foreach (var root in roots.DistinctBy(root => root.Directory, Paths))
        {
            if (!Path.IsPathFullyQualified(root.Directory))
            {
                unavailable.Add(new(root, "来源目录需要完整路径。"));
                continue;
            }
            if (_excludes.Any(directory => IsInside(directory, root.Directory)))
                continue;
            if (!Directory.Exists(root.Directory))
            {
                unavailable.Add(new(root, "目录不存在或无法读取。"));
                continue;
            }
            try
            {
                if (Collect(root, uics, manifests, manifestFound) is { } item)
                    truncated.Add(item);
            }
            catch (Exception error) when (CoreRunner.IsOperationalError(error))
            {
                unavailable.Add(new(root, error.Message));
            }
        }
        return new(uics.DistinctBy(file => file.Path, Paths).ToArray(),
            manifests.DistinctBy(file => file.Path, Paths).ToArray())
        {
            TruncatedSources = truncated,
            UnavailableSources = unavailable,
            ExcludedDirectories = _excludes
        };
    }

    public static Task<RibbonSearchResult> PreviewRibbonSearchRulesAsync(string workspace, RibbonSearchRules draft,
        IReadOnlyList<RibbonSearchRoot> installations) =>
        Task.Run(() => new RibbonFileDiscovery(workspace, draft).Find(installations));

    public bool Accepts(string file) =>
        Path.IsPathFullyQualified(file) && !_excludes.Any(root => IsInside(root, file)) &&
        (!IsRestricted || _includes.Any(root => IsInside(root, file))) &&
        RibbonSearchRules.MatchesName(Path.GetFileName(file), Rules.NameContains);

    public static RibbonSearchRules Read(string workspace)
    {
        var path = new FeatureContext(workspace).FilePath(RulesPath);
        if (!File.Exists(path))
            return new();
        return JsonSerializer.Deserialize(FileContent.StripUtf8Bom(FileContent.ReadBounded(path, RuleLimit)),
            RibbonSearchJson.Default.RibbonSearchRules) ?? new();
    }

    public static void Save(string workspace, RibbonSearchRules rules)
    {
        var context = new FeatureContext(workspace);
        _ = Directories(rules.IncludeDirectories, context.Root);
        _ = Directories(rules.ExcludeDirectories, context.Root);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(rules, RibbonSearchJson.Default.RibbonSearchRules);
        if (bytes.Length > RuleLimit)
            throw new InvalidDataException("Ribbon 搜索规则超过 64 KiB。");
        var path = context.FilePath(RulesPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = context.FilePath($".core30/ribbon-search-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.Write(bytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private RibbonSearchTruncation? Collect(RibbonSearchRoot root, List<RibbonFileCandidate> uics,
        List<RibbonFileCandidate> manifests, Action<RibbonFileCandidate>? manifestFound)
    {
        _ = new FeatureContext(root.Directory);
        var scan = new FileSystemEnumerable<string>(root.Directory, (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions { RecurseSubdirectories = root.Recursive, MaxRecursionDepth = IsRestricted ? int.MaxValue : 5,
                IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
        {
            ShouldRecursePredicate = (ref FileSystemEntry entry) => Recurse(entry.ToFullPath()),
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
            {
                if (entry.IsDirectory) return false;
                var isManifest = IsManifest(entry.FileName);
                if (isManifest && manifestFound is not null) return true;
                return (isManifest || IsUic(entry.FileName)) &&
                    RibbonSearchRules.MatchesName(entry.FileName.ToString(), Rules.NameContains);
            }
        };
        var uicLimit = uics.Count + PerRootLimit;
        var manifestLimit = manifests.Count + PerRootLimit;
        var moreUics = false;
        var moreManifests = false;
        var moreReferences = false;
        var referenceSources = 0;
        foreach (var path in scan)
        {
            if (IsUic(path))
            {
                if (uics.Count < uicLimit)
                    uics.Add(new(path, root.Origin));
                else
                    moreUics = true;
            }
            else
            {
                var manifest = new RibbonFileCandidate(path, root.Origin);
                if (manifestFound is not null)
                {
                    if (referenceSources < PerRootLimit)
                    {
                        manifestFound(manifest);
                        referenceSources++;
                    }
                    else moreReferences = true;
                }
                if (RibbonSearchRules.MatchesName(manifest.Name, Rules.NameContains))
                {
                    if (manifests.Count < manifestLimit) manifests.Add(manifest);
                    else moreManifests = true;
                }
            }
            if (moreUics && moreManifests)
                break;
        }
        return moreUics || moreManifests || moreReferences ? new(root, moreUics, moreManifests || moreReferences) : null;
    }

    private bool Recurse(string directory) =>
        !_excludes.Any(root => IsInside(root, directory)) &&
        (IsRestricted || !Paths.Equals(directory, Path.Combine(Workspace, ".core30")) &&
            !Paths.Equals(directory, Path.Combine(Workspace, "ribbon-state")));

    private static bool IsUic(ReadOnlySpan<char> name) => name.EndsWith(".uic", StringComparison.OrdinalIgnoreCase);

    private static bool IsManifest(ReadOnlySpan<char> name) =>
        name.EndsWith("Customization.xml", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Customisation.xml", StringComparison.OrdinalIgnoreCase);

    private static string[] Directories(string[]? paths, string workspace) =>
        (paths ?? []).Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim(), workspace))).Distinct(Paths).ToArray();

    private static bool IsInside(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathFullyQualified(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(RibbonSearchRules))]
internal sealed partial class RibbonSearchJson : JsonSerializerContext;
