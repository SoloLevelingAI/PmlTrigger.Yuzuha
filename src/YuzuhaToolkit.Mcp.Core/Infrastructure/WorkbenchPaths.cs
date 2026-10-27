namespace YuzuhaToolkit.Mcp;

/// <summary>将已有信任文件约定适配到安装器现有 runtime/desktop 布局。</summary>
public static class WorkbenchPaths
{
    /// <summary>保留已有环境覆盖优先级；托管安装与 MCP 共用包根下的人工信任记录。</summary>
    public static string TrustFile(string baseDirectory, string? trustOverride, string? rootOverride)
    {
        if (!string.IsNullOrWhiteSpace(trustOverride)) return Path.GetFullPath(trustOverride.Trim());
        var directory = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        var root = !string.IsNullOrWhiteSpace(rootOverride) ? Path.GetFullPath(rootOverride.Trim()) : directory.Name.Equals("desktop", StringComparison.OrdinalIgnoreCase) && directory.Parent?.Name.Equals("runtime", StringComparison.OrdinalIgnoreCase) == true ? directory.Parent.Parent!.FullName : directory.FullName;
        return Path.Combine(root, "trust", "pml-function-trust.json");
    }
}
