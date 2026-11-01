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
    /// <summary>在真实Dispatcher上证明同步Manager处理器阻塞期间仍可阅读，且输入、重复调用、写入和关窗仍受门禁约束。</summary>
    /// <param name="checkClosing">由真实主窗口调用OnClosing，验证等待时拒绝关窗及完成后解除。</param>
    internal async Task AssertSmokeDispatcherResponsiveAsync(Action<bool> checkClosing)
    {
        var original = work;var uiThread = Environment.CurrentManagedThreadId;var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();var watchdogTriggered = 0;var calls = 0;
        await using var watchdog = new System.Threading.Timer(_ => { Interlocked.Exchange(ref watchdogTriggered, 1);release.Set(); }, null, TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan);
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"packageRoot\":{\"type\":\"string\"}},\"required\":[\"packageRoot\"],\"additionalProperties\":false}");
        var executor = new OperationExecutor(new OperationRegistry([new(new("setup.handoff.inspect", 1, "synchronously blocked native dispatcher fixture", schema.RootElement.Clone()), new DelegateOperationHandler((id, input, context, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1) return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { scope = "先前已收到的合成回执，不代表本次完成" })));
            entered.TrySetResult(Environment.CurrentManagedThreadId);release.Wait();
            return Task.FromResult(context.AllowWrite ? OperationResult.Fail(id, 4, "FIXTURE_WRITE_REJECTED", "合成只读处理器拒绝写入。") : OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { scope = "同步阻塞夹具，不访问真实厂商环境" })));
        }))]));
        var fixture = new PreparationWorkbench(executor.InvokeAsync);Task? pending = null;
        try
        {
            UseFixture(fixture);await Run(() => work.InspectAsync());var previousResult = work.Result;pending = Run(() => work.InspectAsync());
            var backendThread = await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Dispatcher.InvokeAsync(() =>
            {
                if (Volatile.Read(ref watchdogTriggered) != 0 || release.IsSet || pending.IsCompleted || !work.Busy || backendThread == uiThread || !Dispatcher.CheckAccess()) throw new InvalidOperationException("同步处理器尚未释放时，原生Dispatcher没有保持可响应。");
                Stages.SelectedItem = stages.First(s => s.Id == "recover");Readers.SelectedIndex = 3;UpdateLayout();
                if (work.Stage != "recover" || work.Reader != "evidence" || PackagePanel.IsEnabled || PrimaryAction.IsEnabled || !EvidenceOutcome.IsVisible) throw new InvalidOperationException($"等待期间阅读未响应，或输入/主写入动作仍可用：{work.Stage}/{work.Reader}/input={PackagePanel.IsEnabled}/action={PrimaryAction.IsEnabled}/evidence={EvidenceOutcome.IsVisible}。");
                if (!InlineResult.Text.StartsWith("正在等待", StringComparison.Ordinal) || !EvidenceOutcome.Text.Contains("当前正在等待", StringComparison.Ordinal) || !EvidenceOutcome.Text.Contains("上一次已收到", StringComparison.Ordinal) || !EvidenceOperation.Text.StartsWith("上一次回执 · setup.handoff.inspect", StringComparison.Ordinal) || !ReferenceEquals(previousResult, work.Result) || !EvidenceResult.Text.Contains("先前已收到的合成回执", StringComparison.Ordinal)) throw new InvalidOperationException("等待状态混入上次成功，或清除了上次已收到的回执。");
                checkClosing(true);
                if (!IsRunning || !InlineResult.Text.Contains("等待本次准备回执", StringComparison.Ordinal) || !ReferenceEquals(previousResult, work.Result)) throw new InvalidOperationException("等待时关窗没有就近提示，或破坏了当前调用和回执。");
                var rejected = false;try { work.Change(work.Draft with { PackageRoot = "changed" }); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("等待期间允许改写执行输入。");
                foreach (var duplicate in new[] { work.InspectAsync(), work.ApplyAsync(true) })
                {
                    if (!duplicate.IsCompleted) throw new InvalidOperationException("等待期间重复发送了管理器操作。");
                    rejected = false;try { duplicate.GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
                    if (!rejected) throw new InvalidOperationException("等待期间的重复或写入调用没有被拒绝。");
                }
                if (Volatile.Read(ref calls) != 2 || release.IsSet) throw new InvalidOperationException("Dispatcher验收没有发生在同一次同步处理器等待期间。");
            }, DispatcherPriority.ApplicationIdle);
        }
        finally { release.Set();if (pending is not null) await pending;try { checkClosing(false); } finally { UseFixture(original); } }
        if (Volatile.Read(ref watchdogTriggered) != 0 || fixture.Busy || fixture.Result?.Success != true || calls != 2) throw new InvalidOperationException("后台同步处理器未可靠完成唯一等待调用。");
    }
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
        await AssertSmokeMetadataAsync();
    }
    /// <summary>仅改变合成截图的阅读位置，不重新发送操作。</summary>
    internal void SelectSmokeStage(string stage, string reader = "summary") { work.SelectStage(stage);work.Reader = reader;Render(); }
    /// <summary>通过注入执行器产生带恢复引用的合成未知结果，验证原生出口可见。</summary>
    internal async Task ApplyUnknownSmokeAsync()
    {
        await Run(() => work.ApplyAsync(true));if (!work.Unresolved || work.Receipt is null || work.CanApply) throw new InvalidOperationException("未知写入的恢复引用或执行门禁未保留。");SelectSmokeStage("recover");
        if (RecoveryIdentityExpander.IsExpanded || !RecoveryIdentity.Text.Contains(work.Receipt.PlanId, StringComparison.Ordinal) || !RecoveryIdentity.Text.Contains(work.Receipt.Sha256, StringComparison.Ordinal) || RecoverySummary.Text.Contains(work.Receipt.PlanId, StringComparison.Ordinal)) throw new InvalidOperationException("恢复身份未保留在辅助区，或技术编号抢占了业务摘要。");
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
    /// <summary>验证辅助身份可完整选择，证据仍保留真实数值，展开与选字不会改变执行身份。</summary>
    internal async Task AssertSmokeMetadataAsync()
    {
        var before = CaptureState();var plan = work.Plan ?? throw new InvalidOperationException("缺少计划夹具。");
        PlanIdentityExpander.IsExpanded = true;await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);PlanIdentity.SelectAll();
        if (PlanIdentity.SelectedText != PlanIdentity.Text || !PlanIdentity.Text.Contains(plan.PlanId, StringComparison.Ordinal) || !PlanIdentity.Text.Contains(plan.Sha256, StringComparison.Ordinal)) throw new InvalidOperationException("计划完整身份不可选择或已被截断。");
        PlanIdentity.Select(0, 0);PlanIdentityExpander.IsExpanded = false;
        SelectSmokeStage("preview", "scripts");var identities = ScriptFields.Children.OfType<Border>().Select(b => b.Child).OfType<TextBox>().ToArray();
        if (identities.Length != stages.First(s => s.Id == "preview").Scripts.Length || identities.Any(t => !t.IsReadOnly || t.FontSize >= StageTitle.FontSize)) throw new InvalidOperationException("脚本身份没有保留为可复制辅助信息。");
        foreach (var identity in identities) { identity.SelectAll();if (identity.SelectedText != identity.Text) throw new InvalidOperationException("脚本完整类型/成员/路径不能选择。");identity.Select(0, 0); }
        SelectSmokeStage("preview", "evidence");
        if (EvidenceOperation.Text != "deployment.preview · runtime.plan" || !EvidenceResult.Text.Contains("文件数：4", StringComparison.Ordinal) || EvidenceResult.Text.Contains("runtime.plan", StringComparison.Ordinal)) throw new InvalidOperationException("证据业务统计或原始操作身份丢失，或层级混合。");
        SelectSmokeStage("preview");if (CaptureState() != before) throw new InvalidOperationException("辅助信息阅读改变了业务执行身份。");
    }
    /// <summary>核对主要输入、动作和就近结果位于显式客户区内，细节可在独立阅读区滚动。</summary>
    internal void AssertSmokeGeometry(FrameworkElement surface, double height)
    {
        foreach (var element in new FrameworkElement[] { PrimaryAction, InlineResult, TargetSummary })
        { var point = element.TransformToAncestor(surface).Transform(new Point());if (point.Y < 0 || point.Y + element.ActualHeight > height + 1) throw new InvalidOperationException("准备工作区核心内容超出首屏：" + element.Name); }
        if (work.Stage == "preview" && !PackageBox.IsVisible) throw new InvalidOperationException("预览前的载荷输入不可见。");
    }
}
