namespace Core30.Core;

/// <summary>Resolves explicit local inputs while retaining workspace and reparse-point checks.</summary>
public static class LocalSource
{
    public static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    public static string Resolve(FeatureContext workspace, string path)
    {
        if (!Path.IsPathFullyQualified(path))
            return workspace.FilePath(path);
        return OpenParent(path).FilePath(Path.GetFileName(path));
    }

    public static FeatureContext OpenParent(string path)
    {
        var local = RequireAbsolutePath(path);
        return new FeatureContext(Path.GetDirectoryName(local)
            ?? throw new InvalidDataException("请选择本机文件。"));
    }

    public static string RequireAbsolutePath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException("需要本机绝对路径。");
        return Path.GetFullPath(path);
    }
}
