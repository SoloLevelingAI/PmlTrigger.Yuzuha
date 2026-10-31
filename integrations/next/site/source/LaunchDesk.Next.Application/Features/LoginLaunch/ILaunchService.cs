using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Application.Features.LoginLaunch;

// Feature API, deliberately separate from the neutral infrastructure contracts.
public interface ILaunchService
{
    Task<LaunchResult> ExecuteAsync(LaunchRequest request, ISecretHandle? secret = null,
        CancellationToken ct = default);
    // Snapshot execution uses the captured draft; it does not require a separate profile-save click.
    Task<LaunchResult> ExecuteSnapshotAsync(LaunchRequest request, ISecretHandle? secret = null,
        CancellationToken ct = default) => ExecuteAsync(request, secret, ct);
    IAsyncEnumerable<LaunchEvent> ObserveAsync(Guid runId, CancellationToken ct = default);
    LaunchResult? GetLatest(Guid runId);
}
