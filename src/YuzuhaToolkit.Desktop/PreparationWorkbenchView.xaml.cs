using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Desktop;

/// <summary>安装到操作的原生准备工作区；输入、阅读与明确写入分开，复用统一Manager执行边界。</summary>
public partial class PreparationWorkbenchView : UserControl
{
    /// <summary>当前原生控件绑定的可测试工作状态。</summary>
    private PreparationWorkbench work = new();
    /// <summary>阻止状态渲染触发新的用户输入意图。</summary>
    private bool rendering = true;
    /// <summary>保留当前动作的本地校验错误。</summary>
    private string? notice;
    /// <summary>直接来自Manager冻结目录的八阶段契约。</summary>
    private readonly ManagerContractStage[] stages = ManagerContractCatalog.Describe().Stages;
    /// <summary>用户明确请求跳转到既有启动或会话页面。</summary>
    public event Action<string>? NavigationRequested;
    /// <summary>用户请求打开已安装的精确DocFX页面。</summary>
    public event Action<string>? DocumentationRequested;
    /// <summary>用户明确切换配色后由宿主保存偏好。</summary>
    public event Action<string>? PaletteRequested;

    /// <summary>建立固定控件和阅读面板，不发现安装、不写文件、不启动产品。</summary>
    public PreparationWorkbenchView()
    {
        InitializeComponent();
        PalettePicker.ItemsSource = DesktopPalettes.All;
        Stages.ItemsSource = stages; Stages.SelectedIndex = 0;
        ScanBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AVEVA");
        rendering = false;Render();
    }
    /// <summary>接收安装器的根目录作为非秘密草稿；仍由用户明确核对载荷。</summary>
    public void PrepareInstallation(string packageRoot)
    {
        work.Change(work.Draft with { PackageRoot = Path.GetFullPath(packageRoot) });work.SelectStage("install");Render();
    }
    /// <summary>同步原生配色选择，不修改草稿或执行计划。</summary>
    public void RefreshPalette()
    {
        rendering = true;PalettePicker.SelectedValue = DesktopTheme.Current;rendering = false;
    }
    /// <summary>捕获用于主题/导航回归的非秘密工作状态。</summary>
    internal string CaptureState() => JsonSerializer.Serialize(new { work.Draft, InstallationId = work.Installation?.Id, work.Stage, work.Reader, Plan = work.Plan?.PlanId, Receipt = work.Receipt?.PlanId });
    /// <summary>仅在隔离界面验收中使用注入执行器，不访问真实厂商安装。</summary>
    internal void UseFixture(PreparationWorkbench fixture) { work = fixture;Render(); }
    /// <summary>读取准备工作区当前状态，供隔离截图断言使用。</summary>
    internal PreparationWorkbench State => work;

    /// <summary>安装列表的可读标签及其明确候选身份。</summary>
    private sealed record CandidateRow(string Label, ManagerInstallationCandidate Candidate);
    /// <summary>同步非秘密草稿并失效旧预览，不写入业务目标。</summary>
    private void DraftChanged(object sender, TextChangedEventArgs? e)
    {
        if (rendering || PackageBox is null || MigrationBox is null) return;
        try { work.Change(new(PackageBox.Text.Trim(), RuntimeBox.Text.Trim(), (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "reference", EnvironmentBox.Text.Trim(), ProfileBox.SelectedValue?.ToString() ?? "", (MigrationFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rewrite", MigrationBox.Text.Trim()));RenderStatus(); }
        catch (InvalidOperationException error) { InlineResult.Text = error.Message; }
    }
    /// <summary>把旧所有权的环境输入同步为唯一草稿字段。</summary>
    private void LegacyDraftChanged(object sender, TextChangedEventArgs e)
    {
        if (rendering || EnvironmentBox is null) return;EnvironmentBox.Text = LegacyEnvironmentBox.Text;
    }
    /// <summary>切换引用或复制方式并显示对应运行根输入。</summary>
    private void ModeChanged(object sender, SelectionChangedEventArgs e) { if (!rendering) { DraftChanged(sender, null);RuntimePanel.Visibility = work.Draft.Mode == "copy" ? Visibility.Visible : Visibility.Collapsed; } }
    /// <summary>同步只读迁移格式输入。</summary>
    private void MigrationFormatChanged(object sender, SelectionChangedEventArgs e) { if (!rendering) DraftChanged(sender, null); }
    /// <summary>同步用户明确选择的Host版本身份。</summary>
    private void ProfileChanged(object sender, SelectionChangedEventArgs e) { if (!rendering) DraftChanged(sender, null); }
    /// <summary>范围改变后撤销旧计划，避免按过期范围写入。</summary>
    private void PreviewModeChanged(object sender, RoutedEventArgs e) { if (!rendering) { work.DiscardPreview();work.SelectStage("preview");Render(); } }
    /// <summary>只切换契约阅读阶段，不推定执行完成。</summary>
    private void StageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (rendering || Stages.SelectedItem is not ManagerContractStage stage) return;work.SelectStage(stage.Id);Render();
    }
    /// <summary>保留用户选择的摘要、契约、脚本或证据栏目。</summary>
    private void ReaderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!rendering && ReferenceEquals(e.Source, Readers) && Readers.SelectedItem is TabItem tab) work.Reader = tab.Tag?.ToString() ?? "summary";
    }
    /// <summary>把用户明确选择的发现候选绑定到预览草稿。</summary>
    private void InstallationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (rendering || Installations.SelectedItem is not CandidateRow row) return;
        work.Select(row.Candidate);work.SelectStage("preview");Render();
    }
    /// <summary>把明确的配色意图交给主窗口保存。</summary>
    private void PaletteChanged(object sender, SelectionChangedEventArgs e) { if (!rendering && PalettePicker.SelectedValue is string id) PaletteRequested?.Invoke(id); }

    /// <summary>根据真实状态推进阅读位置，写入前要求明确确认当前冻结计划。</summary>
    private async void RunPrimary(object sender, RoutedEventArgs e)
    {
        if (work.Busy) return;
        if (NeedsOwnershipTransfer() && work.Stage is "preview" or "apply") { work.SelectStage("ownership");Render();return; }
        switch (work.Stage)
        {
            case "install": if (CanContinueFromInstall()) { work.SelectStage("environment");Render(); } else await Run(() => work.InspectAsync());break;
            case "environment": await Run(() => work.DiscoverAsync(ScanBox.Text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));break;
            case "ownership": if (work.CanApply && work.ApplyOperation == "setup.legacy.transfer") { work.SelectStage("apply");Render(); } else await Run(() => work.PreviewOwnershipAsync());break;
            case "preview": if (work.CanApply) { work.SelectStage("apply");Render(); } else await Run(() => work.PreviewAsync(EvarsOnly.IsChecked == true));break;
            case "apply":
                if (work.Unresolved) { work.SelectStage("recover");Render();break; }
                if (LastApplySucceeded()) { work.SelectStage(LastTransferSucceeded() ? "preview" : "launch");Render();break; }
                if (!work.CanApply) { InlineResult.Text = "先回到预览阶段生成并核对冻结计划。";break; }
                if (MessageBox.Show(Window.GetWindow(this), "仅应用当前冻结计划：\n\n" + PlanText(work.Plan!) + "\n\n源/目标改变会被拒绝。写入成功不等于AVEVA加载。是否继续？", "确认本次写入", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await Run(() => work.ApplyAsync(true));
                break;
            case "recover": Restore(sender, e);break;
            case "launch": GoLaunch(sender, e);break;
            case "verify": GoVerify(sender, e);break;
        }
    }
    /// <summary>保留可明确重做的只读安装交接核验。</summary>
    private async void InspectHandoff(object sender, RoutedEventArgs e) => await Run(() => work.InspectAsync());
    /// <summary>只读冻结选定旧环境的所有权移交计划。</summary>
    private async void PreviewOwnership(object sender, RoutedEventArgs e) => await Run(() => work.PreviewOwnershipAsync());
    /// <summary>读取迁移来源，不导入或激活配置。</summary>
    private async void PreviewMigration(object sender, RoutedEventArgs e) => await Run(() => work.PreviewMigrationAsync());
    /// <summary>在明确确认后按原回执条件恢复，外部改动由后端拒绝。</summary>
    private async void Restore(object sender, RoutedEventArgs e)
    {
        if (work.Receipt is null) { InlineResult.Text = "没有可核对的恢复回执。";return; }
        if (MessageBox.Show(Window.GetWindow(this), "只按此回执条件恢复，外部改动时停止：\n\n" + PlanText(work.Receipt), "确认条件恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await Run(() => work.RestoreAsync(true));
    }
    /// <summary>进入已有启动配置工作区，不自动创建厂商进程。</summary>
    private void GoLaunch(object sender, RoutedEventArgs e) => NavigationRequested?.Invoke("启动工程");
    /// <summary>进入已有会话工作区核对实际运行证据。</summary>
    private void GoVerify(object sender, RoutedEventArgs e) => NavigationRequested?.Invoke("工程会话");
    /// <summary>请求当前阶段的已安装指南及显式锚点。</summary>
    private void OpenContractDocument(object sender, RoutedEventArgs e) => DocumentationRequested?.Invoke("install-to-operation.zh-CN.html#" + work.Stage);
    /// <summary>锁定输入并等待单次回执；异常在动作旁保留，不自动重试。</summary>
    private async Task Run(Func<Task<OperationResult>> action)
    {
        if (work.Busy) return;
        notice = null;
        PackagePanel.IsEnabled = DiscoveryPanel.IsEnabled = DeploymentPanel.IsEnabled = OwnershipPanel.IsEnabled = false;PrimaryAction.IsEnabled = RestoreButton.IsEnabled = false;
        InlineResult.Text = "正在等待本次回执；不会自动重发。";
        try { await action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or UnauthorizedAccessException or IOException) { notice = error.Message; }
        finally { Render(); }
    }
    /// <summary>保持同一组原生控件，按当前草稿、契约目录和阅读位置更新。</summary>
    private void Render()
    {
        rendering = true;
        try
        {
            var stage = stages.First(s => s.Id == work.Stage);
            Stages.SelectedItem = stage;StageTitle.Text = stage.Title;
            TargetSummary.Text = work.Installation is { } target ? target.Product + " " + target.ProductVersion + " · " + target.Root : "先核对应用交接，再明确选择一个本机产品环境。";
            PackageBox.Text = work.Draft.PackageRoot;RuntimeBox.Text = work.Draft.RuntimeRoot;EnvironmentBox.Text = LegacyEnvironmentBox.Text = work.Draft.EnvironmentFile;MigrationBox.Text = work.Draft.MigrationSource;
            ProfileBox.SelectedValue = work.Draft.Profile;
            ModeBox.SelectedIndex = work.Draft.Mode == "copy" ? 1 : 0;RuntimePanel.Visibility = work.Draft.Mode == "copy" ? Visibility.Visible : Visibility.Collapsed;
            Installations.ItemsSource = work.Candidates.Select(c => new CandidateRow(c.Identity.Product + " " + c.Identity.ProductVersion + " · " + c.Identity.Root, c)).ToArray();
            InstallationSummary.Text = work.Installation is { } item ? item.Product + " " + item.ProductVersion + " · " + item.Executable : "尚未选择；先进入“选择环境”。";
            PackagePanel.Visibility = work.Stage is "install" or "preview" ? Visibility.Visible : Visibility.Collapsed;
            ReinspectButton.Visibility = work.Stage == "install" ? Visibility.Visible : Visibility.Collapsed;
            DiscoveryPanel.Visibility = work.Stage == "environment" ? Visibility.Visible : Visibility.Collapsed;
            DeploymentPanel.Visibility = work.Stage == "preview" ? Visibility.Visible : Visibility.Collapsed;
            OwnershipPanel.Visibility = work.Stage == "ownership" ? Visibility.Visible : Visibility.Collapsed;
            PlanPanel.Visibility = work.Stage is "apply" or "preview" ? Visibility.Visible : Visibility.Collapsed;
            RecoveryPanel.Visibility = work.Stage == "recover" ? Visibility.Visible : Visibility.Collapsed;
            LaunchPanel.Visibility = work.Stage == "launch" ? Visibility.Visible : Visibility.Collapsed;
            VerifyPanel.Visibility = work.Stage == "verify" ? Visibility.Visible : Visibility.Collapsed;
            foreach (TabItem tab in Readers.Items) if (Equals(tab.Tag, work.Reader)) Readers.SelectedItem = tab;
            ContractFields.Children.Clear();ScriptFields.Children.Clear();
            foreach (var field in new[] { ("输入", stage.Input), ("前置", stage.Precondition), ("算法与调用", stage.Method), ("作用", stage.Effect), ("校验", stage.Validation), ("回执", stage.Receipt), ("恢复", stage.Recovery) }) AddText(ContractFields, field.Item1, field.Item2);
            foreach (var script in stage.Scripts)
            {
                AddText(ScriptFields, script.TypeName + " · " + script.Member, script.Explanation + "\n" + script.Path);
                var link = new Button { Content = "打开精确DocFX类型 / 方法 ↗", HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 0, 16) };
                var page = script.DocFxPath;link.Click += (_, _) => DocumentationRequested?.Invoke(page);ScriptFields.Children.Add(link);
            }
            EvidenceBoundary.Text = stage.EvidenceBoundary;ActionEffect.Text = stage.Effect;
            PalettePicker.SelectedValue = DesktopTheme.Current;
            RenderStatus();
        }
        finally { rendering = false; }
    }
    /// <summary>从真实计划和最近回执生成动作、状态及恢复出口。</summary>
    private void RenderStatus()
    {
        PackagePanel.IsEnabled = DiscoveryPanel.IsEnabled = DeploymentPanel.IsEnabled = OwnershipPanel.IsEnabled = !work.Busy;
        PrimaryAction.Content = work.Stage switch { "install" => CanContinueFromInstall() ? "选择产品环境 →" : "核对安装交接", "environment" => "只读发现环境", "ownership" => work.CanApply && work.ApplyOperation == "setup.legacy.transfer" ? "核对并移交 →" : "预览旧所有权", "preview" => work.CanApply ? "核对计划并继续 →" : "生成冻结预览", "apply" => work.Unresolved ? "查看回执与恢复 →" : LastTransferSucceeded() ? "继续部署预览 →" : LastApplySucceeded() ? "继续配置与启动 →" : "应用已核对计划…", "recover" => "核对恢复回执…", "launch" => "前往启动工程 →", _ => "前往连接核验 →" };
        if (NeedsOwnershipTransfer() && work.Stage is "preview" or "apply") PrimaryAction.Content = "先核对旧所有权 →";
        PrimaryAction.IsEnabled = !work.Busy && (work.Stage != "apply" || work.CanApply || work.Unresolved || LastApplySucceeded() || NeedsOwnershipTransfer()) && (work.Stage != "recover" || work.Receipt is not null);
        RestoreButton.IsEnabled = !work.Busy && work.Receipt is not null;
        PlanSummary.Text = work.Plan is null ? "尚无当前冻结计划；输入变化后必须重新预览。" : Friendly(work.Plan.Summary);
        PlanIdentity.Text = work.Plan is null ? "" : work.Plan.Kind + " · " + work.Plan.PlanId + "\nSHA256 " + work.Plan.Sha256;
        RecoverySummary.Text = work.Receipt is null ? work.Unresolved ? "本次没有可执行恢复引用。请展开完整诊断，按备份及原操作身份人工核查；不会自动重放。" : "尚无可恢复回执。旧所有权移交不会被当作可自动撤销的EVARS写入。" : PlanText(work.Receipt);
        StageStatus.Text = work.Unresolved ? "上次结果待核查；未自动重试。" : work.Plan is not null ? "已取得冻结计划，需人工核对并进入明确应用。" : work.Result is not null ? (work.Result.Success ? "最近一次调用成功；不推定其他阶段完成。" : "最近一次调用被拒绝或失败；原因见动作旁回执。") : "当前是阅读位置；尚未执行此阶段。";
        if (work.Result is { } result)
        {
            InlineResult.Text = result.OperationId + " · " + (result.Success ? "成功" : result.Error?.Code + " · " + result.Error?.Message);
            ResultBody.Text = JsonSerializer.Serialize(new { current = result, unresolved = work.UnresolvedResults }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            EvidenceResult.Text = InlineResult.Text + "\n" + (result.Data is { } data ? Friendly(data) : "无成功数据；保留错误，不推定未产生效果。");
        }
        if (notice is not null) InlineResult.Text = notice;
        if (work.Stage == "install" && CanContinueFromInstall()) ActionEffect.Text = "只进入环境选择；不会自动扫描或写入产品环境。";
        if (work.Stage == "apply") ActionEffect.Text = work.Unresolved ? "本次只查看回执和恢复出口，不再次写入。" : LastTransferSucceeded() ? "只进入部署与EVARS预览；所有权移交不等于部署完成。" : LastApplySucceeded() ? "只前往启动配置；不会自动创建工程进程。" : work.ApplyOperation switch { "setup.legacy.transfer" => "仅转移所选旧环境的所有权记录；本次不写EVARS。", "evars.apply" => "仅按冻结计划修改EVARS受管行并保留原始备份。", _ => "按冻结范围部署运行文件与EVARS；不启动AVEVA。" };
        if (NeedsOwnershipTransfer() && work.Stage is "preview" or "apply") ActionEffect.Text = "只进入旧所有权核对，保留当前环境与输入；不会自动移交或写入EVARS。";
    }
    /// <summary>仅把后端明确确认的旧所有权冲突转为核对入口；未知所有权仍要求人工核查。</summary>
    private bool NeedsOwnershipTransfer() => work.Result?.Error?.Code == "SETUP_OWNERSHIP_TRANSFER_REQUIRED";
    /// <summary>核验只对未改变的工具包根有效。</summary>
    private bool CanContinueFromInstall() => work.VerifiedPackageRoot is { } root && root == work.Draft.PackageRoot;
    /// <summary>所有权移交成功必须回到部署预览，不跳过EVARS配置。</summary>
    private bool LastTransferSucceeded() => work.Plan is null && work.Result is { Success: true, OperationId: "setup.legacy.transfer" };
    /// <summary>只把真实apply或transfer成功回执作为继续启动的依据。</summary>
    private bool LastApplySucceeded() => work.Plan is null && work.Result is { Success: true, OperationId: "deployment.apply" or "evars.apply" or "setup.legacy.transfer" };
    /// <summary>使用原生可换行文字呈现契约字段。</summary>
    private static void AddText(Panel panel, string title, string value)
    {
        panel.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 18) });
    }
    /// <summary>格式化明确确认和恢复时需要核对的计划身份。</summary>
    private static string PlanText(ManagerPlanReference plan) => plan.Kind + " · " + plan.PlanId + "\n" + Friendly(plan.Summary) + "\nSHA256 " + plan.Sha256;
    /// <summary>将摘要投影为可读业务字段，技术身份留在折叠诊断中。</summary>
    private static string Friendly(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return element.ToString();
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["file"] = "环境文件", ["runtimeRoot"] = "运行根", ["packageRoot"] = "工具包根", ["profile"] = "Host profile", ["framework"] = "Host框架", ["mode"] = "部署方式", ["environmentFile"] = "INIT / BAT", ["fileCount"] = "文件数", ["changedFiles"] = "变化文件", ["bootstrapToken"] = "引导身份", ["beforeHash"] = "修改前SHA", ["afterHash"] = "修改后SHA", ["managedSettings"] = "修改后受管行", ["beforeManagedSettings"] = "修改前受管行", ["installation"] = "安装身份", ["backupFile"] = "原始备份", ["nextAction"] = "下一步", ["integrationMode"] = "安装交接方式", ["version"] = "版本", ["filesVerified"] = "核验文件", ["legacyEnvironmentCount"] = "旧所有权记录" };
        return string.Join("\n", element.EnumerateObject().Where(p => !p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase) && !p.Name.Contains("Sha256", StringComparison.OrdinalIgnoreCase) && !new[] { "id", "planId" }.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).Select(p => (labels.TryGetValue(p.Name, out var label) ? label : p.Name) + "：" + (p.Value.ValueKind == JsonValueKind.Array ? string.Join("；", p.Value.EnumerateArray().Select(v => v.ToString())) : p.Value.ValueKind == JsonValueKind.Object ? Friendly(p.Value) : p.Value.ToString())));
    }
    /// <summary>打开原生目录选择器，不改变业务文件。</summary>
    private static string? Folder() { var dialog = new OpenFolderDialog();return dialog.ShowDialog() == true ? dialog.FolderName : null; }
    /// <summary>只填入用户明确选择的应用根。</summary>
    private void BrowsePackage(object sender, RoutedEventArgs e) { if (Folder() is { } path) PackageBox.Text = path; }
    /// <summary>只填入独立运行根。</summary>
    private void BrowseRuntime(object sender, RoutedEventArgs e) { if (Folder() is { } path) RuntimeBox.Text = path; }
    /// <summary>只填入有界发现的起始根。</summary>
    private void BrowseScan(object sender, RoutedEventArgs e) { if (Folder() is { } path) ScanBox.Text = path; }
    /// <summary>只选择本次环境文件，不自动解析或写入。</summary>
    private void BrowseEnvironment(object sender, RoutedEventArgs e) { var dialog = new OpenFileDialog { Filter = "厂商环境文件|*.bat;*.init;*.ini|全部文件|*.*" };if (dialog.ShowDialog() == true) EnvironmentBox.Text = dialog.FileName; }
    /// <summary>只选择待只读解释的迁移来源。</summary>
    private void BrowseMigration(object sender, RoutedEventArgs e) { var dialog = new OpenFileDialog { Filter = "迁移来源|*.json;*.xml;*.uic|全部文件|*.*" };if (dialog.ShowDialog() == true) MigrationBox.Text = dialog.FileName; }
}
