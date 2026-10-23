using System.Security.Cryptography;

namespace YuzuhaToolkit.Workspace;

/// <summary>桌面、CLI、知识库与 HTML 共用的用户目录；读取路径不创建或迁移资料。</summary>
public static class UserDataPaths
{
    /// <summary>标记由用户明确选择的数据根；安装器拒绝在该目录及其子目录交付程序文件。</summary>
    public const string MarkerName = ".yuzuha-user-data.json";
    /// <summary>Windows 文档目录中的推荐位置；用户可在设置中修改。</summary>
    public static string RecommendedRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "YuzuhaWorkspace");
    /// <summary>环境变量优先于保存的设置；否则使用推荐目录。</summary>
    public static string Root => Path.GetFullPath(Environment.GetEnvironmentVariable("YUZUHA_USER_DIR") is { Length: > 0 } env ? env
        : WorkspaceSettings.Load().UserDataRoot is { Length: > 0 } saved ? saved : RecommendedRoot);
    /// <summary>解析安装根，用于兼容旧目录与隔离程序文件；开发环境可以明确覆盖。</summary>
    public static string InstallRoot
    {
        get
        {
            if (Environment.GetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT") is { Length: > 0 } root) return Path.GetFullPath(root);
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, ".setup-payload.json"))) return dir.FullName;
            return AppContext.BaseDirectory;
        }
    }
    /// <summary>明确知识库覆盖优先；未配置统一目录时保留旧版现有资料位置。</summary>
    /// <param name="legacyRoot">旧安装根目录；省略时自动定位。</param>
    public static string KnowledgeDirectory(string? legacyRoot = null)
    {
        if (Environment.GetEnvironmentVariable("YUZUHA_KNOWLEDGE_DIR") is { Length: > 0 } env) return Path.GetFullPath(env);
        var settings = WorkspaceSettings.Load();
        if (!string.IsNullOrWhiteSpace(settings.KnowledgeDirectory)) return Path.GetFullPath(settings.KnowledgeDirectory);
        if (string.IsNullOrEmpty(settings.UserDataRoot) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("YUZUHA_USER_DIR")))
        {
            var legacy = Path.Combine(legacyRoot ?? InstallRoot, "knowledge");
            if (Directory.Exists(legacy)) return legacy;
        }
        return Path.Combine(Root, "Knowledge");
    }
    /// <summary>明确 HTML 数据覆盖优先；首次使用统一目录前保留旧应用和绑定。</summary>
    public static string AppsDirectory
    {
        get
        {
            if (Environment.GetEnvironmentVariable("YUZUHA_WEBAPPS_DIR") is { Length: > 0 } env) return Path.GetFullPath(env);
            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuzuhaToolkit", "WebApps");
            if (string.IsNullOrEmpty(WorkspaceSettings.Load().UserDataRoot) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("YUZUHA_USER_DIR")) && Directory.Exists(legacy)) return legacy;
            return Path.Combine(Root, "Apps");
        }
    }
    /// <summary>比较规范化路径的包含关系，区分同前缀目录名。</summary>
    /// <param name="root">要检查的根目录。</param>
    /// <param name="path">待检查文件或目录。</param>
    public static bool Contains(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>用户数据不能与程序安装目录或内部状态目录相互包含，也不能经过目录联接。</summary>
    /// <param name="root">用户选择的完整目录。</param>
    /// <param name="installRoot">可选的安装根覆盖，便于隔离验证。</param>
    public static void ValidateRoot(string root, string? installRoot = null)
    {
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("请选择用户目录的完整路径。");
        root = Path.GetFullPath(root); PathRules.NoLinks(root);
        foreach (var protectedRoot in new[] { installRoot ?? InstallRoot, WorkspaceSettings.StateDirectory })
            if (Contains(root, protectedRoot) || Contains(protectedRoot, root)) throw new IOException("用户目录必须独立于程序安装目录和内部状态目录。");
    }
}

/// <summary>复制预览中的源文件、目标和冻结指纹。</summary>
/// <param name="Source">源文件绝对路径。</param>
/// <param name="Target">目标文件绝对路径。</param>
/// <param name="Sha256">预览时源文件指纹。</param>
public sealed record UserDataCopy(string Source, string Target, string Sha256);

/// <summary>统一目录切换预览；源文件保留，目标冲突拒绝。</summary>
/// <param name="Root">新用户目录。</param>
/// <param name="Files">需要核验或复制的文件。</param>
/// <param name="SettingsHash">预览时工作台设置指纹。</param>
public sealed record UserDataMigration(string Root, UserDataCopy[] Files, string? SettingsHash);

/// <summary>复制并验证旧数据后才保存新目录；从不删除源文件。</summary>
public static class UserDataMigrationService
{
    private static string SettingsFile => Path.Combine(WorkspaceSettings.StateDirectory, "workspace.json");
    private static string? Hash(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>预览知识、应用及已有工作目录的迁移，拒绝重叠路径、联接和不同内容的同名文件。</summary>
    /// <param name="newRoot">用户选择的新根目录。</param>
    public static UserDataMigration Preview(string newRoot)
    {
        if (new[] { "YUZUHA_USER_DIR", "YUZUHA_KNOWLEDGE_DIR", "YUZUHA_WEBAPPS_DIR" }.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))))
            throw new IOException("当前环境变量覆盖了数据目录。请先调整覆盖值并重启相关程序，再从设置切换目录。");
        newRoot = Path.GetFullPath(newRoot); UserDataPaths.ValidateRoot(newRoot);
        var sources = new Dictionary<string, string> { ["Knowledge"] = UserDataPaths.KnowledgeDirectory(), ["Apps"] = UserDataPaths.AppsDirectory };
        foreach (var name in new[] { "Projects", "UserPmlLib", "Exports" }) sources[name] = Path.Combine(UserDataPaths.Root, name);
        var files = new Dictionary<string, UserDataCopy>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sources)
        {
            var source = Path.GetFullPath(pair.Value); var target = Path.Combine(newRoot, pair.Key);
            if (!Directory.Exists(source) || source.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;
            if (UserDataPaths.Contains(source, newRoot) || UserDataPaths.Contains(newRoot, source)) throw new IOException("新旧数据目录不能相互包含。");
            var pending = new Stack<string>(); pending.Push(source);
            while (pending.TryPop(out var directory))
            {
                PathRules.NoLinks(directory);
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    PathRules.NoLinks(path);
                    if (Directory.Exists(path)) { pending.Push(path); continue; }
                    if (path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)) continue;
                    if (path.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) || path.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("知识库仍有 SQLite 活动文件，请关闭使用它的客户端后重新预览。");
                    if (files.Count >= 100000) throw new IOException("迁移文件超过 100000，请按资料目录分别整理后重试。");
                    var destination = PathRules.Inside(target, Path.GetRelativePath(source, path));
                    var hash = Hash(path)!;
                    if (File.Exists(destination) && Hash(destination) != hash) throw new IOException("目标存在不同内容，保留原文件：" + destination);
                    files.Add(destination, new(path, destination, hash));
                }
            }
        }
        return new(newRoot, files.Values.ToArray(), Hash(SettingsFile));
    }

    /// <summary>核验冻结预览并复制；失败只回退本次新增且未被改动的目标文件，旧数据始终保留。</summary>
    /// <param name="plan">已展示给用户的冻结迁移预览。</param>
    public static void Apply(UserDataMigration plan)
    {
        UserDataPaths.ValidateRoot(plan.Root);
        Directory.CreateDirectory(WorkspaceSettings.StateDirectory);
        using var guard = new FileStream(Path.Combine(WorkspaceSettings.StateDirectory, "user-directory-change.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (Hash(SettingsFile) != plan.SettingsHash) throw new IOException("目录设置已变化，请重新预览。");
        var locks = new List<FileStream>(); var created = new List<UserDataCopy>();
        try
        {
            ValidateSnapshot(plan);
            foreach (var file in plan.Files.Where(f => f.Source.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase)))
                locks.Add(new FileStream(file.Source + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            foreach (var file in plan.Files)
            {
                if (!UserDataPaths.Contains(plan.Root, file.Target)) throw new IOException("目标越出预览根目录。");
                PathRules.NoLinks(file.Source); PathRules.NoLinks(file.Target);
                if (Hash(file.Source) != file.Sha256 || File.Exists(file.Target) && Hash(file.Target) != file.Sha256) throw new IOException("迁移文件已变化，请重新预览。");
                if (File.Exists(file.Target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(file.Target)!);
                var temporary = file.Target + "." + Guid.NewGuid().ToString("N") + ".migration-tmp";
                try
                {
                    using (var input = new FileStream(file.Source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                    if (Hash(temporary) != file.Sha256) throw new IOException("复制校验失败。");
                    File.Move(temporary, file.Target, false); created.Add(file);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            ValidateSnapshot(plan);
            foreach (var name in new[] { "Knowledge", "Apps", "Projects", "UserPmlLib", "Exports" }) Directory.CreateDirectory(Path.Combine(plan.Root, name));
            var marker = Path.Combine(plan.Root, UserDataPaths.MarkerName);
            if (!File.Exists(marker)) File.WriteAllText(marker, "{\"owner\":\"Yuzuha.UserData\",\"schema\":1}");
            var settings = WorkspaceSettings.Load(); settings.UserDataRoot = plan.Root; settings.KnowledgeDirectory = ""; settings.Save();
        }
        catch
        {
            foreach (var file in created.AsEnumerable().Reverse())
                if (UserDataPaths.Contains(plan.Root, file.Target) && Hash(file.Target) == file.Sha256) { PathRules.NoLinks(file.Target); File.Delete(file.Target); }
            throw;
        }
        finally { foreach (var held in locks) held.Dispose(); }
    }
    private static void ValidateSnapshot(UserDataMigration plan)
    {
        var current = Preview(plan.Root);
        if (current.SettingsHash != plan.SettingsHash || !current.Files.OrderBy(f => f.Target).SequenceEqual(plan.Files.OrderBy(f => f.Target)))
            throw new IOException("源目录、文件或设置已变化，请重新预览。");
    }
}
