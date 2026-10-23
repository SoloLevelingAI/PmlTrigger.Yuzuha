using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Desktop;

/// <summary>启动配置的产品选择和只读 LaunchDesk 草稿带入。</summary>
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
        ExecutableBox.Text=draft.Executable; InitializationBox.Text=draft.Initialization;
        WorkingBox.Text=draft.WorkingDirectory; ProductBox.Text=draft.Product; KindBox.SelectedIndex=draft.Kind=="Monitor"?0:1;
        if(_selected is null && string.IsNullOrWhiteSpace(ProjectBox.Text)) ModuleBox.Text=draft.Module;
        LaunchApplicationSummary.Text=$"{draft.Kind} · {draft.Executable}\n工作目录：{draft.WorkingDirectory}";
        Say("已自动填入产品入口、初始化文件和工作目录；填写项目、用户即可继续。");
    }
    private void ReadLaunchDeskDrafts(object sender,RoutedEventArgs e)
    {
        try
        {
            string? detected=null;
            foreach(var process in Process.GetProcessesByName("LaunchDesk"))
            {
                using(process)
                {
                    try
                    {
                        var directory=Path.GetDirectoryName(process.MainModule?.FileName);
                        if(directory is not null && File.Exists(Path.Combine(directory,"Data","workspace.json"))) detected=Path.Combine(directory,"Data","workspace.json");
                    }
                    catch(System.ComponentModel.Win32Exception) { }
                }
            }
            var dialog=new OpenFileDialog { Title="选择 LaunchDesk 的 Data/workspace.json",Filter="LaunchDesk 配置|workspace.json|JSON 文件|*.json",
                FileName=detected??"workspace.json",InitialDirectory=detected is null?"":Path.GetDirectoryName(detected)! };
            if(dialog.ShowDialog(this)!=true) return;
            LaunchDeskDraftsBox.ItemsSource=LaunchDrafts.ReadLaunchDesk(dialog.FileName);
            LaunchDeskDraftsPanel.Visibility=Visibility.Visible; LaunchDeskDraftsBox.SelectedIndex=0;
            Say("已读取 LaunchDesk 非密码配置；选择一项带入草稿，尚未保存或启动。");
        }
        catch(Exception error) { Say(error.Message); }
    }
    private void UseLaunchDeskDraft(object sender,RoutedEventArgs e)
    {
        if(LaunchDeskDraftsBox.SelectedItem is not LaunchProfile draft) return;
        ProfilesBox.SelectedItem=null; _selected=null; FillProfileFields(draft);
        ProfileDetailsExpander.IsExpanded=false; LaunchPathsExpander.IsExpanded=false;
        LaunchApplicationSummary.Text="已带入 "+draft.Name+"；路径与登录字段可直接编辑。";
        Say("已带入未保存草稿；密码需在 Yuzuha 输入一次，许可沿用当前环境。点击保存或保存并启动生效。");
    }
}
