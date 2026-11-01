using System.Windows;

namespace YuzuhaToolkit.Desktop;

/// <summary>主窗口根据会话状态导航至启动、连接诊断、明确目标或任务中心。</summary>
public partial class MainWindow
{
    private void UpdateDashboardPrimaryAction()
    {
        var action = DesktopTaskNavigation.Decide(DashboardSnapshot, SessionView.HasFreshTarget);
        DashboardPrimaryAction.Content = DesktopTaskNavigation.Label(action) + " →";
        if (Tabs.SelectedIndex == 4) { HeaderActionText.Text = DesktopTaskNavigation.Label(action); HeaderActionButton.IsEnabled = !SessionView.IsRunning; }
    }
    private async void DashboardNext(object sender, RoutedEventArgs e)
    {
        if (SessionView.IsRunning) { Say("当前操作仍在等待结果，不能切换或重发。"); return; }
        var action = DesktopTaskNavigation.Decide(DashboardSnapshot, SessionView.HasFreshTarget);
        if (action == DesktopDashboardAction.Start) { StartAnotherEngineeringSession(sender, e); return; }
        Tabs.SelectedIndex = 3;
        if (action == DesktopDashboardAction.Tasks) SessionView.OpenTaskHub();
        else await SessionView.OpenSelectionAsync();
    }
    private void StartAnotherEngineeringSession(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 0; SetPage("启动工程"); }
}
