using System.Windows;
using System.Windows.Controls;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>在用户输入完整配置名称后才允许删除本机启动配置。</summary>
public partial class ProfileDeleteDialog : Window
{
    private readonly string _profileName;
    /// <summary>展示实际配置名称、默认切换和运行会话的影响范围。</summary>
    /// <param name="profile">用户明确选择删除的配置。</param>
    /// <param name="remainingCount">删除后保留的配置数量。</param>
    public ProfileDeleteDialog(LaunchProfile profile, int remainingCount)
    {
        _profileName = profile.Name;
        InitializeComponent();
        ProfileNameText.Text = profile.Name;
        DefaultImpactText.Text = profile.IsDefault
            ? remainingCount > 0 ? "这是一套默认配置。删除后，第一套剩余配置将成为默认配置。" : "这是一套默认配置。删除后将没有默认配置。"
            : "当前默认配置不会改变。";
    }
    private void ConfirmationChanged(object sender, TextChangedEventArgs e)
    {
        if (DeleteButton is not null) DeleteButton.IsEnabled = string.Equals(ConfirmationBox.Text, _profileName, StringComparison.Ordinal);
    }
    private void ConfirmDelete(object sender, RoutedEventArgs e)
    {
        if (DeleteButton.IsEnabled) DialogResult = true;
    }
}
