using LaunchDesk.Next.Application.Features.LoginLaunch;
namespace LaunchDesk.Next.Application.Features.LoginLaunch;

public sealed record DiscoveryCandidate(string Id, string DisplayName, string ExecutablePath, EntryProtocol Protocol, LaunchRequest Prefill, IReadOnlyDictionary<string, string> Sources, ProjectModuleReport? ProjectModules = null);
public sealed record DiscoveryDiagnostic(string Code, string Path, string Message);
public sealed record DiscoveryOptions(IReadOnlyList<string> SearchRoots, int MaximumDepth = 5, int MaximumFiles = 10000, int TimeoutSeconds = 20, IReadOnlyDictionary<string, string>? Presets = null, ProjectModuleOptions? ProjectModules = null);
public sealed record DiscoveryReport(Guid ScanId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, IReadOnlyList<DiscoveryCandidate> Candidates, IReadOnlyList<DiscoveryDiagnostic> Diagnostics, string ImplementationVersion);
public interface IApplicationDiscovery { Task<DiscoveryReport> DiscoverAsync(DiscoveryOptions options, CancellationToken cancellationToken = default); }
