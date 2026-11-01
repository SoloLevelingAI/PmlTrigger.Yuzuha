using System.IO;

namespace YuzuhaToolkit.Desktop;

/// <summary>经过安装目录和相对路径约束的DocFX目标。</summary>
/// <param name="File">本机实际HTML文件。</param>
/// <param name="RelativeUrl">只含站内路径及可选锚点的URL。</param>
internal sealed record DesktopDocumentationTarget(string File, string RelativeUrl)
{
    /// <summary>只接受已安装HTML及锚点，拒绝外部URL、编码绕过、链接和路径越界。</summary>
    internal static DesktopDocumentationTarget Resolve(string site, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 2048 || relative.Any(char.IsControl) || relative.IndexOfAny(['\\', ':', '%', '?']) >= 0 || relative.StartsWith('/')) throw new ArgumentException("文档目标必须是已安装站点内的相对HTML路径。");
        var parts = relative.Split('#');if (parts.Length > 2) throw new ArgumentException("文档锚点无效。");
        var segments = parts[0].Split('/');
        if (segments.Any(s => string.IsNullOrWhiteSpace(s) || s is "." or "..") || !parts[0].EndsWith(".html", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("文档目标不是有效站内HTML。");
        var root = Path.GetFullPath(site);var file = Path.GetFullPath(Path.Combine(root, parts[0]));
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(file)) throw new FileNotFoundException("安装包中没有此契约/脚本页面，请核对文档版本。", parts[0]);
        for (var current = file; current.Length >= root.Length; current = Path.GetDirectoryName(current) ?? "")
        {
            if ((System.IO.File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("文档目标包含链接，未打开页面。");
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        return new(file, string.Join('/', segments.Select(Uri.EscapeDataString)) + (parts.Length == 2 ? "#" + Uri.EscapeDataString(parts[1]) : ""));
    }
}
