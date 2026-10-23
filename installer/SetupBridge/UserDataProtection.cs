using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

/// <summary>安装计划的用户数据隔离与既有文件所有权保护。</summary>
internal static partial class Program
{
    /// <summary>拒绝程序载荷进入用户目录、数据库和内部设置；本地改过的旧程序文件必须先人工迁移。</summary>
    /// <param name="root">安装根目录。</param>
    /// <param name="relative">载荷中的相对文件名。</param>
    /// <param name="after">新版本文件指纹。</param>
    /// <param name="checkOwnership">复制前核对旧版本所有权；复制后的校验只检查目录边界。</param>
    internal static void ProtectUserData(string root, string relative, string after, bool checkOwnership)
    {
        string path = Inside(root, relative);
        string normalized = path.Substring(root.TrimEnd('\\').Length + 1).Replace('\\', '/');
        string first = normalized.Split('/')[0];
        string[] reserved = { "knowledge", "trust", ".yuzuha-state", "UserPmlLib", "Projects", "Apps", "Exports", "WebApps", ".yuzuha-user-data.json" };
        if (reserved.Contains(first, StringComparer.OrdinalIgnoreCase) ||
            new[] { ".sqlite3", ".db", "-wal", "-shm" }.Any(s => normalized.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            throw Bad("程序包包含用户数据路径，拒绝覆盖 / User data in payload: " + relative);
        for (string dir = Path.GetDirectoryName(path); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            if (File.Exists(Path.Combine(dir, ".yuzuha-user-data.json")))
                throw Bad("用户目录不能作为程序更新目标 / User directory protected: " + dir);
        if (!checkOwnership) return;
        string current = FileDigest(path);
        if (current == null || Same(current, after)) return;
        string manifest = Path.Combine(root, ".setup-payload.json");
        var files = File.Exists(manifest) ? ReadObject(manifest)["files"] as JArray : null;
        var previous = files == null ? null : files.OfType<JObject>().SingleOrDefault(item =>
            string.Equals(((string)item["path"]).Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (previous == null || !Same(current, (string)previous["sha256"]))
            throw Bad("目标文件由用户修改或不属于上版程序包；请先复制到用户目录再重新预览 / Locally modified file retained: " + path);
    }
}
