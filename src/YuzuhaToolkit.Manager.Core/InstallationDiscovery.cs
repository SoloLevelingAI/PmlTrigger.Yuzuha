using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using YuzuhaToolkit.Manager.LaunchDesk;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>实际入口内容和环境文件联合身份；目录提示不等于厂商版本验证。</summary>
public sealed record ManagerInstallationIdentity(string Id, string Root, string Executable, string ExecutableSha256,
    string Product, string ProductVersion, string VersionHint, string Kind, string? EnvironmentFile, string? EnvironmentSha256);
/// <summary>安装候选与静态工程/模块证据；不宣称这是运行时生效环境。</summary>
public sealed record ManagerInstallationCandidate(ManagerInstallationIdentity Identity, ProjectModuleReport ProjectModules);
/// <summary>有界扫描报告；不完整状态不能显示为全部产品已找到。</summary>
public sealed record ManagerInstallationReport(bool Complete, long FilesSeen, ManagerInstallationCandidate[] Candidates, DiscoveryDiagnostic[] Diagnostics);

/// <summary>采用 Next 的有界发现和静态配置解析行为，运行在管理器自身进程内。</summary>
public static class ManagerInstallationDiscovery
{
    /// <summary>只扫描用户选定根目录，跳过模型库和已识别产品的内部目录。</summary>
    public static ManagerInstallationReport Scan(string[] roots, int maximumDepth = 5, int maximumFiles = 300000,
        int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        if (roots.Length is < 1 or > 32 || maximumDepth is < 0 or > 8 || maximumFiles is < 1 or > 300000 || timeoutSeconds is < 1 or > 120)
            throw new ArgumentException("安装扫描范围或预算无效。");
        var clock = Stopwatch.StartNew(); var diagnostics = new List<DiscoveryDiagnostic>(); var found = new List<ManagerInstallationCandidate>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var pending = new Queue<(string Root, int Depth)>();
        foreach (var root in roots)
        {
            if (!Path.IsPathFullyQualified(root) || root.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("请选择本机绝对目录。");
            pending.Enqueue((Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), 0));
        }
        long files = 0; var complete = true;
        while (pending.TryDequeue(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (clock.Elapsed.TotalSeconds >= timeoutSeconds || files >= maximumFiles || visited.Count >= 10000)
            { complete = false; Note("ScanLimit", item.Root, "安装扫描达到时间、文件或目录预算，请缩小范围。"); break; }
            if (!visited.Add(item.Root)) continue;
            if (!Directory.Exists(item.Root)) { Note("RootMissing", item.Root, "安装目录不存在。"); continue; }
            try
            {
                PathRules.NoLinks(item.Root);
                var entries = new List<string>();
                foreach (var file in Directory.EnumerateFiles(item.Root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (files >= maximumFiles || clock.Elapsed.TotalSeconds >= timeoutSeconds)
                    { complete = false; Note("ScanLimit", item.Root, "安装扫描达到时间或文件预算，请缩小范围。"); break; }
                    files++;
                    if (new[] { "mon.exe", "marine.bat", "marine.cmd", "pdms.bat", "pdms.cmd" }.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)) entries.Add(file);
                }
                var batches = entries.Where(e => !Path.GetExtension(e).Equals(".exe", StringComparison.OrdinalIgnoreCase)).ToArray();
                var selected = batches.Length > 0 ? batches : entries.ToArray();
                var recognized = false;
                foreach (var entry in selected)
                {
                    var product = Product(entry); if (product is null) continue;
                    recognized = true;
                    try
                    {
                        var identity = Identify(entry, product);
                        var report = new FindAvevaProjectsAndModules(new DiscoveryFileSystem()).Execute(item.Root, new(TimeoutSeconds: Math.Min(5, timeoutSeconds)), cancellationToken);
                        found.Add(new(identity, report));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
                    { Note("InstallationUnreadable", entry, Message(error)); }
                }
                // Product roots contain model stores and large PML trees; discovery does not crawl them.
                if (!recognized && item.Depth < maximumDepth)
                    foreach (var directory in Directory.EnumerateDirectories(item.Root))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (clock.Elapsed.TotalSeconds >= timeoutSeconds)
                        { complete = false; Note("ScanLimit", item.Root, "安装扫描达到时间预算，请缩小范围。"); break; }
                        if (new[] { "databases", "pdmsdata", "data", "PMLLIB", "PMLUI", "PDMSUI" }.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase)) continue;
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) { Note("LinkSkipped", directory, "跳过链接目录。"); continue; }
                        pending.Enqueue((directory, item.Depth + 1));
                        if (pending.Count > 10000) { complete = false; Note("ScanLimit", item.Root, "待扫描目录达到预算。"); break; }
                    }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { complete = false; Note("ReadFailed", item.Root, Message(error)); }
        }
        return new(complete, files, found.DistinctBy(c => c.Identity.Id).OrderBy(c => c.Identity.Root, StringComparer.OrdinalIgnoreCase).ToArray(), diagnostics.ToArray());
        void Note(string code, string file, string message) { if (diagnostics.Count < 128) diagnostics.Add(new(code, file, message)); }
    }

    /// <summary>显式入口身份；不执行文件，也不把文件夹名当作已核验厂商版本。</summary>
    public static ManagerInstallationIdentity Identify(string executable, string product, string? selectedEnvironmentFile = null)
    {
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("产品入口需要绝对路径。");
        executable = Path.GetFullPath(executable); var root = Path.GetDirectoryName(executable)!;
        PathRules.NoLinks(executable);
        product = ProductModuleCatalog.NormalizeProduct(product);
        if (product is not ("E3D" or "AM" or "PDMS") ||
            !new[] { "mon.exe", "marine.bat", "marine.cmd", "pdms.bat", "pdms.cmd" }.Contains(Path.GetFileName(executable), StringComparer.OrdinalIgnoreCase) ||
            Product(executable) is { } inferred && inferred != product)
            throw new ArgumentException("入口与产品不匹配。");
        var hash = Hash(executable); var version = FileVersionInfo.GetVersionInfo(executable).ProductVersion ?? "";
        var environment = EvarsEditor.SelectFile(root, product, selectedEnvironmentFile); var environmentHash = Hash(environment);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(product + "\0" + executable.ToUpperInvariant() + "\0" + hash + "\0" + environmentHash))).ToLowerInvariant();
        return new(id, root, executable, hash, product, version, Path.GetFileName(root),
            Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? "Monitor" : "MarineBatch", environment, environmentHash);
    }
    /// <summary>部署前唯一选择安装入口；多入口要求明确选择，不能跨产品根猜测。</summary>
    public static ManagerInstallationIdentity Select(string installationRoot, string product, string? executable = null, string? environmentFile = null)
    {
        if (!Path.IsPathFullyQualified(installationRoot)) throw new ArgumentException("产品安装根需要绝对路径。");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installationRoot)); PathRules.NoLinks(root);
        if (executable is null)
        {
            var names = product switch { "E3D" => new[] { "mon.exe" }, "AM" => ["marine.bat", "marine.cmd"], "PDMS" => ["pdms.bat", "pdms.cmd"], _ => throw new ArgumentException("未知产品。") };
            var entries = names.Select(name => Path.Combine(root, name)).Where(File.Exists).ToArray();
            if (entries.Length == 0 && product is "AM" or "PDMS" && File.Exists(Path.Combine(root, "mon.exe"))) entries = [Path.Combine(root, "mon.exe")];
            if (entries.Length != 1) throw new IOException("安装入口缺失或不唯一，请明确选择该产品的实际入口。");
            executable = entries[0];
        }
        var identity = Identify(executable, product, environmentFile);
        if (!identity.Root.Equals(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("产品入口不属于所选安装根。");
        return identity;
    }
    private static string Hash(string file) { using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
    private static string? Product(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith("marine.", StringComparison.OrdinalIgnoreCase)) return "AM";
        if (name.StartsWith("pdms.", StringComparison.OrdinalIgnoreCase)) return "PDMS";
        if (path.Contains("Everything3D", StringComparison.OrdinalIgnoreCase)) return "E3D";
        if (path.Contains("Marine", StringComparison.OrdinalIgnoreCase) || path.Split('\\', '/').Contains("AM", StringComparer.OrdinalIgnoreCase)) return "AM";
        return path.Contains("PDMS", StringComparison.OrdinalIgnoreCase) ? "PDMS" : null;
    }
    private static string Message(Exception error) => string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
}
