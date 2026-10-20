using System.Diagnostics;

namespace YuzuhaToolkit.Workspace;

/// <summary>注册目录的有界扫描报告；不读取文件正文，也不执行 PML REHASH。</summary>
/// <param name="Root">扫描的绝对目录。</param>
/// <param name="Files">已见普通文件数。</param>
/// <param name="Bytes">已见文件的总字节数。</param>
/// <param name="PmlFiles">已见 PML 源文件数。</param>
/// <param name="Complete">是否完整扫描。</param>
/// <param name="Warnings">阈值、链接、不可访问路径等提醒。</param>
/// <param name="LargestFolders">按一级目录汇总的体积。</param>
/// <param name="Extensions">按扩展名汇总的文件数。</param>
public sealed record DirectoryHealth(string Root, int Files, long Bytes, int PmlFiles, bool Complete,
    string[] Warnings, Dictionary<string, long> LargestFolders, Dictionary<string, int> Extensions)
{
    /// <summary>需要清理或核对的状态；不完整扫描不能报告健康。</summary>
    public bool NeedsAttention => !Complete || Warnings.Length > 0;
}

/// <summary>在时间、条目和深度预算内统计 PMLLIB，防止检查本身长时间占用界面。</summary>
public static class DirectoryInspector
{
    /// <summary>递归统计文件及大目录；不跟随符号链接或目录联接，不删除内容。</summary>
    /// <param name="root">用户确认已注册的 PMLLIB 目录。</param>
    /// <param name="warnFiles">文件数提醒阈值。</param>
    /// <param name="warnBytes">字节数提醒阈值。</param>
    /// <param name="maxEntries">最多访问的目录与文件条目。</param>
    /// <param name="budget">检查时间预算，默认两秒。</param>
    /// <param name="cancellationToken">取消扫描。</param>
    public static DirectoryHealth Scan(string root, int warnFiles = 3000, long warnBytes = 209715200,
        int maxEntries = 50000, TimeSpan? budget = null, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("注册目录不存在：" + root);
        PathRules.NoLinks(root);
        var warnings = new HashSet<string>();
        var folders = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var extensions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var watch = Stopwatch.StartNew(); var complete = true; var entries = 0; var files = 0; var pml = 0; long bytes = 0;
        var pending = new Stack<(string Path, int Depth)>(); pending.Push((root, 0));
        while (pending.TryPop(out var item))
        {
            try
            {
                foreach (var entry in new DirectoryInfo(item.Path).EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++entries > maxEntries || watch.Elapsed > (budget ?? TimeSpan.FromSeconds(2))) { complete = false; pending.Clear(); break; }
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) { warnings.Add("跳过链接或目录联接：" + Path.GetRelativePath(root, entry.FullName)); complete = false; continue; }
                    if (entry is DirectoryInfo)
                    {
                        if (new[] { ".git", "node_modules", "bin", "obj", "logs", "backup", "backups", "knowledge" }.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
                            warnings.Add("开发或历史数据位于注册目录内，建议移出：" + Path.GetRelativePath(root, entry.FullName));
                        if (item.Depth >= 40) { complete = false; warnings.Add("目录深度超过 40 层。"); } else pending.Push((entry.FullName, item.Depth + 1));
                    }
                    else if (entry is FileInfo file)
                    {
                        files++; bytes += file.Length;
                        var extension = file.Extension.ToLowerInvariant(); extensions[extension] = extensions.GetValueOrDefault(extension) + 1;
                        if (extension.StartsWith(".pml", StringComparison.Ordinal)) pml++;
                        var relative = Path.GetRelativePath(root, file.FullName); var top = relative.Split(Path.DirectorySeparatorChar)[0];
                        folders[top] = folders.GetValueOrDefault(top) + file.Length;
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { complete = false; warnings.Add("部分目录无法读取：" + Path.GetRelativePath(root, item.Path)); }
            if (!complete && (entries > maxEntries || watch.Elapsed > (budget ?? TimeSpan.FromSeconds(2)))) break;
        }
        if (!complete) warnings.Add("扫描未完成；当前数量仅为下限，建议检查大目录后重新扫描。");
        if (files >= warnFiles) warnings.Add($"已见 {files:N0} 个文件，达到工作区提醒预算 {warnFiles:N0}；建议移出旧插件和开发副本。");
        if (bytes >= warnBytes) warnings.Add($"已见 {bytes / 1048576d:N1} MiB，达到目录体积提醒预算；建议清理大文件。");
        return new(root, files, bytes, pml, complete, warnings.Take(40).ToArray(), folders.OrderByDescending(p => p.Value).Take(12).ToDictionary(), extensions);
    }
}

/// <summary>所有复制路径共用的目录边界与联接检查。</summary>
public static class PathRules
{
    /// <summary>解析根目录内的相对路径，拒绝绝对路径、向上跳转和 NTFS 数据流。</summary>
    /// <param name="root">受管理根目录。</param>
    /// <param name="relative">目录内的相对名称。</param>
    public static string Inside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Replace('\\', '/').Split('/').Any(p => p is ".." or "." or ""))
            throw new InvalidDataException("相对路径无效：" + relative);
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
