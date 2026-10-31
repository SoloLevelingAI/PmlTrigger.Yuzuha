namespace LaunchDesk.Windows;

/// <summary>显式 --config、portable、默认目录的唯一解析与打开入口。</summary>
public sealed class WorkspaceLocationResolver : IWorkspaceLocationResolver
{
    /// <summary>按优先级选择；相对 portable 路径基于 EXE，不受工作目录影响。</summary>
    /// <inheritdoc/>
    public Result<WorkspaceLocation> Resolve(StartupOptions options, string executableDirectory)
    {
        try
        {
            var executableRoot = Path.GetFullPath(executableDirectory);
            var configured = options.DataDirectory;
            var portable = Path.Combine(executableRoot, "portable.json");
            if (configured is null && File.Exists(portable))
                configured = AtomicFiles.Read<PortableOptions>(portable).DataDirectory;
            if (options.Portable && configured is null) configured = "Data.Archon";
            var file = options.ConfigPath is not null
                ? Path.GetFullPath(options.ConfigPath, executableRoot)
                : Path.Combine(configured is not null
                    ? Path.GetFullPath(configured, executableRoot)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "LaunchDesk", "Workspaces", "Archon"), "workspace.json");
            var directory = Path.GetDirectoryName(file) ??
                            throw new ArgumentException("Invalid configuration directory.");
            var machine = CanonicalDigest.Text(Environment.MachineName + "/" +
                                               Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            return Result<WorkspaceLocation>.Ok(new WorkspaceLocation(directory, file,
                Path.Combine(directory, ".control"), new Guid(Convert.FromHexString(machine[..32]))));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<WorkspaceLocation>(ex, "InvalidLocation");
        }
    }

    /// <summary>创建缺失目录并以目录对象打开，不伪造配置。</summary>
    /// <inheritdoc/>
    public ValueTask<Result<Unit>> OpenDirectoryAsync(WorkspaceLocation location, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(location.Directory);
            Process.Start(new ProcessStartInfo
                { FileName = "explorer.exe", ArgumentList = { location.Directory }, UseShellExecute = false });
            return ValueTask.FromResult(Result<Unit>.Ok(new Unit()));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return ValueTask.FromResult(AtomicFiles.Failure<Unit>(ex, "OpenDirectoryFailed"));
        }
    }

    private sealed record PortableOptions(string DataDirectory);
}