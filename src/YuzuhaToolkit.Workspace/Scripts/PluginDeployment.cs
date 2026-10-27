using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace YuzuhaToolkit.Workspace;

/// <summary>复制预览的一项；目标散列用于检测预览后的并发修改。</summary>
/// <param name="RelativePath">插件内部相对路径。</param>
/// <param name="Bytes">来源字节数。</param>
/// <param name="SourceHash">来源内容散列。</param>
/// <param name="TargetHash">已有目标内容散列；空值表示新增。</param>
public sealed record DeploymentFile(string RelativePath, long Bytes, string SourceHash, string? TargetHash)
{
    /// <summary>复制预览的新增、覆盖或内容相同状态。</summary>
    public string Action => TargetHash is null ? "新增" : TargetHash == SourceHash ? "相同 · 跳过" : "覆盖 · 先备份";
}
/// <summary>已冻结的来源与目标预览；不会执行复制。</summary>
/// <param name="Source">目录、文件或 ZIP 路径。</param>
/// <param name="Target">注册目录中的插件专用子目录。</param>
/// <param name="Files">选中的文件快照。</param>
/// <param name="Skipped">省略的开发产物、索引及其他文件。</param>
public sealed record DeploymentPlan(string Source, string Target, DeploymentFile[] Files, int Skipped);
/// <summary>复制结果；备份位于 PMLLIB 外，旧文件不会自动删除。</summary>
/// <param name="Id">本次复制编号。</param>
/// <param name="Target">插件目标目录。</param>
/// <param name="Files">成功复制的文件列表。</param>
/// <param name="BackupDirectory">覆盖前文件的备份目录。</param>
public sealed record DeploymentReceipt(string Id, string Target, string[] Files, string BackupDirectory);

/// <summary>参照 LaunchDesk 的目录、文件与 ZIP 复制；先预览散列，再按冻结快照应用。</summary>
public static class PluginDeployment
{
    private static readonly HashSet<string> Extensions = new([".pmlobj", ".pmlfnc", ".pmlfrm", ".pmlcmd", ".pmlmac", ".pml", ".mac", ".dll", ".png", ".jpg", ".jpeg", ".svg", ".ico", ".xml", ".json", ".dat", ".init", ".txt"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Excluded = new([".git", "node_modules", "bin", "obj", "artifacts", "logs", "backups", "backup", "__pycache__"], StringComparer.OrdinalIgnoreCase);

    /// <summary>枚举插件载荷，跳过开发产物和旧索引；支持文件、目录与标准 ZIP。</summary>
    /// <param name="source">插件源路径。</param>
    /// <param name="registeredPmlLib">用户选择并已注册的 PMLLIB 目录。</param>
    /// <param name="pluginName">单一插件子目录名。</param>
    public static DeploymentPlan Preview(string source, string registeredPmlLib, string pluginName)
    {
        source = Path.GetFullPath(source); registeredPmlLib = Path.GetFullPath(registeredPmlLib);
        if (!Directory.Exists(registeredPmlLib)) throw new DirectoryNotFoundException("请先选择已注册的 PMLLIB 目录。");
        if (pluginName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || pluginName.EndsWith(' ') || pluginName.EndsWith('.') || string.IsNullOrWhiteSpace(pluginName)) throw new ArgumentException("插件名必须是一个目录名，不能以空格或点结尾。");
        var target = PathRules.Inside(registeredPmlLib, pluginName); PathRules.NoLinks(source);
        if (IsWithin(source, registeredPmlLib) || Directory.Exists(source) && IsWithin(registeredPmlLib, source)) throw new IOException("开发源与注册目录不能相互包含；请把开发副本放在 PMLLIB 外。");
        var files = new List<DeploymentFile>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var skipped = 0; long total = 0; var entries = 0;
        void Add(string relative, Stream content, long length)
        {
            if (++entries > 20000) throw new IOException("插件条目超过 20,000，请缩小复制范围。");
            relative = relative.Replace('\\', '/');
            var destination = PathRules.Inside(target, relative);
            if (Directory.Exists(destination)) throw new IOException("文件目标已有同名目录：" + relative);
            if (!seen.Add(relative)) throw new InvalidDataException("插件包含重复目标路径：" + relative);
            if (relative.Split('/').Any(Excluded.Contains) || Path.GetFileName(relative).Equals("pml.index", StringComparison.OrdinalIgnoreCase) || !Extensions.Contains(Path.GetExtension(relative))) { skipped++; return; }
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
        if (files.Count == 0) throw new IOException("未找到 PML 或受支持资源；请选择插件目录、单文件或 ZIP。");
        return new(source, target, files.ToArray(), skipped);
    }

    /// <summary>校验预览仍然有效后复制；覆盖前备份，失败时恢复已应用的文件。</summary>
    /// <param name="plan">用户查看的冻结预览。</param>
    /// <param name="stateDirectory">备份根目录；测试可使用隔离目录，且必须位于 PMLLIB 外。</param>
    public static DeploymentReceipt Apply(DeploymentPlan plan, string? stateDirectory = null)
    {
        var registered = Path.GetDirectoryName(plan.Target)!;
        var fresh = Preview(plan.Source, registered, Path.GetFileName(plan.Target));
        if (!plan.Files.SequenceEqual(fresh.Files)) throw new IOException("来源或目标在预览后变化，请重新预览。");
        var id = DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N");
        var transaction = Path.Combine(stateDirectory ?? UserDataPaths.StateDirectory, "Deployments", id); var stage = Path.Combine(transaction, "stage"); var backup = Path.Combine(transaction, "backups");
        if (IsWithin(transaction, registered)) throw new IOException("备份目录必须位于注册 PMLLIB 外；请更改 YUZUHA_STATE_DIR。");
        PathRules.NoLinks(transaction); Directory.CreateDirectory(stage); var applied = new List<DeploymentFile>();
        try
        {
            using var zip = !Directory.Exists(plan.Source) && Path.GetExtension(plan.Source).Equals(".zip", StringComparison.OrdinalIgnoreCase) ? ZipFile.OpenRead(plan.Source) : null;
            var prefix = zip is null ? "" : ZipPrefix(zip, Path.GetFileName(plan.Target));
            foreach (var file in plan.Files)
            {
                var staged = PathRules.Inside(stage, file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                using var input = zip is not null ? zip.Entries.Single(entry => entry.FullName.Replace('\\', '/').Equals(prefix + file.RelativePath, StringComparison.OrdinalIgnoreCase)).Open() : File.OpenRead(Directory.Exists(plan.Source) ? PathRules.Inside(plan.Source, file.RelativePath) : plan.Source);
                using (var output = File.Create(staged)) input.CopyTo(output);
                if (HashOrNull(staged) != file.SourceHash) throw new IOException("来源在暂存时变化，请重新预览。");
            }
            foreach (var file in plan.Files)
            {
                var destination = PathRules.Inside(plan.Target, file.RelativePath);
                if (HashOrNull(destination) != file.TargetHash) throw new IOException("目标被其他程序修改，复制已停止。");
                if (file.TargetHash == file.SourceHash) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (file.TargetHash is not null) { var original = PathRules.Inside(backup, file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(original)!); File.Copy(destination, original); }
                var temporary = destination + ".yuzuha-" + id + ".tmp";
                try { File.Copy(PathRules.Inside(stage, file.RelativePath), temporary); File.Move(temporary, destination, true); applied.Add(file); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            var receipt = new DeploymentReceipt(id, plan.Target, applied.Select(f => f.RelativePath).ToArray(), backup);
            File.WriteAllText(Path.Combine(transaction, "receipt.json"), JsonSerializer.Serialize(receipt, WorkspaceJsonContext.Default.DeploymentReceipt));
            return receipt;
        }
        catch (Exception failure)
        {
            var recoveryErrors = new List<string>();
            foreach (var file in applied.AsEnumerable().Reverse())
            {
                try
                {
                    var destination = PathRules.Inside(plan.Target, file.RelativePath);
                    if (HashOrNull(destination) != file.SourceHash) throw new IOException("目标又被修改，保留现状：" + file.RelativePath);
                    if (file.TargetHash is null) File.Delete(destination); else File.Copy(PathRules.Inside(backup, file.RelativePath), destination, true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { recoveryErrors.Add(error.Message); }
            }
            throw new IOException(failure.Message + (recoveryErrors.Count > 0 ? " 恢复需人工处理：" + string.Join("；", recoveryErrors) : " 已恢复本次复制的修改。") + " 备份：" + backup, failure);
        }
        finally { if (Directory.Exists(stage)) { PathRules.NoLinks(stage); Directory.Delete(PathRules.Inside(transaction, "stage"), true); } }
    }

    private static bool IsWithin(string path, string root) => string.Equals(Path.GetFullPath(path).TrimEnd('\\', '/'), Path.GetFullPath(root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string? HashOrNull(string file) { if (!File.Exists(file)) return null; using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static string ZipPrefix(ZipArchive zip, string pluginName)
    {
        var paths = zip.Entries.Where(entry => !entry.FullName.EndsWith('/')).Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        var prefix = pluginName + "/";
        return paths.Length > 0 && paths.All(relative => relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? prefix : "";
    }
}
