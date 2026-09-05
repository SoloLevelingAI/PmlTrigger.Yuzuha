namespace YuzuhaToolkit.Mcp;

/// <summary>
///     Resolves well-known files inside the managed Yuzuha package layout
///     <c>&lt;root&gt;\runtime\net10\YuzuhaToolkit.Mcp.exe</c>. The environment
///     variable <c>YUZUHA_TOOLKIT_ROOT</c> overrides the layout probe for
///     development checkouts.
/// </summary>
internal static class YuzuhaToolkitPaths
{
    internal static string ToolkitRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return Path.GetFullPath(overrideRoot.Trim());

        var exeDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(exeDirectory);
        var root = parent is null ? null : Path.GetDirectoryName(parent);

        if (root is not null &&
            string.Equals(
                Path.GetFileName(exeDirectory),
                "net10",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFileName(parent),
                "runtime",
                StringComparison.OrdinalIgnoreCase))
            return root;

        return exeDirectory;
    }

    internal static string TrustStateFile()
    {
        var overrideFile = Environment.GetEnvironmentVariable("YUZUHA_TRUST_FILE");
        if (!string.IsNullOrWhiteSpace(overrideFile))
            return Path.GetFullPath(overrideFile.Trim());

        return Path.Combine(ToolkitRoot(), "trust", "pml-function-trust.json");
    }

    internal static string KnowledgeDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("YUZUHA_KNOWLEDGE_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            return Path.GetFullPath(overrideDirectory.Trim());

        return Path.Combine(ToolkitRoot(), "knowledge");
    }
}
