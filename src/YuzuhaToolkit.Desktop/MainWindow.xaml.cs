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
    private ActivityEntry[] _recentActivity = [];
    private IReadOnlyList<YuzuhaToolkit.Mcp.AvevaSessionCandidate> _sessionCandidates = [];

    /// <summary>读取已有无密码配置，建立状态轮询；不自动启动 AVEVA 或复制插件。</summary>
    public MainWindow()
    {
        InitializeComponent(); _settings = WorkspaceSettings.Load();
        InitializeKnowledge();
        Width = Math.Min(Width, Math.Max(MinWidth, SystemParameters.WorkArea.Width - 16));
        Height = Math.Min(Height, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 16));
        SessionView.SectionChanged += title => { if (Tabs.SelectedIndex == 3) SetPage(title); };
        SessionView.TargetChanged += target => { SidebarTargetText.Text = target; OverviewTargetText.Text = SessionView.TargetDisplayName; OverviewSessionText.Text = target; OverviewTargetDetail.Text = target; RenderCandidates(); };
        SessionView.CandidatesChanged += candidates => { _sessionCandidates = candidates; RenderCandidates(); };
        MaintainerText.Text = Environment.UserName;
        try { DesktopTheme.Apply(DesktopDisplaySettings.ReadTheme(WorkspaceSettings.StateDirectory)); } catch (Exception error) { Say("显示偏好读取失败：" + error.Message); }
        SetPage("总览"); UpdateThemeButtons();
        RegisteredBox.Text = _settings.RegisteredPmlLib; SourceBox.Text = _settings.PluginSource; PluginBox.Text = _settings.PluginName;
        WarnFilesBox.Text = _settings.WarnFiles.ToString(); WarnSizeBox.Text = (_settings.WarnBytes / 1048576).ToString();
        ReloadProfiles(); if (_settings.Profiles.Count > 0) ProfilesBox.SelectedItem = _settings.Profiles.FirstOrDefault(profile => profile.IsDefault) ?? _settings.Profiles[0];
        _timer.Tick += async (_, _) => await RefreshRuntimeAsync(); _timer.Start();
        Loaded += async (_, _) => { await RefreshRuntimeAsync(); await DiscoverAsync(); SessionView.RefreshCandidates(); };
        Closed += (_, _) => { _closed = true; _timer.Stop(); SessionView.Dispose(); _journal.Dispose(); };
    }

    private void Say(string message) => MessageText.Text = message;
    private void RenderCandidates()
    {
        OverviewServices.Text = _sessionCandidates.Count.ToString();
        OverviewSessionsGrid.ItemsSource = _sessionCandidates.Take(3).Select(candidate => new { candidate.Product, candidate.Project, candidate.ProcessId, Verified = SessionView.MatchesTarget(candidate), StatusText = SessionView.MatchesTarget(candidate) ? "已核验" : candidate.PipeDetected ? "已发现 · 待核验" : "未发现 Host" }).ToArray();
    }
    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (SidebarEnvironmentButton is not null)
        {
            var compact = ActualHeight < 760;
            SidebarEnvironmentButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            SidebarAccountButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            SidebarFooter.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        }
        if (HeartbeatText is not null) HeartbeatText.Visibility = ActualWidth < 1100 ? Visibility.Collapsed : Visibility.Visible;
        if (SearchPanel is not null) SearchPanel.Visibility = ActualWidth < 1250 ? Visibility.Collapsed : Visibility.Visible;
        if (OverviewKpis is not null)
        {
            var narrow = ActualWidth < 1100; OverviewKpis.Columns = narrow ? 2 : 4;
            foreach (var grid in new[] { OverviewColumns1, OverviewColumns2, OverviewColumns3, LaunchColumns, LaunchLowerColumns, RuntimeColumns, RuntimeLowerColumns, LogColumns, SettingsColumns, SettingsLowerColumns, ReviewColumns, TestsColumns, WebColumns, KnowledgeColumns, DeploymentColumns, DeploymentLowerColumns, ProfileLibraryColumns })
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
        PageSubtitleText.Text = title switch { "总览" => "本机工作台 · 工程环境与近期工具活动", "工程会话" => "先核验 PID 与启动身份，再查询或调用 PML", "能力与函数" => "登记源码、人工授权 · 登记版本与会话加载版本分别核对", "原生 Form / Gadget" => "原生窗体 → 控件 → 属性 · 有界读取与续读", "启动工程" => "保存本机登录配置与凭据，下次自动使用", "启动配置" => "按名称、项目或路径查找，管理多套本机配置", "知识库" => "本窗口检索、正文、来源与定位", "插件部署" => "来源、复制预览、备份与本机记录", _ => "本机工具与交付记录" };
        HeaderActionText.Text = title switch { "工程会话" => "扫描会话", "能力与函数" => "登记函数", "原生 Form / Gadget" => "重新核验", "AI 运行" => "刷新状态", "工具日志" => "日志目录", "插件部署" => "预览复制", "设置" => "保存设置", _ => "启动工程" };
        if (title is "评审中心" or "测试中心" or "Web 应用" or "知识库") HeaderActionText.Text = title switch { "评审中心" => "审核草稿", "测试中心" => "运行测试", "Web 应用" => "打开 HTML 管理器", _ => "搜索知识" };
        if (title == "启动配置") HeaderActionText.Text = "新建配置";
        HeaderActionButton.IsEnabled = title is not ("评审中心" or "测试中心");
        HeaderActionIcon.Data = (Geometry)FindResource(title is "工程会话" or "原生 Form / Gadget" or "AI 运行" ? "Icon.refresh" : title is "能力与函数" or "Web 应用" or "启动配置" ? "Icon.plus" : "Icon.play");
        foreach (var button in SidebarNav.Children.OfType<Button>())
        {
            var selected = button.Uid == (title is "能力与函数" or "原生 Form / Gadget" ? "工程会话" : title == "启动配置" ? "启动工程" : title);
            button.SetResourceReference(BackgroundProperty, selected ? "Brand.NavSelected" : "Brand.Sidebar");
            button.SetResourceReference(ForegroundProperty, selected ? "Brand.NavSelectedText" : "Brand.NavText");
            button.SetResourceReference(BorderBrushProperty, "Brand.NavIndicator");
            button.BorderThickness = new Thickness(selected ? 3 : 0, 0, 0, 0);
        }
    }
    private void ChooseTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string theme) return;
        try { DesktopTheme.Apply(theme); UpdateThemeButtons(); DesktopDisplaySettings.SaveTheme(WorkspaceSettings.StateDirectory, theme); }
        catch (Exception error) { Say("主题已切换，但显示偏好未保存：" + error.Message); }
    }
    private void UpdateThemeButtons()
    {
        foreach (var button in new[] { LightThemeButton, DarkThemeButton })
        {
            var selected = Equals(button.Tag, DesktopTheme.Current); button.SetResourceReference(BackgroundProperty, selected ? "Surface.Card" : "Surface.Track");
            button.SetResourceReference(ForegroundProperty, selected ? "Text.Primary" : "Text.Secondary");
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            button.BorderThickness = new Thickness(0);
        }
    }
    private void HeaderAction(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedIndex == 3) SessionView.PrimaryAction();
        else if (Tabs.SelectedIndex == 2) _ = RefreshRuntimeAsync();
        else if (Tabs.SelectedIndex == 7) OpenLogs(sender, e);
        else if (Tabs.SelectedIndex == 6) SaveWorkspace(sender, e);
        else if (Tabs.SelectedIndex == 1) PreviewCopy(sender, e);
        else if (Tabs.SelectedIndex == 0) { if (LaunchButton.IsEnabled) Launch(sender, e); }
        else if (Tabs.SelectedIndex == 8) { NewProfile(sender, e); Tabs.SelectedIndex = 0; }
        else if (Tabs.SelectedIndex == 5 && PlannedPages.SelectedIndex == 2) OpenHtmlManager(sender,e);
        else if (Tabs.SelectedIndex == 5 && PlannedPages.SelectedIndex == 3) _ = SearchKnowledgeAsync();
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
        if (name is "启动配置" or "配置库") { OpenProfileLibrary(this, new()); NavigationSearch.Clear(); e.Handled = true; return; }
        var button = SidebarNav.Children.OfType<Button>().FirstOrDefault(b => name.Length > 0 && b.Uid.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (button is not null) { Navigate(button, new()); NavigationSearch.Clear(); } else Say("未找到页面。可输入：工程会话、启动工程、工具日志等。");
        e.Handled = true;
    }
    private void OpenLogsPage(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 7; SetPage("工具日志"); }
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
            Tabs.SelectedIndex = 5; PlannedTitleText.Text = button.Uid + " · 功能待接入，保留原稿布局";
            PlannedPages.SelectedIndex = button.Uid switch { "评审中心" => 0, "测试中心" => 1, "Web 应用" => 2, _ => 3 };
            PlannedDescriptionText.Text = button.Uid switch
            {
                "评审中心" => "M3：同一名人工审核差异、契约和测试证据，再批准迁移。复用已有复制、哈希和备份能力，补批准快照、回执与恢复记录。",
                "测试中心" => "M2a / M2b：先交付源码目录与证据浏览，再从 C# 调用既有按 ID 测试脚本。现阶段可使用 DocFX 测试目录与 PowerShell 独立复刻；人工实机项保持 NotRun。",
                "Web 应用" => "桌面按钮打开独立 HTML 管理器，共用现有 IPC；打开网页不执行 PML。应用分类、按钮和参数在网页中配置。",
                _ => "本页直接调用现有 C# 知识仓库，查看完整内置指南或本机资料的正文、来源、行号与数据库身份。索引构建和经验写入尚未接入；检索命中不等于实测或调用许可。"
            };
        }
        if (button.Uid == "工程会话") SessionView.OpenSessions();
        if (button.Uid == "Web 应用") PlannedTitleText.Text = "Web 应用 · 独立本地管理器";
        if (button.Uid == "知识库") { PlannedTitleText.Text = "知识检索 · 资料与实测证据分开"; if (KnowledgeResultsGrid.Items.Count == 0) _ = SearchKnowledgeAsync(); }
        SetPage(button.Uid);
    }
    private void OpenFunctionsPage(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 3; SessionView.OpenFunctions(); SetPage("能力与函数"); }
    private void OpenQueriesPage(object sender, RoutedEventArgs e) { Tabs.SelectedIndex = 3; SessionView.OpenQueries(); SetPage("原生 Form / Gadget"); }
    private void ReloadProfiles()
    {
        var credentialId=(CredentialProfilesBox.SelectedItem as LaunchProfile)?.Id;
        var mdb=MdbBox.Text;
        ProfilesBox.ItemsSource = null; ProfilesBox.ItemsSource = _settings.Profiles;
        CredentialProfilesBox.ItemsSource=null; CredentialProfilesBox.ItemsSource=_settings.Profiles;
        CredentialProfilesBox.SelectedItem=_settings.Profiles.FirstOrDefault(p=>p.Id==credentialId)??_settings.Profiles.FirstOrDefault(p=>p.Id==_selected?.Id)??_settings.Profiles.FirstOrDefault();
        MdbBox.ItemsSource=_settings.Profiles.Select(p=>p.Mdb).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); MdbBox.Text=mdb;
        RefreshProfileLibrary();
    }
    private LaunchProfile ReadProfile() => new()
    {
        Id = _selected?.Id ?? Guid.NewGuid().ToString("N"), IsFavorite = _selected?.IsFavorite ?? false,
        IsDefault = _selected?.IsDefault ?? false, LastStartedUtc = _selected?.LastStartedUtc,
        Name = NameBox.Text.Trim(), Kind = ((ComboBoxItem)KindBox.SelectedItem).Content.ToString()!, Executable = ExecutableBox.Text.Trim(),
        Initialization = InitializationBox.Text.Trim(), Product = ProductBox.Text.Trim(), WorkingDirectory = WorkingBox.Text.Trim(),
        Project = ProjectBox.Text.Trim(), User = UserBox.Text.Trim(), Mdb = MdbBox.Text.Trim(), Module = ModuleBox.Text.Trim(), License = LicenseBox.Text.Trim(),
        Environment = ReadStartupEnvironment(), CredentialKey = _selected?.CredentialKey ?? "", RememberPassword = RememberPasswordBox.IsChecked == true
    };
    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProfilesBox.SelectedItem is not LaunchProfile profile) return; _selected = profile;
        FillProfileFields(profile);
    }
    private void FillProfileFields(LaunchProfile profile)
    {
        FillStartupPolicy(profile);
        NameBox.Text = profile.Name; KindBox.SelectedIndex = profile.Kind == "MarineBatch" ? 1 : 0;
        ExecutableBox.Text = profile.Executable; InitializationBox.Text = profile.Initialization; ProductBox.Text = profile.Product;
        WorkingBox.Text = profile.WorkingDirectory; ProjectBox.Text = profile.Project; UserBox.Text = profile.User;
        fillingLaunchApplication=true;
        try { LaunchApplicationBox.SelectedItem=LaunchApplicationBox.Items.OfType<ApplicationChoice>().FirstOrDefault(c=>c.Path.Equals(profile.Executable,StringComparison.OrdinalIgnoreCase)); }
        finally { fillingLaunchApplication=false; }
        LaunchApplicationSummary.Text=profile.Executable.Length==0?"选择产品后自动填写路径。":profile.Executable;
        MdbBox.Text = profile.Mdb; ModuleBox.Text = profile.Module; LicenseBox.Text = profile.License; PasswordInput.Clear();
        RememberPasswordBox.IsChecked = profile.RememberPassword;
        PasswordStatusText.Text = profile.CredentialKey.Length > 0 ? "已关联本机凭据；下次启动自动使用。输入新密码可更新。" : "尚未保存密码。勾选后点击保存配置或保存并启动。";
    }
    private void NewProfile(object sender, RoutedEventArgs e)
    {
        var application=LaunchApplicationBox.SelectedItem as ApplicationChoice;
        ProfilesBox.SelectedItem = null; _selected = null;
        var profile = application is null ? new LaunchProfile() : LaunchDrafts.FromApplication(application.Path);
        FillProfileFields(profile);
        Tabs.SelectedIndex = 0; ProfileDetailsExpander.IsExpanded = false; LaunchPathsExpander.IsExpanded=false;
        if(application is null) LaunchApplicationBox.Focus(); else ProjectBox.Focus();
    }
    private void SaveProfile(object sender, RoutedEventArgs e) => Try(() => { var profile = PersistProfile(); Say(profile.CredentialKey.Length > 0 ? "已保存配置并关联本机凭据，下次启动自动使用。" : "已保存配置；当前没有关联已保存的密码。"); });
    private LaunchProfile PersistProfile()
    {
        var profile = ReadProfile();
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name=="新启动配置")
        {
            profile.Name=LaunchDrafts.SuggestName(profile);
            if(profile.Name.Length==0) profile.Name="新启动配置";
        }
        var index = _selected is null ? -1 : _settings.Profiles.IndexOf(_selected);
        var previous = _selected; var oldKey = previous?.CredentialKey ?? "";
        // Turning off storage must not silently remove an existing password from the daily launch page.
        // Destructive removal is only available through Settings and its explicit confirmation.
        profile.CredentialKey = !profile.RememberPassword && oldKey.Length>0 ? oldKey
            : WindowsCredentialStore.Prepare(oldKey, PasswordInput.Password, profile.RememberPassword);
        try
        {
            if (index < 0) _settings.Profiles.Add(profile); else _settings.Profiles[index] = profile;
            SaveWorkspaceFields();
        }
        catch
        {
            if (index < 0) _settings.Profiles.Remove(profile); else _settings.Profiles[index] = previous!;
            if (profile.CredentialKey != oldKey) WindowsCredentialStore.Delete(profile.CredentialKey);
            throw;
        }
        ReloadProfiles(); ProfilesBox.SelectedItem = profile;
        if (oldKey != profile.CredentialKey && !_settings.Profiles.Any(item => item.CredentialKey == oldKey)) WindowsCredentialStore.Delete(oldKey);
        return profile;
    }
    private void ClearSavedPassword(object sender, RoutedEventArgs e) => Try(() =>
    {
        if(CredentialProfilesBox.SelectedItem is not LaunchProfile profile || profile.CredentialKey.Length==0)
        { Say("此配置没有已保存的密码。"); return; }
        if(MessageBox.Show(this,$"清除“{profile.Name}”已保存的登录密码？\n下次登录需要重新输入，配置本身会保留。",
            "确认清除已存密码",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes) return;
        var oldKey=profile.CredentialKey; var remember=profile.RememberPassword;
        profile.CredentialKey=""; profile.RememberPassword=false;
        try { _settings.Save(); }
        catch { profile.CredentialKey=oldKey; profile.RememberPassword=remember; throw; }
        if(!_settings.Profiles.Any(p=>p.CredentialKey==oldKey)) WindowsCredentialStore.Delete(oldKey);
        if(_selected?.Id==profile.Id)
        { RememberPasswordBox.IsChecked=false; PasswordInput.Clear(); PasswordStatusText.Text="已清除保存的密码，下次请重新输入。"; }
        Say("已清除此配置的本机密码关联；配置保留。");
    });
    private void PreviewLaunch(object sender, RoutedEventArgs e) => Try(() => LaunchPreviewBox.Text = LaunchService.Preview(ReadProfile()));
    private async void Launch(object sender, RoutedEventArgs e)
    {
        if (_copying) { Say("插件正在复制，请完成后再启动工程。"); return; }
        LaunchButton.IsEnabled = false;
        try
        {
            var enteredPassword = PasswordInput.Password; var profile = PersistProfile();
            if (!string.IsNullOrWhiteSpace(_settings.RegisteredPmlLib))
            {
                var health = await ScanAsync();
                if (_closed) return;
                if (health.NeedsAttention && MessageBox.Show(FormatHealth(health) + "\n\n建议先移出旧插件、备份和开发副本。仍然启动吗？", "PMLLIB 清理提醒", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            var password = enteredPassword.Length > 0 ? enteredPassword : profile.CredentialKey.Length > 0 ? WindowsCredentialStore.Read(profile.CredentialKey) : "";
            var start = LaunchService.Build(profile, password);
            var id = _journal.Begin("launch_aveva", JsonSerializer.Serialize(new { profile = profile.Name, executable = profile.Executable, startupPolicy = (start.Environment.TryGetValue(StartupPolicy.Variable,out var startupActions) ? startupActions : "none"), startupId = (start.Environment.TryGetValue("YUZUHA_STARTUP_ID",out var startupId) ? startupId : null), startupLog = (start.Environment.TryGetValue("YUZUHA_STARTUP_LOG",out var startupLog) ? startupLog : null), phase = "requested-before-process-start" }));
            try
            {
                using var process = Process.Start(start) ?? throw new IOException("未创建启动入口进程。");
                _journal.Complete(id, "succeeded", "启动入口进程 PID " + process.Id);
                Say("启动入口已创建，PID " + process.Id + "。工程会话就绪后可由 MCP 发现并选择；入口进程创建不等于登录完成。");
                profile.LastStartedUtc = DateTimeOffset.UtcNow;
                try { _settings.Save(); RefreshProfileLibrary(); }
                catch (Exception error) { Say("启动入口已创建，PID " + process.Id + "；最近启动记录未保存：" + error.Message); }
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
            PopulateLaunchApplications(candidates.Where(candidate=>candidate.Kind=="application"));
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
        ApplyLaunchApplication(candidate.Path);
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
        OverviewFilesText.Text = $"{health.DuplicateGroups:N0} 组同名 · 多出 {health.DuplicateExtraFiles:N0} 项";
        OverviewSizeText.Text = $"{health.PmlFiles:N0} 个 PML · {health.PmlBytes / 1048576d:N2} MiB · " + (health.Complete ? "完整扫描" : "部分扫描，数量为下限");
        HealthText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, health.NeedsAttention ? "Text.Warning" : "Text.Success"); return health;
    }
    private static string FormatHealth(DirectoryHealth health) => $"{(health.Complete ? "完整扫描" : "部分扫描，数量仅为下限")} · 目录全部文件 {health.Files:N0} 项 / {health.Bytes / 1048576d:N2} MiB\n"
        + $"PML 源文件 {health.PmlFiles:N0} 项 / {health.PmlBytes:N0} 字节；同名候选 {health.DuplicateGroups:N0} 组，多出 {health.DuplicateExtraFiles:N0} 项\n"
        + string.Join("；", health.PmlTypes.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}: {pair.Value.Files:N0} 项 / {pair.Value.Bytes:N0} 字节")) + "\n"
        + (health.Warnings.Length > 0 ? string.Join("\n", health.Warnings) : "未达到当前清理提醒预算。")
        + "\nPML 所在目录（各目录直属文件，最多显示 20 项）：\n" + string.Join("\n", health.PmlFolders.OrderByDescending(pair => pair.Value.Bytes).Take(20).Select(pair => $"{pair.Key}: {pair.Value.Files:N0} 项 / {pair.Value.Bytes:N0} 字节"))
        + (health.DuplicateGroups == 0 ? "" : "\n同名候选路径（未比较内容，最多显示 20 组，每组 10 个路径）：\n" + string.Join("\n", health.DuplicatePmlNames.Take(20).Select(pair => pair.Key + ": " + string.Join("；", pair.Value.Take(10)) + (pair.Value.Length > 10 ? "；其余路径见完整扫描 JSON" : ""))));
    private async void ScanDirectory(object sender, RoutedEventArgs e) { try { await ScanAsync(); } catch (Exception error) { Say(error.Message); } }
    private void OpenRegistered(object sender, RoutedEventArgs e) => Try(() => OpenDirectory(RegisteredBox.Text.Trim()));
    private void OpenLogs(object sender, RoutedEventArgs e) => Try(() => { Directory.CreateDirectory(Path.Combine(WorkspaceSettings.StateDirectory, "Activity")); OpenDirectory(Path.Combine(WorkspaceSettings.StateDirectory, "Activity")); });
    private static void OpenDirectory(string directory) { if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory); Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
    private void DeploymentInputsChanged(object sender, TextChangedEventArgs e) { developmentPreview=null; if(RunDevelopmentButton is not null) RunDevelopmentButton.IsEnabled=false; _plan = null; if (ApplyCopyButton is not null) ApplyCopyButton.IsEnabled = false; }
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
            if (_closed) return;
            var selectedActivity = ActivityGrid.SelectedItem as ActivityEntry;
            _recentActivity = data.Events;
            RuntimeGrid.ItemsSource = data.Statuses; RuntimeCards.ItemsSource = data.Statuses.Take(3).ToArray(); RuntimeActivityGrid.ItemsSource = data.Events.Take(8).ToArray(); ActivityGrid.ItemsSource = data.Events;
            if (selectedActivity is not null) ActivityGrid.SelectedItem = data.Events.FirstOrDefault(item => item.CallId == selectedActivity.CallId && item.ProcessId == selectedActivity.ProcessId && item.Phase == selectedActivity.Phase && item.TimeUtc == selectedActivity.TimeUtc);
            HeartbeatText.Text = $"{data.Statuses.Count(status => status.State is "idle" or "running")} 个本机服务在线 · {data.Statuses.Sum(status => status.ActiveTools.Length)} 个工具运行中";
            var summary = DesktopActivitySummary.Create(data.Events, DateTimeOffset.Now);
            OverviewCalls.Text = summary.Completed.ToString("N0");
            OverviewLatency.Text = summary.AverageMilliseconds?.ToString("N0") + (summary.AverageMilliseconds is null ? "—" : " ms");
            OverviewSuccess.Text = summary.SuccessPercent?.ToString("N1") + (summary.SuccessPercent is null ? "—" : "%");
            RuntimeSuccessText.Text = OverviewSuccess.Text; RuntimeMetricText.Text = summary.Completed == 0 ? "当前没有完成项" : summary.Successful + " / " + summary.Completed + " · 已保留完成项";
            RuntimeErrorsList.ItemsSource = data.Events.Where(item => item.Phase is not ("started" or "succeeded")).Take(3).ToArray();
            DeploymentHistoryGrid.ItemsSource = data.Events.Where(item => item.Tool == "copy_plugin" && item.Phase != "started").Take(8).ToArray();
            OverviewRecentText.Text = summary.Days.Sum(day => day.Total).ToString("N0");
            var maximum = Math.Max(1, summary.Days.Max(day => day.Total));
            OverviewChart.ItemsSource = summary.Days.Select(day => new { day.Label, CountLabel = day.Total == 0 ? "" : day.Total.ToString(), QueryHeight = day.Query * 94d / maximum, ExecuteHeight = day.Execute * 94d / maximum, KnowledgeHeight = day.Knowledge * 94d / maximum, Tooltip = day.Label + " · 已保留 " + day.Total + " 项" }).ToArray();
            OverviewChartEmptyText.Visibility = summary.Days.All(day => day.Total == 0) ? Visibility.Visible : Visibility.Collapsed;
            OverviewFeed.ItemsSource = data.Events.Take(3).ToArray();
            OverviewFeedEmptyText.Visibility = data.Events.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            OverviewRuntimeText.Text = HeartbeatText.Text + "\n\n" + string.Join("\n", data.Statuses.Take(3).Select(status => status.Service + " · " + status.StateLabel));
        }
        catch (Exception error) { HeartbeatText.Text = "状态读取失败：" + error.Message; }
        finally { _refreshing = false; }
    }
    private void ActivitySelected(object sender, SelectionChangedEventArgs e)
    {
        if (ActivityGrid.SelectedItem is ActivityEntry entry)
        {
            ActivityIdentityText.Text = $"{entry.TimeUtc.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm:ss zzz}\n{entry.Service} · PID {entry.ProcessId}\n关联：{entry.CallId}";
            ActivityRequestText.Text = string.IsNullOrEmpty(entry.Arguments) ? _recentActivity.FirstOrDefault(item => item.CallId == entry.CallId && item.ProcessId == entry.ProcessId && item.Service == entry.Service && item.Phase == "started")?.Arguments ?? "开始记录不在当前保留范围，无法恢复请求参数。" : entry.Arguments;
            ActivityDetails.Text = entry.Summary;
        }
    }
    private void CopyActivity(object sender, RoutedEventArgs e) => Try(() => { Clipboard.SetText(ActivityIdentityText.Text + "\n请求：" + ActivityRequestText.Text + "\n结果：" + ActivityDetails.Text); Say("已复制当前调用详情。"); });
    private void Try(Action action) { try { action(); } catch (Exception error) { Say(error.Message); } }

    /// <summary>隔离数据下渲染全部页面，核对两种主题、布局与错误反馈。</summary>
    /// <param name="outputDirectory">本机截图和合成验证报告目录。</param>
    public async Task RunSmokeAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory); _timer.Stop(); await DiscoverAsync();
        var fixtureDirectory = Path.Combine(outputDirectory, "fixture"); Directory.CreateDirectory(fixtureDirectory);
        var launcher = Path.Combine(fixtureDirectory, "mon.exe"); File.WriteAllBytes(launcher, []);
        var initialization = Path.Combine(fixtureDirectory, "launch.init"); File.WriteAllText(initialization, "UI fixture only");
        NameBox.Text = "未保存的离线预览草稿"; ExecutableBox.Text = launcher; InitializationBox.Text = initialization; WorkingBox.Text = fixtureDirectory;
        ProjectBox.Text = "SYNTHETIC"; UserBox.Text = "UI_TEST"; MdbBox.Text = "/ALL_NO_MDS"; ModuleBox.Text = "Design";
        PreviewLaunch(this, new()); LaunchButton.IsEnabled = false;
        var credentialProfile = default(LaunchProfile);
        try
        {
            PasswordInput.Password = "isolated-ui-credential-fixture"; RememberPasswordBox.IsChecked = true;
            credentialProfile = PersistProfile();
            if (credentialProfile.CredentialKey.Length == 0 || WindowsCredentialStore.Read(credentialProfile.CredentialKey) != "isolated-ui-credential-fixture" || PasswordInput.Password.Length != 0) throw new InvalidOperationException("GUI 凭据保存或留空沿用验证失败。");
            var nextLogin = LaunchService.Build(credentialProfile, WindowsCredentialStore.Read(credentialProfile.CredentialKey));
            if (!nextLogin.ArgumentList.Contains("UI_TEST/isolated-ui-credential-fixture")) throw new InvalidOperationException("保存的凭据没有进入下次启动参数。");
        }
        finally
        {
            if (credentialProfile is not null) { WindowsCredentialStore.Delete(credentialProfile.CredentialKey); credentialProfile.CredentialKey = ""; _settings.Save(); }
            PasswordInput.Clear(); PasswordStatusText.Text = "离线凭据存取验证已清理；未启动 AVEVA。";
        }
        NameBox.Text = "未保存的离线预览草稿";
        VerifyProfileLibrarySmoke();
        KnowledgeDirectoryBox.Text = Path.Combine(WorkspaceSettings.StateDirectory, "knowledge");
        var knowledgeSource = Path.Combine(fixtureDirectory, "knowledge-source"); Directory.CreateDirectory(knowledgeSource);
        File.WriteAllText(Path.Combine(knowledgeSource, "UiKnowledgeProbe.pmlfnc"), "define function !!UiKnowledgeProbe() is STRING\nreturn 'isolated-knowledge-fixture'\nendfunction");
        new YuzuhaToolkit.Knowledge.KnowledgeRepository(KnowledgeDirectoryBox.Text).Build(knowledgeSource, null, null, "custom-ui-fixture", KnowledgeDirectoryBox.Text, false, 100);
        KnowledgeQueryBox.Text = "UiKnowledgeProbe"; await SearchKnowledgeAsync();
        if (!KnowledgeDetailBody.Text.Contains("isolated-knowledge-fixture", StringComparison.Ordinal)) throw new InvalidOperationException("发布程序的 GUI 无法读取 SQLite 正文。");
        KnowledgeQueryBox.Text = "当前对象";
        await SearchKnowledgeAsync();
        if (KnowledgeResultsGrid.Items.Count == 0 || !KnowledgeDetailBody.Text.Contains("YuzuhaReadCurrentElement", StringComparison.Ordinal)) throw new InvalidOperationException("GUI 知识检索没有显示完整内置正文。");
        using var fixtureJournal = new RuntimeJournal("离线 UI 合成夹具");
        foreach (var tool in new[] { "synthetic_query", "synthetic_registered_call", "synthetic_knowledge_search" })
        {
            var id = fixtureJournal.Begin(tool, "{\"fixtureOnly\":true}");
            fixtureJournal.Complete(id, "succeeded", "离线合成记录，不是 AVEVA 执行或业务验证。");
        }
        // Let the initial Loaded refresh finish before reading newly written fixture events.
        // Otherwise its in-flight guard can skip this refresh and leave an empty log selection.
        for (var attempt = 0; _refreshing && attempt < 500; attempt++) await Task.Delay(10);
        if (_refreshing) throw new InvalidOperationException("初始状态读取未完成，无法核对本次合成日志。");
        await RefreshRuntimeAsync();
        (int Tab, int Flow, int Planned, string Name)[] screens = {
            (Tab: 4, Flow: 0, Planned: 0, Name: "overview"), (0,0,0,"launch"), (1,0,0,"deployment"), (2,0,0,"runtime"), (7,0,0,"logs"),
            (3,0,0,"sessions"), (3,1,0,"functions"), (3,2,0,"queries"), (5,0,0,"review"), (5,0,1,"tests"), (5,0,2,"web"), (5,0,3,"knowledge"), (6,0,0,"settings"), (8,0,0,"profiles") };
        foreach (var theme in new[] { "Light", "Dark" })
        {
            await SessionView.PrepareSmokeAsync(fixtureDirectory);
            var state = SessionView.CaptureThemeState() + SessionView.CaptureObjectState() + CaptureLaunchDraft();
            var workspaceFile = Path.Combine(WorkspaceSettings.StateDirectory, "workspace.json");
            var workspaceBefore = File.Exists(workspaceFile) ? File.ReadAllText(workspaceFile) : null;
            ChooseTheme(theme == "Light" ? LightThemeButton : DarkThemeButton, new());
            if (state != SessionView.CaptureThemeState() + SessionView.CaptureObjectState() + CaptureLaunchDraft()) throw new InvalidOperationException("主题切换改变了输入、目标或函数许可。");
            if (workspaceBefore != (File.Exists(workspaceFile) ? File.ReadAllText(workspaceFile) : null) || DesktopDisplaySettings.ReadTheme(WorkspaceSettings.StateDirectory) != theme) throw new InvalidOperationException("主题切换未独立保存显示偏好。");
            var expected = ((SolidColorBrush)FindResource("Text.Primary")).Color;
            if (((SolidColorBrush)PageTitleText.Foreground).Color != expected) throw new InvalidOperationException("页面标题没有跟随主题。");
            var themeDirectory = Path.Combine(outputDirectory, theme); Directory.CreateDirectory(themeDirectory);
            Tabs.SelectedIndex = 1; RegistrationsBox.IsDropDownOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); RegistrationsBox.IsDropDownOpen = false;
            foreach (var size in new[] { (Width: 1440d, Height: 960d), (Width: 1280d, Height: 720d), (Width: 853d, Height: 480d) })
            {
                Width = size.Width; Height = size.Height;
                foreach (var screen in screens)
                {
                    SelectSmokeScreen(screen.Tab, screen.Flow, screen.Planned);
                    await CaptureSmokeAsync(Path.Combine(themeDirectory, $"{screen.Name}-{size.Width}.png"));
                }
            }
            var surface = (FrameworkElement)Content;
            Width = 1440 + ActualWidth - surface.ActualWidth;
            Height = 1024 + ActualHeight - surface.ActualHeight;
            foreach (var screen in screens)
            {
                SelectSmokeScreen(screen.Tab, screen.Flow, screen.Planned);
                await CaptureSmokeAsync(Path.Combine(themeDirectory, $"design-{screen.Name}.png"));
            }
            Width = 1280; Height = 900; Tabs.SelectedIndex = 3;
            foreach (var unknown in new[] { false, true })
            {
                await SessionView.FaultSmokeAsync(unknown); SetPage("能力与函数");
                PageSubtitleText.Text = "离线合成验证 · 非 AVEVA 实机";
                await CaptureSmokeAsync(Path.Combine(themeDirectory, unknown ? "state-unknown.png" : "state-rejected.png"));
            }
        }
        File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke.json"), JsonSerializer.Serialize(new {
            passed = true, scope = "WPF creation, synthetic bindings and layout; not AVEVA live or human visual acceptance",
            pages = screens.Length, tabs = Tabs.Items.Count, profiles = ProfilesBox.Items.Count, errorStates = 4,
            themes = new[] { "Light", "Dark" }, themePreservesWork = true, savedCredentials = true, guiKnowledge = true, objectQueries = true, profileLibrary = true,
            viewports = new[] { "1440x960", "1280x720", "853x480 logical (1280x720 at 150%)" },
            designCanvas = "1440x1024 client", finishedUtc = DateTime.UtcNow, dpi = VisualTreeHelper.GetDpi(this).DpiScaleX, fontSize = FontSize }));
    }
    private void SelectSmokeScreen(int tab, int flow, int planned)
    {
        Tabs.SelectedIndex = tab;
        if (tab == 3) SessionView.SelectSmokeFlow(flow);
        if (tab == 5)
        {
            PlannedPages.SelectedIndex = planned; PlannedTitleText.Text = planned == 3 ? "GUI 知识检索 · 离线验证" : "离线界面验证 · 功能尚未接入";
        }
        var title = tab == 3 ? flow switch { 1 => "能力与函数", 2 => "原生 Form / Gadget", _ => "工程会话" }
            : tab == 5 ? new[] { "评审中心", "测试中心", "Web 应用", "知识库" }[planned] : ((TabItem)Tabs.SelectedItem).Header.ToString();
        SetPage(title); PageSubtitleText.Text = "离线合成验证 · 非 AVEVA 实机";
        if (tab == 7)
        {
            ActivityGrid.SelectedItem = _recentActivity.FirstOrDefault(item => item.Tool.StartsWith("synthetic_", StringComparison.Ordinal) && item.Phase == "succeeded");
            if (!ActivityRequestText.Text.Contains("fixtureOnly", StringComparison.Ordinal)) throw new InvalidOperationException("完成记录没有定位到同次开始记录的请求参数。");
        }
    }
    private async Task CaptureSmokeAsync(string path)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(100);
        var content = (FrameworkElement)Content;
        var bitmap = new RenderTargetBitmap((int)Math.Round(content.ActualWidth), (int)Math.Round(content.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
    private string CaptureLaunchDraft() => JsonSerializer.Serialize(new { profile = ReadProfile(), passwordLength = PasswordInput.Password.Length, knowledgeQuery = KnowledgeQueryBox.Text, knowledgeDirectory = KnowledgeDirectoryBox.Text });
}
