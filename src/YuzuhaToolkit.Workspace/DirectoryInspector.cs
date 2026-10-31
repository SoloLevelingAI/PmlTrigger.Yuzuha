using System.Diagnostics;

namespace YuzuhaToolkit.Workspace;

/// <summary>PML 文件的磁盘占用；不是方法数量或 AVEVA 运行时内存。</summary>
/// <param name="Files">已扫描文件数。</param>
/// <param name="Bytes">已扫描文件长度之和。</param>
public sealed record PmlStorage(int Files, long Bytes);

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
    /// <summary>仅 PML 源文件的磁盘总字节，不包含索引、日志及其他资源。</summary>
    public long PmlBytes { get; init; }
    /// <summary>按扩展名汇总的 PML 文件数和字节数。</summary>
    public Dictionary<string, PmlStorage> PmlTypes { get; init; } = [];
    /// <summary>按文件所在相对目录直接汇总；不重复累计子目录，根目录用点表示。</summary>
    public Dictionary<string, PmlStorage> PmlFolders { get; init; } = [];
    /// <summary>同一扩展名及文件名（忽略大小写）的重复候选和相对路径；未比较正文或解析加载顺序。</summary>
    public Dictionary<string, string[]> DuplicatePmlNames { get; init; } = [];
    /// <summary>同名重复候选组数；部分扫描时仅为已发现下限。</summary>
    public int DuplicateGroups => DuplicatePmlNames.Count;
    /// <summary>每组保留一个条目后多出的文件数；不表示允许删除。</summary>
    public int DuplicateExtraFiles => DuplicatePmlNames.Values.Sum(paths => paths.Length - 1);
    /// <summary>需要清理或核对的状态；不完整扫描不能报告健康。</summary>
    public bool NeedsAttention => !Complete || Warnings.Length > 0;
}

/// <summary>在时间、条目和深度预算内统计 PMLLIB，防止检查本身长时间占用界面。</summary>
public static class DirectoryInspector
{
    private static readonly HashSet<string> PmlExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pmlfnc", ".pmlobj", ".pmlfrm", ".pmlmac", ".pmlcmd" };
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
        var pmlTypes = new Dictionary<string, PmlStorage>(StringComparer.OrdinalIgnoreCase);
        var pmlFolders = new Dictionary<string, PmlStorage>(StringComparer.OrdinalIgnoreCase);
        var pmlNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        long pmlBytes = 0;
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
                        var length = file.Length;
                        files++; bytes += length;
                        var extension = file.Extension.ToLowerInvariant(); extensions[extension] = extensions.GetValueOrDefault(extension) + 1;
                        var relative = Path.GetRelativePath(root, file.FullName); var top = relative.Split(Path.DirectorySeparatorChar)[0];
                        folders[top] = folders.GetValueOrDefault(top) + length;
                        if (PmlExtensions.Contains(extension))
                        {
                            pml++; pmlBytes += length;
                            AddStorage(pmlTypes, extension, length);
                            var folder = Path.GetDirectoryName(relative);
                            AddStorage(pmlFolders, string.IsNullOrEmpty(folder) ? "." : folder, length);
                            if (!pmlNames.TryGetValue(file.Name, out var paths)) pmlNames[file.Name] = paths = [];
                            paths.Add(relative);
                        }
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
        var duplicates = pmlNames.Where(pair => pair.Value.Count > 1).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Order(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        if (duplicates.Count > 0) warnings.Add($"发现 {duplicates.Count:N0} 组同名 PML 文件候选；需核对加载路径，未自动判断覆盖关系或删除文件。");
        return new(root, files, bytes, pml, complete, warnings.Take(40).ToArray(), folders.OrderByDescending(p => p.Value).Take(12).ToDictionary(), extensions)
        { PmlBytes = pmlBytes, PmlTypes = pmlTypes, PmlFolders = pmlFolders, DuplicatePmlNames = duplicates };
    }

    private static void AddStorage(Dictionary<string, PmlStorage> storage, string key, long bytes)
    {
        var previous = storage.GetValueOrDefault(key);
        storage[key] = new((previous?.Files ?? 0) + 1, (previous?.Bytes ?? 0) + bytes);
    }
}
