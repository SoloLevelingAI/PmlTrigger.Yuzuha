namespace LaunchDesk.Windows;

/// <summary>Windows/来源适配层的入口发现。</summary>
/// <param name="store">配置存储端口。</param>
public sealed class WindowsEntryDiscovery(IWorkspaceStore store) : IEntryDiscovery
{
    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<string>>> FindAsync(Guid applicationId, CancellationToken ct)
    {
        var read = await store.ReadAsync(ct);
        if (!read.IsSuccess) return Result<IReadOnlyList<string>>.Fail(read.Error!);
        var application = read.Value.Applications.FirstOrDefault(a => a.Id == applicationId);
        if (application is null) return Result<IReadOnlyList<string>>.Fail("ApplicationNotFound", "没有找到应用定义。");
        var fileName = Path.GetFileName(application.ExecutablePath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = application.Kind switch
            {
                EntryKind.Monitor => "mon.exe", EntryKind.MarineBatch => "marine.bat", _ => ""
            };
        if (fileName.Length == 0 || fileName.IndexOfAny(['*', '?']) >= 0)
            return Result<IReadOnlyList<string>>.Fail("DiscoveryRuleRequired", "通用应用需要指定入口文件名或人工路径。");
        var roots = application.DiscoveryRoots.IsDefaultOrEmpty
            ? new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"C:\AVEVA"
            }
            : application.DiscoveryRoots.ToArray();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.EnumerateFiles(root, fileName,
                             new EnumerationOptions
                             {
                                 RecurseSubdirectories = true, MaxRecursionDepth = 5, IgnoreInaccessible = true,
                                 AttributesToSkip = FileAttributes.ReparsePoint
                             }).Take(1000))
                {
                    ct.ThrowIfCancellationRequested();
                    found.Add(Path.GetFullPath(file));
                }
            }
            catch (Exception ex) when (AtomicFiles.Expected(ex))
            {
                /* 部分扫描不移除已有人工绑定。 */
            }
        }

        return Result<IReadOnlyList<string>>.Ok(found.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}