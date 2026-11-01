using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Desktop;

/// <summary>准备工作区的原生合成验收；只使用注入回执，不发现或修改真实厂商环境。</summary>
public partial class PreparationWorkbenchView
{
    /// <summary>以合成安装和载荷验证实际表单输入、选择、预览、栏目及回执显示。</summary>
    internal async Task PrepareSmokeAsync()
    {
        var identity = new ManagerInstallationIdentity("synthetic-installation", "C:/Synthetic/Everything3D2.10", "C:/Synthetic/Everything3D2.10/mon.exe", new('a', 64), "E3D", "2.1 · 合成界面夹具", "synthetic", "Monitor", "C:/Synthetic/Everything3D2.10/evars.init", new('b', 64));
        var candidate = new ManagerInstallationCandidate(identity, new([], [], [], [], []));
        var calls = new List<string>();
        var transferred = false;
        var fixture = new PreparationWorkbench((id, input, context, token) =>
        {
            calls.Add(id);
            if (id == "deployment.preview" && !transferred) return Task.FromResult(OperationResult.Fail(id, 5, "SETUP_OWNERSHIP_TRANSFER_REQUIRED", "合成旧所有权尚未移交；不是实机环境。"));
            if (id == "setup.legacy.transfer") transferred = true;
            var data = id == "installation.discover" ? JsonSerializer.SerializeToElement(new ManagerInstallationReport(true, 1, [candidate], []), new JsonSerializerOptions(JsonSerializerDefaults.Web)) :
                id.EndsWith(".preview", StringComparison.Ordinal) || id.EndsWith(".apply", StringComparison.Ordinal) ? JsonSerializer.SerializeToElement(new ManagerPlanReference(new('1', 32), id.EndsWith(".apply", StringComparison.Ordinal) ? "runtime.receipt" : "runtime.plan", new('c', 64), JsonSerializer.SerializeToElement(new { mode = "reference", runtimeRoot = "C:/Synthetic/Phayton", environmentFile = identity.EnvironmentFile, profile = "E3D2.1", framework = "net48", fileCount = 4, changedFiles = 0, managedSettings = new[] { "pmllib=C:/Synthetic/Phayton/PMLLIB;%pmllib%", "pmlui=C:/Synthetic/Phayton/PMLUI;%pmlui%", "Yuzuha=E3D2.1" }, scope = "合成回执；不是AVEVA实机" })), new JsonSerializerOptions(JsonSerializerDefaults.Web)) :
                JsonSerializer.SerializeToElement(new { packageRoot = "C:/Synthetic/Phayton", version = "合成界面夹具", filesVerified = 4, integrationMode = "manager", nextAction = "明确选择环境，预览并核对EVARS与部署范围。" });
            return Task.FromResult(id == "deployment.apply" ? OperationResult.Fail(id, 7, "RUNTIME_APPLY_RECOVERY_REQUIRED", "合成写入回包丢失，请按本次回执核查恢复；不是实机故障。", data, true) : OperationResult.Ok(id, data));
        });
        UseFixture(fixture);PackageBox.Text = "C:/Synthetic/Phayton";await Run(() => work.InspectAsync());
        var previous = calls.Count;PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (work.Stage != "environment" || calls.Count != previous) throw new InvalidOperationException("交接成功后的主动作未只读进入环境选择。");
        await Run(() => work.DiscoverAsync(["C:/Synthetic"]));Installations.SelectedIndex = 0;
        if (work.Installation?.Id != identity.Id || work.Draft.PackageRoot != PackageBox.Text) throw new InvalidOperationException("原生候选/输入未接到准备模型。");
        await Run(() => work.PreviewAsync());var draft = work.Draft;previous = calls.Count;PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (work.Stage != "ownership" || work.Draft != draft || work.Installation?.Id != identity.Id || calls.Count != previous) throw new InvalidOperationException("后端要求移交时，原生入口没有保留目标与输入并只进入所有权核对。");
        await Run(() => work.PreviewOwnershipAsync());SelectSmokeStage("ownership");previous = calls.Count;PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (work.Stage != "apply" || calls.Count != previous || !work.CanApply) throw new InvalidOperationException("旧所有权预览后的主动作未进入明确移交核对。");
        await Run(() => work.ApplyAsync(true));previous = calls.Count;PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (work.Stage != "preview" || calls.Count != previous || calls.Count(id => id == "setup.legacy.transfer") != 1) throw new InvalidOperationException("移交后未进入部署预览，或错误地重发操作。");
        await Run(() => work.PreviewAsync());if (!work.CanApply || work.ApplyOperation != "deployment.apply") throw new InvalidOperationException("原生预览没有冻结实际Manager操作引用。");
        var before = CaptureState();work.Reader = "scripts";work.SelectStage("ownership");Render();work.Reader = "summary";work.SelectStage("preview");Render();
        if (CaptureState() != before) throw new InvalidOperationException("阅读导航改变了草稿或冻结身份。");
    }
    /// <summary>仅改变合成截图的阅读位置，不重新发送操作。</summary>
    internal void SelectSmokeStage(string stage, string reader = "summary") { work.SelectStage(stage);work.Reader = reader;Render(); }
    /// <summary>通过注入执行器产生带恢复引用的合成未知结果，验证原生出口可见。</summary>
    internal async Task ApplyUnknownSmokeAsync()
    {
        await Run(() => work.ApplyAsync(true));if (!work.Unresolved || work.Receipt is null || work.CanApply) throw new InvalidOperationException("未知写入的恢复引用或执行门禁未保留。");SelectSmokeStage("recover");
    }
    /// <summary>只按同一合成回执明确恢复，验证未知提示随真实模型结果解除。</summary>
    internal async Task RestoreUnknownSmokeAsync()
    {
        await Run(() => work.RestoreAsync(true));if (work.Unresolved || work.Receipt is not null) throw new InvalidOperationException("按同一回执恢复后仍有未知结果。");
    }
    /// <summary>实际滚到摘要底部再返回输入，核对小视口可以到达冻结计划及诊断。</summary>
    internal async Task AssertSmokeScrollAsync()
    {
        SelectSmokeStage("preview");SummaryScroll.ScrollToEnd();await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);UpdateLayout();
        if (Math.Abs(SummaryScroll.VerticalOffset - SummaryScroll.ScrollableHeight) > 1) throw new InvalidOperationException("小视口无法滚到摘要底部。");
        SummaryScroll.ScrollToTop();await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);UpdateLayout();
        if (SummaryScroll.VerticalOffset > 1) throw new InvalidOperationException("摘要无法回到实际输入。");
    }
    /// <summary>核对主要输入、动作和就近结果位于显式客户区内，细节可在独立阅读区滚动。</summary>
    internal void AssertSmokeGeometry(FrameworkElement surface, double height)
    {
        foreach (var element in new FrameworkElement[] { PrimaryAction, InlineResult, TargetSummary })
        { var point = element.TransformToAncestor(surface).Transform(new Point());if (point.Y < 0 || point.Y + element.ActualHeight > height + 1) throw new InvalidOperationException("准备工作区核心内容超出首屏：" + element.Name); }
        if (work.Stage == "preview" && !PackageBox.IsVisible) throw new InvalidOperationException("预览前的载荷输入不可见。");
    }
}
