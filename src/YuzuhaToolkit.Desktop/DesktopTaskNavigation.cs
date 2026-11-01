namespace YuzuhaToolkit.Desktop;

internal enum DesktopDashboardAction { Start, Diagnose, SelectTarget, Tasks }
internal static class DesktopTaskNavigation
{
    internal static DesktopDashboardAction Decide(DashboardHealthSnapshot snapshot, bool freshExplicitTarget) =>
        !snapshot.Sessions.Any(row => row.Discovered) && snapshot.Error is null ? DesktopDashboardAction.Start :
        snapshot.AvailableCount == 0 ? DesktopDashboardAction.Diagnose :
        freshExplicitTarget ? DesktopDashboardAction.Tasks : DesktopDashboardAction.SelectTarget;
    internal static string Label(DesktopDashboardAction action) => action switch
    {
        DesktopDashboardAction.Start => "启动工程",
        DesktopDashboardAction.Diagnose => "查看连接原因",
        DesktopDashboardAction.SelectTarget => "选择操作目标",
        _ => "选择任务"
    };
}
