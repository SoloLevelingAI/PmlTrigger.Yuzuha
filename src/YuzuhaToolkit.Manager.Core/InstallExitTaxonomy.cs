namespace YuzuhaToolkit.Manager;

/// <summary>安装器进程与管理器进程的退出码对照；相同数字不表示同一种失败，且都不自动重试。</summary>
/// <param name="Id">稳定分类标识。</param>
/// <param name="Process">产生该退出码的进程。</param>
/// <param name="ExitCode">该进程的数值退出码。</param>
/// <param name="ErrorCode">管理器 <c>OperationError.code</c>；安装器进程没有该字段时使用同一分类标识。</param>
/// <param name="Meaning">给调用方的含义。</param>
/// <param name="AutoRetry">是否允许自动重放。安装与未知结果一律为 false。</param>
public sealed record InstallExitRow(string Id, string Process, int ExitCode, string ErrorCode, string Meaning, bool AutoRetry);

/// <summary>安装交接与旅程操作共用的退出码表。数值保持既有行为，只消除魔法数字和跨进程混读。</summary>
public static class InstallExitTaxonomy
{
    /// <summary>Inno <c>GetCustomSetupExitCode</c>：apply-plan 失败。只属于安装器进程。</summary>
    public const int InstallerApplyFailed = 10;
    /// <summary>安装器 apply 失败的稳定分类名。</summary>
    public const string InstallerApplyFailedCode = "INSTALL_APPLY_FAILED";
    /// <summary>管理器处理器失败沿用 <see cref="PmlTwin.Phayton.OperationExitCodes.TransportFailed"/> 的数值 7；具体原因看 error.Code。</summary>
    public const int ManagerHandlerExit = PmlTwin.Phayton.OperationExitCodes.TransportFailed;
    /// <summary>管理器进程内的配置冲突。数值与安装器 10 相同，但是另一个进程。</summary>
    public const int ManagerConfigurationExit = 10;
    /// <summary>配置修订冲突的稳定错误码。</summary>
    public const string ConfigurationConflictCode = "CONFIGURATION_CONFLICT";

    /// <summary>供契约导出和测试引用的完整对照，不作为第二份手写操作目录。</summary>
    public static IReadOnlyList<InstallExitRow> Rows { get; } =
    [
        new(InstallerApplyFailedCode, "Inno Setup", InstallerApplyFailed, InstallerApplyFailedCode, "apply-plan 失败。安装器进程退出码，不是管理器操作结果。", false),
        new("INVALID_INPUT", "Manager", PmlTwin.Phayton.OperationExitCodes.InvalidInput, "INVALID_INPUT", "输入不符合已注册 schema。", false),
        new("UNKNOWN_OPERATION", "Manager", PmlTwin.Phayton.OperationExitCodes.UnknownOperation, "UNKNOWN_OPERATION", "操作 ID 未注册。", false),
        new("EXECUTION_DENIED", "Manager", PmlTwin.Phayton.OperationExitCodes.ExecutionDenied, "EXECUTION_NOT_AUTHORIZED", "缺少显式执行、写入或启动授权。未发送业务动作。", false),
        new("TARGET_FAILED", "Manager", PmlTwin.Phayton.OperationExitCodes.TargetFailed, "PID_REQUIRED", "缺少明确目标身份。", false),
        new("HOST_FAILED", "Manager", PmlTwin.Phayton.OperationExitCodes.HostFailed, "HOST_FAILED", "Host 能力或握手失败。", false),
        new("MANAGER_HANDLER_FAILURE", "Manager", ManagerHandlerExit, "error.Code", "处理器拒绝或传输失败。数值 7 与 TransportFailed 相同；稳定原因是 error.Code，不是安装器 10。", false),
        new("CANCELLED", "Manager", PmlTwin.Phayton.OperationExitCodes.Cancelled, "CANCELLED", "已取消。不自动重放。", false),
        new("INTERNAL_ERROR", "Manager", PmlTwin.Phayton.OperationExitCodes.InternalError, "INTERNAL_ERROR", "未分类的处理器异常。", false),
        new(ConfigurationConflictCode, "Manager", ManagerConfigurationExit, ConfigurationConflictCode, "配置修订冲突。管理器进程退出码 10，不得读成安装器 INSTALL_APPLY_FAILED。", false),
        new("UNKNOWN_NO_RETRY", "Manager", ManagerHandlerExit, "*_UNKNOWN", "结果未知。mayHaveExecuted 时按原回执核对，禁止自动重放。", false)
    ];
}
