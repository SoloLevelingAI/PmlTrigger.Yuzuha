using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Desktop;

/// <summary>三条真实桌面流程，直接调用已有 C# 服务，保留错误与原始回执。</summary>
public partial class SessionWorkbenchView : UserControl, IDisposable
{
    private readonly PmlFunctionRegistry registry = new();
    private readonly McpWorkbenchHost host;
    private SessionWorkbench model;
    private CancellationTokenSource? pending;
    private bool initialized;
    private bool rendering;
    private bool smokeMode;
    private DesktopSmokeHost? smokeHost;
    private bool queryInputsDirty;
    private string? parameterIdentity;
    private string? formName, gadgetName;
    private List<ParameterRow> parameters = [];

    /// <summary>建立本机服务依赖；不会自动选择目标或执行查询。</summary>
    public SessionWorkbenchView()
    {
        host = new(registry); model = new(host, registry); InitializeComponent();
        Loaded += (_, _) => { if (initialized) return; initialized = true; DiscoverNow(); ReloadFunctions(); Render(); };
        SizeChanged += (_, _) => { StackNarrowColumns(SessionColumns); StackNarrowColumns(FunctionColumns); };
    }
    private void StackNarrowColumns(Grid grid)
    {
        var narrow = ActualWidth < 800;
        if (grid.RowDefinitions.Count == 0) { grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
        var cards = grid.Children.OfType<Border>().ToArray();
        if (cards.Length != 2) return;
        Grid.SetColumnSpan(cards[0], narrow ? 3 : 1); Grid.SetColumn(cards[1], narrow ? 0 : 2); Grid.SetRow(cards[1], narrow ? 1 : 0); Grid.SetColumnSpan(cards[1], narrow ? 3 : 1);
    }
    private CancellationToken Token => pending?.Token ?? CancellationToken.None;
    private void DiscoverNow()
    {
        try { var rows = model.Discover().Sessions.Select(c => new SessionRow(c)).ToArray(); SessionsGrid.ItemsSource = rows; CandidateCountText.Text = "全部 " + rows.Length; }
        catch (Exception error) { FeedbackText.Text = error.Message; }
    }
    private void DiscoverSessions(object sender, RoutedEventArgs e) { DiscoverNow(); Render(); }
    private async void SelectSession(object sender, RoutedEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not SessionRow row) { FeedbackText.Text = "请选择一个发现到的会话。"; return; }
        await Run(() => model.SelectAsync(row.Candidate, token: Token));
    }
    private async void VerifyTarget(object sender, RoutedEventArgs e) => await Run(() => model.VerifyAsync(Token));
    private void ShowFunctions(object sender, RoutedEventArgs e) => FlowTabs.SelectedIndex = 1;
    private void ShowQueries(object sender, RoutedEventArgs e) => FlowTabs.SelectedIndex = 2;
    private async Task Run(Func<Task> action)
    {
        if (pending is not null) return;
        pending = new(); FlowTabs.IsEnabled = false; CancelWaitButton.Visibility = Visibility.Visible;
        FeedbackText.Text = "正在处理，请等待结果。发送后的取消不能撤回远端操作。";
        try { await Task.Run(action); }
        catch (Exception error) { FeedbackText.Text = model.Message == "" ? error.Message : model.Message; }
        finally { pending.Dispose(); pending = null; FlowTabs.IsEnabled = true; CancelWaitButton.Visibility = Visibility.Collapsed; Render(); }
    }
    private void CancelWait(object sender, RoutedEventArgs e) { pending?.Cancel(); CancelWaitButton.IsEnabled = false; }
    private void BrowseFunction(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PML 全局函数|*.pmlfnc" };
        if (dialog.ShowDialog() == true) FunctionSourceBox.Text = dialog.FileName;
    }
    private void RegisterFunction(object sender, RoutedEventArgs e) => Local(() =>
    {
        var entry = model.Register(FunctionSourceBox.Text.Trim(), FunctionNameBox.Text.Trim()); ReloadFunctions(entry.Name);
        FeedbackText.Text = "已登记当前源码，许可已关闭。确认源码后可人工授予；登记不会加载 PML。";
    });
    private void RefreshFunctions(object sender, RoutedEventArgs e) => Local(() => ReloadFunctions());
    private void ExpandRegistration(object sender, RoutedEventArgs e) { RegistrationPanel.IsExpanded = true; FunctionSourceBox.Focus(); }
    private FunctionRow SelectedFunction() => FunctionsGrid.SelectedItem as FunctionRow ?? throw new InvalidOperationException("请先选择一个已登记的函数。");
    private void GrantFunction(object sender, RoutedEventArgs e) => ChangePermission(true);
    private void RevokeFunction(object sender, RoutedEventArgs e) => ChangePermission(false);
    private void ChangePermission(bool allow) => Local(() =>
    {
        var entry = SelectedFunction().Entry;
        if (entry.Name != FunctionNameBox.Text.Trim() || !string.Equals(entry.SourceFile, FunctionSourceBox.Text.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("源码或函数输入已变化，请先登记或重新选择注册项。");
        var name = entry.Name; model.SetPermission(name, allow); ReloadFunctions(name);
        FeedbackText.Text = allow ? "已授予当前源码版本调用许可；执行前仍核验目标与源码。" : "已撤销新的注册调用许可；已发出的操作不能撤回。";
    });
    private void ReloadFunctions(string? selectedName = null)
    {
        var name = selectedName ?? (FunctionsGrid.SelectedItem as FunctionRow)?.Entry.Name;
        var rows = model.Functions().Select(entry => new FunctionRow(entry)).ToArray(); FunctionsGrid.ItemsSource = rows;
        if (name is not null) FunctionsGrid.SelectedItem = rows.FirstOrDefault(r => r.Entry.Name == name);
    }
    private void FunctionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (FunctionsGrid.SelectedItem is not FunctionRow row) return;
        FunctionNameBox.Text = row.Entry.Name; FunctionSourceBox.Text = row.Entry.SourceFile;
        InvocationNameText.Text = row.Entry.Name + " · " + row.Signature;
        FunctionSourceText.Text = row.Entry.SourceFile;
        FunctionHashText.Text = "登记 SHA256\n" + row.Entry.SourceSha256[..16] + "…\n\n保存许可：" + row.Permission + "\n" + row.Identity;
        var identity = row.Entry.Name + "|" + row.Entry.SourceSha256;
        var previous = parameterIdentity == identity ? parameters : [];
        parameterIdentity = identity;
        parameters = row.Entry.ParameterTypes.Select((type, i) => new ParameterRow { Type = type, Label = $"参数 {i + 1} · {type}", Value = previous.ElementAtOrDefault(i)?.Type == type ? previous[i].Value : "" }).ToList();
        ParametersList.ItemsSource = parameters; CallPreviewBox.Clear(); SourceDetailsBox.Visibility = Visibility.Collapsed;
    }
    private void InspectFunction(object sender, RoutedEventArgs e) => Local(() =>
    {
        var entry = SelectedFunction().Entry; var info = new FileInfo(entry.SourceFile);
        if (info.Length > 1024 * 1024) throw new InvalidDataException("源码超过登记支持的 1 MiB，请在编辑器中检查。");
        var bytes = File.ReadAllBytes(entry.SourceFile);
        SourceDetailsBox.Text = $"源码：{entry.SourceFile}\n登记 SHA256：{entry.SourceSha256}\n当前 SHA256：{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}\n登记时未保存旧正文；历史差异需定位 Git 版本，不能凭哈希还原。\n\n{System.Text.Encoding.UTF8.GetString(bytes)}";
        SourceDetailsBox.Visibility = Visibility.Visible;
    });
    private List<DynamicParameterInput> Inputs() => parameters.Select(p => new DynamicParameterInput { Type = p.Type, Value = p.Value }).ToList();
    private void PreviewFunction(object sender, RoutedEventArgs e) => Local(() => { CallPreviewBox.Text = model.Preview(SelectedFunction().Entry.Name, Inputs()); FeedbackText.Text = "源码、许可与参数已核对；这里只生成调用文本。"; });
    private async void InvokeFunction(object sender, RoutedEventArgs e)
    {
        try { var name = SelectedFunction().Entry.Name; var values = Inputs(); await Run(() => model.InvokeAsync(name, values, Token)); ReloadFunctions(name); }
        catch (Exception error) { FeedbackText.Text = error.Message; }
    }
    private (int Items, int Characters) Budget() => (int.Parse(ItemsBudgetBox.Text), int.Parse(CharsBudgetBox.Text));
    private PmlFormQueryRequest Query(string kind, string? form = null, string? gadget = null)
    {
        var budget = Budget();
        return new() { Kind = kind, FormName = form, GadgetName = gadget, StartIndex = 1, MaxItems = kind == "gadget" ? 1 : budget.Items, MaxCharacters = budget.Characters };
    }
    private async Task ReadFormsCore(bool append)
    {
        try { var query = Query(ShownOnlyBox.IsChecked == true ? "shown-forms" : "forms"); queryInputsDirty = false; await Run(() => model.QueryAsync(query, append, Token)); }
        catch (Exception error) { FeedbackText.Text = error.Message; }
    }
    private async void ReadForms(object sender, RoutedEventArgs e) => await ReadFormsCore(false);
    private async void ContinueForms(object sender, RoutedEventArgs e) => await ReadFormsCore(true);
    private void FormSelected(object sender, SelectionChangedEventArgs e)
    {
        if (rendering) return;
        var next = (FormsGrid.SelectedItem as PmlFormQueryItem)?.Name;
        if (next == formName) return;
        formName = next; gadgetName = null; model.ResetGadgets();
        SelectedFormText.Text = next is null ? "先选择窗体。" : "窗体：" + next;
        SelectedGadgetText.Text = "先选择控件。"; GadgetsGrid.ItemsSource = null; PropertiesGrid.ItemsSource = null;
        ContinueGadgetsButton.IsEnabled = false;
        ReceiptBox.Text = "窗体选择已变化，请读取所选窗体的控件。";
    }
    private async Task ReadGadgetsCore(bool append)
    {
        try
        {
            if (formName is null) throw new InvalidOperationException("请先在窗体列表中选择一个已加载的窗体。");
            var request = Query("gadgets", formName); queryInputsDirty = false; await Run(() => model.QueryAsync(request, append, Token));
        }
        catch (Exception error) { FeedbackText.Text = error.Message; }
    }
    private async void ReadGadgets(object sender, RoutedEventArgs e) => await ReadGadgetsCore(false);
    private async void ContinueGadgets(object sender, RoutedEventArgs e) => await ReadGadgetsCore(true);
    private void GadgetSelected(object sender, SelectionChangedEventArgs e) { if (rendering) return; gadgetName = (GadgetsGrid.SelectedItem as PmlFormQueryItem)?.Name; model.ResetGadgetDetails(); SelectedGadgetText.Text = gadgetName is null ? "先选择控件。" : $"{formName}.{gadgetName}"; PropertiesGrid.ItemsSource = null; ReceiptBox.Text = "控件选择已变化，请读取所选控件的属性。"; }
    private async void ReadGadget(object sender, RoutedEventArgs e)
    {
        try
        {
            if (formName is null || gadgetName is null) throw new InvalidOperationException("请先选择窗体与控件。");
            var query = Query("gadget", formName, gadgetName); queryInputsDirty = false; await Run(() => model.QueryAsync(query, token: Token));
        }
        catch (Exception error) { FeedbackText.Text = error.Message; }
    }
    private async Task ReadArrayCore(bool append)
    {
        try { var budget = Budget(); var name = ArrayNameBox.Text.Trim(); queryInputsDirty = false; await Run(() => model.ReadArrayAsync(name, budget.Items, budget.Characters, append, Token)); }
        catch (Exception error) { FeedbackText.Text = error.Message; }
    }
    private async void ReadArray(object sender, RoutedEventArgs e) => await ReadArrayCore(false);
    private async void ContinueArray(object sender, RoutedEventArgs e) => await ReadArrayCore(true);
    private void QueryInputsChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        ContinueFormsButton.IsEnabled = ContinueGadgetsButton.IsEnabled = ContinueArrayButton.IsEnabled = false;
        queryInputsDirty = true; FeedbackText.Text = (model.Outcome == "unknown" ? model.Message + "\n" : "") + "查询条件已变化。下方保留上次返回项；请开始新查询。";
    }
    private void Render()
    {
        var status = model.Status;
        TargetDisplayName = status.TargetVerified ? SessionsGrid.Items.OfType<SessionRow>().FirstOrDefault(row => row.Candidate.ProcessId == status.Host!.ProcessId && row.Candidate.ProcessStartTimeUtcTicks == status.Host.ProcessStartTimeUtcTicks)?.Candidate.Product ?? "AVEVA 工程会话" : "尚未选择 AVEVA 工程";
        TargetText.Text = (smokeMode ? "UI 合成夹具 · " : "") + (status.TargetVerified ? $"已核验 · PID {status.Host!.ProcessId} · {status.Host.Model}" : "尚未选择并核验目标");
        TargetDetailsText.Text = status.TargetVerified ? $"启动 {new DateTime(status.Host!.ProcessStartTimeUtcTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm:ss}\n{status.Host.HostFramework} / Host {status.Host.HostVersion}\nFormQuery={status.Host.FormQueryVersion} · BoundedArrayRead={status.Host.BoundedArrayReadVersion}" : "请发现、选择并核验；旧查询不能作为当前结果。";
        TargetChanged?.Invoke(TargetText.Text);
        FunctionTargetText.Text = TargetText.Text; IdentityStateText.Text = status.TargetVerified ? "已核验" : "待核验";
        FeedbackText.Text = queryInputsDirty && FlowTabs.SelectedIndex == 2 && model.Outcome is not ("unknown" or "rejected") ? "查询条件已变化。下方保留上次返回项；请开始新查询。" : model.Message; CancelWaitButton.IsEnabled = true;
        FeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(model.Outcome == "unknown" ? "#8A5A12" : model.Outcome == "rejected" ? "#C0261B" : "#0F5F56"));
        FeedbackPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(model.Outcome == "unknown" ? "#FDF6E7" : model.Outcome == "rejected" ? "#FEF3F2" : "#E9F7F3"));
        rendering = true;
        try { BindGrid(FormsGrid, model.Forms.Items); BindGrid(GadgetsGrid, model.Gadgets.Items); }
        finally { rendering = false; }
        formName = (FormsGrid.SelectedItem as PmlFormQueryItem)?.Name;
        gadgetName = (GadgetsGrid.SelectedItem as PmlFormQueryItem)?.Name;
        SelectedFormText.Text = formName is null ? "先选择窗体。" : "窗体：" + formName;
        SelectedGadgetText.Text = gadgetName is null ? "先选择控件。" : $"{formName}.{gadgetName}";
        FormsWindowText.Text = Describe(model.Forms.Last); GadgetsWindowText.Text = Describe(model.Gadgets.Last);
        PropertiesGrid.ItemsSource = model.GadgetDetails?.FormQueryResult?.Items?.FirstOrDefault()?.Properties?.Select(p => new { Label = p.Name + "\n" + p.Status, Value = p.Status == "available" ? p.Value == "" ? "（空文本）" : p.Value : "不可用：" + p.Error }).ToArray();
        var validItems = int.TryParse(ItemsBudgetBox.Text, out var itemBudget);
        var validCharacters = int.TryParse(CharsBudgetBox.Text, out var characterBudget);
        var validBudget = validItems && validCharacters;
        var suffix = $"|{itemBudget}|{characterBudget}";
        ContinueFormsButton.IsEnabled = validBudget && model.Forms.NextIndex != 0 && model.Forms.Key == $"{(ShownOnlyBox.IsChecked == true ? "shown-forms" : "forms")}||" + suffix;
        ContinueGadgetsButton.IsEnabled = validBudget && model.Gadgets.NextIndex != 0 && model.Gadgets.Key == $"gadgets|{formName}|" + suffix;
        ContinueArrayButton.IsEnabled = validBudget && model.CanContinueArray(ArrayNameBox.Text.Trim(), itemBudget, characterBudget);
        ArrayWindowText.Text = model.ArrayLast is { } a ? $"本页 {a.StartIndex} 起 · 上界 {a.IndexUpperBound}（非条目数）· 字符 {a.Characters} · NextIndex {a.NextIndex}" : "尚未读取。索引上界不是条目总数。";
        ArrayRowsBox.Text = string.Join(Environment.NewLine, model.ArrayRows.Select((v, i) => $"[{i + 1}] {v}"));
        ReceiptBox.Text = model.LastResponse is null ? "本次尚无可用回执。" : JsonSerializer.Serialize(model.LastResponse, new JsonSerializerOptions { WriteIndented = true });
    }
    private static void BindGrid(DataGrid grid, List<PmlFormQueryItem> values)
    {
        var name = (grid.SelectedItem as PmlFormQueryItem)?.Name; grid.ItemsSource = values.ToArray();
        if (name is not null) grid.SelectedItem = values.FirstOrDefault(i => i.Name == name);
    }
    private static string Describe(PmlFormQueryResult? r) => r is null ? "尚未读取" : $"本页起点 {r.StartIndex} · 返回 {r.Items?.Length ?? 0}\n扫描 {r.Scanned} · 索引上界 {r.IndexUpperBound}\n字符 {r.Characters} · NextIndex {r.NextIndex}\n" + (r.NextIndex > 0 ? "仍有后续；空页也可继续。" : "本次枚举到达末端。");
    private void Local(Action action) { try { action(); } catch (Exception error) { FeedbackText.Text = error.Message + "\n请核对源码或注册表后重试。"; FeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(192,38,27)); } }
    /// <summary>释放本窗口资源；不关闭 AVEVA 或保存工程。</summary>
    public void Dispose() { pending?.Cancel(); host.Dispose(); }
    /// <summary>窗口关闭前检查是否仍在等待操作结果。</summary>
    internal bool IsRunning => pending is not null;
    /// <summary>从总览进入函数二级入口。</summary>
    internal void OpenFunctions() => FlowTabs.SelectedIndex = 1;
    /// <summary>从总览进入原生查询入口。</summary>
    internal void OpenQueries() => FlowTabs.SelectedIndex = 2;
    /// <summary>复用当前页已有操作，供全局主按钮使用。</summary>
    internal void PrimaryAction()
    {
        if (pending is not null) { FeedbackText.Text = "当前操作尚未结束，请先等待结果或取消等待。"; return; }
        if (FlowTabs.SelectedIndex == 1) ExpandRegistration(this, new());
        else if (FlowTabs.SelectedIndex == 2) VerifyTarget(this, new());
        else DiscoverSessions(this, new());
    }
    /// <summary>通知窗口更新当前任务标题。</summary>
    internal event Action<string>? SectionChanged;
    /// <summary>通知全局侧栏保留当前目标摘要。</summary>
    internal event Action<string>? TargetChanged;
    /// <summary>与已核验启动身份匹配的产品名称，用于总览横幅。</summary>
    internal string TargetDisplayName { get; private set; } = "尚未选择 AVEVA 工程";
    private void FlowChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, FlowTabs)) return;
        SectionChanged?.Invoke(FlowTabs.SelectedIndex switch { 1 => "能力与函数", 2 => "原生 Form / Gadget", _ => "工程会话" });
    }
    private void QueryCardsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var columns = Math.Clamp((int)(e.NewSize.Width / 354), 1, 3);
        foreach (var border in QueryCardsPanel.Children.OfType<Border>()) border.Width = Math.Max(260, e.NewSize.Width / columns - 14);
    }
    /// <summary>在隔离目录中走通同一状态与绑定路径，输出只能作为离线界面证据。</summary>
    internal async Task PrepareSmokeAsync(string root)
    {
        Directory.CreateDirectory(root);
        var smokeRegistry = new PmlFunctionRegistry(Path.Combine(root, "functions.json"));
        smokeHost = new(); model = new(smokeHost, smokeRegistry); smokeMode = true;
        var candidate = model.Discover().Sessions.Single(); SessionsGrid.ItemsSource = new[] { new SessionRow(candidate) }; SessionsGrid.SelectedIndex = 0;
        CandidateCountText.Text = "全部 1 · UI 合成夹具";
        await Run(() => model.SelectAsync(candidate));
        var source = Path.Combine(root, "UiProbe.pmlfnc");
        File.WriteAllText(source, "define function !!UiProbe(!name is STRING) is STRING\nreturn !name\nendfunction");
        FunctionSourceBox.Text = source; FunctionNameBox.Text = "UiProbe"; RegisterFunction(this, new());
        GrantFunction(this, new()); parameters[0].Value = "sample";
        await Run(() => model.QueryAsync(new() { Kind = "shown-forms", StartIndex = 1, MaxItems = 100, MaxCharacters = 32768 }));
        FormsGrid.SelectedIndex = 0;
        await Run(() => model.QueryAsync(new() { Kind = "gadgets", FormName = "UiProbeForm", StartIndex = 1, MaxItems = 100, MaxCharacters = 32768 }));
        GadgetsGrid.SelectedIndex = 0;
        await Run(() => model.QueryAsync(new() { Kind = "gadget", FormName = "UiProbeForm", GadgetName = "accept", StartIndex = 1, MaxItems = 1, MaxCharacters = 32768 }));
        if (FormsGrid.Items.Count != 1 || GadgetsGrid.Items.Count != 1 || PropertiesGrid.Items.Count != 5) throw new InvalidOperationException("界面夹具没有正确绑定窗体、控件与属性。");
        Render();
    }
    /// <summary>仅界面验证按序展示三个流程页。</summary>
    internal void SelectSmokeFlow(int index) => FlowTabs.SelectedIndex = index;
    /// <summary>只在合成模式验证拒绝与未知反馈，不执行 AVEVA。</summary>
    internal async Task FaultSmokeAsync(bool unknown)
    {
        if (!smokeMode || smokeHost is null) throw new InvalidOperationException("故障夹具仅限隔离界面验证。");
        if (unknown)
        {
            File.WriteAllText(FunctionSourceBox.Text, "define function !!UiProbe(!name is STRING) is STRING\nreturn !name\nendfunction");
            model.Register(FunctionSourceBox.Text, "UiProbe"); model.SetPermission("UiProbe", true); smokeHost.LoseInvocationReply = true;
        }
        else File.AppendAllText(FunctionSourceBox.Text, "\n-- synthetic source changed");
        FlowTabs.SelectedIndex = 1;
        await Run(() => model.InvokeAsync("UiProbe", [new() { Type = "string", Value = "sample" }], Token));
        ReloadFunctions("UiProbe");
        if (model.Outcome != (unknown ? "unknown" : "rejected")) throw new InvalidOperationException("故障状态未正确显示。");
    }
    /// <summary>发现候选的只读界面行；不代表已连接。</summary>
    private sealed record SessionRow(AvevaSessionCandidate Candidate)
    {
        /// <summary>产品和项目的界面摘要。</summary>
        public string Display => Candidate.Product + " · " + Candidate.Project;
        /// <summary>候选进程启动时间，转换为本机显示。</summary>
        public string Started => new DateTime(Candidate.ProcessStartTimeUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        /// <summary>区分发现管道与身份核验成功。</summary>
        public string HostLabel => Candidate.PipeDetected ? "已发现，待核验" : "未发现 Host";
    }
    /// <summary>登记项的许可、签名和当前源码哈希视图。</summary>
    private sealed record FunctionRow(PmlFunctionRegistration Entry)
    {
        /// <summary>保持登记参数顺序并显示返回类型。</summary>
        public string Signature => string.Join(", ", Entry.ParameterTypes) + " → " + Entry.ReturnType;
        /// <summary>显示保存的调用许可，不代替当前可调用性。</summary>
        public string Permission => Entry.AllowInvoke ? "允许" : "拒绝";
        /// <summary>本次列表刷新时的源码身份检查。</summary>
        public string Identity { get; } = ReadIdentity(Entry);
        private static string ReadIdentity(PmlFunctionRegistration entry)
        {
            try { return new FileInfo(entry.SourceFile).Length <= 1024 * 1024 && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry.SourceFile))).Equals(entry.SourceSha256, StringComparison.OrdinalIgnoreCase) ? "源码哈希匹配" : "源码变化 · 调用拒绝"; }
            catch (Exception error) { return "无法读取：" + error.Message; }
        }
    }
    /// <summary>按登记签名顺序编辑的单个标量参数。</summary>
    private sealed class ParameterRow
    {
        /// <summary>登记声明中的参数类型。</summary>
        public string Type { get; init; } = "";
        /// <summary>参数序号和类型提示。</summary>
        public string Label { get; init; } = "";
        /// <summary>用户输入的标量文本，执行前由既有生成器校验。</summary>
        public string Value { get; set; } = "";
    }
}
