using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace YuzuhaToolkit.Desktop;

public partial class SessionWorkbenchView
{
    private int? resultTargetPid;
    internal bool HasFreshTarget => model.Status.TargetVerified && model.Status.Host is { } hostIdentity && DashboardAllows(hostIdentity);
    internal async Task OpenSelectionAsync()
    {
        if (pending is not null || model.IsBusy && discoveryTask is null) { FeedbackText.Text = "当前操作尚未结束，请等待回执。"; return; }
        FlowTabs.SelectedIndex = 0; TaskResultPanel.Visibility = Visibility.Collapsed;
        await DiscoverTaskAsync();
        if (!disposed) { SessionsGrid.BringIntoView(); SessionsGrid.Focus(); }
    }
    internal void OpenTaskHub()
    {
        if (pending is not null || model.IsBusy) { FeedbackText.Text = "当前操作尚未结束，请等待结果。"; return; }
        FlowTabs.SelectedIndex = 3; TaskResultPanel.Visibility = Visibility.Collapsed; RenderTaskNavigation(); TaskHubTitle.Focus();
    }
    private void BackToTasks(object sender, RoutedEventArgs e) => OpenTaskHub();
    private void ChooseTask(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string task } || pending is not null || model.IsBusy) return;
        if (!HasFreshTarget) { FeedbackText.Text = "请先明确选择并重新核验目标；任务入口不会自动选择或执行。"; return; }
        TaskResultPanel.Visibility = Visibility.Collapsed;
        switch (task)
        {
            case "object": FlowTabs.SelectedIndex = 5; TaskObjectKindBox.Focus(); break;
            case "forms": FlowTabs.SelectedIndex = 2; ShownOnlyBox.Focus(); break;
            case "array": FlowTabs.SelectedIndex = 4; TaskArrayNameBox.Focus(); break;
            case "function": FlowTabs.SelectedIndex = 1; FunctionsGrid.Focus(); break;
        }
        RenderTaskNavigation();
    }
    private void RenderTaskNavigation()
    {
        if (TaskPathText is null) return;
        var ready = HasFreshTarget;
        TaskPathText.Text = ready ? $"操作目标 PID {model.Status.Host!.ProcessId} · {model.Status.Host.Model}  →  选择任务  →  本次结果" : "选择目标并核验  →  选择任务  →  本次结果";
        TaskHubState.Text = ready ? "目标已明确且身份新鲜核验。选择任务只导航，不会读取、授权或执行。" : "尚无明确且可用的操作目标。返回连接页扫描、选择并核验；监测绿灯不会代替你的选择。";
        foreach (var button in new[] { TaskObjectButton, TaskFormsButton, TaskArrayButton, TaskFunctionButton }) button.IsEnabled = ready && pending is null && !model.IsBusy;
        TaskBackButton.IsEnabled = pending is null && !model.IsBusy;
        FunctionArrayColumns.Visibility = Visibility.Collapsed;
    }
    private void ShowTaskResult()
    {
        TaskResultPanel.Visibility = Visibility.Visible;
        var receipt = model.LastResponse;
        var count = ReferenceEquals(receipt, model.ObjectResult?.Receipt) && receipt is not null ? model.ObjectResult?.Items.Length : receipt?.FormQueryResult?.Items?.Length ?? receipt?.ArrayWindow?.Rows?.Length;
        TaskResultSummary.Text = $"{model.Outcome} · 请求目标 PID {resultTargetPid?.ToString() ?? "未锁定"} · 当前身份{(HasFreshTarget ? "已核验" : "需重新核验")}" + (count is null ? "" : $" · 本次返回 {count} 项") + "\n" + model.Message;
        TaskResultReceipt.Text = receipt is null ? "本次未取得可靠回执。未知结果不得重发；请按已有编号和原回执核对。" : JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true });
        TaskResultSummary.BringIntoView(); TaskResultSummary.Focus();
    }
    private void VerifyTaskNavigationSmoke()
    {
        if (!HasFreshTarget) throw new InvalidOperationException("任务导航夹具必须先明确核验目标。");
        if (SessionLegacyDetailsExpander.IsExpanded) throw new InvalidOperationException("目标选择页不应默认铺开其他查询与结果。");
        var receipt = model.LastResponse; var inputs = CaptureObjectState(); var themeInputs = CaptureThemeState();
        foreach (var task in new[] { "object", "forms", "array", "function" })
        {
            OpenTaskHub(); ChooseTask(new Button { Tag = task }, new());
            var expected = task switch { "object" => 5, "forms" => 2, "array" => 4, _ => 1 };
            if (FlowTabs.SelectedIndex != expected || !ReferenceEquals(receipt, model.LastResponse) || inputs != CaptureObjectState() || themeInputs != CaptureThemeState()) throw new InvalidOperationException("任务导航执行了操作或改变了输入。");
        }
        OpenTaskHub();
        if (FlowTabs.SelectedIndex != 3) throw new InvalidOperationException("无法返回任务入口。");
    }
}
