using System.IO;

namespace YuzuhaToolkit.Desktop;

/// <summary>用户指南与需明确选择目录的独立开发参考。</summary>
internal enum DocumentationCollection
{
    /// <summary>默认交付的任务帮助。</summary>
    User,
    /// <summary>同修订独立参考包中的精确页面。</summary>
    Developer
}

/// <summary>经过文档集合清单和相对路径约束的HTML目标。</summary>
/// <param name="File">本机实际HTML文件。</param>
/// <param name="RelativeUrl">只含站内路径及可选锚点的URL。</param>
internal sealed record DesktopDocumentationTarget(string File, string RelativeUrl)
{
    /// <summary>只接受所选集合中的HTML及锚点，拒绝外部URL、编码绕过、链接和路径越界。</summary>
    internal static DesktopDocumentationTarget Resolve(string site, string relative, IEnumerable<string>? publishedPaths = null)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 2048 || relative.Any(char.IsControl) || relative.IndexOfAny(['\\', ':', '%', '?']) >= 0 || relative.StartsWith('/')) throw new ArgumentException("文档目标必须是所选集合内的相对HTML路径。");
        var parts = relative.Split('#');if (parts.Length > 2) throw new ArgumentException("文档锚点无效。");
        var segments = parts[0].Split('/');
        if (segments.Any(s => string.IsNullOrWhiteSpace(s) || s is "." or "..") || !parts[0].EndsWith(".html", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("文档目标不是有效站内HTML。");
        var root = Path.GetFullPath(site);var file = Path.GetFullPath(Path.Combine(root, parts[0]));
        if (publishedPaths is not null && !publishedPaths.Contains(parts[0], StringComparer.Ordinal))
            throw new FileNotFoundException("此页面不属于当前文档清单，请核对文档集合与版本。", parts[0]);
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(file)) throw new FileNotFoundException("所选文档集合中没有此页面，请核对配套版本。", parts[0]);
        for (var current = file; current.Length >= root.Length; current = Path.GetDirectoryName(current) ?? "")
        {
            FileSystemInfo info = System.IO.File.Exists(current) ? new FileInfo(current) : new DirectoryInfo(current);
            if (info.LinkTarget is not null) throw new IOException("文档目标包含目录链接或符号链接，未打开页面。");
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        return new(file, string.Join('/', segments.Select(Uri.EscapeDataString)) + (parts.Length == 2 ? "#" + Uri.EscapeDataString(parts[1]) : ""));
    }
}
