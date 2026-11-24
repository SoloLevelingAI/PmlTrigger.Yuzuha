namespace Core30.Core;

public sealed class FeatureContext
{
    public FeatureContext(string workspace)
    {
        Root = Path.GetFullPath(workspace);
        EnsureWorkspace(Root);
    }

    public string Root { get; }
    public RunInput? Input { get; init; }

    public static void ValidateRelativePath(string relativePath) => WorkspaceSegments(relativePath);

    public string FilePath(string relativePath)
    {
        var segments = WorkspaceSegments(relativePath);
        var path = Root;
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
            EnsureNotLink(path);
        }
        return path;
    }

    private static string[] WorkspaceSegments(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path == ".")
            return [];

        var segments = path.Split('/', '\\');
        foreach (var segment in segments)
            EnsureSafeSegment(segment);
        return segments;
    }

    private static void EnsureSafeSegment(string segment)
    {
        if (segment.Length == 0 || segment.EndsWith('.') || segment.EndsWith(' ')
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.Contains(':'))
            throw new InvalidDataException("文件位置须为工作区内的普通相对路径。");

        var stem = segment.Split('.')[0].TrimEnd().ToUpperInvariant();
        if (OperatingSystem.IsWindows() && IsDeviceName(stem))
            throw new InvalidDataException("文件位置不可指向 Windows 设备。");
    }

    private static bool IsDeviceName(string name) =>
        name is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
        || name.Length == 4 && (name.StartsWith("COM") || name.StartsWith("LPT"))
            && "123456789¹²³".Contains(name[3]);

    private static void EnsureWorkspace(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("工作区目录不存在。请先准备隔离工作区。");

        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            EnsureNotLink(directory.FullName);
    }

    private static void EnsureNotLink(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("工作区文件位置不可经过链接或重解析点。");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
}
