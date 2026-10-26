using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>运行记录筛选与选择；与对象查询和后台汇总各自保留状态。</summary>
public partial class MainWindow
{
    private bool _refreshingActivityFilters;
    private const string AllOperations = "全部操作";

    private void RefreshActivityFilters()
    {
        var operation = ActivityOperationBox.SelectedItem as string;
        _refreshingActivityFilters = true;
        try
        {
            var operations = new[] { AllOperations }.Concat(_recentActivity.Select(e => e.Tool).Distinct().Order()).ToArray();
            ActivityOperationBox.ItemsSource = operations;
            ActivityOperationBox.SelectedItem = operations.Contains(operation) ? operation : AllOperations;
        }
        finally { _refreshingActivityFilters = false; }
        ApplyActivityFilters();
    }

    private void ActivityFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyActivityFilters();
    private void ActivitySearchChanged(object sender, TextChangedEventArgs e) => ApplyActivityFilters();
    private void ApplyActivityFilters()
    {
        if (_refreshingActivityFilters || ActivityGrid is null || ActivitySearchBox is null || ActivityOperationBox is null || ActivityFilterSummary is null) return;
        var selected = ActivityGrid.SelectedItem as ActivityEntry;
        var phase = (ActivityStatusBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        var operation = ActivityOperationBox.SelectedItem as string;
        var rows = DesktopActivityFilter.Apply(_recentActivity, phase, operation == AllOperations ? null : operation, ActivitySearchBox.Text);
        ActivityGrid.ItemsSource = rows;
        ActivityGrid.SelectedItem = rows.FirstOrDefault(e => e == selected);
        ActivityFilterSummary.Text = $"匹配 {rows.Length} / {_recentActivity.Length} 条 · 本机滚动保留范围";
    }

    private void ResetActivityFilters(object sender, RoutedEventArgs e)
    {
        _refreshingActivityFilters = true;
        try { ActivityStatusBox.SelectedIndex = 0; ActivityOperationBox.SelectedItem = AllOperations; ActivitySearchBox.Clear(); }
        finally { _refreshingActivityFilters = false; }
        ApplyActivityFilters();
    }

    private void VerifyActivityFiltersSmoke()
    {
        Tabs.SelectedIndex = 7;
        ActivityStatusBox.SelectedIndex = 2;
        ActivityOperationBox.SelectedItem = "synthetic_query";
        ActivitySearchBox.Text = "SYNTHETIC_QUERY";
        if (ActivityGrid.Items.Count != 1 || ActivityGrid.Items[0] is not ActivityEntry { Tool: "synthetic_query", Phase: "succeeded" })
            throw new InvalidOperationException("日志状态、操作和输入未取交集。");
        ActivityGrid.SelectedIndex = 0;
        ActivitySearchBox.Text = "no-match";
        if (ActivityGrid.Items.Count != 0 || ActivityDetails.Text.Length != 0)
            throw new InvalidOperationException("空筛选保留了过期日志详情。");
        ResetActivityFilters(this, new());
        if (ActivityGrid.Items.Count != _recentActivity.Length) throw new InvalidOperationException("重置未恢复全部日志。");
    }
}
