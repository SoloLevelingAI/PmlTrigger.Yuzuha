using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Core30.Core;
using Core30.Ui;
using Core30.Workbench;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow : ChromeWindow
{
    private sealed record Route(string Id, string Group, string Label, string Title, string Detail);
    private static readonly Route[] Routes =
    [
        new("Home", "Environment", "状态检查", "会话工作台", ""),
        new("Ev", "Environment", "EVARS", "配置 EVARS", "检查当前安装的启动文件与配置路径。"),
        new("Launch", "Launch", "登录", "启动器", "选择保存的配置并打开对应工程模块。"),
        new("Profiles", "Launch", "配置管理", "管理登录配置", "新增、修改、复制和归档登录配置。"),
        new("Cli", "Session", "CLI 状态", "PmlTwin CLI 状态", "查看当前 AVEVA 会话的 CLI 连接。"),
        new("Functions", "Session", "PML 函数", "PML 函数", "登记或执行函数，在日志中查看回执。"),
        new("Logs", "Session", "执行日志", "PmlTwin 执行日志", "按状态筛选，查看退出码及输出。"),
        new("Folders", "Session", "文件部署", "文件部署", "同步共用 PML 资源，统一预览各版本的 EVARS 接入。"),
        new("Record", "Session", "宏录制", "宏录制", "选择要录制的 AVEVA 窗体，记录操作并保存宏。"),
        new("Macros", "Session", "宏记录", "宏记录", "按日期和名称查找宏，评审后生成可复用的经验。"),
        new("Sag", "SAG", "正文检索", "SAG 正文检索", "导入 HTML、Markdown、TXT 或未加密 PML，搜索正文并打开原文件。"),
        new("Docs", "SAG", "资料部署", "SAG 资料部署", "检查并部署 HTML、PDF、Markdown 和文本。"),
        new("CheckMacro", "SAG", "宏检查", "SAG 宏检查", "读取或导入宏，核对流程并生成经验。"),
        new("Settings", "Settings", "常规", "PmlTwin 设置", "管理 PML 只读模式和 SAG 默认资料库。"),
        new("About", "Settings", "关于", "关于 PmlTwin", "查看产品和维护者信息。"),
        new("Update", "Settings", "更新", "更新组件", "管理 PmlTwin 安装包和 SAG 组件。"),
        new("Feedback", "Settings", "Bug 反馈", "Bug 反馈", "记录问题及复现步骤，复制或导出。")
    ];
    private static readonly (string Key, string Title, string Glyph, string First)[] Areas =
    [
        ("Environment", "首页", "Overview", "Home"), ("Launch", "启动器", "Grid", "Launch"),
        ("Session", "会话与 CLI", "Terminal", "Cli"), ("SAG", "知识库", "Book", "Sag"),
        ("Settings", "PmlTwin 设置", "Settings", "Settings")
    ];
    private readonly WorkbenchStore _store;
    private WorkbenchData Data => _store.Data;
    private readonly DispatcherTimer _toast = new() { Interval = TimeSpan.FromSeconds(4) };
    private Route _page = Routes[0];
    private string _install = "";
    private bool _readOnly => PmlChannelMode.Read() == PmlChannelMode.ReadOnly;
    private bool _compact;
    private string _latestDiscovery = "";

    internal MainWindow(WorkbenchStore store)
    {
        _store = store;
        InitializeComponent();
        WorkspaceLabel.ToolTip = store.DirectoryPath;
        _toast.Tick += (_, _) => { StatusBar.Visibility = Visibility.Collapsed; _toast.Stop(); };
        SizeChanged += (_, e) =>
        {
            var next = e.NewSize.Width < 980;
            if (next == _compact) return;
            _compact = next;
            RailWidth.Width = new GridLength(next ? 68 : 196);
            BrandText.Visibility = WorkspaceLabel.Visibility = next ? Visibility.Collapsed : Visibility.Visible;
            RenderNav();
        };
        Loaded += async (_, _) =>
        {
            FitInitialWindow();
            Render();
            if (_store.LoadWarning.Length > 0) Tell(_store.LoadWarning);
            await Task.WhenAll(ScanInstallations(), RefreshSessions());
        };
    }

    private void FitInitialWindow()
    {
        var work = SystemParameters.WorkArea;
        if (Width <= work.Width - 12 && Height <= work.Height - 12) return;
        Width = Math.Max(MinWidth, Math.Min(Width, work.Width - 12));
        Height = Math.Max(MinHeight, Math.Min(Height, work.Height - 12));
        Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
        Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
    }

    private void Go(string id)
    {
        _page = Routes.First(route => route.Id == id);
        StatusBar.Visibility = Visibility.Collapsed;
        Render();
        Viewport.ScrollToHome();
        if (id is "Cli" or "Functions" or "Record") _ = RefreshSessions();
        if (id == "Functions") _ = RefreshFunctions();
        if (id == "Sag" && !_sagInitialized) _ = RefreshSag();
    }

    private void RenderNav()
    {
        Navigation.Children.Clear();
        FooterNavigation.Children.Clear();
        foreach (var (key, title, glyph, first) in Areas)
        {
            var active = _page.Group == key;
            var button = Id(Styled(new ToggleButton
            {
                Content = IconLabel(glyph, title, _compact, active ? "primary" : "secondary"), IsChecked = active,
                ToolTip = title, Padding = new Thickness(_compact ? 8 : 12, 0, 0, 0), Margin = new Thickness(0, 0, 0, 4)
            }, "UiNavigationButton"), "Nav" + key);
            AutomationProperties.SetName(button, title);
            button.Checked += (_, _) => Go(first);
            button.Unchecked += (_, _) =>
            {
                if (_page.Group == key) button.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            };
            (key == "Settings" ? FooterNavigation : Navigation).Children.Add(button);
        }
    }

    private void Render()
    {
        RenderNav();
        Breadcrumb.Text = _page.Id == "Home" ? "首页" : Areas.First(area => area.Key == _page.Group).Title;
        Header.Content = _page.Id == "Home" ? HomeHero() : PageHeading();
        Tabs.Content = _page.Id == "Home" ? null : SectionTabs();
        Body.Content = Below(_page.Id switch
        {
            "Home" => HomePage(), "Ev" => EvarsPage(),
            "Launch" => LaunchPage(false), "Profiles" => LaunchPage(true),
            "Cli" => CliPage(), "Functions" => FunctionsPage(), "Logs" => LogsPage(), "Folders" => FoldersPage(),
            "Record" => RecorderPage(), "Macros" => MacrosPage(false),
            "Sag" => SagPage(), "Docs" => DocumentsPage(), "CheckMacro" => MacrosPage(true),
            "Settings" => SettingsPage(), "About" => AboutPage(), "Update" => UpdatesPage(), "Feedback" => FeedbackPage(),
            _ => throw new InvalidOperationException(_page.Id)
        }, 9);
    }

    private FrameworkElement PageHeading()
    {
        var title = Id(Views.Text(_page.Title, "Text.Section"), "PageTitle");
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        var icon = new Border
        {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = Brush("selected"),
            Child = Glyph(Areas.First(area => area.Key == _page.Group).Glyph, 21), Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        return Leading(icon, Stack(title, Below(Muted(_page.Detail), 3)));
    }

    private FrameworkElement HomeHero()
    {
        var title = Id(Views.Text("会话工作台", "Text.Section"), "PageTitle");
        title.FontSize = 29;
        title.FontWeight = FontWeights.Bold;
        var canvas = new Grid { ClipToBounds = true };
        canvas.SizeChanged += (_, e) => canvas.Clip = new RectangleGeometry(new Rect(e.NewSize), 17, 17);
        var art = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/PmlTwin.Workbench;component/Assets/process-skid.png")),
            Stretch = Stretch.UniformToFill, Width = 255, Height = 135, Opacity = .59,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            OpacityMask = new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(0, .5), new Point(.48, .5))
        };
        canvas.Children.Add(art);
        var headline = new DockPanel { Margin = new Thickness(20, 9, 14, 9), VerticalAlignment = VerticalAlignment.Center };
        var mode = ReadOnlyButton();
        DockPanel.SetDock(mode, Dock.Right);
        headline.Children.Add(mode);
        var product = Muted("PmlTwin");
        product.Foreground = Brush("primary");
        headline.Children.Add(Stack(product, title));
        canvas.Children.Add(headline);
        var hero = new Border
        {
            BorderBrush = Brush("hero-edge"), BorderThickness = new Thickness(1),
            Background = Brush("hero"), CornerRadius = new CornerRadius(18), Height = 88, Child = canvas
        };
        return hero;
    }

    private FrameworkElement SectionTabs()
    {
        var currentTab = _page.Id == "Macros" ? "Record" : _page.Id;
        var tabs = Actions(Routes.Where(route => route.Group == _page.Group && route.Id != "Macros").Select(route =>
            Btn(route.Label, "Tab" + route.Id, () => Go(route.Id), route.Id == currentTab ? "Button.Soft" : "Button.Quiet")).ToArray());
        var row = _page.Group == "Session" ? Leading(ReadOnlyButton(), tabs, Dock.Right) : (FrameworkElement)tabs;
        return new Border { Margin = new Thickness(0, 8, 0, 0), BorderBrush = Brush("border"), BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
    }

    private Button ReadOnlyButton()
    {
        var mode = _readOnly;
        var button = Btn("", "ReadOnlyToggle", () =>
        {
            var enabled = !_readOnly;
            if (!enabled && !Confirm("恢复 PML 执行", "恢复后，已授权的客户端可以再次向会话提交执行请求。", "恢复执行")) return;
            try
            {
                PmlChannelMode.SetReadOnly(enabled);
                if (_readOnly != enabled) throw new IOException("未能核验 Windows 的 PML 通道模式。");
                Render();
                var message = enabled ? "已开启只读模式，可用结构化查询和已授权的只读函数。" : "已恢复当前用户的 PML 执行模式。";
                if (enabled && _sessions.Any(row => !row.EnforcesReadOnly)) message += " 有会话尚未确认支持只读模式，请更新安装包后重开会话。";
                else if (enabled && _session is { SupportsReviewedFunctions: false }) message += " 当前会话需更新 Host 才能调用只读函数。";
                else if (enabled && _session is { ReviewedFunctionLibraryReady: false }) message += " 当前会话需注册只读函数目录后重启。";
                if (enabled && RecordingNeedsRestore)
                    message += " 当前录制的回调等待恢复。";
                Tell(message);
            }
            catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
        }, mode ? "Button.Soft" : "Button.Toolbar");
        button.Content = mode ? "只读 · 开启" : "只读 · 关闭";
        AutomationProperties.SetName(button, "PmlTwin " + button.Content);
        return button;
    }

    private void Tell(string message)
    {
        StatusBar.Text = message;
        StatusBar.Visibility = Visibility.Visible;
        _toast.Stop();
        _toast.Start();
    }

    private void Save(string? message = null)
    {
        try
        {
            _store.Save();
            if (message is not null) Tell(message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Tell("工作区保存失败。变更暂存于本次运行。");
        }
    }

    private static Button Btn(string label, string id, Action action, string style = "Button.Base")
    {
        var button = ActionButton(label, action, id, style);
        button.MinHeight = 32;
        button.Padding = new Thickness(10, 2, 10, 2);
        return button;
    }

    private static Button Primary(string label, string id, Action action) => Btn(label, id, action, "Button.Primary");
    private Button Jump(string label, string route) => Btn(label, "Open" + route, () => Go(route), "Button.Quiet");
    private static FrameworkElement Leading(FrameworkElement leading, FrameworkElement body, Dock dock = Dock.Left)
    {
        var row = new DockPanel();
        DockPanel.SetDock(leading, dock);
        row.Children.Add(leading);
        row.Children.Add(body);
        return row;
    }

    private static TextBox Code(string text, string id, double height = 122)
    {
        var box = Id(new TextBox
        {
            Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Height = height
        }, id);
        box.SetResourceReference(FontFamilyProperty, "font-family.mono");
        return box;
    }

    private static CheckBox Check(string text, string id, bool enabled = false)
    {
        var check = Id(Styled(new CheckBox { Content = text, IsChecked = enabled }, "UiCheckBox"), id);
        AutomationProperties.SetName(check, text);
        return check;
    }

    private bool Modal(string title, FrameworkElement content, string action, Func<bool> accept, Control? focus = null,
        double width = 580, bool cancel = true)
    {
        var dialog = new DialogWindow(this, title, width: width);
        content.Margin = new Thickness(0, 2, 0, 0);
        dialog.Body.Children.Add(content);
        dialog.SetActions(action, accept, cancel);
        if (focus is not null) dialog.FocusOnOpen(focus);
        return dialog.ShowDialog() == true;
    }

    private bool Confirm(string title, string message, string button) => Modal(title, Views.Text(message), button, () => true);
}
