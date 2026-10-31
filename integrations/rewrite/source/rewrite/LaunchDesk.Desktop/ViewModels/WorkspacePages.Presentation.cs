namespace LaunchDesk.Desktop;

/// <summary>选择性导出、导入预览、明确提交及原提交号核验。</summary>
public sealed partial class SharingPageViewModel
{
    /// <summary>本次导出可明确选择的登录集合。</summary>
    public ObservableCollection<ToolChoice> ProfileChoices { get; } = [];
    /// <summary>本次导出可明确选择的独立工具集合。</summary>
    public ObservableCollection<ToolChoice> ToolChoices { get; } = [];
    private bool includeTools = true;
    /// <summary>是否含所选登录关联的工具定义。</summary>
    public bool IncludeTools { get => includeTools; set => Set(ref includeTools, value); }
    /// <summary>全选登录和工具。</summary>
    public RelayCommand SelectAllCommand => new(() => { foreach (var choice in ProfileChoices.Concat(ToolChoices)) choice.Selected = true; });
    /// <summary>清空登录和工具选择。</summary>
    public RelayCommand ClearSelectionCommand => new(() => { foreach (var choice in ProfileChoices.Concat(ToolChoices)) choice.Selected = false; });
    private void RefreshChoices()
    {
        var profiles = ProfileChoices.ToDictionary(p => p.Id, p => p.Selected);
        var tools = ToolChoices.ToDictionary(p => p.Id, p => p.Selected);
        ProfileChoices.Clear(); ToolChoices.Clear();
        foreach (var profile in state.Snapshot.Profiles) ProfileChoices.Add(new(profile.Id, profile.Name, profiles.GetValueOrDefault(profile.Id, true)));
        foreach (var tool in state.Snapshot.Tools) ToolChoices.Add(new(tool.Id, tool.Name, tools.GetValueOrDefault(tool.Id, false)));
    }
    /// <summary>仅导出指定工具的非秘密定义，不含实际文件内容或运行时回执。</summary>
    /// <param name="toolId">关联工具的稳定标识</param>
    /// <param name="path">明确选择的文件或目录路径。</param>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask ExportToolAsync(Guid toolId, string path, CancellationToken ct)
    {
        var bundle = await sharing.ExportAsync(new([], true, [toolId]), ct);
        if (!bundle.IsSuccess) { Error(bundle.Error!.SafeMessage); return; }
        var result = await documents.WriteShareAsync(path, bundle.Value, ct);
        state.Message = result.IsSuccess ? "工具配置已导出，文件包内容、凭据和更新回执不在分享文件内。" : result.Error!.SafeMessage;
    }
}

/// <summary>管理数据目录、旧版迁移预览与不确定结果核验入口。</summary>
public sealed partial class SettingsPageViewModel
{
    /// <summary>核验原迁移副本启用身份。</summary>
    public AsyncCommand VerifyMigrationCommand { get; }
}

/// <summary>组合各页面模型、导航与操作记录，不直接访问平台服务。</summary>
public sealed partial class ShellViewModel
{
    /// <summary>随窗口尺寸计算的设计度量。</summary>
    public DesignLayout Layout { get; } = new();
    /// <summary>查询并显示原操作记录。</summary>
    public AsyncCommand InspectRunCommand { get; }
    private bool taskDetailsOpen;
    /// <summary>是否显示原操作详情面板。</summary>
    public bool IsTaskDetailsOpen { get => taskDetailsOpen; set => Set(ref taskDetailsOpen, value); }
    /// <summary>关闭操作详情。</summary>
    public RelayCommand CloseTaskDetailsCommand => new(() => IsTaskDetailsOpen = false);
    /// <summary>进入工具库。</summary>
    public RelayCommand GoToolsCommand => new(() => Page = PageId.Tools);
    /// <summary>进入导入与分享。</summary>
    public RelayCommand GoSharingCommand => new(() => Page = PageId.Sharing);
    /// <summary>进入设置与迁移。</summary>
    public RelayCommand GoSettingsCommand => new(() => Page = PageId.Settings);
    /// <summary>工作台主标题。</summary>
    public string DashboardTitle => "每个版本，都有自己的入口";
    /// <summary>当前页面主操作图标。</summary>
    public string HeaderIcon => CurrentPage.Header.Icon;
    /// <summary>当前页面次操作按钮文字。</summary>
    public string SecondaryText => SecondaryAction.Text;
    /// <summary>当前页面次操作图标。</summary>
    public string SecondaryIcon => SecondaryAction.Icon;
    /// <summary>执行当前页面次操作。</summary>
    public ICommand SecondaryCommand => SecondaryAction.Command(this);
    /// <summary>当前页面主操作按钮文字。</summary>
    public string HeaderAction => CurrentPage.Header.Text;
    /// <summary>执行当前页面主操作。</summary>
    public ICommand HeaderCommand => CurrentPage.Header.Command(this);
    /// <summary>工作台展示的前两个许可连接投影。</summary>
    public IEnumerable<LicenseRow> SummaryResources => Workspace.Resources.Take(2);
    /// <summary>当前工作空间登记的工具名称摘要。</summary>
    public string ToolNames => Workspace.Snapshot.Tools.Length == 0 ? "尚未添加工具" : string.Join(" / ", Workspace.Snapshot.Tools.Select(t => t.Name));
    /// <summary>当前登记工具数量的可读说明。</summary>
    public string ToolCountLabel => $"工具与扩展 · {Workspace.Snapshot.Tools.Length} 项";
    /// <summary>选中登录的本机绑定核对说明。</summary>
    public string DashboardCheck => Profiles.Selected is null ? "请选择登录配置" : Profiles.Selected.Profile.BindingStatus;
    /// <summary>工作台核对提示的语义底色。</summary>
    public string DashboardCheckBackground => "#EEF0FF";
    /// <summary>最近观察到的原操作投影。</summary>
    public RunRow? LatestRun => Workspace.Runs.FirstOrDefault();
    /// <summary>是否有可查询的操作记录。</summary>
    public bool HasRun => Workspace.Runs.Count > 0;
    /// <summary>登录、应用与许可条目数量的摘要。</summary>
    public string WorkspaceCounts => $"{Workspace.Snapshot.Profiles.Length} 个登录配置 · {Workspace.Snapshot.Applications.Length} 个应用入口 · {Workspace.Snapshot.LicenseEndpoints.Length} 套许可连接";
    private void InitializePresentation()
    {
        Workspace.WorkspaceChanged += RefreshSummary;
        Workspace.Resources.CollectionChanged += (_, _) => Raise(nameof(SummaryResources));
        Workspace.Runs.CollectionChanged += (_, _) => { Notify(nameof(LatestRun), nameof(HasRun)); };
        Profiles.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Profiles.Selected)) Raise(nameof(DashboardCheck)); };
    }
    private void RefreshSummary()
    {
        Notify(nameof(SummaryResources), nameof(ToolNames), nameof(ToolCountLabel), nameof(WorkspaceCounts), nameof(DashboardCheck));
    }
}
