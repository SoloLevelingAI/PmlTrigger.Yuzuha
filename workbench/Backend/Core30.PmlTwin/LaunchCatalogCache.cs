using System.Text;
using System.Text.Json;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed record LaunchSourceStamp(string Path, bool Directory, bool Exists, long Modified, long Size)
{
    public static LaunchSourceStamp Read(string path, bool directory)
    {
        FileSystemInfo item = directory ? new DirectoryInfo(path) : new FileInfo(path);
        return new(path, directory, item.Exists, item.Exists ? item.LastWriteTimeUtc.Ticks : 0,
            item is FileInfo file && file.Exists ? file.Length : 0);
    }

    public bool IsCurrent() => this == Read(Path, Directory);
}

internal sealed class LaunchCatalogSources
{
    private readonly Dictionary<string, LaunchSourceStamp> _observed = new(StringComparer.OrdinalIgnoreCase);
    public LaunchSourceStamp[] Stamps => _observed.Values.ToArray();

    public LaunchSourceStamp Observe(string path, bool directory = false)
    {
        if (!_observed.TryGetValue(path, out var stamp))
            _observed[path] = stamp = LaunchSourceStamp.Read(path, directory);
        return stamp;
    }
}

internal sealed record CachedLaunchCatalog(DateTimeOffset ObservedUtc, LaunchProjectSource[] Projects, LaunchLoginSource[] Logins,
    LaunchSourceStamp[] Sources);

/// <summary>Persists discovered choices per installation and Windows profile, and rechecks their inputs before reuse.</summary>
internal static class LaunchCatalogCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private const int MaxBytes = 2 * 1024 * 1024;

    public static (CachedLaunchCatalog Catalog, string State) Read(FeatureContext workspace, string product,
        string release, string installation, bool refresh)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var environment = LaunchProjectEnvironment.ReadEnvironment();
        var key = CacheKey(product, release, installation, localData, environment);
        var path = workspace.FilePath($".core30/launch-catalog/{key}.json");
        if (!refresh && ReadCurrent(path) is { } existing)
            return (existing, "hit");
        var current = Scan(product, release, installation, localData, environment);
        return (current, TryStore(path, current) ? "scanned" : "uncached");
    }

    private static CachedLaunchCatalog Scan(string product, string release, string installation, string localData,
        Dictionary<string, string> environment)
    {
        var sources = new LaunchCatalogSources();
        var projects = new LaunchProjectEnvironment(sources, environment).Read(product, installation);
        var logins = AvevaLaunchHistory.Read(localData, product, release, sources);
        return new(DateTimeOffset.UtcNow, projects, logins, sources.Stamps);
    }

    private static string CacheKey(string product, string release, string installation, string localData,
        Dictionary<string, string> environment)
    {
        var variables = environment.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key.ToUpperInvariant()}={item.Value}");
        return FileContent.Sha256(Encoding.UTF8.GetBytes(string.Join('\0',
            new[] { "launch-catalog-v1", product, release, installation.ToUpperInvariant(), localData }.Concat(variables))));
    }

    private static CachedLaunchCatalog? ReadCurrent(string path)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<CachedLaunchCatalog>(FileContent.ReadBounded(path, MaxBytes));
            return catalog is not null && IsCurrent(catalog) ? catalog : null;
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return null; }
    }

    private static bool IsCurrent(CachedLaunchCatalog catalog) =>
        catalog.ObservedUtc <= DateTimeOffset.UtcNow && DateTimeOffset.UtcNow - catalog.ObservedUtc < Lifetime
        && catalog.Sources is not null && catalog.Sources.All(source => source is not null && source.IsCurrent())
        && catalog.Projects is not null && catalog.Projects.All(project => project is not null && File.Exists(project.SystemDatabase) == project.Found)
        && catalog.Logins is not null;

    private static bool TryStore(string path, CachedLaunchCatalog catalog)
    {
        var pending = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(pending, JsonSerializer.SerializeToUtf8Bytes(catalog));
            File.Move(pending, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return false; }
        finally { TryRemovePending(pending); }
    }

    private static void TryRemovePending(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { }
    }
}
