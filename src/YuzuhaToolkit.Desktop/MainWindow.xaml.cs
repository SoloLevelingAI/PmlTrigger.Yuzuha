using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>同一窗口管理启动、插件目录和本机 AI 工具活动；后台扫描不阻塞 UI。</summary>
public partial class MainWindow : Window
{
    private readonly WorkspaceSettings _settings;
    private readonly RuntimeJournal _journal = new("Yuzuha 原生启动台");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private DeploymentPlan? _plan;
    private bool _refreshing;
    private bool _closed;
    private bool _copying;
    private LaunchProfile? _selected;

    /// <summary>读取已有无密码配置，建立状态轮询；不自动启动 AVEVA 或复制插件。</summary>
    public MainWindow()
    {
        InitializeComponent(); _settings = WorkspaceSettings.Load();
        SessionView.SectionChanged += title => { if (Tabs.SelectedIndex == 3) SetPage(title); };
        SessionView.TargetChanged += target => { SidebarTargetText.Text = target; OverviewTargetText.Text = SessionView.TargetDisplayName; OverviewSessionText.Text = target; OverviewTargetDetail.Text = target; };
        MaintainerText.Text = Environment.UserName; SetPage("总览");
        RegisteredBox.Text = _settings.RegisteredPmlLib; SourceBox.Text = _settings.PluginSource; PluginBox.Text = _settings.PluginName;
        WarnFilesBox.Text = _settings.WarnFiles.ToString(); WarnSizeBox.Text = (_settings.WarnBytes / 1048576).ToString();
        ReloadProfiles(); if (_settings.Profiles.Count > 0) ProfilesBox.SelectedIndex = 0;
        _timer.Tick += async (_, _) => await RefreshRuntimeAsync(); _timer.Start();
        Loaded += async (_, _) => { await RefreshRuntimeAsync(); await DiscoverAsync(); };
        Closed += (_, _) => { _closed = true; _timer.Stop(); SessionView.Dispose(); _journal.Dispose(); };
    }

    private void Say(string message) => MessageText.Text = message;
    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (HeartbeatText is not null) HeartbeatText.Visibility = ActualWidth < 1100 ? Visibility.Collapsed : Visibility.Visible;
        if (SearchPanel is not null) SearchPanel.Visibility = ActualWidth < 1250 ? Visibility.Collapsed : Visibility.Visible;
        if (OverviewKpis is not null)
        {
            var narrow = ActualWidth < 1100; OverviewKpis.Columns = narrow ? 2 : 4;
            foreach (var grid in new[] { OverviewColumns1, OverviewColumns2, OverviewColumns3, LaunchColumns, LaunchLowerColumns })
            {
                if (grid.RowDefinitions.Count == 0) { grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
                var cards = grid.Children.OfType<Border>().ToArray();
                Grid.SetColumnSpan(cards[0], narrow ? 3 : 1); Grid.SetColumn(cards[1], narrow ? 0 : 2); Grid.SetRow(cards[1], narrow ? 1 : 0); Grid.SetColumnSpan(cards[1], narrow ? 3 : 1);
            }
        }
    }
    private void SetPage(string? title)
    {
        title = title?.Trim() ?? "总览"; PageTitleText.Text = title;
        PageSubtitleText.Text = title switch { "总览" => "本机工作台 · 工程环境与近期工具活动", "工程会话" => "先核验 PID 与启动身份，再查询或调用 PML", "能力与函数" => "登记源码、人工授权 · 登记版本与会话加载版本分别核对", "原生 Form / Gadget" => "原生窗体 → 控件 → 属性 · 有界读取与续读", "启动工程" => "复用已有启动配置 · 本次密码不保存", "插件部署" => "来源、复制预览、备份与本机记录", _ => "本机工具与交付记录" };
        HeaderActionText.Text = title switch { "工程会话" => "扫描会话", "能力与函数" => "登记函数", "原生 Form / Gadget" => "重新核验", _ => "启动工程" };
        foreach (var button in SidebarNav.Children.OfType<Button>())
        {
            var selected = button.Uid == (title is "能力与函数" or "原生 Form / Gadget" ? "工程会话" : title);
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected ? "#103B3D" : "#0B2D30"));
            button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected ? "#F2F8F4" : "#9DBDB7"));
            button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDBD62"));
            button.BorderThickness = new Thickness(selected ? 3 : 0, 0, 0, 0);
        }
    }
    private void HeaderAction(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedIndex == 3) SessionView.PrimaryAction();
        else { Tabs.SelectedIndex = 0; SetPage("启动工程"); }
    }
    private void ShowHelp(object sender, RoutedEventArgs e) => MessageBox.Show(this, "工程会话：扫描 → 选择 → 核验目标。\n能力与函数：登记源码 → 检查源码 → 授予许可 → 预览 → 执行一次。\n原生查询：窗体 → 控件 → 固定属性；使用 NextIndex 继续读取。\n\n源码变化后重新登记和授权；结果未知时先核查回执，不自动重试。\nCtrl+K 可按页面名称导航；所有界面证据与实机验证分别记录。", "Yuzuha · 使用说明");
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control) { SearchPanel.Visibility = Visibility.Visible; NavigationSearch.Focus(); e.Handled = true; }
    }
    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var name = NavigationSearch.Text.Trim();
        var button = SidebarNav.Children.OfType<Button>().FirstOrDefault(b => name.Length > 0 && b.Uid.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (button is not null) { Navigate(button, new()); NavigationSearch.Clear(); } else Say("未找到页面。可输入：工程会话、启动工程、工具日志等。");
        e.Handled = true;
    }
    private void OpenLogsPage(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 2; SetPage("工具日志"); }
    private void PageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Tabs) && SidebarNav is not null && Tabs.SelectedItem is TabItem tab) SetPage(tab.Header?.ToString());
    }
    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (int.TryParse(button.Tag?.ToString(), out var index)) Tabs.SelectedIndex = index;
        else
        {
            Tabs.SelectedIndex = 5; PlannedTitleText.Text = button.Uid;
            PlannedDescriptionText.Text = button.Uid switch
            {
                "评审中心" => "M3：同一名人工审核差异、契约和测试证据，再批准迁移。复用已有复制、哈希和备份能力，补批准快照、回执与恢复记录。",
                "测试中心" => "M2a / M2b：先交付源码目录与证据浏览，再从 C# 调用既有按 ID 测试脚本。现阶段可使用 DocFX 测试目录与 PowerShell 独立复刻；人工实机项保持 NotRun。",
                "Web 应用" => "M5a：C# 管理本机 HTML 应用目录、版本、入口与停用记录。M5b 再验证宿主并开放受控查询桥；窗口打开与调用权限分别表达。",
                _ => "知识检索已有独立工具入口。本批保留其使用方式；来源、经验与验证证据的新界面按后续设计交付。"
            };
        }
        SetPage(button.Uid);
    }
    private void OpenFunctionsPage(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 3; SessionView.OpenFunctions(); SetPage("能力与函数"); }
    private void OpenQueriesPage(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 3; SessionView.OpenQueries(); SetPage("原生 Form / Gadget"); }
    private void ReloadProfiles() { ProfilesBox.ItemsSource = null; ProfilesBox.ItemsSource = _settings.Profiles; }
    private LaunchProfile ReadProfile() => new()
    {
        Name = NameBox.Text.Trim(), Kind = ((ComboBoxItem)KindBox.SelectedItem).Content.ToString()!, Executable = ExecutableBox.Text.Trim(),
        Initialization = InitializationBox.Text.Trim(), Product = ProductBox.Text.Trim(), WorkingDirectory = WorkingBox.Text.Trim(),
        Project = ProjectBox.Text.Trim(), User = UserBox.Text.Trim(), Mdb = MdbBox.Text.Trim(), Module = ModuleBox.Text.Trim(), License = LicenseBox.Text.Trim(),
        Environment = _selected?.Environment ?? new(StringComparer.OrdinalIgnoreCase)
    };
    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProfilesBox.SelectedItem is not LaunchProfile profile) return; _selected = profile;
        NameBox.Text = profile.Name; KindBox.SelectedIndex = profile.Kind == "MarineBatch" ? 1 : 0;
        ExecutableBox.Text = profile.Executable; InitializationBox.Text = profile.Initialization; ProductBox.Text = profile.Product;
        WorkingBox.Text = profile.WorkingDirectory; ProjectBox.Text = profile.Project; UserBox.Text = profile.User;
        MdbBox.Text = profile.Mdb; ModuleBox.Text = profile.Module; LicenseBox.Text = profile.License; PasswordInput.Clear();
    }
    private void NewProfile(object sender, RoutedEventArgs e)
    {
        var profile = new LaunchProfile(); _settings.Profiles.Add(profile); ReloadProfiles(); ProfilesBox.SelectedItem = profile;
    }
    private void SaveProfile(object sender, RoutedEventArgs e) => Try(() =>
    {
        var profile = ReadProfile();
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ArgumentException("请填写配置名称。");
        var index = _selected is null ? -1 : _settings.Profiles.IndexOf(_selected);
        if (index < 0) _settings.Profiles.Add(profile); else _settings.Profiles[index] = profile;
        SaveWorkspaceFields(); ReloadProfiles(); ProfilesBox.SelectedItem = profile; Say("已保存本机配置，密码未保存。");
    });
    private void PreviewLaunch(object sender, RoutedEventArgs e) => Try(() => LaunchPreviewBox.Text = LaunchService.Preview(ReadProfile()));
    private async void Launch(object sender, RoutedEventArgs e)
    {
        if (_copying) { Say("插件正在复制，请完成后再启动工程。"); return; }
        LaunchButton.IsEnabled = false;
        try
        {
            var profile = ReadProfile(); SaveWorkspaceFields();
            if (!string.IsNullOrWhiteSpace(_settings.RegisteredPmlLib))
            {
                var health = await ScanAsync();
                if (_closed) return;
                if (health.NeedsAttention && MessageBox.Show(FormatHealth(health) + "\n\n建议先移出旧插件、备份和开发副本。仍然启动吗？", "PMLLIB 清理提醒", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            var start = LaunchService.Build(profile, PasswordInput.Password);
            var id = _journal.Begin("launch_aveva", JsonSerializer.Serialize(new { profile = profile.Name, executable = profile.Executable }));
            try
            {
                using var process = Process.Start(start) ?? throw new IOException("未创建启动入口进程。");
                _journal.Complete(id, "succeeded", "启动入口进程 PID " + process.Id);
                Say("启动入口已创建，PID " + process.Id + "。工程会话就绪后可由 MCP 发现并选择；入口进程创建不等于登录完成。");
            }
            catch (Exception error) { _journal.Complete(id, "failed", error.Message); throw; }
        }
        catch (Exception error) { Say(error.Message); }
        finally { PasswordInput.Clear(); LaunchButton.IsEnabled = true; }
    }

    private async Task DiscoverAsync()
    {
        try
        {
            var candidates = await Task.Run(RegistrationDiscovery.Discover);
            if (_closed) return;
            ApplicationsBox.ItemsSource = candidates.Where(candidate => candidate.Kind == "application").ToArray();
            RegistrationsBox.ItemsSource = candidates.Where(candidate => candidate.Kind == "pmllib").ToArray();
            if (RegistrationsBox.Items.Count > 0) RegistrationsBox.SelectedIndex = 0;
            if (string.IsNullOrWhiteSpace(RegisteredBox.Text) && RegistrationsBox.Items.Count == 1) RegisteredBox.Text = ((RegistrationCandidate)RegistrationsBox.Items[0]).Path;
            Say($"发现 {ApplicationsBox.Items.Count} 个产品入口、{RegistrationsBox.Items.Count} 个字面注册目录候选。未更改环境。");
        }
        catch (Exception error) { Say("发现失败：" + error.Message); }
    }
    private async void Discover(object sender, RoutedEventArgs e) => await DiscoverAsync();
    private void UseApplication(object sender, RoutedEventArgs e)
    {
        if (ApplicationsBox.SelectedItem is not RegistrationCandidate candidate) return;
        ExecutableBox.Text = candidate.Path; KindBox.SelectedIndex = Path.GetExtension(candidate.Path).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        WorkingBox.Text = Path.GetDirectoryName(candidate.Path)!;
        var initialization = Path.Combine(WorkingBox.Text, "launch.init"); if (File.Exists(initialization)) InitializationBox.Text = initialization;
        Say("已填入所选入口；产品代码、项目、用户及初始化文件请按实际配置保存。");
    }
    private void UseRegistration(object sender, RoutedEventArgs e) { if (RegistrationsBox.SelectedItem is RegistrationCandidate candidate) RegisteredBox.Text = candidate.Path; }
    private static string? Folder() { var dialog = new OpenFolderDialog(); return dialog.ShowDialog() == true ? dialog.FolderName : null; }
    private static string? FilePath(string filter) { var dialog = new OpenFileDialog { Filter = filter }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    private void BrowseExecutable(object sender, RoutedEventArgs e) { var file = FilePath("AVEVA 入口|*.exe;*.bat;*.cmd"); if (file is not null) ExecutableBox.Text = file; }
    private void BrowseInitialization(object sender, RoutedEventArgs e) { var file = FilePath("初始化文件|*.init;*.bat|所有文件|*.*"); if (file is not null) InitializationBox.Text = file; }
    private void BrowseWorkingDirectory(object sender, RoutedEventArgs e) { var folder = Folder(); if (folder is not null) WorkingBox.Text = folder; }
    private void BrowseRegistered(object sender, RoutedEventArgs e) { var folder = Folder(); if (folder is not null) RegisteredBox.Text = folder; }
    private void BrowseSourceFolder(object sender, RoutedEventArgs e) { var folder = Folder(); if (folder is not null) SourceBox.Text = folder; }
    private void BrowseSourceFile(object sender, RoutedEventArgs e) { var file = FilePath("PML、插件与 ZIP|*.zip;*.pmlobj;*.pmlfnc;*.pmlfrm;*.pmlcmd;*.dll|所有文件|*.*"); if (file is not null) SourceBox.Text = file; }
    private void SaveWorkspaceFields()
    {
        _settings.RegisteredPmlLib = RegisteredBox.Text.Trim(); _settings.PluginSource = SourceBox.Text.Trim(); _settings.PluginName = PluginBox.Text.Trim();
        _settings.WarnFiles = int.Parse(WarnFilesBox.Text); _settings.WarnBytes = checked(long.Parse(WarnSizeBox.Text) * 1048576); _settings.Save();
    }
    private void SaveWorkspace(object sender, RoutedEventArgs e) => Try(() => { SaveWorkspaceFields(); Say("已保存目录设置；未修改 EVAR 或注册表。"); });
    private async Task<DirectoryHealth> ScanAsync()
    {
        var root = RegisteredBox.Text.Trim(); var files = int.Parse(WarnFilesBox.Text); var bytes = checked(long.Parse(WarnSizeBox.Text) * 1048576);
        var health = await Task.Run(() => DirectoryInspector.Scan(root, files, bytes));
        var text = FormatHealth(health); HealthText.Text = text; LaunchHealthText.Text = text;
        OverviewFilesText.Text = health.Files.ToString("N0") + " 个文件"; OverviewSizeText.Text = (health.Bytes / 1048576d).ToString("N1") + " MiB · " + (health.Complete ? "完整扫描" : "部分扫描");
        HealthText.Foreground = health.NeedsAttention ? new SolidColorBrush(Color.FromRgb(164, 94, 12)) : new SolidColorBrush(Color.FromRgb(30, 118, 91)); return health;
    }
    private static string FormatHealth(DirectoryHealth health) => $"{(health.Complete ? "完整扫描" : "部分扫描")} · {health.Files:N0} 个文件 · {health.PmlFiles:N0} 个 PML 文件 · {health.Bytes / 1048576d:N1} MiB\n"
        + (health.Warnings.Length > 0 ? string.Join("\n", health.Warnings) : "未达到当前清理提醒预算。")
        + "\n大目录 / 文件：" + string.Join("；", health.LargestFolders.Take(5).Select(pair => pair.Key + " " + (pair.Value / 1048576d).ToString("N1") + " MiB"));
    private async void ScanDirectory(object sender, RoutedEventArgs e) { try { await ScanAsync(); } catch (Exception error) { Say(error.Message); } }
    private void OpenRegistered(object sender, RoutedEventArgs e) => Try(() => OpenDirectory(RegisteredBox.Text.Trim()));
    private void OpenLogs(object sender, RoutedEventArgs e) => Try(() => { Directory.CreateDirectory(Path.Combine(WorkspaceSettings.StateDirectory, "Activity")); OpenDirectory(Path.Combine(WorkspaceSettings.StateDirectory, "Activity")); });
    private static void OpenDirectory(string directory) { if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory); Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
    private void DeploymentInputsChanged(object sender, TextChangedEventArgs e) { _plan = null; if (ApplyCopyButton is not null) ApplyCopyButton.IsEnabled = false; }
    private async void PreviewCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            var source = SourceBox.Text.Trim(); var root = RegisteredBox.Text.Trim(); var name = PluginBox.Text.Trim();
            var plan = await Task.Run(() => PluginDeployment.Preview(source, root, name));
            if (source != SourceBox.Text.Trim() || root != RegisteredBox.Text.Trim() || name != PluginBox.Text.Trim()) { Say("输入已变化，请重新预览。"); return; }
            _plan = plan; CopyGrid.ItemsSource = plan.Files; ApplyCopyButton.IsEnabled = true;
            CopySummaryText.Text = $"{plan.Files.Length} 个文件，{plan.Files.Sum(file => file.Bytes) / 1048576d:N2} MiB，跳过 {plan.Skipped} 个开发产物或非插件文件。目标：{plan.Target}";
        }
        catch (Exception error) { Say(error.Message); }
    }
    private async void ApplyCopy(object sender, RoutedEventArgs e)
    {
        if (_plan is null || _copying) return; var plan = _plan; ApplyCopyButton.IsEnabled = false; _copying = true;
        var id = _journal.Begin("copy_plugin", JsonSerializer.Serialize(new { plan.Source, plan.Target, files = plan.Files.Length }));
        try
        {
            var receipt = await Task.Run(() => PluginDeployment.Apply(plan));
            _journal.Complete(id, "succeeded", "复制 " + receipt.Files.Length + " 个文件，备份：" + receipt.BackupDirectory);
            Say("复制完成，覆盖前备份：" + receipt.BackupDirectory + "。未自动执行 REHASH。"); _plan = null; await ScanAsync();
        }
        catch (Exception error) { _journal.Complete(id, "failed", error.Message); Say(error.Message); }
        finally { _copying = false; }
    }

    /// <summary>复制事务完成前保留进程，避免窗口关闭中断文件恢复。</summary>
    /// <param name="e">窗口关闭事件。</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_copying || SessionView.IsRunning) { e.Cancel = true; Say("操作正在进行。请先等待结果，或在会话页取消等待；取消不会撤回已发出的 AVEVA 操作。"); }
        base.OnClosing(e);
    }

    private async Task RefreshRuntimeAsync()
    {
        if (_refreshing || _closed) return; _refreshing = true;
        try
        {
            var data = await Task.Run(() => (Statuses: RuntimeReader.Statuses(), Events: RuntimeReader.Recent()));
            if (_closed) return; RuntimeGrid.ItemsSource = data.Statuses; ActivityGrid.ItemsSource = data.Events;
            HeartbeatText.Text = $"{data.Statuses.Count(status => status.State is "idle" or "running")} 个本机服务在线 · {data.Statuses.Sum(status => status.ActiveTools.Length)} 个工具运行中";
            OverviewServices.Text = data.Statuses.Count(status => status.State is "idle" or "running").ToString();
            var completed = data.Events.Where(item => item.Phase != "started").ToArray();
            OverviewCalls.Text = completed.Length.ToString("N0");
            OverviewLatency.Text = completed.Length == 0 ? "—" : completed.Average(item => item.Milliseconds).ToString("N0") + " ms";
            OverviewSuccess.Text = completed.Length == 0 ? "—" : (100d * completed.Count(item => item.Phase == "succeeded") / completed.Length).ToString("N1") + "%";
            OverviewRecentText.Text = completed.Length == 0 ? "尚无完成记录" : completed.Length.ToString("N0") + " 次完成";
            OverviewFeed.ItemsSource = data.Events.Take(3).ToArray();
            OverviewRuntimeText.Text = HeartbeatText.Text + "\n\n" + string.Join("\n", data.Statuses.Take(3).Select(status => status.Service + " · " + status.StateLabel));
        }
        catch (Exception error) { HeartbeatText.Text = "状态读取失败：" + error.Message; }
        finally { _refreshing = false; }
    }
    private void ActivitySelected(object sender, SelectionChangedEventArgs e)
    {
        if (ActivityGrid.SelectedItem is ActivityEntry entry) ActivityDetails.Text = $"{entry.TimeUtc.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm:ss zzz} · {entry.Service} · PID {entry.ProcessId}\n关联：{entry.CallId}\n参数：{entry.Arguments}\n结果：{entry.Summary}";
    }
    private void Try(Action action) { try { action(); } catch (Exception error) { Say(error.Message); } }

    /// <summary>在隔离夹具中渲染三个页面截图，验证窗口创建、绑定与目录扫描。</summary>
    /// <param name="outputDirectory">截图与验证报告输出目录。</param>
    public async Task RunSmokeAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory); await DiscoverAsync();
        if (Directory.Exists(_settings.RegisteredPmlLib)) await ScanAsync();
        if ((Directory.Exists(_settings.PluginSource) || File.Exists(_settings.PluginSource)) && Directory.Exists(_settings.RegisteredPmlLib))
        {
            var plan = await Task.Run(() => PluginDeployment.Preview(_settings.PluginSource, _settings.RegisteredPmlLib, _settings.PluginName));
            CopyGrid.ItemsSource = plan.Files; CopySummaryText.Text = $"界面验证：预览 {plan.Files.Length} 个文件，未执行复制。";
        }
        await RefreshRuntimeAsync();
        await SessionView.PrepareSmokeAsync(Path.Combine(outputDirectory, "fixture"));
        foreach (var size in new[] { (Width: 1440d, Height: 960d), (Width: 1280d, Height: 720d), (Width: 853d, Height: 480d) })
        {
            Width = size.Width; Height = size.Height;
            foreach (var screen in new[] { (Tab: 4, Flow: 0, Name: "overview"), (Tab: 0, Flow: 0, Name: "launch"), (Tab: 1, Flow: 0, Name: "deployment"), (Tab: 2, Flow: 0, Name: "logs"), (Tab: 3, Flow: 0, Name: "sessions"), (Tab: 3, Flow: 1, Name: "functions"), (Tab: 3, Flow: 2, Name: "queries") })
            {
                Tabs.SelectedIndex = screen.Tab; SessionView.SelectSmokeFlow(screen.Flow);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(150);
                var content = (FrameworkElement)Content;
                var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(outputDirectory, $"{screen.Name}-{size.Width}.png")); encoder.Save(output);
            }
        }
        Width = 1280; Height = 900; Tabs.SelectedIndex = 3;
        foreach (var unknown in new[] { false, true })
        {
            await SessionView.FaultSmokeAsync(unknown);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(100);
            var content = (FrameworkElement)Content;
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(outputDirectory, unknown ? "state-unknown.png" : "state-rejected.png")); encoder.Save(output);
        }
        File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke.json"), JsonSerializer.Serialize(new { passed = true, scope = "WPF creation, synthetic M1 binding and logical viewport checks; not AVEVA live or human usability acceptance", tabs = Tabs.Items.Count, profiles = ProfilesBox.Items.Count, errorStates = 2, viewports = new[] { "1440x960", "1280x720", "853x480 logical (1280x720 at 150%)" }, finishedUtc = DateTime.UtcNow, dpi = VisualTreeHelper.GetDpi(this).DpiScaleX, fontSize = FontSize }));
    }
}
