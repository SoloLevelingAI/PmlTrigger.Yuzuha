namespace Core30.Core;

/// <summary>Registers an implementation for a key and, optionally, one exact compatibility version.</summary>
public sealed record ImplementationRegistration<TKey, TImplementation>(TKey Key, TImplementation Implementation,
    string? Version = null) where TKey : notnull where TImplementation : class;

/// <summary>Selects a single implementation before it runs. Exact versions take precedence over the key's default.</summary>
public sealed class ImplementationCatalog<TKey, TImplementation> where TKey : notnull where TImplementation : class
{
    private readonly Dictionary<TKey, Dictionary<string, TImplementation>> _entries;

    public ImplementationCatalog(IEnumerable<ImplementationRegistration<TKey, TImplementation>> registrations,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        _entries = new(keyComparer);
        foreach (var registration in registrations)
            Add(registration);
    }

    public TImplementation? Find(TKey key, string? version = null) =>
        _entries.TryGetValue(key, out var versions)
            ? versions.GetValueOrDefault(VersionKey(version)) ?? versions.GetValueOrDefault("")
            : null;

    public TImplementation Require(TKey key, string? version = null) => Find(key, version) ??
        throw new InvalidDataException($"未登记实现：{key}{(string.IsNullOrWhiteSpace(version) ? "" : $" / {version}")}。");

    private void Add(ImplementationRegistration<TKey, TImplementation> registration)
    {
        ArgumentNullException.ThrowIfNull(registration.Implementation);
        if (!_entries.TryGetValue(registration.Key, out var versions))
        {
            versions = new(StringComparer.OrdinalIgnoreCase);
            _entries.Add(registration.Key, versions);
        }
        versions.Add(VersionKey(registration.Version), registration.Implementation);
    }

    private static string VersionKey(string? version) => version?.Trim() ?? "";
}
