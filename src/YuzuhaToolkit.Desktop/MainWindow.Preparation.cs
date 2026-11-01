using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>把安装交接、原生准备工作区、配色及精确文档入口接入已有主窗口。</summary>
public partial class MainWindow
{
    /// <summary>避免两个配色选择器同步时递归保存。</summary>
    private bool choosingPalette;
    /// <summary>连接现有导航、文档与配色服务；只填充默认输入。</summary>
    private void InitializePreparation()
    {
        PreparationView.NavigationRequested += page => OpenNavigation(new(page, page));
        PreparationView.DocumentationRequested += page => _ = OpenDocumentationAt(page);
        PreparationView.PaletteRequested += ApplyPalette;
        ThemePalettePicker.ItemsSource = DesktopPalettes.All;
        PreparationView.PrepareInstallation(HtmlAppLauncher.FindRoot());
        UpdateThemeButtons();
    }
    /// <summary>从安装器入口进入准备工作区；参数仅填入草稿，不是写入授权。</summary>
    public void OpenPreparation(string? packageRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(packageRoot)) PreparationView.PrepareInstallation(packageRoot);
        Tabs.SelectedIndex = 9;SetPage("安装与环境准备");
    }
    /// <summary>响应Dashboard的准备入口。</summary>
    private void OpenPreparationClick(object sender, RoutedEventArgs e) => OpenPreparation();
    /// <summary>处理设置页的明确配色选择，避免同步选项时递归保存。</summary>
    private void ChoosePalette(object sender, SelectionChangedEventArgs e)
    {
        if (!choosingPalette && ThemePalettePicker.SelectedValue is string palette && palette != DesktopTheme.Current) ApplyPalette(palette);
    }
    /// <summary>应用原生资源并只保存显示偏好，保留所有工作草稿。</summary>
    private void ApplyPalette(string palette)
    {
        if (choosingPalette) return;
        choosingPalette = true;
        try { DesktopTheme.Apply(palette);DesktopDisplaySettings.SaveTheme(WorkspaceSettings.StateDirectory, palette);UpdateThemeButtons(); }
        catch (Exception error) { Say("配色切换/保存失败：" + error.Message); }
        finally { choosingPalette = false; }
    }
}
