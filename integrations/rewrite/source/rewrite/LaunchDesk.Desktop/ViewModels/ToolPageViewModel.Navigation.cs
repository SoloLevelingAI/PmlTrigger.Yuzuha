namespace LaunchDesk.Desktop;

/// <summary>来源选择、计划预览、摘要绑定确认及结果导航。</summary>
public sealed partial class ToolPageViewModel
{
    /// <summary>选择工具并进入配置。</summary>
    public ParameterCommand<ToolDefinition> ConfigureCommand => new(tool => { Selected = tool; State.Navigate(PageId.ToolSettings); });
    /// <summary>选择工具并进入部署。</summary>
    public ParameterCommand<ToolDefinition> OpenDeploymentCommand => new(tool => { Selected = tool; State.Navigate(PageId.Deployment); });
    /// <summary>返回工具库。</summary>
    public RelayCommand BackCommand => new(() => State.Navigate(PageId.Tools));
    /// <summary>编辑当前工具。</summary>
    public RelayCommand EditSelectedCommand => new(() => State.Navigate(PageId.ToolSettings));
    /// <summary>进入当前工具部署。</summary>
    public RelayCommand OpenSelectedDeploymentCommand => new(() => State.Navigate(PageId.Deployment));
    private bool editingVariable;
    /// <summary>是否显示变量编辑与预览面板。</summary>
    public bool IsEditingVariable { get => editingVariable; set => Set(ref editingVariable, value); }
    /// <summary>编辑或重命名所选变量。</summary>
    public RelayCommand EditVariableCommand => new(() => IsEditingVariable = true, () => SelectedVariable is not null);
    /// <summary>关闭变量编辑。</summary>
    public RelayCommand CloseVariableEditorCommand => new(() => IsEditingVariable = false);
    private bool editingFiles;
    /// <summary>是否显示普通文件映射编辑面板。</summary>
    public bool IsEditingFiles { get => editingFiles; set => Set(ref editingFiles, value); }
    /// <summary>编辑普通文件映射。</summary>
    public RelayCommand EditFilesCommand => new(() => IsEditingFiles = true);
    /// <summary>关闭普通文件映射编辑。</summary>
    public RelayCommand CloseFilesCommand => new(() => IsEditingFiles = false);
    /// <summary>通过稳定工具标识找到的关联登录名称。</summary>
    public string Associations => string.Join(" · ", State.Snapshot.Profiles.Where(p => p.DisplayFields.ToolIds.Contains(Draft.Id)).Select(p => p.Name)) is { Length: > 0 } names ? names : "尚未关联登录配置";
}
