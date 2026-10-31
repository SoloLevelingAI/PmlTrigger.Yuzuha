using System.Windows.Shell;

namespace LaunchDesk.Desktop;

/// <summary>桌面主题偏好；当前提供核定的浅色主题。</summary>
public enum ThemePreference
{
    /// <summary>核定的浅色主题。</summary>
    Light
}

/// <summary>核定主题的颜色及布局度量快照。</summary>
/// <param name="Name">显示名或配置名</param>
/// <param name="Colors">核定的语义颜色名称与值</param>
/// <param name="CardRadius">卡片圆角半径</param>
/// <param name="SidebarWidth">固定侧栏宽度</param>
public sealed record ThemeSnapshot(
    string Name,
    ImmutableDictionary<string, string> Colors,
    double CardRadius,
    double SidebarWidth);

/// <summary>集中管理经批准的视觉令牌和控件状态资源。</summary>
public sealed class ThemeResourceCatalog
{
    /// <summary>解析显式资源键及主题；图稿缺失则标记未核定，不编造颜色/间距/图标/圆角验收值。</summary>
    /// <param name="preference">需解析的已支持主题偏好。</param>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/returns"/>
    public Result<ThemeSnapshot> Resolve(ThemePreference preference)
    {
        if (preference != ThemePreference.Light || Application.Current is null)
            return Result<ThemeSnapshot>.Fail("ThemeUnavailable", "当前只核定了浅色主题。");
        var names = new[] { "CanvasBrush", "InkBrush", "MutedBrush", "AccentBrush", "LineBrush" };
        if (names.Any(name => Application.Current.TryFindResource(name) is not System.Windows.Media.SolidColorBrush))
            return Result<ThemeSnapshot>.Fail("MissingResource", "主题颜色资源不完整。");
        if (Application.Current.TryFindResource("Icon.Dashboard") is null)
            return Result<ThemeSnapshot>.Fail("MissingResource", "图标资源不完整。");
        return Result<ThemeSnapshot>.Ok(new ThemeSnapshot("FigCraft v2 · Light",
            names.ToImmutableDictionary(name => name,
                name => ((System.Windows.Media.SolidColorBrush)Application.Current.FindResource(name)).Color
                    .ToString()), 12, 220));
    }

    /// <summary>把核定的浅色主题、图标和设计页面模板合并到指定 WPF 应用资源。</summary>
    /// <param name="app">需应用核定主题资源的 WPF 应用实例。</param>
    public void Apply(Application app)
    {
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/LaunchDesk.Desktop;component/Resources/Theme.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/LaunchDesk.Desktop;component/Resources/Icons.xaml", UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/LaunchDesk.Desktop;component/Resources/Pages.xaml", UriKind.Relative) });
    }
}
