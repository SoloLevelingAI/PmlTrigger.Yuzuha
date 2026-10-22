using System.Windows;

namespace YuzuhaToolkit.Desktop;

/// <summary>按设计变量切换资源字典；不重建页面、目标连接或用户输入。</summary>
internal static class DesktopTheme
{
    /// <summary>当前显示模式。</summary>
    internal static string Current { get; private set; } = "Light";
    /// <summary>只切换绘制资源；保存偏好由用户操作单独触发。</summary>
    internal static void Apply(string mode)
    {
        if (mode is not ("Light" or "Dark")) throw new ArgumentException("Unknown desktop theme.", nameof(mode));
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var next = new ResourceDictionary { Source = new Uri("Themes/" + mode + ".xaml", UriKind.Relative) };
        var old = dictionaries.FirstOrDefault(item => item.Source?.OriginalString.StartsWith("Themes/", StringComparison.Ordinal) == true);
        if (old is null) dictionaries.Add(next); else dictionaries[dictionaries.IndexOf(old)] = next;
        Current = mode;
    }
}
