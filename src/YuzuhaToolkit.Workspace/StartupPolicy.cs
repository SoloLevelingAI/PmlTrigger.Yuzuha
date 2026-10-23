using System.Diagnostics;

namespace YuzuhaToolkit.Workspace;

/// <summary>启动特殊策略的环境约定；PML 初始化后的原生 Addin 执行实际动作。</summary>
public static class StartupPolicy
{
    /// <summary>none 或 rehash-all；只影响新启动进程。</summary>
    public const string Variable = "YUZUHA_STARTUP_ACTIONS";
    /// <summary>桌面显式选择的优先值，避免厂商 INIT/BAT 的默认值覆盖本次选择。</summary>
    public const string OverrideVariable = "YUZUHA_STARTUP_OVERRIDE";
    /// <summary>规范化已支持策略；未知值在创建进程前拒绝。</summary>
    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "none" => "none", "rehash-all" => "rehash-all",
        _ => throw new ArgumentException("YUZUHA_STARTUP_ACTIONS 目前支持 none 或 rehash-all。")
    };
    /// <summary>为特殊启动加入关联 ID 和独立日志路径，不改变系统环境变量。</summary>
    public static void Prepare(ProcessStartInfo start)
    {
        start.Environment.TryGetValue(Variable, out var configured);
        if (start.Environment.TryGetValue(OverrideVariable, out var explicitValue)) configured = explicitValue;
        var policy = Normalize(configured);
        if (policy == "none")
        {
            start.Environment.Remove("YUZUHA_STARTUP_ID"); start.Environment.Remove("YUZUHA_STARTUP_LOG"); return;
        }
        start.Environment[Variable] = policy;
        var id = Guid.NewGuid().ToString("N");
        start.Environment["YUZUHA_STARTUP_ID"] = id;
        start.Environment["YUZUHA_STARTUP_LOG"] = Path.Combine(Path.GetTempPath(), "YuzuhaStartup-" + id + ".log");
    }
}
