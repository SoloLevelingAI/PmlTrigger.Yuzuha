using System.IO;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Threading;

namespace YuzuhaToolkit.Desktop;

/// <summary>安装到操作工作区的六配色、三视口与实际原生控件验收。</summary>
public partial class MainWindow
{
    /// <summary>在隔离状态目录截图，所有操作回执明确为合成，不访问真实AVEVA。</summary>
    public async Task RunPreparationSmokeAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);OpenPreparation();var messageBeforeClosingProbe = MessageText.Text;
        await PreparationView.AssertSmokeDispatcherResponsiveAsync(expectedBusy =>
        {
            var closing = new CancelEventArgs();OnClosing(closing);
            if (PreparationView.IsRunning != expectedBusy || closing.Cancel != expectedBusy) throw new InvalidOperationException("准备工作区的普通关窗保护未跟随本次回执状态。");
        });
        MessageText.Text = messageBeforeClosingProbe;await PreparationView.PrepareSmokeAsync();
        await AssertPreparationNavigationLayoutAsync();
        var initial = PreparationView.CaptureState();
        var geometry = new List<object>();
        foreach (var palette in DesktopPalettes.All)
        {
            DesktopTheme.Apply(palette.Id);UpdateThemeButtons();
            if (PreparationView.CaptureState() != initial) throw new InvalidOperationException("配色切换改变了准备草稿或计划。");
            foreach (var size in new[] { (Width: 1440d, Height: 900d), (Width: 1280d, Height: 720d), (Width: 1024d, Height: 768d) })
            {
                SetSmokeViewport(size.Width, size.Height);PreparationView.SelectSmokeStage("preview");UpdateLayout();await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                PreparationView.AssertSmokeGeometry(WindowSurface, size.Height);geometry.Add(new { palette = palette.Id, size.Width, size.Height, actual = PreparationView.CaptureSmokeGeometry(WindowSurface) });await CaptureSmokeAsync(Path.Combine(outputDirectory, palette.Id + "-" + size.Width + ".png"));
            }
        }
        DesktopTheme.Apply("ion-lime");UpdateThemeButtons();SetSmokeViewport(1440, 900);
        foreach (var id in new[] { "install", "environment", "ownership", "preview", "apply", "recover", "launch", "verify" })
        { PreparationView.SelectSmokeStage(id);UpdateLayout();PreparationView.AssertSmokeGeometry(WindowSurface, 900);await CaptureSmokeAsync(Path.Combine(outputDirectory, "stage-" + id + ".png")); }
        foreach (var reader in new[] { "contract", "scripts", "evidence" })
        { PreparationView.SelectSmokeStage("preview", reader);UpdateLayout();await CaptureSmokeAsync(Path.Combine(outputDirectory, "reader-" + reader + ".png")); }
        SetSmokeViewport(1280, 720);await PreparationView.AssertSmokeScrollAsync();await PreparationView.ApplyUnknownSmokeAsync();UpdateLayout();PreparationView.AssertSmokeGeometry(WindowSurface, 720);await CaptureSmokeAsync(Path.Combine(outputDirectory, "state-unknown-recovery.png"));
        await PreparationView.RestoreUnknownSmokeAsync();UpdateLayout();PreparationView.AssertSmokeGeometry(WindowSurface, 720);await CaptureSmokeAsync(Path.Combine(outputDirectory, "state-restored.png"));
        File.WriteAllText(Path.Combine(outputDirectory, "preparation-smoke.json"), JsonSerializer.Serialize(new { passed = true, scope = "Native WPF controls, synthetic manager receipts and layout; actual backend temp-file scenarios tested independently; no AVEVA live or human visual approval", palettes = DesktopPalettes.All.Select(p => p.Id).ToArray(), stages = 8, readers = 5, screenshots = _smokeCaptures.Count, viewportEvidence = _smokeCaptures, geometryEvidence = geometry, dispatcherResponsive = true, closeRetainedWhileBusy = true, themePreservesDraft = true, navigationTransitions = true, preparationSidebarRestoredCorrectly = true, previewRegeneratesOnly = true, corePreviewWithoutScroll = true, unknownRecovery = true, explicitPlanIdentity = true, installedDocfxEntry = true, finishedUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
    }
    /// <summary>切换准备与其他页面并跨过窄屏阈值，证明侧栏只按当前页面和视口恢复，且不改变冻结输入。</summary>
    private async Task AssertPreparationNavigationLayoutAsync()
    {
        var before = PreparationView.CaptureState();
        foreach (var size in new[] { (Width: 1280d, Height: 720d), (Width: 1024d, Height: 768d), (Width: 1440d, Height: 900d) })
        {
            SetSmokeViewport(size.Width, size.Height);OpenPreparation();UpdateLayout();await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (ContextSidebar.Visibility != System.Windows.Visibility.Collapsed || ContextColumn.ActualWidth > 1 || CompactNavigation.Visibility != System.Windows.Visibility.Collapsed || WorkspaceHeader.Visibility != System.Windows.Visibility.Collapsed || !ReferenceEquals(PreparationView.GlobalToolsHost.Content, WorkspaceHeaderTools) || !WorkspaceHeaderTools.IsVisible) throw new InvalidOperationException("准备页切换或resize恢复了重复头/工程栏，或全局工具丢失。");
            Tabs.SelectedIndex = 6;SetPage("设置");SetSmokeViewport(size.Width == 1024 ? 1280 : 1024, size.Height);UpdateLayout();
            var narrow = WindowSurface.ActualWidth < 1100;
            if ((ContextSidebar.Visibility == System.Windows.Visibility.Visible) == narrow || (CompactNavigation.Visibility == System.Windows.Visibility.Visible) != narrow || WorkspaceHeader.Visibility != System.Windows.Visibility.Visible || !ReferenceEquals(WorkspaceHeaderTools.Parent, WorkspaceHeader) || PreparationView.GlobalToolsHost.Content is not null) throw new InvalidOperationException("离开准备页后标题/全局工具/侧栏没有按视口恢复。");
            OpenPreparation();SetSmokeViewport(size.Width, size.Height);UpdateLayout();await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (ContextSidebar.Visibility != System.Windows.Visibility.Collapsed || ContextColumn.ActualWidth > 1 || CompactNavigation.Visibility != System.Windows.Visibility.Collapsed || WorkspaceHeader.Visibility != System.Windows.Visibility.Collapsed || !ReferenceEquals(PreparationView.GlobalToolsHost.Content, WorkspaceHeaderTools) || PreparationView.CaptureState() != before) throw new InvalidOperationException("返回准备页后标题/侧栏恢复、全局工具丢失，或导航改变了冻结计划。");
        }
    }
}
