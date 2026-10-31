using System.Windows;
using System.Windows.Controls;

namespace YuzuhaToolkit.Desktop;

/// <summary>Phayton R2 分组导航与原生窗口边框。只切换视图，保留现有页面实例和用户输入。</summary>
public partial class MainWindow
{
    private bool rebuildingNavigation;
    /// <summary>保留已有页面入口的分组导航项；Section 仅选择知识库或设置的子页。</summary>
    /// <param name="Label">当前导航中显示的名称。</param>
    /// <param name="Page">已有主页面名称。</param>
    /// <param name="Section">子页面序号，负数表示不改变当前子页面。</param>
    private sealed record NavigationItem(string Label, string Page, int Section = -1);
    private static readonly string[] PageNames = ["启动工程", "启动配置", "总览", "工程会话", "能力与函数", "原生 Form / Gadget", "Web 应用", "插件部署", "知识库", "AI 运行", "工具日志", "设置"];

    private static string NavigationGroup(string title) => title switch
    {
        "启动配置" or "工程会话" or "能力与函数" or "原生 Form / Gadget" => "启动工程",
        "插件部署" => "Web 应用",
        "工具日志" or "AI 运行" => "总览",
        _ => title
    };

    private void UpdateR2Navigation(string title)
    {
        if (ContextNav is null || KnowledgePages is null || SettingsPages is null) return;
        var group = NavigationGroup(title);
        NavigationItem[] entries = group switch
        {
            "启动工程" => [new("启动工程", "启动工程"), new("全部配置", "启动配置"), new("连接与核验", "工程会话"), new("能力与函数", "能力与函数"), new("原生 Form / Gadget", "原生 Form / Gadget")],
            "总览" => [new("Dashboard", "总览"), new("AI 运行", "AI 运行"), new("工具日志", "工具日志")],
            "Web 应用" => [new("HTML 应用管理器", "Web 应用"), new("PML 部署与验证", "插件部署")],
            "知识库" => [new("检索资料", "知识库", 0), new("个人笔记", "知识库", 1), new("资料源与回收站", "知识库", 2), new("目录与检索记录", "知识库", 3)],
            "设置" => [new("用户目录", "设置", 0), new("环境与路径", "设置", 1), new("外观", "设置", 2), new("登录凭据", "设置", 3), new("AI 客户端", "设置", 4), new("关于 Phayton", "设置", 5)],
            _ => [new("AI 运行", "AI 运行"), new("工具日志", "工具日志")]
        };
        rebuildingNavigation = true;
        try
        {
            ContextTitle.Text = group switch { "启动工程" => "工程", "Web 应用" => "应用与开发", _ => group };
            ContextDescription.Text = group switch { "启动工程" => "从一个工程开始", "知识库" => "让经验成为下次的起点", "设置" => "把工作空间调成你的习惯", "工程会话" => "连接、核验、读取与调用", "Web 应用" => "从界面到 PML", _ => "了解本机的运行状态" };
            RecentProjectsPanel.Visibility = group == "启动工程" ? Visibility.Visible : Visibility.Collapsed;
            ContextNav.Children.Clear();
            NavigationItem? selected = null;
            foreach (var item in entries)
            {
                var active = item.Page == title && (item.Section < 0 || item.Section == (title == "知识库" ? KnowledgePages.SelectedIndex : SettingsPages.SelectedIndex));
                if (active) selected = item;
                var button = new Button { Content = item.Label, Tag = item, Style = (Style)FindResource("NavButton"), Margin = new(0, 0, 0, 6), Padding = new(12, 12, 8, 12) };
                button.SetResourceReference(BackgroundProperty, active ? "Surface.TintBrand" : "Surface.Card");
                button.SetResourceReference(ForegroundProperty, active ? "Text.Brand" : "Text.Secondary");
                button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                button.Click += (_, _) => OpenNavigation(item);
                ContextNav.Children.Add(button);
            }
            CompactNavigation.ItemsSource = entries;
            CompactNavigation.SelectedItem = selected;
            foreach (var button in SidebarNav.Children.OfType<Button>())
            {
                var active = button.Uid == group;
                button.SetResourceReference(BackgroundProperty, active ? "Brand.NavSelected" : "Brand.Sidebar");
                button.SetResourceReference(ForegroundProperty, active ? "Brand.NavSelectedText" : "Brand.NavText");
                button.BorderThickness = new(0);
            }
            HeaderActionButton.Visibility = title == "设置" && SettingsPages.SelectedIndex != 1 ? Visibility.Collapsed : Visibility.Visible;
            if (title == "设置") PageSubtitleText.Text = selected?.Label + " · 修改由你决定";
            if (title == "知识库")
            {
                PageSubtitleText.Text = selected?.Label + " · 来源、正文与修订各有位置";
                HeaderActionText.Text = KnowledgePages.SelectedIndex == 1 ? "新建笔记" : "搜索知识";
            }
        }
        finally { rebuildingNavigation = false; }
    }

    private void OpenNavigation(NavigationItem item)
    {
        if (item.Page == "能力与函数") OpenFunctionsPage(this, new());
        else if (item.Page == "原生 Form / Gadget") OpenQueriesPage(this, new());
        else if (item.Page == "启动配置") OpenProfileLibrary(this, new());
        else
        {
            var tag = item.Page switch { "启动工程" => "0", "插件部署" => "1", "AI 运行" => "2", "工程会话" => "3", "总览" => "4", "设置" => "6", "工具日志" => "7", _ => "planned" };
            Navigate(new Button { Uid = item.Page, Tag = tag }, new());
            if (item.Section >= 0) (item.Page == "知识库" ? KnowledgePages : SettingsPages).SelectedIndex = item.Section;
        }
        SetPage(item.Page);
    }

    private void CompactNavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!rebuildingNavigation && ReferenceEquals(e.Source, CompactNavigation) && CompactNavigation.SelectedItem is NavigationItem item) OpenNavigation(item);
    }

    private void ContextPageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (rebuildingNavigation || PageTitleText is null) return;
        if (ReferenceEquals(e.Source, KnowledgePages) || ReferenceEquals(e.Source, SettingsPages)) UpdateR2Navigation(PageTitleText.Text);
    }

    private void ModuleChanged(object sender, RoutedEventArgs e) => UpdateLaunchDraftState();
    private void UpdateModuleButtons() => UpdateLaunchDraftState();

    private void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void VerifyR2NavigationSmoke()
    {
        if (SidebarNav.Children.OfType<Button>().Select(button => button.Uid).SequenceEqual(new[] { "总览", "启动工程", "Web 应用", "知识库", "设置" }) != true)
            throw new InvalidOperationException("主导航必须为 Dashboard 优先的五个工作区。");
        var launchDraft = CaptureLaunchDraft();
        var noteDraft = NoteContentBox.Text;
        foreach (var page in PageNames)
        {
            OpenNavigation(new(page, page));
            if (PageTitleText.Text != page || ContextNav.Children.Count == 0)
                throw new InvalidOperationException("分组导航无法到达：" + page);
        }
        for (var index = 0; index < SettingsPages.Items.Count; index++)
        {
            OpenNavigation(new("设置", "设置", index));
            if (SettingsPages.SelectedIndex != index) throw new InvalidOperationException("设置分组没有切换。");
        }
        for (var index = 0; index < KnowledgePages.Items.Count; index++)
        {
            OpenNavigation(new("知识库", "知识库", index));
            if (KnowledgePages.SelectedIndex != index) throw new InvalidOperationException("知识库分组没有切换。");
        }
        if (launchDraft != CaptureLaunchDraft() || noteDraft != NoteContentBox.Text)
            throw new InvalidOperationException("导航改变了未保存的输入。");
        if (System.Windows.Shell.WindowChrome.GetWindowChrome(this)?.CaptionHeight != 52)
            throw new InvalidOperationException("原生窗口边框未启用。");
        OpenNavigation(new("启动工程", "启动工程")); UpdateLayout(); MdbBox.ApplyTemplate();
        if (MdbBox.Template.FindName("PART_EditableTextBox", MdbBox) is not TextBox editor || editor.Text != MdbBox.Text)
            throw new InvalidOperationException("MDB 可编辑文本未显示。");
        var mdb = MdbBox.Text; editor.Text = "/CUSTOM_R2";
        if (ReadProfile().Mdb != "/CUSTOM_R2") throw new InvalidOperationException("MDB 手动输入未进入启动草稿。");
        MdbBox.Text = mdb;
        var module = ReadModuleValue();
        ModuleBox.SelectedValue = "Paragon";
        if (!ReadProfile().Module.Equals("Paragon", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("模块选项未连接既有启动参数。");
        RefreshProductModules(module, false);
        SettingsPages.SelectedIndex = 0; KnowledgePages.SelectedIndex = 0;
    }
}
