namespace LaunchDesk.Next.Application.Features.LoginLaunch;

public sealed record ProjectModuleOptions(int MaximumFiles = 128, int MaximumDepth = 3,
    int MaximumDirectories = 512, int MaximumFileBytes = 262144, int TimeoutSeconds = 5);
public sealed record DiscoveryOrigin(string File, int Line, string Kind);
public sealed record ProjectHint(string Code, string Directory, bool DirectoryExists, DiscoveryOrigin Origin);
public sealed record ModuleHint(string Name, DiscoveryOrigin Origin);
public sealed record ProjectRootHint(string Directory, DiscoveryOrigin Origin);
public sealed record ProjectModuleReport(IReadOnlyList<ProjectHint> Projects, IReadOnlyList<ModuleHint> Modules,
    IReadOnlyList<ProjectRootHint> ProjectRoots, IReadOnlyList<string> FilesRead,
    IReadOnlyList<DiscoveryDiagnostic> Diagnostics);
