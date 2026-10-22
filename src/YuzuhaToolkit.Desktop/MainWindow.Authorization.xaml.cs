using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>现有设置页的三档执行授权与日志筛选/导出，使用同一后端策略文件。</summary>
public partial class MainWindow
{
    private void RefreshAuthorization()
    {
        try
        {
            var policy = new ExecutionAuthorizationStore().Read();
            AuthorizationModeBox.SelectedIndex = policy.Mode switch { "Conservative" => 0, "Development" => 2, _ => 1 };
            DevelopmentPidBox.Text = policy.ProcessId > 0 ? policy.ProcessId.ToString() : "";
            DevelopmentModelBox.Text = policy.Model; DevelopmentRootBox.Text = policy.SourceRoot;
            DevelopmentRulesBox.Text = string.Join(',', policy.BypassRules); DevelopmentReasonBox.Text = policy.Reason;
            AuthorizationStatusText.Text = policy.Mode == "Development" ? $"PID {policy.ProcessId} · 启动 {policy.ProcessStartTimeUtcTicks} · 到期 {policy.ExpiresUtc.ToLocalTime():g}" : policy.Mode == "Conservative" ? "仅登记和管理配置；不派发 PML。" : "按登记源码逐函数授权；类型化只读查询可用。";
        }
        catch (Exception error) { AuthorizationStatusText.Text = error.Message; }
    }
    private async void ApplyAuthorization(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        button.IsEnabled = false;
        var call = _journal.Begin("desktop.authorization", "{}", int.TryParse(DevelopmentPidBox.Text, out var auditPid) ? auditPid : null);
        try
        {
            using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery());
            var tools = new DevelopmentTools(new ExecutionAuthorizationStore(), bridge, _journal);
            if (AuthorizationModeBox.SelectedIndex == 2)
            {
                if (!int.TryParse(DevelopmentPidBox.Text, out var pid)) throw new ArgumentException("填写会话扫描中准确的开发 PID。");
                await tools.ConnectDevelopmentSession(pid, DevelopmentModelBox.Text.Trim(), DevelopmentRootBox.Text.Trim(), DevelopmentReasonBox.Text.Trim(), DevelopmentRulesBox.Text.Trim());
            }
            else tools.SetExecutionAuthorization(AuthorizationModeBox.SelectedIndex == 0 ? "Conservative" : "Standard");
            RefreshAuthorization();
            _journal.Complete(call, "succeeded", tools.GetExecutionAuthorization(), auditPid > 0 ? auditPid : null);
            Say("已保存执行授权；所有入口在每次派发前重新核验。");
        }
        catch (Exception error) { _journal.Complete(call, "failed", error.Message); Say("授权未保存：" + error.Message); }
        finally { button.IsEnabled = true; }
    }
    private void FilterLogs(object sender, RoutedEventArgs e) => ApplyLogFilter();
    private ActivityEntry[] FilteredLogs() => RuntimeReader.Filter(_recentActivity,
        targetPid: int.TryParse(LogPidBox?.Text, out var pid) ? pid : null,
        phase: LogPhaseBox?.SelectedIndex switch { 1 => "succeeded", 2 => "failed", 3 => "cancelled-unknown", 4 => "started", _ => null },
        text: LogSearchBox?.Text).ToArray();
    private void ApplyLogFilter() { if (ActivityGrid != null) ActivityGrid.ItemsSource = FilteredLogs(); }
    private void ExportLogs(object sender, RoutedEventArgs e) => Try(() =>
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "pml-events-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl", Filter = "JSONL|*.jsonl", OverwritePrompt = true };
        if (dialog.ShowDialog(this) == true) Say("已导出 " + RuntimeReader.Export(dialog.FileName, FilteredLogs()) + " 条保留日志。");
    });
}
