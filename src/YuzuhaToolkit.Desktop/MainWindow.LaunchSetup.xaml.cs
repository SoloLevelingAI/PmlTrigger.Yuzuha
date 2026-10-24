using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>按安装入口带入产品信息，并刷新对应的模块目录。</summary>
public partial class MainWindow
{
    private bool fillingLaunchApplication;
    /// <summary>用户可选的本机产品入口，保留完整路径供使用前核对。</summary>
    private sealed record ApplicationChoice(string Path)
    {
        /// <summary>在日常下拉框显示版本目录及启动程序名。</summary>
        public string Label => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path))+" · "+System.IO.Path.GetFileName(Path);
    }
    private void PopulateLaunchApplications(IEnumerable<RegistrationCandidate> candidates)
    {
        fillingLaunchApplication=true;
        try
        {
            LaunchApplicationBox.ItemsSource=candidates.Select(c=>new ApplicationChoice(c.Path)).ToArray();
            LaunchApplicationBox.SelectedItem=LaunchApplicationBox.Items.OfType<ApplicationChoice>().FirstOrDefault(c=>c.Path.Equals(ExecutableBox.Text,StringComparison.OrdinalIgnoreCase));
        }
        finally { fillingLaunchApplication=false; }
    }
    private void LaunchApplicationSelected(object sender,SelectionChangedEventArgs e)
    {
        if(fillingLaunchApplication || LaunchApplicationBox.SelectedItem is not ApplicationChoice choice) return;
        ApplyLaunchApplication(choice.Path);
    }
    private void ApplyLaunchApplication(string path)
    {
        var draft=LaunchDrafts.FromApplication(path);
        _fillingLaunchDraft = true;
        try
        {
            ExecutableBox.Text = draft.Executable; InitializationBox.Text = draft.Initialization;
            WorkingBox.Text = draft.WorkingDirectory; ProductBox.Text = draft.Product; VersionBox.Text = draft.Version;
            KindBox.SelectedIndex = draft.Kind == "Monitor" ? 0 : 1;
        }
        finally { _fillingLaunchDraft = false; }
        RefreshProductModules(draft.Module, chooseDefault: true);
        LaunchApplicationSummary.Text = $"{draft.Product} {draft.Version} · {draft.Executable}\n工作目录：{draft.WorkingDirectory}";
        UpdateLaunchDraftState();
        Say("已带入产品与入口，并切换到对应模块列表；请核对工程、用户和 MDB 后保存。");
    }
}
