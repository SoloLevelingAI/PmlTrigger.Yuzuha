using System;
using System.IO;
using System.Linq;

/// <summary>将用户数据排除在程序载荷以外。</summary>
internal static partial class Program
{
    /// <summary>核对程序文件目标，不以现有程序文件的内容决定是否可更新。</summary>
    /// <param name="root">安装根目录。</param>
    /// <param name="relative">载荷中的相对文件名。</param>
    internal static string ProtectUserData(string root, string relative)
    {
        string path = Inside(root, relative);
        string normalized = path.Substring(root.TrimEnd('\\').Length + 1).Replace('\\', '/');
        string first = normalized.Split('/')[0];
        string[] reserved = { "knowledge", "trust", ".yuzuha-state", "UserPmlLib", "Projects", "Apps", "Exports", "WebApps", ".yuzuha-user-data.json" };
        if (reserved.Contains(first, StringComparer.OrdinalIgnoreCase) || Same(Path.GetFileName(path), "pml.index") ||
            new[] { ".sqlite3", ".db", "-wal", "-shm" }.Any(s => normalized.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            throw Bad("程序包包含用户数据路径，拒绝覆盖 / User data in payload: " + relative);
        for (string dir = Path.GetDirectoryName(path); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            if (File.Exists(Path.Combine(dir, ".yuzuha-user-data.json")))
                throw Bad("用户目录不能作为程序更新目标 / User directory protected: " + dir);
        return path;
    }
}
