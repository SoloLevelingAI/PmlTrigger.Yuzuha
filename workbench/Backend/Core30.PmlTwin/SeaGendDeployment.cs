using Core30.Core;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Core30.PmlTwin;

public sealed record SeaGendDeployment(string Downloads, string Archive, string ArchiveStatus, string Installed,
    string InstalledStatus, string Comparison)
{
    private const int FileLimit = 50000;
    private static readonly EnumerationOptions Tree = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public static SeaGendDeployment Detect(string? evars, string? product, string? installation)
    {
        var downloads = DownloadsPath();
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(evars) && !string.IsNullOrEmpty(installation) && !string.IsNullOrEmpty(product))
        {
            try { roots.AddRange(PmlTwinFeatures.ReadLibraryFolders(evars, product, installation, [])
                .Where(folder => folder.State == "available").Select(folder => folder.Path)); }
            catch (Exception error) when (CoreRunner.IsOperationalError(error)) { }
        }
        var conventional = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "AVEVA", "PMLLIB", "SeaGend");
        var installed = new[] { conventional }.Concat(roots.Select(root =>
                Path.GetFileName(Path.TrimEndingDirectorySeparator(root)).Equals("SeaGend", StringComparison.OrdinalIgnoreCase)
                    ? root : Path.Combine(root, "SeaGend")))
            .FirstOrDefault(Directory.Exists) ?? conventional;
        var archive = "";
        var archiveStatus = "未找到 SeaGend*.7z";
        var installedStatus = "未找到 SeaGend 目录";
        Dictionary<string, long>? packageFiles = null, folderFiles = null;
        try
        {
            var package = Directory.Exists(downloads)
                ? new DirectoryInfo(downloads).EnumerateFiles("SeaGend*.7z")
                    .Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0)
                    .OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                : null;
            if (package is not null)
            {
                archive = package.FullName;
                packageFiles = ArchiveFiles(package.FullName);
                archiveStatus = $"{packageFiles.Count:N0} 个文件 · {package.LastWriteTime:yyyy-MM-dd HH:mm}";
            }
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error) || error is SharpCompressException)
        {
            archiveStatus = "无法核对下载包 · " + error.Message;
        }

        try
        {
            if (Directory.Exists(installed))
            {
                var scope = new FeatureContext(installed);
                folderFiles = Directory.EnumerateFiles(scope.Root, "*", Tree)
                    .Select(path => (Name: Path.GetRelativePath(scope.Root, path).Replace('\\', '/'), Size: new FileInfo(path).Length))
                    .Where(file => !ZipToolPackages.IsPmlIndex(file.Name)).Take(FileLimit + 1)
                    .ToDictionary(file => file.Name, file => file.Size, StringComparer.OrdinalIgnoreCase);
                if (folderFiles.Count > FileLimit) throw new InvalidDataException("SeaGend 目录超过检测文件数上限。");
                installedStatus = $"已检测到 · {folderFiles.Count:N0} 个文件";
            }
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            installedStatus = "无法核对本机目录 · " + error.Message;
            folderFiles = null;
        }

        var comparison = "—";
        if (packageFiles is not null && folderFiles is not null)
        {
            var added = packageFiles.Keys.Count(file => !folderFiles.ContainsKey(file));
            var changed = packageFiles.Count(file => folderFiles.TryGetValue(file.Key, out var size) && size != file.Value);
            var local = folderFiles.Keys.Count(file => !packageFiles.ContainsKey(file));
            comparison = $"下载包新增 {added:N0} · 同名大小不同 {changed:N0} · 仅在本机 {local:N0}";
        }
        return new(downloads, archive, archiveStatus, installed, installedStatus, comparison);
    }

    private static string DownloadsPath()
    {
        const string downloadsId = "{374DE290-123F-4565-9164-39C4925E467B}";
        if (OperatingSystem.IsWindows())
        {
            using var folders = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
            if (folders?.GetValue(downloadsId) is string configured && Path.IsPathFullyQualified(configured))
                return configured;
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static Dictionary<string, long> ArchiveFiles(string path)
    {
        using var input = File.OpenRead(path);
        using var archive = SevenZipArchive.OpenArchive(input, new ReaderOptions());
        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;
        foreach (var entry in archive.Entries)
        {
            if (++scanned > FileLimit) throw new InvalidDataException("压缩包超过检测文件数上限。");
            if (entry.IsDirectory) continue;
            if (entry.IsEncrypted || !string.IsNullOrEmpty(entry.LinkTarget))
                throw new InvalidDataException("发现加密文件或链接。");
            var key = entry.Key?.Replace('\\', '/') ?? throw new InvalidDataException("缺少归档路径。");
            if (key.StartsWith("./", StringComparison.Ordinal)) key = key[2..];
            if (key.StartsWith("SeaGend/", StringComparison.OrdinalIgnoreCase)) key = key["SeaGend/".Length..];
            ZipToolPackages.RequireEntry(key, entry.Attrib ?? 0, false);
            if (!ZipToolPackages.IsPmlIndex(key) && !files.TryAdd(key, entry.Size))
                throw new InvalidDataException("压缩包内有重复文件。");
        }
        if (files.Count == 0) throw new InvalidDataException("压缩包内没有 SeaGend 文件。");
        return files;
    }
}
