using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace YuzuhaToolkit.Desktop;

public partial class MainWindow
{
    private DashboardSessionMonitor? dashboardMonitor;
    private readonly DispatcherTimer dashboardTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource dashboardCancellation = new();
    private DateTimeOffset dashboardNextPoll;
    /// <summary>当前只读可用性快照，与人工选择的操作目标独立。</summary>
    internal DashboardHealthSnapshot DashboardSnapshot => dashboardMonitor?.Snapshot ?? DashboardHealthSnapshot.Empty;
    internal event Action<DashboardHealthSnapshot>? DashboardUpdated;
    private void InitializeDashboardMonitoring(bool isolated)
    {
        if (isolated) return;
        dashboardMonitor = new(new DashboardSessionProbe());
        dashboardMonitor.Updated += snapshot =>
        {
            if (_closed || Dispatcher.HasShutdownStarted) return;
            _ = Dispatcher.InvokeAsync(() => { if (!_closed) RenderDashboard(snapshot); });
        };
        dashboardTimer.Tick += (_, _) =>
        {
            if (_closed) return;
            RenderDashboard(DashboardSnapshot);
            if (DateTimeOffset.UtcNow >= dashboardNextPoll && !dashboardMonitor.IsPolling) _ = PollDashboardAsync();
        };
        Loaded += (_, _) => { dashboardTimer.Start(); _ = PollDashboardAsync(); };
        Closed += (_, _) => { dashboardTimer.Stop(); dashboardCancellation.Cancel(); dashboardMonitor.Dispose(); };
    }
    private async Task PollDashboardAsync()
    {
        if (dashboardMonitor is null || _closed) return;
        dashboardNextPoll = DateTimeOffset.UtcNow.AddSeconds(3);
        await Task.Run(() => dashboardMonitor.PollAsync(dashboardCancellation.Token));
    }
    private void RenderDashboard(DashboardHealthSnapshot snapshot)
    {
        DashboardStateText.Text = snapshot.Summary;
        DashboardStateLamp.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, snapshot.State switch
        {
            DashboardSessionState.Available => "Text.Success",
            DashboardSessionState.Failed or DashboardSessionState.Expired => "Text.Danger",
            DashboardSessionState.Detected or DashboardSessionState.Checking => "Text.Warning",
            _ => "Text.Tertiary"
        });
        DashboardObservedText.Text = snapshot.ObservedAt == DateTimeOffset.MinValue ? "尚未观察 · 仅只读监测，不自动选择操作目标" : $"观察 {snapshot.ObservedAt.ToLocalTime():HH:mm:ss} · {snapshot.Sessions.Count(row => row.Discovered)} 个候选 · 新鲜期 ≤10秒";
        DashboardSessionsList.ItemsSource = snapshot.Sessions.Select(row => new
        {
            Title = $"{row.Product} · PID {row.ProcessId} · {row.Module ?? "模块待核验"}",
            Detail = row.StateText + " · " + row.ObservedText,
            row.Reason,
            Color = (Brush)FindResource(row.State switch { DashboardSessionState.Available => "Text.Success", DashboardSessionState.Failed or DashboardSessionState.Expired => "Text.Danger", _ => "Text.Warning" })
        }).ToArray();
        SessionView.ApplyDashboardSnapshot(snapshot);
        RenderDashboardCandidates(snapshot);
        UpdateDashboardPrimaryAction();
        DashboardUpdated?.Invoke(snapshot);
    }
    private void RenderDashboardCandidates(DashboardHealthSnapshot snapshot)
    {
        OverviewServices.Text = snapshot.Sessions.Count(row => row.Discovered).ToString();
        OverviewSessionsGrid.ItemsSource = snapshot.Sessions.Take(3).Select(row => new { row.Product, row.Project, row.ProcessId, Verified = row.IsAvailable, StatusText = row.StateText }).ToArray();
    }
}
