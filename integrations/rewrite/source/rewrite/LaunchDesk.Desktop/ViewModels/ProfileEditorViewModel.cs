namespace LaunchDesk.Desktop;

/// <summary>编辑草稿、一次性接收秘密、管理稳定保存意图和显示核验结果。</summary>
public sealed class ProfileEditorViewModel : PageViewModel
{
    private readonly ICatalogService catalog;
    private Guid? id;
    private string name = "新登录配置", group = "", project = "", user = "", mdb = "/ALL_NO_MDS", module = "design";
    private ApplicationDefinition? application;
    private LicenseDefinition? license;
    private bool clearPassword, mustVerify;
    /// <summary>本次编辑会话的临时秘密输入状态。</summary>
    public PasswordEntryBehavior Password { get; }

    /// <summary>显示名或配置名。</summary>
    public string Name
    {
        get => name;
        set => Set(ref name, value);
    }

    /// <summary>工具分组或作者说明。</summary>
    public string Group
    {
        get => group;
        set => Set(ref group, value);
    }

    /// <summary>目标应用项目名称。</summary>
    public string Project
    {
        get => project;
        set
        {
            Set(ref project, value);
            Raise(nameof(Preview));
        }
    }

    /// <summary>目标应用用户名。</summary>
    public string User
    {
        get => user;
        set
        {
            Set(ref user, value);
            Raise(nameof(Preview));
        }
    }

    /// <summary>目标应用 MDB 名称。</summary>
    public string Mdb
    {
        get => mdb;
        set
        {
            Set(ref mdb, value);
            Raise(nameof(Preview));
        }
    }

    /// <summary>待进入的模块名称。</summary>
    public string Module
    {
        get => module;
        set
        {
            Set(ref module, value);
            Raise(nameof(Preview));
        }
    }

    /// <summary>应用入口定义。</summary>
    public ApplicationDefinition? Application
    {
        get => application;
        set
        {
            Set(ref application, value);
            Raise(nameof(Preview));
        }
    }

    /// <summary>关联许可连接。</summary>
    public LicenseDefinition? License
    {
        get => license;
        set
        {
            Set(ref license, value);
            Raise(nameof(Preview));
        }
    }

    /// <summary>本次保存是否明确解除旧凭据关联。</summary>
    public bool ClearPassword
    {
        get => clearPassword;
        set => Set(ref clearPassword, value);
    }

    /// <summary>上次保存是否未确定并要求按原编号核验。</summary>
    public bool MustVerify
    {
        get => mustVerify;
        private set
        {
            Set(ref mustVerify, value);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>本页面最近一次保存使用的原提交号。</summary>
    public CommitId? LastCommitId { get; private set; }
    /// <summary>已登记应用集。</summary>
    public IEnumerable<ApplicationDefinition> Applications => state.Snapshot.Applications;
    /// <summary>许可监测页面模型或连接选择集合。</summary>
    public IEnumerable<LicenseDefinition> Licenses => state.Snapshot.LicenseEndpoints;

    /// <summary>可选择或继续编辑的目标模块名称。</summary>
    public string[] Modules { get; } =
        ["design", "draw", "paragon", "spooler", "isodraft", "Outfitting", "MarineDrafting", "Admin", "Lexicon"];

    /// <summary>独立工具或关联工具集。</summary>
    public ObservableCollection<ToolChoice> Tools { get; } = [];

    /// <summary>只读预览或脱敏说明。</summary>
    public string Preview => Application is null
        ? "请先在应用与版本中登记程序入口。"
        : $"\"{Application.ExecutablePath}\"\n" +
          (Application.Kind == EntryKind.MarineBatch
              ? $"{Module} noconsole {Project} {User}/<本机凭据> /{Mdb.Trim().TrimStart('/')}"
              : $"{(Application.Kind == EntryKind.Monitor ? $"PROD {Application.ProductCode} init \"{Application.InitializationPath}\"\n" : "")}{Project} {User}/<本机凭据> /{Mdb.Trim().TrimStart('/')} {Module}") +
          $"\n\n许可：{(License is null ? "不注入许可变量" : License.Resource.RmsPort + "@" + License.Resource.Host)}";

    /// <summary>保存当前草稿。</summary>
    public AsyncCommand SaveCommand { get; }
    /// <summary>核验最近保存。</summary>
    public AsyncCommand VerifyCommand { get; }

    /// <summary>建立登录草稿编辑、临时秘密移交与稳定提交核验的页面模型。</summary>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="password">本次编辑会话的秘密输入行为。</param>
    /// <param name="state">公开页面状态。</param>
    public ProfileEditorViewModel(ICatalogService catalog, PasswordEntryBehavior password,
        WorkspacePresentationState state) : base(state)
    {
        this.catalog = catalog;
        Password = password;
        SaveCommand = Async(async () =>
        {
            using var secret = Password.HasInput ? SecretUpdate.Replace(Password.DetachInput()) :
                ClearPassword ? SecretUpdate.Clear() : SecretUpdate.Keep();
            await SaveAsync(secret, default);
        }, () => !MustVerify && !state.HasPendingCommit && Application is not null);
        VerifyCommand = Async(async () =>
        {
            if (LastCommitId is { } commit) await VerifySaveAsync(commit, default);
        }, () => LastCommitId.HasValue);
        state.WorkspaceChanged += () =>
        {
            Notify(nameof(Applications), nameof(Licenses));
            var selected = Tools.Where(tool => tool.Selected).Select(tool => tool.Id).ToHashSet();
            Tools.Clear();
            foreach (var tool in state.Snapshot.Tools) Tools.Add(new ToolChoice(tool.Id, tool.Name, selected.Contains(tool.Id)));
        };
    }

    /// <summary>将指定不可变记录复制为独立页面草稿，并清除上一次临时秘密显示。</summary>
    /// <param name="profile">登录配置或内部记录</param>
    public void Load(ProfileSnapshot? profile)
    {
        Password.HideAndClear();
        ClearPassword = false;
        MustVerify = false;
        LastCommitId = null;
        var fields = profile?.DisplayFields;
        id = profile?.Id;
        Name = fields?.Name ?? "新登录配置";
        Group = fields?.VersionGroup ?? "";
        Project = fields?.Project ?? "";
        User = fields?.User ?? "";
        Mdb = fields?.Mdb ?? "/ALL_NO_MDS";
        Module = fields?.Module ?? "design";
        Application = state.Snapshot.Applications.FirstOrDefault(a => a.Id == fields?.ApplicationId) ??
                      state.Snapshot.Applications.FirstOrDefault();
        License = state.Snapshot.LicenseEndpoints.FirstOrDefault(l => l.Id == fields?.LicenseId);
        Tools.Clear();
        foreach (var tool in state.Snapshot.Tools)
            Tools.Add(new ToolChoice(tool.Id, tool.Name, fields?.ToolIds.Contains(tool.Id) ?? false));
    }

    /// <summary>在Dispatcher明确Keep/Clear/Replace；Replace先经DetachInput取得唯一秘密所有权再调用。提交前冻结草稿和输入标识、分配CommitId，阻止同一输入重入；finally释放移交秘密并清理剩余输入。结果未知时保留CommitId及非秘密意图、显示待核验；不缓存秘密、不自动重试。</summary>
    /// <param name="secretChange">本次保存的秘密更新意图，调用方负责限定其生命周期。</param>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask SaveAsync(SecretUpdate secretChange, CancellationToken ct)
    {
        if (MustVerify || state.HasPendingCommit || Application is null) return;
        LastCommitId = CommitId.New();
        var draft = new ProfileDraft(id, Name, Application.Id, Group, Project, User, Mdb, Module, License?.Id,
            Tools.Where(t => t.Selected).Select(t => t.Id).ToImmutableArray());
        var result =
            await catalog.SaveProfileAsync(LastCommitId.Value, draft, secretChange, state.Snapshot.Revision, ct);
        if (!result.IsSuccess)
        {
            MustVerify = result.Error!.Code is "CommitIndeterminate" or "CommitPending";
            state.CommitFailed(LastCommitId.Value, result.Error);
            return;
        }

        await RefreshWorkspace(catalog, ct);
        Load(result.Value);
        state.Message = "登录配置已保存；密码只保留在本机凭据库，不会回填到界面。";
    }

    /// <summary>调用ICatalogService.InspectCommitAsync：Committed显示原修订和实体ID，再独立刷新配置；Pending/Indeterminate保持待核验；仅确定NotCommitted后允许重新输入，以新CommitId保存。</summary>
    /// <include file="../../Comments.xml" path="doc/CommitId/*"/>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask VerifySaveAsync(CommitId commitId, CancellationToken ct)
    {
        var result = await catalog.InspectCommitAsync(commitId, ct);
        if (!result.IsSuccess)
        {
            Error(result.Error!.SafeMessage);
            return;
        }

        if (result.Value.State == CommitState.Committed && result.Value.Profile is not null)
        {
            state.PendingCommitId = null;
            await RefreshWorkspace(catalog, ct);
            Load(result.Value.Profile);
            state.Message = "已按原提交编号确认保存成功。";
        }
        else if (result.Value.State == CommitState.NotCommitted)
        {
            state.PendingCommitId = null;
            MustVerify = false;
            state.Message = "已确认未提交，可检查字段并重新输入密码后保存。";
        }
        else
        {
            state.Message = "原提交仍待核验，暂不创建新的写入请求。";
        }
    }


    /// <summary>Dispatcher上标记失效，隐藏、销毁未移交输入并解绑事件；取消页面等待；已移交对象仍由保存调用finally释放。拒绝迟到属性更新；离页不代表配置未提交。</summary>
    /// <returns>异步完成任务。</returns>
    public ValueTask DisposeAsync()
    {
        Password.HideAndClear();
        return ValueTask.CompletedTask;
    }
}
