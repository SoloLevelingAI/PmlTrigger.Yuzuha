namespace LaunchDesk.Desktop;

public sealed partial class SharingPageViewModel : PageViewModel
{
    private readonly IConfigurationShareService sharing;
    private readonly IConfigurationDocuments documents;
    private ImportProposal? proposal;
    private bool confirmed;

    /// <summary>只读预览或脱敏说明。</summary>
    public string Preview => proposal is null
        ? "选择配置文件后先核对内容，再导入。"
        : $"应用 {proposal.Bundle.Applications.Length} · 登录 {proposal.Bundle.Profiles.Length} · 许可 {proposal.Bundle.LicenseEndpoints.Length} · 工具 {proposal.Bundle.Tools.Length}\n\n" +
          string.Join("\n", proposal.Notes);

    /// <summary>用户是否确认本机路径、许可与工具绑定需核验。</summary>
    public bool Confirmed
    {
        get => confirmed;
        set
        {
            Set(ref confirmed, value);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>导出明确选择的非秘密定义。</summary>
    public AsyncCommand ExportCommand { get; }
    /// <summary>生成只读预览。</summary>
    public AsyncCommand PreviewCommand { get; }
    /// <summary>确认导入本机已核对配置。</summary>
    public AsyncCommand ImportCommand { get; }
    /// <summary>按原编号核验导入。</summary>
    public AsyncCommand VerifyImportCommand { get; }
    /// <summary>最近一次导入使用的原提交号。</summary>
    public CommitId? LastImportId { get; private set; }

    /// <summary>连接分享文件读写、导入预览、稳定提交及原提交核验。</summary>
    /// <param name="sharing">分享配置用例端口。</param>
    /// <param name="documents">分享文件读写端口。</param>
    /// <param name="state">公开页面状态。</param>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <include file="../../Comments.xml" path="doc/Dialogs/*"/>
    public SharingPageViewModel(IConfigurationShareService sharing, IConfigurationDocuments documents,
        WorkspacePresentationState state, ICatalogService catalog, IPresentationDialogs dialogs) : base(state)
    {
        this.sharing = sharing;
        this.documents = documents;
        ExportCommand = Async(async () =>
        {
            var path = dialogs.SaveFile("导出可分享的配置", "LaunchDesk-config.json");
            if (path is null) return;
            var bundle =
                await sharing.ExportAsync(
                    new ShareSelection(ProfileChoices.Where(p => p.Selected).Select(p => p.Id).ToImmutableArray(),
                        IncludeTools, ToolChoices.Where(t => t.Selected).Select(t => t.Id).ToImmutableArray()), default);
            if (!bundle.IsSuccess)
            {
                Error(bundle.Error!.SafeMessage);
                return;
            }

            var result = await documents.WriteShareAsync(path, bundle.Value, default);
            state.Message = result.IsSuccess ? "已导出配置；密码和本机凭据关联不会进入文件。" : result.Error!.SafeMessage;
        });
        PreviewCommand = Async(async () =>
        {
            var path = dialogs.OpenFile("预览导入配置", "配置文件|*.json");
            if (path is null) return;
            var bundle = await documents.ReadShareAsync(path, default);
            if (!bundle.IsSuccess)
            {
                Error(bundle.Error!.SafeMessage);
                return;
            }

            var result = await sharing.PreviewImportAsync(bundle.Value, default);
            if (!result.IsSuccess)
            {
                Error(result.Error!.SafeMessage);
                return;
            }

            proposal = result.Value;
            Confirmed = false;
            Raise(nameof(Preview));
        });
        VerifyImportCommand = Async(async () =>
        {
            if (LastImportId is not { } original) return;
            await VerifyCommit(original, sharing.InspectCommitAsync, catalog,
                ("原导入已确认成功，无需再次导入。", "已确认原导入未提交。", "原导入仍未确定，保留原提交编号继续核验。"));
        }, () => LastImportId.HasValue);
        ImportCommand = Async(async () =>
        {
            if (proposal is null || !Confirmed) return;
            var id = CommitId.New();
            LastImportId = id;
            var result = await sharing.CommitAsync(id, proposal with { LocalBindingsConfirmed = true },
                state.Snapshot.Revision, default);
            if (!result.IsSuccess)
            {
                state.CommitFailed(id, result.Error!);
                return;
            }

            await RefreshWorkspace(catalog);
            proposal = null;
            Confirmed = false;
            Raise(nameof(Preview));
            state.Message = "已导入为独立条目。请在本机核验路径、许可地址并重新输入密码。";
        }, () => proposal is not null && Confirmed && !state.HasPendingCommit);
        state.WorkspaceChanged += RefreshChoices;
        RefreshChoices();
    }

}

public sealed partial class SettingsPageViewModel : PageViewModel
{
    private string sourcePath = "";
    private MigrationPreview? preview;

    /// <summary>明确选择的源文件路径。</summary>
    public string SourcePath
    {
        get => sourcePath;
        set
        {
            Set(ref sourcePath, value);
            preview = null;
            Raise(nameof(Preview));
        }
    }

    /// <summary>数据目录的绝对路径。</summary>
    public string DataDirectory => state.Location.Directory;

    /// <summary>只读预览或脱敏说明。</summary>
    public string Preview => preview is null
        ? "迁移先创建独立副本并校验，再切换新工作空间；旧版目录保留。"
        : $"登录 {preview.Preview.Profiles.Length} · 应用 {preview.Preview.Applications.Length} · 工具 {preview.Preview.Tools.Length}\n\n" +
          string.Join("\n", preview.Notes);

    /// <summary>选择旧版配置来源。</summary>
    public RelayCommand BrowseCommand { get; }
    /// <summary>生成只读预览。</summary>
    public AsyncCommand PreviewCommand { get; }
    /// <summary>明确确认迁移副本。</summary>
    public AsyncCommand MigrateCommand { get; }
    /// <summary>打开已解析的数据目录。</summary>
    public AsyncCommand OpenDirectoryCommand { get; }
    /// <summary>按原编号核验未确定提交。</summary>
    public AsyncCommand VerifyCommitCommand { get; }

    /// <summary>连接工作空间目录操作、旧配置迁移预览与原操作核验。</summary>
    /// <param name="state">公开页面状态。</param>
    /// <param name="locations">工作空间位置解析端口。</param>
    /// <param name="migration">迁移预览或副本存储端口。</param>
    /// <param name="operations">有限持久操作提交端口。</param>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <include file="../../Comments.xml" path="doc/Dialogs/*"/>
    /// <param name="migrationQueries">原迁移结果查询端口。</param>
    public SettingsPageViewModel(WorkspacePresentationState state, IWorkspaceLocationResolver locations,
        IMigrationPlanningService migration, IOperationCommands operations, ICatalogService catalog,
        IPresentationDialogs dialogs, IMigrationQueries? migrationQueries = null) : base(state)
    {
        VerifyMigrationCommand = Async(async () =>
        {
            var original = state.Runs.FirstOrDefault(r => r.Snapshot.Kind == OperationKind.MigrateConfiguration);
            if (original is null || migrationQueries is null) return;
            var result = await migrationQueries.InspectAsync(original.Snapshot.Id, default);
            state.Message = result.IsSuccess ? result.Value.SafeMessage : result.Error!.SafeMessage;
        }, () => migrationQueries is not null && state.Runs.Any(r => r.Snapshot.Kind == OperationKind.MigrateConfiguration));
        BrowseCommand = Sync(() =>
        {
            var path = dialogs.OpenFile("选择旧工作空间配置", "配置文件|*.json");
            if (path is not null) SourcePath = path;
        });
        PreviewCommand = Async(async () =>
        {
            var result = await migration.PreviewAsync(SourcePath, state.Snapshot.Revision, default);
            if (result.IsSuccess)
            {
                preview = result.Value;
                Raise(nameof(Preview));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                Error(result.Error!.SafeMessage);
            }
        });
        MigrateCommand = Async(async () =>
        {
            if (preview is null || !dialogs.Confirm(Preview + "\n确认迁移到当前独立工作空间？", "确认迁移")) return;
            var result = await operations.SubmitAsync(
                new OperationRequest(OperationId.New(), OperationKind.MigrateConfiguration, state.Snapshot.Revision,
                    Migration: preview.Plan), default);
            state.Message = result.IsSuccess ? "迁移已受理；完成后刷新配置。旧版仍可使用原目录。" : result.Error!.SafeMessage;
        }, () => preview is not null && !state.HasPendingCommit);
        OpenDirectoryCommand = Async(async () =>
        {
            var result = await locations.OpenDirectoryAsync(state.Location, default);
            state.Message = result.IsSuccess ? "配置目录已打开。" : result.Error!.SafeMessage;
        });
        VerifyCommitCommand = Async(async () =>
        {
            if (state.PendingCommitId is not { } id) return;
            await VerifyCommit(id, catalog.InspectCommitAsync, catalog,
                ("原提交已确认成功。", "原提交已确认未写入，可以重新编辑。", "原提交仍待核验，继续保留原编号。"), clearOnlyMatching: false);
        }, () => state.HasPendingCommit);
    }

}

public sealed partial class ShellViewModel : PageViewModel
{
    private PageId page;
    /// <summary>Workspace。</summary>
    public WorkspacePresentationState Workspace { get; }
    /// <summary>登录配置集。</summary>
    public ProfilesPageViewModel Profiles { get; }
    /// <summary>登录编辑页面模型。</summary>
    public ProfileEditorViewModel ProfileEditor { get; }
    /// <summary>已登记应用集。</summary>
    public ApplicationsPageViewModel Applications { get; }
    /// <summary>许可监测页面模型或连接选择集合。</summary>
    public LicensePageViewModel Licenses { get; }
    /// <summary>独立工具或关联工具集。</summary>
    public ToolPageViewModel Tools { get; }
    /// <summary>分享与导入页面模型。</summary>
    public SharingPageViewModel Sharing { get; }
    /// <summary>数据和迁移页面模型。</summary>
    public SettingsPageViewModel Settings { get; }

    /// <summary>当前外壳页面标识。</summary>
    public PageId Page
    {
        get => page;
        set
        {
            if (Set(ref page, value))
            {
                ProfileEditor.Password.HideDisplay();
                Notify(nameof(Title), nameof(Subtitle), nameof(HeaderAction), nameof(HeaderCommand), nameof(HeaderIcon), nameof(SecondaryText), nameof(SecondaryIcon), nameof(SecondaryCommand));
                PageChanged?.Invoke(value);
            }
        }
    }

    /// <summary>当前页面或资源的标题。</summary>
    public string Title => CurrentPage.Title;

    /// <summary>当前页面的职责说明。</summary>
    public string Subtitle => CurrentPage.Subtitle;

    /// <summary>外壳当前页面发生变化后通知窗口切换设计模板。</summary>
    public event Action<PageId>? PageChanged;

    /// <summary>固定侧栏中的页面入口。</summary>
    public ObservableCollection<NavigationItem> Navigation { get; } =
    [
        new(PageId.Dashboard, "工作台", "Dashboard"), new(PageId.Profiles, "登录配置", "Folder"),
        new(PageId.Licenses, "许可监测", "Server"), new(PageId.Tools, "工具与集成", "Package"),
        new(PageId.History, "更新记录", "History"), new(PageId.Applications, "应用与版本", "Application"),
        new(PageId.Sharing, "导入与分享", "Import"), new(PageId.Settings, "设置与迁移", "Settings")
    ];

    /// <summary>刷新实际配置或连接。</summary>
    public AsyncCommand RefreshCommand { get; }
    private RunRow? selectedRun;
    /// <summary>操作记录中明确选中的原操作。</summary>
    public RunRow? SelectedRun { get => selectedRun; set { Set(ref selectedRun, value); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
    /// <summary>停止当前原操作的观察或未执行步骤。</summary>
    public AsyncCommand CancelCommand { get; }

    /// <summary>组合页面模型与导航，订阅工作空间投影并建立操作记录视图。</summary>
    /// <param name="state">公开页面状态。</param>
    /// <param name="profiles">登录配置集</param>
    /// <param name="editor">登录草稿页面模型。</param>
    /// <param name="applications">已登记应用集</param>
    /// <param name="licenses">许可页面模型。</param>
    /// <param name="tools">独立工具或关联工具集</param>
    /// <param name="sharing">分享配置用例端口。</param>
    /// <param name="settings">数据与迁移页面模型。</param>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="operations">有限持久操作提交端口。</param>
    /// <param name="queries">持久操作投影查询端口。</param>
    /// <include file="../../Comments.xml" path="doc/Dialogs/*"/>
    public ShellViewModel(WorkspacePresentationState state, ProfilesPageViewModel profiles,
        ProfileEditorViewModel editor,
        ApplicationsPageViewModel applications, LicensePageViewModel licenses, ToolPageViewModel tools,
        SharingPageViewModel sharing,
        SettingsPageViewModel settings, ICatalogService catalog, IOperationCommands operations,
        ITaskQueries? queries = null, IPresentationDialogs? dialogs = null) : base(state)
    {
        Workspace = state;
        Profiles = profiles;
        ProfileEditor = editor;
        Applications = applications;
        Licenses = licenses;
        Tools = tools;
        Sharing = sharing;
        Settings = settings;
        InspectRunCommand = Async(async () =>
        {
            var selected = SelectedRun ?? state.Runs.FirstOrDefault();
            if (selected is null || queries is null) return;
            var result = await queries.ReadAsync(selected.Snapshot.Id, default);
            if (!result.IsSuccess) { state.Message = result.Error!.SafeMessage; return; }
            state.UpdateRun(result.Value);
            SelectedRun = state.Runs.FirstOrDefault(r => r.Snapshot.Id == result.Value.Id);
            IsTaskDetailsOpen = true;
        }, () => queries is not null && state.Runs.Count > 0);
        InitializePresentation();
        state.NavigateRequested += id => Page = id;
        profiles.EditRequested += profile =>
        {
            editor.Load(profile);
            Page = PageId.Profiles;
        };
        RefreshCommand = Async(async () =>
        {
            await RefreshWorkspace(catalog, reportFailure: true);
            foreach (var endpoint in state.Snapshot.LicenseEndpoints) await licenses.RefreshAsync(endpoint.Id, default);
            await tools.RefreshHistoryAsync();
        });
        CancelCommand = Async(async () =>
        {
            if (SelectedRun is null) return;
            var result = await operations.CancelAsync(SelectedRun.Snapshot.Id, default);
            state.Message = result.IsSuccess ? "已请求停止本次观察或尚未执行的步骤，目标应用不会被强制终止。" : result.Error!.SafeMessage;
        }, () => SelectedRun is not null);
    }
}
