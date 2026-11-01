using System.Windows;

namespace YuzuhaToolkit.Desktop;

/// <summary>按设计变量切换资源字典；不重建页面、目标连接或用户输入。</summary>
internal static class DesktopTheme
{
    /// <summary>当前显示模式。</summary>
    internal static string Current { get; private set; } = "Light";
    /// <summary>窗口边框使用主题明暗模式，与具体配色身份分开。</summary>
    internal static bool IsDark => Current == "Dark" || DesktopPalettes.All.FirstOrDefault(p => p.Id == Current)?.Scheme == "Dark";
    /// <summary>只切换绘制资源；保存偏好由用户操作单独触发。</summary>
    internal static void Apply(string mode)
    {
        var palette = DesktopPalettes.All.FirstOrDefault(p => p.Id == mode);
        if (mode is not ("Light" or "Dark") && palette is null) throw new ArgumentException("Unknown desktop theme.", nameof(mode));
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        foreach (var old in dictionaries.Where(item => item.Source?.OriginalString.StartsWith("Themes/", StringComparison.Ordinal) == true).ToArray()) dictionaries.Remove(old);
        dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/" + (palette?.Scheme ?? mode) + ".xaml", UriKind.Relative) });
        if (palette is not null) dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/Ion-" + palette.Id[4..] + ".xaml", UriKind.Relative) });
        Current = mode;
        if (Application.Current.MainWindow is { } window) NativeWindowFrame.Apply(window);
    }
}
