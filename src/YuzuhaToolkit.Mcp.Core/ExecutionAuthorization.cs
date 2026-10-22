using System.Text.Json;
using System.Text.Json.Serialization;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>本机授权配置；开发许可绑定一个进程的完整启动身份并设有到期时间。</summary>
public sealed class ExecutionAuthorization
{
    /// <summary>Conservative、Standard 或 Development；默认按函数授权。</summary>
    public string Mode { get; set; } = "Standard";
    /// <summary>开发目标 PID。</summary>
    public int ProcessId { get; set; }
    /// <summary>开发目标进程启动时间，防止 PID 重用。</summary>
    public long ProcessStartTimeUtcTicks { get; set; }
    /// <summary>开发目标模块。</summary>
    public string Model { get; set; } = "";
    /// <summary>允许 CLI 写入源码和测试的绝对根目录。</summary>
    public string SourceRoot { get; set; } = "";
    /// <summary>显式允许规避的项目规则：raw-command 或 function-permission。</summary>
    public string[] BypassRules { get; set; } = [];
    /// <summary>授予开发许可的原因。</summary>
    public string Reason { get; set; } = "";
    /// <summary>开发许可到期 UTC，最长 24 小时。</summary>
    public DateTimeOffset ExpiresUtc { get; set; }
}

/// <summary>授权状态的 Native AOT JSON 元数据。</summary>
[JsonSerializable(typeof(ExecutionAuthorization))]
internal partial class AuthorizationJsonContext : JsonSerializerContext { }

/// <summary>持久化和核验三档授权；项目规避不能关闭身份、源码、日志或 MARKDB 核验。</summary>
public sealed class ExecutionAuthorizationStore
{
    private readonly string file;
    /// <summary>使用统一工作区授权文件，或隔离测试指定的文件。</summary>
    public ExecutionAuthorizationStore(string? path = null) => file = Path.GetFullPath(path ?? Path.Combine(WorkspaceSettings.StateDirectory, "authorization.json"));
    /// <summary>读取当前策略；损坏或未知模式明确失败。</summary>
    public ExecutionAuthorization Read()
    {
        if (!File.Exists(file)) return new();
        var state = JsonSerializer.Deserialize(File.ReadAllText(file), AuthorizationJsonContext.Default.ExecutionAuthorization) ?? throw new InvalidDataException("Empty authorization state.");
        Validate(state); return state;
    }
    /// <summary>原子保存完整策略，撤销时使用 Conservative 或 Standard。</summary>
    public ExecutionAuthorization Save(ExecutionAuthorization state)
    {
        Validate(state); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var held = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(state, AuthorizationJsonContext.Default.ExecutionAuthorization)); File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return state;
    }
    /// <summary>对实际已核验的 Host 身份检查执行许可，返回本次策略快照。</summary>
    public ExecutionAuthorization Require(HostIdentityResponse identity, bool registered, string? sourceFile = null, bool typedReadOnly = false)
    {
        var state = Read();
        if (state.Mode == "Conservative") throw new UnauthorizedAccessException("PML_CONSERVATIVE_MODE: registration only; no PML dispatch.");
        if (state.Mode == "Development")
        {
            if (state.ExpiresUtc <= DateTimeOffset.UtcNow || state.ProcessId != identity.ProcessId || state.ProcessStartTimeUtcTicks != identity.ProcessStartTimeUtcTicks || !state.Model.Equals(identity.Model, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("PML_DEVELOPMENT_TARGET_MISMATCH_OR_EXPIRED: reconnect the explicit development session.");
            if (sourceFile != null) RequireSourcePath(state.SourceRoot, sourceFile);
            if (!registered && !typedReadOnly && !state.BypassRules.Contains("raw-command", StringComparer.Ordinal))
                throw new UnauthorizedAccessException("PML_RAW_COMMAND_DENIED: explicit raw-command project rule bypass is required.");
        }
        else if (!registered && !typedReadOnly)
            throw new UnauthorizedAccessException("PML_STANDARD_MODE: invoke an authorized registered function, or explicitly configure a development session.");
        return state;
    }
    /// <summary>检查开发文件路径位于源码根目录内，并拒绝沿路径的链接或重解析点。</summary>
    public static string RequireSourcePath(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new UnauthorizedAccessException("PML_SOURCE_OUTSIDE_DEVELOPMENT_ROOT");
        for (var current = full; current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("PML_SOURCE_LINK_NOT_ALLOWED");
        }
        return full;
    }
    private static void Validate(ExecutionAuthorization state)
    {
        if (state.Mode is not ("Conservative" or "Standard" or "Development")) throw new InvalidDataException("Unknown authorization mode.");
        if (state.BypassRules == null || state.BypassRules.Any(rule => rule is not ("raw-command" or "function-permission"))) throw new InvalidDataException("Unknown project rule bypass.");
        if (state.Mode != "Development") { if (state.BypassRules.Length > 0) throw new InvalidDataException("Rule bypass requires Development mode."); return; }
        if (state.ProcessId <= 0 || state.ProcessStartTimeUtcTicks <= 0 || string.IsNullOrWhiteSpace(state.Model) || string.IsNullOrWhiteSpace(state.Reason) || state.Reason.Length > 1024 || !Path.IsPathFullyQualified(state.SourceRoot) || !Directory.Exists(state.SourceRoot) || state.ExpiresUtc > DateTimeOffset.UtcNow.AddHours(24).AddMinutes(1))
            throw new InvalidDataException("Development mode requires an explicit session, existing source root, reason and expiry within 24 hours.");
        RequireSourcePath(state.SourceRoot, Path.Combine(state.SourceRoot, ".scope-check"));
    }
}
