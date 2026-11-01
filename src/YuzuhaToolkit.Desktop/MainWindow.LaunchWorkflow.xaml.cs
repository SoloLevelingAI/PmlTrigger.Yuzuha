using System.Windows;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Desktop;

/// <summary>桌面复用持久启动协调；不把入口创建、观察或等待结束当成登录证据。</summary>
public partial class MainWindow
{
    private bool _launchWorkflowBusy;
    private string? _unresolvedLaunchOperation;
    private LaunchWorkflowService LaunchWorkflow => new(_launchProfiles);

    private async void Launch(object sender, RoutedEventArgs e)
    {
        if (_launchWorkflowBusy || _copying) { Say("启动或复制操作正在进行，请等待结果。"); return; }
        if (_unresolvedLaunchOperation is not null)
        {
            LaunchOperationIdBox.Text = _unresolvedLaunchOperation;
            LifecycleMessage("启动结果尚不确定，请按原编号观察，禁止重放。", true); return;
        }
        _launchWorkflowBusy = true; SetLaunchWorkflowButtons();
        var phase = "prepare";
        string? operationId = null;
        try
        {
            var enteredPassword = PasswordInput.Password;
            var profile = PersistProfile();
            var revision = _profileRevision;
            if (!string.IsNullOrWhiteSpace(_settings.RegisteredPmlLib))
            {
                var health = await ScanAsync();
                if (_closed) return;
                if (health.NeedsAttention && MessageBox.Show(FormatHealth(health) + "\n\n建议先移出旧插件、备份和开发副本。仍然启动吗？", "PMLLIB 清理提醒", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            operationId = Guid.NewGuid().ToString("N");
            LaunchOperationIdBox.Text = operationId;
            var workflow = LaunchWorkflow;
            phase = "plan";
            var plan = await Task.Run(() => workflow.Plan(profile.Id, revision, operationId));
            var password = enteredPassword.Length > 0 ? enteredPassword : profile.CredentialKey.Length > 0 ? WindowsCredentialStore.Read(profile.CredentialKey) : "";
            phase = "execute";
            // 从这里开始异常不能推定未受理；保留原编号供恢复观察。
            _unresolvedLaunchOperation = operationId;
            var receipt = await Task.Run(() => workflow.ExecuteAsync(plan, password, Guid.NewGuid().ToString("N"), true));
            if (receipt.State is not ("unknown" or "accepted")) _unresolvedLaunchOperation = null;
            if (receipt.State == "success")
            {
                // 保留原有最近启动字段；它不是第二份操作事实，失败也不能改变启动回执。
                try
                {
                    profile.LastStartedUtc = receipt.UpdatedAt;
                    var commit = _launchProfiles.Save(profile, revision, true, "记录启动入口创建时间");
                    _settings = commit.Snapshot.Value; _profileRevision = commit.Snapshot.Revision;
                    if (!_closed) RefreshLaunchProfileList();
                }
                catch (Exception) { /* 启动事实以协调器持久回执为准；不输出可能含秘密的异常。 */ }
            }
            if (!_closed) ShowLaunchReceipt(receipt);
        }
        catch (Exception error) when (error is System.IO.InvalidDataException or ArgumentException or UnauthorizedAccessException or YuzuhaToolkit.Workspace.ConfigurationConflictException)
        {
            // Manager 在触达启动边界前拒绝这些输入/授权错误；边界后的失败返回 unknown 回执。
            _unresolvedLaunchOperation = null;
            if (!_closed)
            {
                var code = error is YuzuhaToolkit.Workspace.ConfigurationConflictException
                    ? "CONFIGURATION_CONFLICT" : "DESKTOP_LAUNCH_REJECTED";
                LaunchWorkflowResultText.Text = $"rejected · {phase} · {code}\n{error.GetType().Name}\n操作编号：{operationId ?? "尚未生成"}\n启动未受理，请核对配置修订、文件和权限；不会自动重试。";
                LifecycleMessage(LaunchWorkflowResultText.Text, true);
            }
        }
        catch (Exception error)
        {
            if (!_closed)
            {
                // 不显示底层异常正文：外部启动和凭据错误可能携带秘密。
                var code = phase == "execute" ? "DESKTOP_LAUNCH_RECEIPT_UNAVAILABLE" : "DESKTOP_LAUNCH_REJECTED";
                LaunchWorkflowResultText.Text = $"阶段：{phase} · {code}\n{error.GetType().Name}\n操作编号：{operationId ?? "尚未生成"}\n" +
                    (phase == "execute" ? "未取得可靠回执，请按原编号观察；不会自动再次启动。" : "未调用启动执行，请核对配置与文件后重试。");
                LifecycleMessage(LaunchWorkflowResultText.Text, true);
            }
        }
        finally
        {
            PasswordInput.Clear(); _launchWorkflowBusy = false; SetLaunchWorkflowButtons();
        }
    }

    private async void ObserveLaunchWorkflow(object sender, RoutedEventArgs e)
    {
        if (_launchWorkflowBusy) return;
        var operationId = _unresolvedLaunchOperation ?? LaunchOperationIdBox.Text.Trim();
        if (operationId.Length == 0) { LifecycleMessage("请输入原启动操作编号。", true); return; }
        LaunchOperationIdBox.Text = operationId;
        _launchWorkflowBusy = true; SetLaunchWorkflowButtons();
        try
        {
            var receipt = await Task.Run(() => LaunchWorkflow.Observe(operationId));
            if (receipt.State is "unknown" or "accepted") _unresolvedLaunchOperation = receipt.OperationId;
            else if (_unresolvedLaunchOperation == receipt.OperationId) _unresolvedLaunchOperation = null;
            if (!_closed) ShowLaunchReceipt(receipt);
        }
        catch (Exception error)
        {
            if (!_closed) LifecycleMessage("DESKTOP_LAUNCH_OBSERVE_FAILED · observe · " + error.GetType().Name + "\n操作编号：" + operationId + "\n未取得观察证据；不会启动或重放。", true);
        }
        finally { _launchWorkflowBusy = false; SetLaunchWorkflowButtons(); }
    }

    private void SetLaunchWorkflowButtons()
    {
        LaunchButton.IsEnabled = !_launchWorkflowBusy && _unresolvedLaunchOperation is null;
        if (Tabs.SelectedIndex == 0) HeaderActionButton.IsEnabled = LaunchButton.IsEnabled;
        ObserveLaunchButton.IsEnabled = !_launchWorkflowBusy;
        LaunchOperationIdBox.IsReadOnly = _launchWorkflowBusy || _unresolvedLaunchOperation is not null;
    }

    private void ShowLaunchReceipt(LaunchReceipt receipt)
    {
        LaunchOperationIdBox.Text = receipt.OperationId;
        LaunchWorkflowResultText.Text = $"{receipt.State} · {receipt.Phase} · {receipt.Code}\n{receipt.Message}\n操作编号：{receipt.OperationId}\n尝试编号：{receipt.AttemptId}\n入口 PID：{receipt.ProcessId?.ToString() ?? "未取得"} · 观察：{receipt.ProcessObservation}\nHost：{receipt.HostVerification} · 工程登录需独立核验。";
        LifecycleMessage(receipt.Code + " · " + receipt.Message + "\n操作编号：" + receipt.OperationId,
            receipt.State is "unknown" or "accepted");
    }

    /// <summary>隔离 WPF smoke 使用正式观察按钮核对未知回执；不经过启动边界。</summary>
    private async Task VerifyLaunchWorkflowControlsSmokeAsync(string outputDirectory)
    {
        var receiptRoot = System.IO.Path.Combine(YuzuhaToolkit.Workspace.WorkspaceSettings.StateDirectory, "launch-operations");
        System.IO.Directory.CreateDirectory(receiptRoot);
        var receipt = new LaunchReceipt("desktop-ui-unknown", new string('a', 64), "fixture-attempt", null,
            "unknown", "launch-boundary", "LAUNCH_OUTCOME_UNKNOWN", "合成未知回执：请按原编号观察，禁止自动重放。", DateTimeOffset.UtcNow);
        System.IO.File.WriteAllText(System.IO.Path.Combine(receiptRoot, receipt.OperationId + ".json"),
            System.Text.Json.JsonSerializer.Serialize(receipt, LaunchWorkflowJsonContext.Default.LaunchReceipt));
        Tabs.SelectedIndex = 0;
        LaunchOperationIdBox.Text = receipt.OperationId;
        ObserveLaunchButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        for (var i = 0; _launchWorkflowBusy && i < 200; i++) await Task.Delay(10);
        if (_launchWorkflowBusy || _unresolvedLaunchOperation != receipt.OperationId || LaunchButton.IsEnabled || HeaderActionButton.IsEnabled ||
            !LaunchOperationIdBox.IsReadOnly || !ObserveLaunchButton.IsEnabled ||
            !LaunchWorkflowResultText.Text.Contains("fixture-attempt") || !LaunchWorkflowResultText.Text.Contains("unknown"))
            throw new InvalidOperationException("WPF 原编号观察与 unknown 禁重放状态不一致。");
        Tabs.SelectedIndex = 4; SetPage("总览");
        if (!HeaderActionButton.IsEnabled) throw new InvalidOperationException("离开启动页后页头操作错误地保持禁用。");
        Tabs.SelectedIndex = 0; SetPage("启动工程");
        if (HeaderActionButton.IsEnabled) throw new InvalidOperationException("返回启动页后页头操作未跟随启动禁用态。");
        LaunchWorkflowResultText.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        if (!LaunchOperationIdBox.IsVisible || !ObserveLaunchButton.IsVisible || !LaunchWorkflowResultText.IsVisible)
            throw new InvalidOperationException("WPF 启动编号与恢复观察入口不可见。");
        await CaptureSmokeAsync(System.IO.Path.Combine(outputDirectory, "launch-workflow-unknown.png"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "launch-workflow-ui.json"),
            System.Text.Json.JsonSerializer.Serialize(new { passed = true, operationId = receipt.OperationId,
                observedViaButton = true, unknownBlocksLaunch = true, operationIdReadOnly = true,
                headerLaunchDisabled = !HeaderActionButton.IsEnabled, otherPageHeaderEnabled = true,
                controlsVisible = LaunchOperationIdBox.IsVisible && ObserveLaunchButton.IsVisible && LaunchWorkflowResultText.IsVisible,
                hostOperations = 0, scope = "isolated persisted receipt and actual WPF observe button; no launch" }));
    }
}
