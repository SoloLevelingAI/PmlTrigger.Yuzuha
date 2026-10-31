namespace LaunchDesk.Desktop;

/// <summary>配置列表交互、稳定直接命令及启动提交。</summary>
public sealed class ProfilesPageViewModel : PageViewModel
{
    private readonly ICatalogService catalog;
    private readonly IOperationCommands operations;
    private string search = "", version = "全部版本";
    private ProfileRow? selected;
    private bool rebuilding;
    /// <summary>登录配置集。</summary>
    public ObservableCollection<ProfileRow> Profiles { get; } = [];
    /// <summary>可选择的登录版本分组。</summary>
    public ObservableCollection<string> Versions { get; } = ["全部版本"];

    /// <summary>用于筛选名称、版本或目标的搜索文字。</summary>
    public string Search
    {
        get => search;
        set
        {
            if (Set(ref search, value)) Rebuild();
        }
    }

    /// <summary>版本名称或策略版本。</summary>
    public string Version
    {
        get => version;
        set
        {
            if (Set(ref version, value ?? "全部版本") && !rebuilding) Rebuild();
        }
    }

    /// <summary>页面当前选中条目。</summary>
    public ProfileRow? Selected
    {
        get => selected;
        set
        {
            Set(ref selected, value);
            Raise(nameof(HasSelection));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>是否选中了可执行后续操作的条目。</summary>
    public bool HasSelection => Selected is not null;
    /// <summary>提交当前登录启动请求。</summary>
    public AsyncCommand LaunchCommand { get; }
    /// <summary>复制独立登录且清空凭据关联。</summary>
    public AsyncCommand CopyCommand { get; }
    /// <summary>移除管理记录。</summary>
    public AsyncCommand DeleteCommand { get; }
    /// <summary>请求编辑指定登录快照；空值表示创建独立新草稿。</summary>
    public event Action<ProfileSnapshot?>? EditRequested;
    /// <summary>新增独立条目。</summary>
    public RelayCommand NewCommand { get; }
    /// <summary>编辑选中的登录。</summary>
    public RelayCommand EditCommand { get; }

    /// <summary>连接登录列表、新建复制编辑命令与启动操作受理入口。</summary>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="operations">有限持久操作提交端口。</param>
    /// <param name="state">公开页面状态。</param>
    public ProfilesPageViewModel(ICatalogService catalog, IOperationCommands operations,
        WorkspacePresentationState state) : base(state)
    {
        this.catalog = catalog;
        this.operations = operations;
        NewCommand = Sync(() => EditRequested?.Invoke(null));
        EditCommand = Sync(() => EditRequested?.Invoke(Selected?.Profile), () => Selected is not null);
        LaunchCommand = Async(async () => await LaunchSelectedAsync(default),
            () => Selected is not null);
        CopyCommand = Async(async () =>
        {
            if (Selected is not null) await CopyAsync(Selected.Profile.Id, state.Snapshot.Revision, default);
        }, () => Selected is not null && !state.HasPendingCommit);
        DeleteCommand = Async(async () =>
        {
            if (Selected is not null) await DeleteAsync(Selected.Profile.Id, state.Snapshot.Revision, default);
        }, () => Selected is not null && !state.HasPendingCommit);
        state.WorkspaceChanged += Rebuild;
    }

    /// <summary>返回无ProfileId、秘密或前条残留的空白草稿；实际实体ID在首次提交准备时分配。</summary>
    /// <returns>未持久化、没有密码关联的新登录草稿。</returns>
    public ProfileDraft CreateNew()
    {
        return new ProfileDraft(null, "新登录配置", state.Snapshot.Applications.FirstOrDefault()?.Id ?? Guid.Empty, "", "",
            "",
            "/ALL_NO_MDS", "design", null, []);
    }

    /// <summary>明确复制时先分配CommitId、冻结源ID及修订，调用目录服务并保留核验标识。重入/结果未知只核验原ID，不再复制；成功显示原提交的副本ID。</summary>
    /// <param name="sourceId">复制来源登录配置的稳定标识。</param>
    /// <include file="../../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask CopyAsync(Guid sourceId, Revision expected, CancellationToken ct)
    {
        if (state.HasPendingCommit) return;
        var commit = CommitId.New();
        var copied = await catalog.CopyAsync(commit, sourceId, expected, ct);
        if (!copied.IsSuccess)
        {
            state.CommitFailed(commit, copied.Error!);
            return;
        }

        await Refresh(ct);
        Selected = Profiles.FirstOrDefault(p => p.Profile.Id == copied.Value.Id);
        state.Message = "已复制为独立配置，密码关联不会复制。";
    }

    /// <summary>冻结删除意图和CommitId后提交，仅成功才刷新；重复请求沿用原号，未知结果转核验。</summary>
    /// <param name="profileId">目标登录配置的稳定标识。</param>
    /// <include file="../../Comments.xml" path="doc/Expected/*"/>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask DeleteAsync(Guid profileId, Revision expected, CancellationToken ct)
    {
        if (state.HasPendingCommit) return;
        var commit = CommitId.New();
        var result = await catalog.DeleteAsync(commit, profileId, expected, ct);
        if (!result.IsSuccess)
        {
            state.CommitFailed(commit, result.Error!);
            return;
        }

        await Refresh(ct);
        state.Message = "登录配置已删除；应用文件、工具和历史记录保留。";
    }

    /// <summary>读取原提交回执并显示状态、原修订及生成 ID；确定结果后另行刷新当前列表，不自动补做命令。</summary>
    /// <include file="../../Comments.xml" path="doc/CommitId/*"/>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public ValueTask VerifyCommitAsync(CommitId commitId, CancellationToken ct) =>
        VerifyCommit(commitId, catalog.InspectCommitAsync, catalog,
            ("原提交已确认成功。", "原提交未写入。", "原提交仍未确定，保留原编号继续核验。"), ct, reportReadFailure: true);

    /// <summary>取消页面读取及等待，解绑事件，拒绝迟到更新；不取消已受理操作，也不使提交记录失效。</summary>
    /// <returns>异步完成任务。</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>筛选公开快照，按请求代次丢弃过时结果，在Dispatcher更新；列表读取不代替提交核验。</summary>
    /// <param name="searchText">应用于登录名称、项目或版本的搜索文字。</param>
    /// <include file="../../Comments.xml" path="doc/ApplicationId/*"/>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask RefreshAsync(string searchText, Guid? applicationId, CancellationToken ct)
    {
        Search = searchText;
        await Refresh(ct);
        if (applicationId is { } id)
        {
            var rows = Profiles.Where(p => p.Profile.DisplayFields.ApplicationId == id).ToArray();
            Profiles.Clear();
            foreach (var row in rows) Profiles.Add(row);
        }
    }

    /// <summary>提交所选ID、修订和稳定OperationId，受理后全局查询观察；已有操作不再派发。</summary>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask LaunchSelectedAsync(CancellationToken ct)
    {
        if (Selected is null) return;
        var accepted = await operations.SubmitAsync(
            new OperationRequest(OperationId.New(), OperationKind.Launch, state.Snapshot.Revision, Selected.Profile.Id),
            ct);
        state.Message = accepted.IsSuccess ? "启动请求已受理，请查看下方的实际阶段与结果。" : accepted.Error!.SafeMessage;
    }

    private Task Refresh(CancellationToken ct = default) => RefreshWorkspace(catalog, ct, reportFailure: true);

    private void Rebuild()
    {
        if (rebuilding) return;
        rebuilding = true;
        try
        {
            var old = Selected?.Profile.Id;
            var current = state.Snapshot;
            var oldVersion = version;
            var groups = current.Profiles.Select(p => p.DisplayFields.VersionGroup).Where(v => v.Length > 0).Distinct()
                .OrderBy(v => v).ToArray();
            Versions.Clear();
            Versions.Add("全部版本");
            foreach (var group in groups) Versions.Add(group);
            version = Versions.Contains(oldVersion) ? oldVersion : "全部版本";
            Raise(nameof(Version));
            Profiles.Clear();
            foreach (var profile in current.Profiles)
            {
                var row = new ProfileRow(profile, current);
                if ((Version == "全部版本" || row.Version == Version) && (Search.Length == 0 ||
                                                                      $"{row.Name} {row.Summary} {row.Application} {row.Version}"
                                                                          .Contains(Search,
                                                                              StringComparison.OrdinalIgnoreCase)))
                    Profiles.Add(row);
            }

            Selected = Profiles.FirstOrDefault(p => p.Profile.Id == old) ?? Profiles.FirstOrDefault();
        }
        finally
        {
            rebuilding = false;
        }
    }

}