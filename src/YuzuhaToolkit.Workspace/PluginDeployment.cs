using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace YuzuhaToolkit.Workspace;

/// <summary>复制预览的一项；目标散列用于检测预览后的并发修改。</summary>
/// <param name="RelativePath">插件内部相对路径。</param>
/// <param name="Bytes">来源字节数。</param>
/// <param name="SourceHash">来源内容散列。</param>
/// <param name="TargetHash">已有目标内容散列；空值表示新增。</param>
/// <param name="ReplacementBase64">受管计划明确展示的派生内容；原来源仍独立核验，不修改来源文件。</param>
public sealed record DeploymentFile(string RelativePath, long Bytes, string SourceHash, string? TargetHash,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ReplacementBase64 = null)
{
    /// <summary>写入目标的内容指纹；来源原始指纹仍保留。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DesiredHash => ReplacementBase64 is null ? SourceHash :
        Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(ReplacementBase64)));
    /// <summary>复制预览的新增、覆盖或内容相同状态。</summary>
    public string Action => TargetHash is null ? "新增" : TargetHash == DesiredHash ? "相同 · 跳过" : "覆盖 · 先备份";
}
/// <summary>已冻结的来源与目标预览；不会执行复制。</summary>
/// <param name="Source">目录、文件或 ZIP 路径。</param>
/// <param name="Target">注册目录中的插件专用子目录。</param>
/// <param name="Files">选中的文件快照。</param>
/// <param name="Skipped">省略的开发产物、索引及其他文件。</param>
/// <param name="SelectedPaths">显式载荷清单；省略时保持既有插件扩展名筛选。</param>
/// <param name="Policy">显式 tool 策略允许工具程序文件；空值保持旧 PML/资源选择。</param>
public sealed record DeploymentPlan(string Source, string Target, DeploymentFile[] Files, int Skipped,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[]? SelectedPaths = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Policy = null);
/// <summary>复制结果；备份位于 PMLLIB 外，旧文件不会自动删除。</summary>
/// <param name="Id">本次复制编号。</param>
/// <param name="Target">插件目标目录。</param>
/// <param name="Files">成功复制的文件列表。</param>
/// <param name="BackupDirectory">覆盖前文件的备份目录。</param>
/// <param name="CleanupWarning">成功写入后暂存清理失败的定位；不把已完成写入变成失败。</param>
public sealed record DeploymentReceipt(string Id, string Target, string[] Files, string BackupDirectory,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CleanupWarning = null);

/// <summary>文件事务失败及其实际恢复结果，供上层报告明确的部分恢复。</summary>
public sealed class DeploymentApplyException(string message, string backupDirectory, string[] recoveryErrors, Exception inner) : IOException(message, inner)
{
    /// <summary>保留的文件备份目录。</summary>
    public string BackupDirectory { get; } = backupDirectory;
    /// <summary>尚未恢复的文件或错误；空数组表示本次已应用修改已恢复。</summary>
    public string[] RecoveryErrors { get; } = recoveryErrors;
}

/// <summary>按冻结复制回执恢复的实际结果；外部改动列为未恢复，绝不覆盖。</summary>
public sealed record DeploymentRestoreReceipt(string[] Restored, string[] Unresolved, string BackupDirectory);

/// <summary>参照 LaunchDesk 的目录、文件与 ZIP 复制；先预览散列，再按冻结快照应用。</summary>
public static class PluginDeployment
{
    private static readonly HashSet<string> Extensions = new([".pmlobj", ".pmlfnc", ".pmlfrm", ".pmlcmd", ".pmlmac", ".pml", ".mac", ".dll", ".png", ".jpg", ".jpeg", ".svg", ".ico", ".xml", ".json", ".dat", ".init", ".txt"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Excluded = new([".git", "node_modules", "bin", "obj", "artifacts", "logs", "backups", "backup", "__pycache__"], StringComparer.OrdinalIgnoreCase);

    /// <summary>从明确清单预览受管运行载荷；与普通插件共用暂存、哈希、备份和应用实现。</summary>
    public static DeploymentPlan PreviewSelection(string sourceRoot, string targetRoot, IEnumerable<string> selectedPaths)
    {
        sourceRoot = Path.GetFullPath(sourceRoot); targetRoot = Path.GetFullPath(targetRoot);
        PathRules.NoLinks(sourceRoot); PathRules.NoLinks(targetRoot);
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException("来源目录不存在。");
        if (IsWithin(sourceRoot, targetRoot) || IsWithin(targetRoot, sourceRoot))
            throw new IOException("来源与目标不能相互包含。");
        var names = selectedPaths.Select(p => p.Replace('\\', '/')).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length is < 1 or > 100000 || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new ArgumentException("载荷清单为空、过大或存在重名。");
        var files = new List<DeploymentFile>(); long total = 0;
        foreach (var name in names)
        {
            var source = PathRules.Inside(sourceRoot, name); var target = PathRules.Inside(targetRoot, name);
            PathRules.NoLinks(source); PathRules.NoLinks(target);
            var bytes = new FileInfo(source).Length; total = checked(total + bytes);
            if (bytes > 256L * 1024 * 1024 || total > 1024L * 1024 * 1024) throw new IOException("载荷超过复制预算。");
            files.Add(new(name, bytes, HashOrNull(source)!, HashOrNull(target)));
        }
        return new(sourceRoot, targetRoot, files.ToArray(), 0, names);
    }

    /// <summary>枚举插件载荷，跳过开发产物和旧索引；支持文件、目录与标准 ZIP。</summary>
    /// <param name="source">插件源路径。</param>
    /// <param name="registeredPmlLib">用户选择并已注册的 PMLLIB 目录。</param>
    /// <param name="pluginName">单一插件子目录名。</param>
    public static DeploymentPlan Preview(string source, string registeredPmlLib, string pluginName)
        => PreviewCore(source, registeredPmlLib, pluginName, false);

    /// <summary>明确工具载荷复制；支持程序/脚本，拒绝活动资料库和 AVEVA 工程数据库目录。</summary>
    public static DeploymentPlan PreviewTool(string source, string registeredRoot, string name)
        => PreviewCore(source, registeredRoot, name, true);

    private static DeploymentPlan PreviewCore(string source, string registeredPmlLib, string pluginName, bool tool)
    {
        source = Path.GetFullPath(source); registeredPmlLib = Path.GetFullPath(registeredPmlLib);
        if (!Directory.Exists(registeredPmlLib)) throw new DirectoryNotFoundException("请先选择已注册的 PMLLIB 目录。");
        if (pluginName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || pluginName.EndsWith(' ') || pluginName.EndsWith('.') || string.IsNullOrWhiteSpace(pluginName)) throw new ArgumentException("插件名必须是一个目录名，不能以空格或点结尾。");
        var target = PathRules.Inside(registeredPmlLib, pluginName); PathRules.NoLinks(source);
        if (tool && ProtectedData(source)) throw new IOException("工程数据库或活动知识库请使用专用快照流程，不参与普通工具复制。");
        if (IsWithin(source, registeredPmlLib) || Directory.Exists(source) && IsWithin(registeredPmlLib, source)) throw new IOException("开发源与注册目录不能相互包含；请把开发副本放在 PMLLIB 外。");
        var files = new List<DeploymentFile>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var skipped = 0; long total = 0; var entries = 0;
        void Add(string relative, Stream content, long length)
        {
            if (++entries > 20000) throw new IOException("插件条目超过 20,000，请缩小复制范围。");
            relative = relative.Replace('\\', '/');
            var destination = PathRules.Inside(target, relative);
            if (Directory.Exists(destination)) throw new IOException("文件目标已有同名目录：" + relative);
            if (!seen.Add(relative)) throw new InvalidDataException("插件包含重复目标路径：" + relative);
            if (tool && ProtectedData(relative)) throw new IOException("工具载荷包含工程数据库或活动知识库：" + relative);
            if (relative.Split('/').Any(Excluded.Contains) || Path.GetFileName(relative).Equals("pml.index", StringComparison.OrdinalIgnoreCase) || !tool && !Extensions.Contains(Path.GetExtension(relative))) { skipped++; return; }
            if ((total = checked(total + length)) > 536870912 || length > 134217728) throw new IOException("插件资源超过复制预算（单文件 128 MiB，总计 512 MiB）。");
            files.Add(new(relative, length, Convert.ToHexString(SHA256.HashData(content)), HashOrNull(destination)));
        }
        if (Directory.Exists(source))
        {
            var pending = new Stack<string>(); pending.Push(source);
            while (pending.TryPop(out var directory)) foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if (++entries > 20000) throw new IOException("开发目录条目过多，请选择插件源码子目录。");
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("插件源包含链接：" + entry.Name);
                if (entry is DirectoryInfo) { if (Excluded.Contains(entry.Name)) skipped++; else pending.Push(entry.FullName); }
                else { using var content = File.OpenRead(entry.FullName); Add(Path.GetRelativePath(source, entry.FullName), content, ((FileInfo)entry).Length); }
            }
        }
        else if (Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            if (new FileInfo(source).Length > 536870912) throw new IOException("ZIP 文件超过 512 MiB 预算。");
            using var zip = ZipFile.OpenRead(source);
            if (zip.Entries.Count > 20000) throw new IOException("ZIP 条目超过 20,000，请缩小复制范围。");
            var prefix = ZipPrefix(zip, pluginName);
            foreach (var entry in zip.Entries)
            {
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & 0x400) != 0) throw new IOException("ZIP 不支持链接条目。");
                var relative = entry.FullName.Replace('\\', '/');
                _ = PathRules.Inside(target, relative.TrimEnd('/'));
                if (relative.EndsWith('/')) continue;
                if (prefix.Length > 0) relative = relative[prefix.Length..];
                using var content = entry.Open(); Add(relative, content, entry.Length);
            }
        }
        else { using var content = File.OpenRead(source); Add(Path.GetFileName(source), content, content.Length); }
        if (files.Count == 0) throw new IOException(tool ? "所选工具载荷没有可复制文件。" : "未找到 PML 或受支持资源；请选择插件目录、单文件或 ZIP。");
        return new(source, target, files.ToArray(), skipped, Policy: tool ? "tool" : null);
    }

    /// <summary>保持既有两参数入口；应用和恢复仍使用同一个文件事务。</summary>
    public static DeploymentReceipt Apply(DeploymentPlan plan, string? stateDirectory = null) => Apply(plan, stateDirectory, null);

    /// <summary>校验预览仍然有效后复制；覆盖前备份，失败时恢复已应用的文件。</summary>
    /// <param name="plan">用户查看的冻结预览。</param>
    /// <param name="stateDirectory">备份根目录；测试可使用隔离目录，且必须位于 PMLLIB 外。</param>
    /// <param name="phase">可选阶段观察/故障注入边界；不是脚本执行入口。</param>
    public static DeploymentReceipt Apply(DeploymentPlan plan, string? stateDirectory, Action<string>? phase)
    {
        var registered = Path.GetDirectoryName(plan.Target)!;
        var fresh = plan.Policy == "tool" ? PreviewTool(plan.Source, registered, Path.GetFileName(plan.Target)) : plan.SelectedPaths is null ? Preview(plan.Source, registered, Path.GetFileName(plan.Target))
            : PreviewSelection(plan.Source, plan.Target, plan.SelectedPaths);
        var refreshedFiles = fresh.Files.Select(file => file with { ReplacementBase64 =
            plan.Files.SingleOrDefault(p => p.RelativePath == file.RelativePath)?.ReplacementBase64 }).ToArray();
        if (!plan.Files.SequenceEqual(refreshedFiles)) throw new IOException("来源或目标在预览后变化，请重新预览。");
        var id = DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N");
        var transaction = Path.Combine(stateDirectory ?? WorkspaceSettings.StateDirectory, "Deployments", id); var stage = Path.Combine(transaction, "stage"); var backup = Path.Combine(transaction, "backups");
        if (IsWithin(transaction, plan.SelectedPaths is null ? registered : plan.Target)) throw new IOException(plan.SelectedPaths is null
            ? "备份目录必须位于注册 PMLLIB 外；请更改 YUZUHA_STATE_DIR。" : "备份目录必须位于复制目标之外；请更改 YUZUHA_STATE_DIR。");
        PathRules.NoLinks(transaction); Directory.CreateDirectory(stage); var applied = new List<DeploymentFile>();
        DeploymentReceipt receipt; string? cleanupWarning = null;
        try
        {
            phase?.Invoke("stage");
            using var zip = !Directory.Exists(plan.Source) && Path.GetExtension(plan.Source).Equals(".zip", StringComparison.OrdinalIgnoreCase) ? ZipFile.OpenRead(plan.Source) : null;
            var prefix = zip is null ? "" : ZipPrefix(zip, Path.GetFileName(plan.Target));
            foreach (var file in plan.Files)
            {
                phase?.Invoke("stage:" + file.RelativePath);
                var staged = PathRules.Inside(stage, file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                using var input = zip is not null ? zip.Entries.Single(entry => entry.FullName.Replace('\\', '/').Equals(prefix + file.RelativePath, StringComparison.OrdinalIgnoreCase)).Open() : File.OpenRead(Directory.Exists(plan.Source) ? PathRules.Inside(plan.Source, file.RelativePath) : plan.Source);
                using (var output = File.Create(staged)) input.CopyTo(output);
                if (HashOrNull(staged) != file.SourceHash) throw new IOException("来源在暂存时变化，请重新预览。");
                if (file.ReplacementBase64 is not null)
                {
                    var replacement = Convert.FromBase64String(file.ReplacementBase64);
                    if (replacement.LongLength > 256L * 1024 * 1024) throw new IOException("派生载荷超过预算。");
                    File.WriteAllBytes(staged, replacement);
                    if (HashOrNull(staged) != file.DesiredHash) throw new IOException("派生载荷暂存核验失败。");
                }
            }
            foreach (var file in plan.Files)
            {
                phase?.Invoke("before-write:" + file.RelativePath);
                var destination = PathRules.Inside(plan.Target, file.RelativePath);
                if (HashOrNull(destination) != file.TargetHash) throw new IOException("目标被其他程序修改，复制已停止。");
                if (file.TargetHash == file.DesiredHash) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (file.TargetHash is not null) { var original = PathRules.Inside(backup, file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(original)!); File.Copy(destination, original); }
                var temporary = destination + ".yuzuha-" + id + ".tmp";
                try { File.Copy(PathRules.Inside(stage, file.RelativePath), temporary); File.Move(temporary, destination, true); applied.Add(file); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                phase?.Invoke("after-write:" + file.RelativePath);
            }
            receipt = new DeploymentReceipt(id, plan.Target, applied.Select(f => f.RelativePath).ToArray(), backup);
            phase?.Invoke("receipt");
            File.WriteAllText(Path.Combine(transaction, "receipt.json"), JsonSerializer.Serialize(receipt, WorkspaceJsonContext.Default.DeploymentReceipt));
        }
        catch (Exception failure)
        {
            var recoveryErrors = new List<string>();
            foreach (var file in applied.AsEnumerable().Reverse())
            {
                try
                {
                    var destination = PathRules.Inside(plan.Target, file.RelativePath);
                    if (HashOrNull(destination) != file.DesiredHash) throw new IOException("目标又被修改，保留现状：" + file.RelativePath);
                    if (file.TargetHash is null) File.Delete(destination);
                    else
                    {
                        var original = PathRules.Inside(backup, file.RelativePath); PathRules.NoLinks(original);
                        if (HashOrNull(original) != file.TargetHash) throw new IOException("备份缺失或已变化，保留当前目标：" + file.RelativePath);
                        var temporary = destination + ".rollback-" + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            File.Copy(original, temporary, false);
                            if (HashOrNull(temporary) != file.TargetHash) throw new IOException("恢复暂存与原文件指纹不匹配：" + file.RelativePath);
                            if (HashOrNull(destination) != file.DesiredHash) throw new IOException("恢复前目标又被修改：" + file.RelativePath);
                            File.Move(temporary, destination, true);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { recoveryErrors.Add(string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message); }
            }
            throw new DeploymentApplyException((string.IsNullOrWhiteSpace(failure.Message) ? failure.GetType().Name : failure.Message) +
                (recoveryErrors.Count > 0 ? " 恢复需人工处理：" + string.Join("；", recoveryErrors) : " 已恢复本次复制的修改。") + " 备份：" + backup, backup, recoveryErrors.ToArray(), failure);
        }
        finally
        {
            try { phase?.Invoke("cleanup"); if (Directory.Exists(stage)) { PathRules.NoLinks(stage); Directory.Delete(PathRules.Inside(transaction, "stage"), true); } }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                cleanupWarning = "Stage=" + stage + " " + (string.IsNullOrWhiteSpace(cleanup.Message) ? cleanup.GetType().Name : cleanup.Message);
                try { File.WriteAllText(Path.Combine(transaction, "cleanup-warning.txt"), cleanupWarning); }
                catch (Exception logging) when (logging is IOException or UnauthorizedAccessException) { /* The successful receipt still carries the warning. */ }
            }
        }
        return receipt with { CleanupWarning = cleanupWarning };
    }

    /// <summary>恢复同次已写文件；核对备份及目标指纹，允许重复确认已恢复项，不重试业务操作。</summary>
    public static DeploymentRestoreReceipt Restore(DeploymentPlan plan, DeploymentReceipt receipt)
    {
        if (!string.Equals(Path.GetFullPath(plan.Target), Path.GetFullPath(receipt.Target), StringComparison.OrdinalIgnoreCase))
            throw new IOException("复制回执与计划目标不匹配。");
        PathRules.NoLinks(receipt.BackupDirectory);
        var applied = receipt.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (applied.Any(name => !plan.Files.Any(f => f.RelativePath.Equals(name, StringComparison.OrdinalIgnoreCase))))
            throw new IOException("复制回执包含计划之外的文件。");
        var restored = new List<string>(); var unresolved = new List<string>();
        foreach (var file in plan.Files.Where(f => applied.Contains(f.RelativePath)).Reverse())
        {
            try
            {
                var destination = PathRules.Inside(plan.Target, file.RelativePath); PathRules.NoLinks(destination);
                var current = HashOrNull(destination);
                if (current == file.TargetHash) { restored.Add(file.RelativePath); continue; }
                if (current != file.DesiredHash) throw new IOException("目标已被外部修改。");
                if (file.TargetHash is null) File.Delete(destination);
                else
                {
                    var backup = PathRules.Inside(receipt.BackupDirectory, file.RelativePath); PathRules.NoLinks(backup);
                    if (HashOrNull(backup) != file.TargetHash) throw new IOException("备份缺失或已变化。");
                    var temporary = destination + ".restore-" + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.Copy(backup, temporary, false);
                        if (HashOrNull(destination) != file.DesiredHash) throw new IOException("恢复前目标又被修改。");
                        File.Move(temporary, destination, true);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                restored.Add(file.RelativePath);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { unresolved.Add(file.RelativePath + ": " + (string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message)); }
        }
        return new(restored.ToArray(), unresolved.ToArray(), receipt.BackupDirectory);
    }

    private static bool IsWithin(string path, string root) => string.Equals(Path.GetFullPath(path).TrimEnd('\\', '/'), Path.GetFullPath(root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string? HashOrNull(string file) { if (!File.Exists(file)) return null; using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static bool ProtectedData(string path)
    {
        var extension = Path.GetExtension(path);
        return new[] { ".db", ".sqlite", ".sqlite3", ".lock" }.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
            path.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) || path.EndsWith("-shm", StringComparison.OrdinalIgnoreCase) ||
            path.Replace('\\', '/').Split('/').Any(p => p.Equals("databases", StringComparison.OrdinalIgnoreCase) || p.Equals("pdmsdata", StringComparison.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(p, "^[A-Za-z0-9_]{2,16}000$", System.Text.RegularExpressions.RegexOptions.CultureInvariant));
    }
    private static string ZipPrefix(ZipArchive zip, string pluginName)
    {
        var paths = zip.Entries.Where(entry => !entry.FullName.EndsWith('/')).Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        var prefix = pluginName + "/";
        return paths.Length > 0 && paths.All(relative => relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? prefix : "";
    }
}
