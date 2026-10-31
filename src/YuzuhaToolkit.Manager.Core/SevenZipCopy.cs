using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>可选格式依赖；缺失不影响普通文件、目录和 ZIP。</summary>
public sealed record CopyFormatCapability(string Format, bool Available, string? DependencyPath, string? DependencySha256, string Message);
/// <summary>绑定压缩包、独立 7zr 工具及实际文件差异的冻结计划。</summary>
public sealed record SevenZipCopyPlan(string Archive, string ArchiveSha256, string Tool, string ToolSha256, DeploymentPlan Files);

/// <summary>迁移旧原型的 7zr list/test/extract 流程，独立暂存、验证后才调用共同文件事务。</summary>
public static class SevenZipCopy
{
    /// <summary>明确覆盖优先；不搜索 PATH，也不自动调用其他应用内的程序。</summary>
    public static string ToolPath => Path.GetFullPath(Environment.GetEnvironmentVariable("YUZUHA_7ZR_PATH") is { Length: > 0 } selected
        ? selected : Path.Combine(UserDataPaths.InstallRoot, "Tools", "7zr.exe"));

    /// <summary>只读格式登记与依赖指纹，不启动工具。</summary>
    public static CopyFormatCapability[] Formats() => [new("file", true, null, null, "内置文件复制。"), new("directory", true, null, null, "内置目录复制。"),
        new("zip", true, null, null, "内置 ZIP。"), new("7z", File.Exists(ToolPath), ToolPath, File.Exists(ToolPath) ? Hash(ToolPath) : null,
            File.Exists(ToolPath) ? "独立 7zr 依赖已找到；执行时再次核对身份和压缩完整性。" : "缺少可选 Tools/7zr.exe；请明确配置该依赖后重新预览。")];

    /// <summary>列路径、验证完整性并在私有暂存区展开，预览不写复制目标。</summary>
    public static SevenZipCopyPlan Preview(string archive, string registeredRoot, string name, string? tool = null,
        string? stateDirectory = null, CancellationToken token = default)
    {
        archive = Path.GetFullPath(archive); tool = Path.GetFullPath(tool ?? ToolPath);
        ValidateTool(tool);
        using var toolLease = new FileStream(tool, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archiveLease = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        var archiveHash = Hash(archiveLease); var toolHash = Hash(toolLease);
        using var extracted = Extract(archive, tool, stateDirectory, token);
        var files = PluginDeployment.PreviewTool(Payload(extracted.Directory, name), registeredRoot, name);
        return new(archive, archiveHash, tool, toolHash, files with { Source = archive });
    }

    /// <summary>再次核对包与工具，实际展开文件必须等于预览，之后使用共同备份/恢复事务。</summary>
    public static ManagerCopyReceipt Apply(SevenZipCopyPlan plan, string? stateDirectory = null, CancellationToken token = default)
    {
        ValidateTool(plan.Tool);
        using var toolLease = new FileStream(plan.Tool, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archiveLease = new FileStream(plan.Archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Hash(toolLease) != plan.ToolSha256 || Hash(archiveLease) != plan.ArchiveSha256) throw new IOException("7z 包或格式工具在预览后变化，请重新预览。");
        using var extracted = Extract(plan.Archive, plan.Tool, stateDirectory, token);
        var source = Payload(extracted.Directory, Path.GetFileName(plan.Files.Target));
        var fresh = PluginDeployment.PreviewTool(source, Path.GetDirectoryName(plan.Files.Target)!, Path.GetFileName(plan.Files.Target));
        if (fresh.Target != plan.Files.Target || fresh.Skipped != plan.Files.Skipped || !fresh.Files.SequenceEqual(plan.Files.Files)) throw new IOException("7z 载荷或目标与冻结预览不一致。");
        token.ThrowIfCancellationRequested();
        return new(fresh, PluginDeployment.Apply(fresh, stateDirectory));
    }

    /// <summary>验证 7zr -slt 的每一个条目，拒绝路径别名、链接、重复、加密和超额展开。</summary>
    public static string[] ValidateListing(string text, string boundary)
    {
        var separator = false; var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long bytes = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim() == "----------") { separator = true; continue; }
            if (!separator) continue;
            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                var path = line[7..].Replace('\\', '/'); _ = PathRules.Inside(boundary, path);
                if (!paths.Add(path)) throw new InvalidDataException("7z 有重复或别名路径：" + path);
                if (paths.Count > 20000) throw new IOException("7z 超过 20000 条目预算。");
            }
            else if (line.StartsWith("Size = ", StringComparison.Ordinal))
            {
                if (!long.TryParse(line[7..], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size > 134217728 || (bytes = checked(bytes + size)) > 536870912)
                    throw new InvalidDataException("7z 展开体积超过预算（单文件 128 MiB、总计 512 MiB）。");
            }
            else if (line.StartsWith("Encrypted = +", StringComparison.Ordinal) || line.StartsWith("Symbolic Link =", StringComparison.Ordinal) ||
                line.StartsWith("Hard Link =", StringComparison.Ordinal) || line.StartsWith("Alternate Stream =", StringComparison.Ordinal) ||
                line.StartsWith("Anti = +", StringComparison.Ordinal) || line.StartsWith("Attributes =", StringComparison.Ordinal) &&
                System.Text.RegularExpressions.Regex.IsMatch(line, @"\bl[rwx-]{9}\b"))
                throw new InvalidDataException("7z 不支持加密、符号/硬链接、数据流或反向条目。");
        }
        if (paths.Count == 0) throw new InvalidDataException("7z 为空或没有受支持的路径清单。");
        return paths.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Extraction Extract(string archive, string tool, string? state, CancellationToken token)
    {
        if (!Path.GetExtension(archive).Equals(".7z", StringComparison.OrdinalIgnoreCase) || new FileInfo(archive).Length > 536870912)
            throw new InvalidDataException("请选择 512 MiB 内的 .7z 包。");
        PathRules.NoLinks(archive);
        var root = Path.Combine(state ?? WorkspaceSettings.StateDirectory, "SevenZipStage"); PathRules.NoLinks(root); Directory.CreateDirectory(root);
        var stage = PathRules.Inside(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        var extracted = new Extraction(stage);
        try
        {
            var listing = Run(tool, ["l", "-slt", "-sccUTF-8", "--", archive], token);
            var paths = ValidateListing(listing, stage);
            Run(tool, ["t", "-y", "-sccUTF-8", "--", archive], token);
            Run(tool, ["x", "-y", "-sccUTF-8", "-o" + stage, "--", archive], token);
            foreach (var path in paths) { token.ThrowIfCancellationRequested(); var actual = PathRules.Inside(stage, path); if (!File.Exists(actual) && !System.IO.Directory.Exists(actual)) throw new InvalidDataException("7z 缺少已列出的展开条目：" + path); }
            var pending = new Stack<string>(); pending.Push(stage); long count = 0, bytes = 0;
            while (pending.TryPop(out var directory))
            {
                token.ThrowIfCancellationRequested(); PathRules.NoLinks(directory);
                foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(directory))
                {
                    PathRules.NoLinks(entry); var relative = Path.GetRelativePath(stage, entry).Replace('\\', '/');
                    var isDirectory = System.IO.Directory.Exists(entry);
                    if (!paths.Contains(relative, StringComparer.OrdinalIgnoreCase) && !(isDirectory && paths.Any(p => p.StartsWith(relative + "/", StringComparison.OrdinalIgnoreCase))))
                        throw new InvalidDataException("7z 展开出现未登记路径：" + relative);
                    if (++count > 20000) throw new IOException("7z 展开条目超过预算。");
                    if (isDirectory) pending.Push(entry);
                    else { var size = new FileInfo(entry).Length; if (size > 134217728 || (bytes = checked(bytes + size)) > 536870912) throw new IOException("7z 实际展开体积超过预算。"); }
                }
            }
            return extracted;
        }
        catch { extracted.Dispose(); throw; }
    }

    private static string Run(string tool, string[] arguments, CancellationToken token) => RunAsync(tool, arguments, token).GetAwaiter().GetResult();
    private static async Task<string> RunAsync(string tool, string[] arguments, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(2));
        using var process = new Process { StartInfo = new(tool) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(tool)! } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Task<string>? output = null, errors = null;
        try
        {
            token.ThrowIfCancellationRequested(); process.Start(); process.StandardInput.Close();
            output = Pump(process.StandardOutput, 32 * 1024 * 1024); errors = Pump(process.StandardError, 128 * 1024);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var stdout = await output.ConfigureAwait(false); var stderr = await errors.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidDataException("7zr 检查/展开失败，ExitCode=" + process.ExitCode + " " + Tail(stdout + "\n" + stderr));
            return stdout;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("7zr 超时或输出超过预算；未自动重试。"); }
        catch (System.ComponentModel.Win32Exception error) { throw new IOException("7zr 进程启动失败：" + (string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message), error); }
        finally
        {
            try { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); } }
            catch (InvalidOperationException) { }
            if (output is not null) try { await output.ConfigureAwait(false); } catch (OperationCanceledException) { }
            if (errors is not null) try { await errors.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        async Task<string> Pump(StreamReader reader, int limit)
        {
            var text = new StringBuilder(); var buffer = new char[4096]; int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false)) > 0)
            { if (text.Length + count > limit) { deadline.Cancel(); throw new OperationCanceledException(deadline.Token); } text.Append(buffer, 0, count); }
            return text.ToString();
        }
    }
    private static string Tail(string text) => text.Length > 1800 ? text[^1800..] : text;
    private static string Payload(string stage, string name)
    {
        var entries = System.IO.Directory.GetFileSystemEntries(stage);
        return entries.Length == 1 && System.IO.Directory.Exists(entries[0]) && Path.GetFileName(entries[0]).Equals(name, StringComparison.OrdinalIgnoreCase) ? entries[0] : stage;
    }
    private static void ValidateTool(string tool)
    {
        if (!Path.IsPathFullyQualified(tool) || !Path.GetFileName(tool).Equals("7zr.exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("格式依赖必须明确指定独立的 7zr.exe。");
        PathRules.NoLinks(tool); if (!File.Exists(tool)) throw new FileNotFoundException("可选 7zr 格式依赖缺失，未开始复制。", tool);
    }
    private static string Hash(string file) { using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read); return Hash(input); }
    private static string Hash(Stream input) { input.Position = 0; return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
    /// <summary>拥有一次解压暂存目录的清理租约；清理失败时尽力写出同级警告文件。</summary>
    private sealed class Extraction(string directory) : IDisposable
    {
        /// <summary>本次解压创建并由租约负责清理的暂存目录。</summary>
        public string Directory { get; } = directory;
        /// <summary>检查链接后递归删除暂存目录；对 IO 或权限失败记录清理警告，不掩盖调用方结果。</summary>
        public void Dispose()
        {
            try { PathRules.NoLinks(Directory); if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(Directory)!, Path.GetFileName(Directory) + ".cleanup-warning.txt"), Directory + " " + (string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
                catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
