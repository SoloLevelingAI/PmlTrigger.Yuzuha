using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        RegisteredBox.Text = _settings.RegisteredPmlLib; SourceBox.Text = _settings.PluginSource; PluginBox.Text = _settings.PluginName;
        WarnFilesBox.Text = _settings.WarnFiles.ToString(); WarnSizeBox.Text = (_settings.WarnBytes / 1048576).ToString();
        ReloadProfiles(); if (_settings.Profiles.Count > 0) ProfilesBox.SelectedIndex = 0;
        _timer.Tick += async (_, _) => await RefreshRuntimeAsync(); _timer.Start();
        Loaded += async (_, _) => { await RefreshRuntimeAsync(); await DiscoverAsync(); };
        Closed += (_, _) => { _closed = true; _timer.Stop(); _journal.Dispose(); };
    }

    private void Say(string message) => MessageText.Text = message;
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
        if (_copying) { e.Cancel = true; Say("插件复制正在完成，请等待结果后再退出。"); }
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
        for (var index = 0; index < 3; index++)
        {
            Tabs.SelectedIndex = index; await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(250);
            var content = (FrameworkElement)Content;
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(outputDirectory, "desktop-" + index + ".png")); encoder.Save(output);
        }
        File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke.json"), JsonSerializer.Serialize(new { passed = true, tabs = Tabs.Items.Count, profiles = ProfilesBox.Items.Count, registered = RegisteredBox.Text, processes = RuntimeGrid.Items.Count }));
    }
}
