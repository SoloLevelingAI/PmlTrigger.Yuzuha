namespace YuzuhaToolkit.Desktop;

/// <summary>Dashboard 主入口可导航的启动、诊断、目标选择及任务中心。</summary>
internal enum DesktopDashboardAction { Start, Diagnose, SelectTarget, Tasks }
/// <summary>按监测证据和人工目标的新鲜度决定下一入口，不产生业务副作用。</summary>
internal static class DesktopTaskNavigation
{
    /// <summary>仅在存在可用 Host 且人工目标仍有效时进入任务，否则先启动、诊断或选择目标。</summary>
    internal static DesktopDashboardAction Decide(DashboardHealthSnapshot snapshot, bool freshExplicitTarget) =>
        !snapshot.Sessions.Any(row => row.Discovered) && snapshot.Error is null ? DesktopDashboardAction.Start :
        snapshot.AvailableCount == 0 ? DesktopDashboardAction.Diagnose :
        freshExplicitTarget ? DesktopDashboardAction.Tasks : DesktopDashboardAction.SelectTarget;
    /// <summary>返回与主入口导航动作一致的中文按钮名称。</summary>
    internal static string Label(DesktopDashboardAction action) => action switch
    {
        DesktopDashboardAction.Start => "启动工程",
        DesktopDashboardAction.Diagnose => "查看连接原因",
        DesktopDashboardAction.SelectTarget => "选择操作目标",
        _ => "选择任务"
    };
}
