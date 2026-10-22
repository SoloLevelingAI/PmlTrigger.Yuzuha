namespace YuzuhaToolkit.Mcp;

/// <summary>共享桥明确证明正文尚未派发的失败，避免 CLI 将授权拒绝误报为可能执行。</summary>
internal sealed class PmlDispatchPreflightException(Exception cause) : Exception(cause.Message, cause)
{
    /// <summary>授权、输入或目标失败的稳定退出码。</summary>
    public int ExitCode => InnerException is UnauthorizedAccessException ? 4 : InnerException is InvalidDataException or ArgumentException or NotSupportedException ? 2 : 5;
    /// <summary>区分权限拒绝、源码/能力预检和目标状态失败。</summary>
    public string Code => ExitCode == 4 ? "AUTHORIZATION_DENIED" : ExitCode == 2 ? "PML_PREFLIGHT_FAILED" : "TARGET_PREFLIGHT_FAILED";
}
