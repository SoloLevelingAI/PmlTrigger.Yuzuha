#pragma warning disable CS1591 // Retained prototype report DTOs.
namespace YuzuhaToolkit.Manager.LaunchDesk;

/// <summary>静态发现的文件数、包含深度、目录数、单文件字节数与总耗时上限。</summary>
public sealed record ProjectModuleOptions(int MaximumFiles = 128, int MaximumDepth = 3,
    int MaximumDirectories = 512, int MaximumFileBytes = 262144, int TimeoutSeconds = 5);
/// <summary>候选事实的来源文件、行号和证据类别；目录或可执行文件线索使用行号零。</summary>
public sealed record DiscoveryOrigin(string File, int Line, string Kind);
/// <summary>项目代码与解析后的目录候选，连同存在性检查及配置或目录来源。</summary>
public sealed record ProjectHint(string Code, string Directory, bool DirectoryExists, DiscoveryOrigin Origin);
/// <summary>从分派语句、配置赋值或模块可执行文件提取的模块名称线索。</summary>
public sealed record ModuleHint(string Name, DiscoveryOrigin Origin);
/// <summary>由 projects_dir 或 project_dir 配置提取的项目扫描根目录。</summary>
public sealed record ProjectRootHint(string Directory, DiscoveryOrigin Origin);
/// <summary>返回项目、模块、根目录、已读文件和诊断；仅表示静态候选，不证明运行时配置生效。</summary>
public sealed record ProjectModuleReport(IReadOnlyList<ProjectHint> Projects, IReadOnlyList<ModuleHint> Modules,
    IReadOnlyList<ProjectRootHint> ProjectRoots, IReadOnlyList<string> FilesRead,
    IReadOnlyList<DiscoveryDiagnostic> Diagnostics);
