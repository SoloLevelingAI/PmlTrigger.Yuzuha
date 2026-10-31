namespace YuzuhaToolkit.Workspace;

/// <summary>所有复制路径共用的目录边界与联接检查。</summary>
#if YUZUHA_INTERNAL_PATH_RULES
internal
#else
public
#endif
static class PathRules
{
    /// <summary>解析根目录内的相对路径，拒绝绝对路径、向上跳转和 NTFS 数据流。</summary>
    /// <param name="root">受管理根目录。</param>
    /// <param name="relative">目录内的相对名称。</param>
    public static string Inside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Replace('\\', '/').Split('/').Any(p => p is ".." or "." or ""))
            throw new InvalidDataException("相对路径无效：" + relative);
        foreach (var part in relative.Replace('\\', '/').Split('/'))
        {
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                    stem.Length == 4 && "123456789¹²³".Contains(stem[3]))
                throw new InvalidDataException("路径含 Windows 别名、设备名或无效文件名：" + relative);
        }
        var result = Path.GetFullPath(Path.Combine(root, relative));
        if (!result.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("路径越出目标目录。");
        NoLinks(result); return result;
    }

    /// <summary>拒绝路径及所有已存在祖先中的符号链接或目录联接。</summary>
    /// <param name="path">待使用的绝对文件或目录路径。</param>
    public static void NoLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("路径包含链接或目录联接：" + current);
    }
}
