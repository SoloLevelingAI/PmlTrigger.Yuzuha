using System.Text.Json;

namespace LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;

public sealed class CheckSavedProfile(SavedProfiles profiles)
{
    public async Task<string?> ExecuteAsync(LaunchRequest request, CancellationToken ct)
    {
        var saved = await profiles.ReadAsync(request.ConfigurationId, ct);
        if (saved is null || saved.Version != request.Version)
            return "Configuration is missing or its version changed; save/reload first.";
        if (Fingerprint(saved) != Fingerprint(request))
            return "Launch fields differ from the saved configuration; save changes before launching.";
        return null;
    }

    private static string Fingerprint(LaunchRequest request) => JsonSerializer.Serialize(request with
    {
        AttemptId = Guid.Empty,
        Environment = new SortedDictionary<string, string>(
            request.Environment.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal),
        Sources = new SortedDictionary<string, string>(
            request.Sources.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal)
    });
}
