using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Workspace;

/// <summary>启动和插件复制所用的本机设置；只保存凭据引用，不保存密码正文。</summary>
public sealed class WorkspaceSettings
{
    /// <summary>实际已注册到 PMLLIB 搜索路径的目录。</summary>
    public string RegisteredPmlLib { get; set; } = "";
    /// <summary>插件源目录或 ZIP；开发副本不应位于注册目录内。</summary>
    public string PluginSource { get; set; } = "";
    /// <summary>注册目录中的插件子目录名。</summary>
    public string PluginName { get; set; } = "MyPlugin";
    /// <summary>扫描后提醒清理的文件数阈值；是工作区预算，不是 AVEVA 产品上限。</summary>
    public int WarnFiles { get; set; } = 3000;
    /// <summary>扫描后提醒清理的总字节预算。</summary>
    public long WarnBytes { get; set; } = 200L * 1024 * 1024;
    /// <summary>用户保存的启动配置。</summary>
    public List<LaunchProfile> Profiles { get; set; } = [];
    /// <summary>GUI 明确选择的本机知识库目录；空值使用现有安装布局。</summary>
    public string KnowledgeDirectory { get; set; } = "";

    /// <summary>本机状态根目录；测试和便携环境可通过 YUZUHA_STATE_DIR 隔离。</summary>
    public static string StateDirectory => Path.GetFullPath(Environment.GetEnvironmentVariable("YUZUHA_STATE_DIR")
        ?? (Environment.GetEnvironmentVariable("YUZUHA_TOOLKIT_ROOT") is { Length: > 0 } root ? Path.Combine(root, ".yuzuha-state")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuzuhaToolkit")));

    /// <summary>读取已有设置；未配置时返回空工作区，不猜测注册目录。</summary>
    public static WorkspaceSettings Load()
    {
        var file = Path.Combine(StateDirectory, "workspace.json");
        return File.Exists(file) ? JsonSerializer.Deserialize(File.ReadAllText(file), WorkspaceJsonContext.Default.WorkspaceSettings)
            ?? throw new InvalidDataException("工作区设置为空。") : new();
    }

    /// <summary>原子保存非秘密设置；不修改任何 AVEVA 环境文件或注册表。</summary>
    public void Save()
    {
        if (WarnFiles < 1 || WarnBytes < 1) throw new ArgumentException("目录提醒阈值必须为正数。");
        Directory.CreateDirectory(StateDirectory);
        var file = Path.Combine(StateDirectory, "workspace.json");
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this, WorkspaceJsonContext.Default.WorkspaceSettings)); File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

/// <summary>AVEVA 启动配置；密码保存在 Windows 凭据库，配置只保留本机引用。</summary>
public sealed class LaunchProfile
{
    /// <summary>配置显示名称。</summary>
    public string Name { get; set; } = "新启动配置";
    /// <summary>Windows 凭据管理器中的本应用引用；不是密码，不与其他应用共用。</summary>
    public string CredentialKey { get; set; } = "";
    /// <summary>用户是否选择保存本机密码，供下次启动自动使用。</summary>
    public bool RememberPassword { get; set; } = true;
    /// <summary>Monitor 或 MarineBatch。</summary>
    public string Kind { get; set; } = "Monitor";
    /// <summary>mon.exe、marine.bat 或 marine.cmd 的完整路径。</summary>
    public string Executable { get; set; } = "";
    /// <summary>Monitor 所用初始化文件，按实际产品选择。</summary>
    public string Initialization { get; set; } = "";
    /// <summary>产品代码；不从模块名推断。</summary>
    public string Product { get; set; } = "E3D";
    /// <summary>程序的工作目录。</summary>
    public string WorkingDirectory { get; set; } = "";
    /// <summary>项目代码。</summary>
    public string Project { get; set; } = "";
    /// <summary>登录用户名。</summary>
    public string User { get; set; } = "";
    /// <summary>MDB 名称。</summary>
    public string Mdb { get; set; } = "/ALL";
    /// <summary>启动模块，例如 Design 或 Paragon。</summary>
    public string Module { get; set; } = "Design";
    /// <summary>仅传给本次子进程的许可连接；空值沿用当前进程设置。</summary>
    public string License { get; set; } = "";
    /// <summary>仅传给本次子进程的额外环境；不能包含密码。</summary>
    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>工作区、日志和目录报告的 Native AOT JSON 元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WorkspaceSettings))]
[JsonSerializable(typeof(DirectoryHealth))]
[JsonSerializable(typeof(RuntimeStatus[]))]
[JsonSerializable(typeof(ActivityEntry[]))]
[JsonSerializable(typeof(RuntimeStatus))]
[JsonSerializable(typeof(ActivityEntry))]
[JsonSerializable(typeof(DeploymentReceipt))]
[JsonSerializable(typeof(RegistrationCandidate[]))]
public partial class WorkspaceJsonContext : JsonSerializerContext;
