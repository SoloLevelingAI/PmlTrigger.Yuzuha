namespace YuzuhaToolkit.Mcp;

/// <summary>
///     Resolves well-known files inside the managed Yuzuha package layout
///     <c>&lt;root&gt;\runtime\net10\YuzuhaToolkit.Mcp.exe</c>. The environment
///     variable <c>YUZUHA_TOOLKIT_ROOT</c> overrides the layout probe for
///     development checkouts.
/// </summary>
internal static class YuzuhaToolkitPaths
{
    /// <summary>优先使用显式环境覆盖，否则解析托管安装根目录。</summary>
    internal static string ToolkitRoot() => ResolveRoot(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT"));

    internal static string ResolveRoot(string baseDirectory, string? overrideRoot)
    {
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return Path.GetFullPath(overrideRoot.Trim());

        var exeDirectory = Path.GetFullPath(baseDirectory).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(exeDirectory);
        var root = parent is null ? null : Path.GetDirectoryName(parent);

        if (root is not null &&
            new[] { "net10", "cli", "desktop", "manager" }.Contains(Path.GetFileName(exeDirectory), StringComparer.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFileName(parent),
                "runtime",
                StringComparison.OrdinalIgnoreCase))
            return root;

        return exeDirectory;
    }

    /// <summary>解析可显式覆盖的本机函数信任文件路径。</summary>
    internal static string TrustStateFile()
    {
        var overrideFile = Environment.GetEnvironmentVariable("YUZUHA_TRUST_FILE");
        if (!string.IsNullOrWhiteSpace(overrideFile))
            return Path.GetFullPath(overrideFile.Trim());

        return Path.Combine(ToolkitRoot(), "trust", "pml-function-trust.json");
    }

    /// <summary>解析可显式覆盖的本机知识库根目录。</summary>
    internal static string KnowledgeDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("YUZUHA_KNOWLEDGE_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            return Path.GetFullPath(overrideDirectory.Trim());

        return YuzuhaToolkit.Workspace.UserDataPaths.KnowledgeDirectory(ToolkitRoot());
    }
}
