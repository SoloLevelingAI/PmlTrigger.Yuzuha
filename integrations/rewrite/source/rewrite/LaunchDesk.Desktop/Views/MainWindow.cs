namespace LaunchDesk.Desktop;

/// <summary>拥有固定窗口壳、页面缓存、导航同步与原生圆角边缘。</summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel shell;
    private readonly Dictionary<PageId, UIElement> pages;
    private bool updatingNavigation;
    /// <summary>创建窗口壳并连接页面导航、尺寸适配和原生窗口边缘。</summary>
    /// <param name="shell">组合后的页面模型。</param>
    public MainWindow(ShellViewModel shell)
    {
        this.shell = shell;
        InitializeComponent();
        DataContext = shell;
        pages = new()
        {
            [PageId.Dashboard] = new DashboardView(shell), [PageId.Profiles] = new ProfilesView(shell),
            [PageId.Applications] = new ApplicationsView(shell.Applications), [PageId.Licenses] = new LicensesView(shell.Licenses),
            [PageId.Tools] = new ToolsView(shell.Tools), [PageId.ToolSettings] = new ToolSettingsView(shell.Tools),
            [PageId.Deployment] = new DeploymentView(shell.Tools), [PageId.History] = new HistoryView(shell.Tools),
            [PageId.Sharing] = new SharingView(shell.Sharing), [PageId.Settings] = new SettingsView(shell.Settings)
        };
        shell.PageChanged += ShowPage;
        ShowPage(shell.Page);
        ApplyLayout();
        InitializeWindowFrame();
        Deactivated += (_, _) => shell.ProfileEditor.Password.HideDisplay();
        Closed += (_, _) => { shell.PageChanged -= ShowPage; shell.ProfileEditor.Password.HideAndClear(); };
    }
    private void ShowPage(PageId page)
    {
        PageHost.Content = pages[page];
        var navPage = page is PageId.Deployment or PageId.ToolSettings ? PageId.Tools : page;
        updatingNavigation = true;
        try { SidebarNavigation.SelectedItem = SidebarNavigation.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == navPage.ToString()); }
        finally { updatingNavigation = false; }
    }
    private void OnNavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingNavigation || e.Source != SidebarNavigation || shell is null) return;
        if (SidebarNavigation.SelectedItem is ListBoxItem { Tag: string id } && Enum.TryParse<PageId>(id, out var page))
            shell.Page = page;
    }
    private void OnDesignSizeChanged(object sender, SizeChangedEventArgs e) { if (shell is not null) ApplyLayout(); }
    private void ApplyLayout()
    {
        shell.Layout.Update(ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
        Resources["Design.FieldHeight"] = shell.Layout.FieldHeight;
        Resources["Design.CardPadding"] = shell.Layout.FormPadding;
        Resources["Design.HeadingSize"] = shell.Layout.Compact ? 16d : 18d;
        Resources["Design.GroupGap"] = new Thickness(0, shell.Layout.Short ? 9 : 17, 0, 0);
        Resources["Design.GroupHeight"] = shell.Layout.Short ? 19d : 25d;
    }
    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
