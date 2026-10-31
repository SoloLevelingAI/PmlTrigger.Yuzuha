using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch;

// Profile validation and revisions belong to the feature, not the JSON file adapter.
public sealed class SavedProfiles(IJsonDocumentStore document)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<LaunchRequest>> ReadAllAsync(CancellationToken ct = default) =>
        (await document.ReadAsync<List<LaunchRequest>>(ct) ?? []).Select(Freeze).ToArray();

    public async Task<LaunchRequest?> ReadAsync(string id, CancellationToken ct = default) =>
        (await ReadAllAsync(ct)).FirstOrDefault(row => row.ConfigurationId == id);

    public async Task<SaveResult> SaveAsync(LaunchRequest request, CancellationToken ct = default)
    {
        request = Freeze(request);
        var error = LaunchValidation.Validate(request);
        if (error != null) return new(false, null, error);

        await gate.WaitAsync(ct);
        try
        {
            var rows = (await ReadAllAsync(ct)).ToList();
            var existing = rows.FirstOrDefault(row => row.ConfigurationId == request.ConfigurationId);
            if ((existing?.Version ?? 0) != request.Version)
                return new(false, null, "Configuration version changed; reload before saving.");

            var saved = request with { AttemptId = Guid.Empty, Version = request.Version + 1 };
            rows.RemoveAll(row => row.ConfigurationId == saved.ConfigurationId);
            rows.Add(saved);
            await document.ReplaceAsync(rows, ct);
            return new(true, saved, "Configuration saved.");
        }
        finally { gate.Release(); }
    }

    private static LaunchRequest Freeze(LaunchRequest request) =>
        LaunchInput.FromRequest(request).CreateSnapshot(request.AttemptId);
}
