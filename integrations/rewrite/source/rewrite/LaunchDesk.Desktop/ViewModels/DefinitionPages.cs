namespace LaunchDesk.Desktop;

/// <summary>通过目录端口保存有限定义并刷新公开快照的页面基类。</summary>
/// <param name="catalog">公开配置目录端口。</param>
/// <param name="state">公开页面状态。</param>
public abstract class CatalogPageBase(ICatalogService catalog, WorkspacePresentationState state) : PageViewModel(state)
{
    /// <summary>页面使用的公开配置目录端口。</summary>
    protected ICatalogService Catalog { get; } = catalog;
    /// <summary>最近归约的可读连接或提交状态。</summary>
    protected WorkspacePresentationState State { get; } = state;

    /// <summary>通过目录端口保存有限定义并核验生成身份，不写部署目标或持久变量。</summary>
    /// <param name="draft">未持久化的可编辑草稿。</param>
    /// <returns>已保存定义的稳定标识；失败、取消或存在未决提交时为空值。</returns>
    protected async Task<Guid?> SaveDefinition(DefinitionDraft draft)
    {
        if (State.HasPendingCommit) return null;
        var id = CommitId.New();
        var result = await Catalog.SaveDefinitionAsync(id, draft, State.Snapshot.Revision, default);
        if (!result.IsSuccess)
        {
            State.CommitFailed(id, result.Error!);
            return null;
        }

        var receipt = await Catalog.InspectCommitAsync(id, default);
        await Reload();
        State.Message = draft.Delete ? "条目已移除，已有文件和操作历史保留。" : "配置已保存；尚未执行启动、部署或持久变量应用。";
        return receipt.IsSuccess ? receipt.Value.GeneratedEntityId : null;
    }

    /// <summary>刷新公开配置快照供页面绑定使用。</summary>
    /// <returns>异步完成任务。</returns>
    protected Task Reload() => RefreshWorkspace(Catalog, reportFailure: true);
}

/// <summary>应用登记的编辑草稿；生成定义不会启动程序。</summary>
public sealed class ApplicationEditor
{
    /// <summary>稳定标识。</summary>
    public Guid Id { get; set; }
    /// <summary>显示名或配置名。</summary>
    public string Name { get; set; } = "新应用";
    /// <summary>版本名称或策略版本。</summary>
    public string Version { get; set; } = "";
    /// <summary>定义、入口、来源或操作的有限类别。</summary>
    public EntryKind Kind { get; set; } = EntryKind.Monitor;
    /// <summary>应用入口的本机路径。</summary>
    public string Executable { get; set; } = "";
    /// <summary>初始化文件路径草稿。</summary>
    public string Initialization { get; set; } = "";
    /// <summary>目标应用的工作目录。</summary>
    public string WorkingDirectory { get; set; } = "";
    /// <summary>Monitor 入口的产品代码。</summary>
    public string ProductCode { get; set; } = "E3D";
    /// <summary>是否保留人工指定路径。</summary>
    public bool ManualBinding { get; set; } = true;
    /// <summary>按行输入的入口发现目录。</summary>
    public string Roots { get; set; } = "";
    /// <summary>通用入口的非秘密参数模板。</summary>
    public string ArgumentTemplate { get; set; } = "";

    /// <summary>从当前草稿或冻结输入构造配置定义、计划或独立子进程环境。</summary>
    /// <returns>由当前非秘密草稿字段构建的独立应用定义。</returns>
    public ApplicationDefinition Build()
    {
        return new ApplicationDefinition(Id, Name, Version, Kind, Executable, Initialization, WorkingDirectory,
            ProductCode, ManualBinding,
            Lines(Roots), Lines(ArgumentTemplate));
    }

    internal static ImmutableArray<string> Lines(string value)
    {
        return value.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToImmutableArray();
    }
}

/// <summary>组织应用登记、路径选择、候选发现及保存命令。</summary>
public sealed class ApplicationsPageViewModel : DefinitionPageViewModel<ApplicationDefinition, ApplicationEditor>
{
    private string? selectedCandidate;
    /// <summary>已登记应用集。</summary>
    public IEnumerable<ApplicationDefinition> Applications => State.Snapshot.Applications;

    /// <summary>可选择的应用入口类别。</summary>
    public EntryKind[] Kinds { get; } = Enum.GetValues<EntryKind>();
    /// <summary>本轮发现的候选项集合。</summary>
    public IEnumerable<string> Candidates => Selected?.DiscoveredPaths ?? [];

    /// <summary>用户明确选择的发现候选路径。</summary>
    public string? SelectedCandidate
    {
        get => selectedCandidate;
        set
        {
            if (Set(ref selectedCandidate, value) && value is not null)
            {
                Draft.Executable = value;
                Draft.WorkingDirectory = System.IO.Path.GetDirectoryName(value)!;
                Draft.ManualBinding = true;
                Raise(nameof(Draft));
            }
        }
    }

    /// <summary>选择应用入口。</summary>
    public RelayCommand BrowseExecutableCommand { get; }
    /// <summary>选择初始化文件。</summary>
    public RelayCommand BrowseInitCommand { get; }
    /// <summary>选择工作目录。</summary>
    public RelayCommand BrowseWorkingDirectoryCommand { get; }
    /// <summary>发现应用入口候选。</summary>
    public AsyncCommand DiscoverCommand { get; }

    /// <summary>绑定应用目录、发现与保存命令，以及本机路径选择对话框。</summary>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="state">公开页面状态。</param>
    /// <include file="../../Comments.xml" path="doc/Dialogs/*"/>
    public ApplicationsPageViewModel(ICatalogService catalog, WorkspacePresentationState state,
        IPresentationDialogs dialogs) : base(catalog, state, dialogs)
    {
        RegisterDefinition(() => Draft.Id, Define, Load, SelectSaved,
            ("仅删除应用登记，程序文件和历史记录保留。", "移除应用"), NewDraftCreated);
        BrowseExecutableCommand = Sync(() =>
        {
            var path = dialogs.OpenFile("选择应用入口", "程序入口|*.exe;*.bat;*.cmd|所有文件|*.*");
            if (path is null) return;
            Draft.Executable = path;
            Draft.WorkingDirectory = System.IO.Path.GetDirectoryName(path)!;
            Draft.ManualBinding = true;
            Raise(nameof(Draft));
        });
        BrowseInitCommand = Sync(() =>
        {
            var path = dialogs.OpenFile("选择初始化文件", "初始化文件|*.init|所有文件|*.*");
            if (path is not null)
            {
                Draft.Initialization = path;
                Raise(nameof(Draft));
            }
        });
        BrowseWorkingDirectoryCommand = Sync(() =>
        {
            var path = dialogs.OpenFolder("选择工作目录");
            if (path is not null)
            {
                Draft.WorkingDirectory = path;
                Raise(nameof(Draft));
            }
        });
        DiscoverCommand = Async(async () =>
        {
            if (Selected is null)
            {
                Error("请先保存应用与发现规则。");
                return;
            }

            var commit = CommitId.New();
            var id = Selected.Id;
            var result = await catalog.RefreshDiscoveryAsync(commit, id, state.Snapshot.Revision, default);
            if (!result.IsSuccess)
            {
                state.CommitFailed(commit, result.Error!);
                return;
            }

            state.Update(result.Value);
            Selected = Applications.FirstOrDefault(a => a.Id == id);
            if (Selected is not null) Load(Selected);
            state.Message = "已更新候选列表；已有人工路径保持不变，请从候选中明确选择。";
        }, () => !state.HasPendingCommit);
        state.WorkspaceChanged += () =>
        {
            Notify(nameof(Applications), nameof(Candidates));
        };
    }

    private DefinitionDraft Define(bool delete) =>
        new(DefinitionKind.Application, delete ? Selected : Draft.Build(), Delete: delete);
    private void SelectSaved(Guid id) => Selected = Applications.FirstOrDefault(a => a.Id == id);
    private void NewDraftCreated() => Raise(nameof(Candidates));

    private void Load(ApplicationDefinition value)
    {
        Draft = new ApplicationEditor
        {
            Id = value.Id, Name = value.Name, Version = value.Version, Kind = value.Kind,
            Executable = value.ExecutablePath, Initialization = value.InitializationPath,
            WorkingDirectory = value.WorkingDirectory, ProductCode = value.ProductCode,
            ManualBinding = value.ManualBinding, Roots = string.Join("\n", value.DiscoveryRoots),
            ArgumentTemplate = string.Join("\n", value.ArgumentTemplate)
        };
        Raise(nameof(Candidates));
    }

}

/// <summary>许可连接地址、探测策略及可选本机服务和虚拟机的草稿。</summary>
public sealed class LicenseEditor
{
    /// <summary>稳定标识。</summary>
    public Guid Id { get; set; }
    /// <summary>显示名或配置名。</summary>
    public string Name { get; set; } = "新许可连接";
    /// <summary>许可主机名或 IP 地址。</summary>
    public string Host { get; set; } = "localhost";
    /// <summary>RMS TCP 端口。</summary>
    public int RmsPort { get; set; } = 4545;
    /// <summary>RULES TCP 端口。</summary>
    public int RulesPort { get; set; } = 4546;
    /// <summary>是否要求 RULES 连通证据。</summary>
    public bool RequireRules { get; set; } = true;
    /// <summary>是否启用该资源探测。</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>是否采集本机 RMS 服务证据。</summary>
    public bool UseService { get; set; }
    /// <summary>可选的本机 RMS 服务名称。</summary>
    public string ServiceName { get; set; } = "Sentinel RMS License Manager_AVEVA";
    /// <summary>可选 VMware 虚拟机文件路径。</summary>
    public string VmPath { get; set; } = "";
    /// <summary>每轮探测的最大尝试次数。</summary>
    public int Attempts { get; set; } = 3;
    /// <summary>确认持续掉线前的连续失败轮数。</summary>
    public int FailedRounds { get; set; } = 3;
    /// <summary>单次探测超时毫秒数。</summary>
    public int TimeoutMilliseconds { get; set; } = 2000;
    /// <summary>后台探测轮询间隔秒数。</summary>
    public int PollSeconds { get; set; } = 5;
}

/// <summary>展示分项证据、时间及复查状态。</summary>
public sealed class LicensePageViewModel : DefinitionPageViewModel<LicenseRow, LicenseEditor>
{
    private readonly IResourceMonitor monitor;
    /// <summary>Workspace。</summary>
    public WorkspacePresentationState Workspace => State;

    /// <summary>选择要监测的虚拟机。</summary>
    public RelayCommand BrowseVmCommand { get; }
    /// <summary>刷新实际配置或连接。</summary>
    public AsyncCommand RefreshCommand { get; }

    /// <summary>绑定许可定义编辑、连接探测和分项证据展示。</summary>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="monitor">资源监测端口。</param>
    /// <param name="state">公开页面状态。</param>
    /// <include file="../../Comments.xml" path="doc/Dialogs/*"/>
    public LicensePageViewModel(ICatalogService catalog, IResourceMonitor monitor, WorkspacePresentationState state,
        IPresentationDialogs dialogs) : base(catalog, state, dialogs)
    {
        RegisterDefinition(() => Draft.Id, Define, Load, SelectSaved,
            ("仅移除此连接配置，不会停止许可服务或虚拟机。", "移除许可连接"));
        this.monitor = monitor;
        BrowseVmCommand = Sync(() =>
        {
            var path = dialogs.OpenFile("选择要监测的虚拟机", "VMware 虚拟机|*.vmx");
            if (path is not null)
            {
                Draft.VmPath = path;
                Raise(nameof(Draft));
            }
        });
        RefreshCommand = Async(async () =>
        {
            if (Selected is not null) await RefreshAsync(Selected.Definition.Id, default);
        }, () => Selected is not null);
    }

    /// <summary>订阅 IResourceMonitor，Dispatcher 更新 VM、服务、TCP 各自状态与时效。</summary>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask ActivateAsync(CancellationToken ct)
    {
        if (Selected is not null) await RefreshAsync(Selected.Definition.Id, ct);
    }

    /// <summary>请求用例复查，不自行计数或推断许可签出。</summary>
    /// <param name="resourceId">许可资源的稳定标识</param>
    /// <include file="../../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>异步完成任务。</returns>
    public async ValueTask RefreshAsync(Guid resourceId, CancellationToken ct)
    {
        var result = await monitor.RefreshAsync(resourceId, ct);
        if (result.IsSuccess) State.UpdateProbe(result.Value);
        else Error(result.Error!.SafeMessage);
    }

    /// <summary>取消本页订阅并异步等待退出，解绑事件；监测服务继续运行。</summary>
    /// <returns>异步完成任务。</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private void SelectSaved(Guid id) => Selected = state.Resources.FirstOrDefault(l => l.Definition.Id == id);
    private DefinitionDraft Define(bool delete)
    {
        if (delete) return new(DefinitionKind.License, License: Selected!.Definition, Delete: true);
        var policy = new ProbePolicy(AttemptsPerRound: Draft.Attempts,
            TimeoutMilliseconds: Draft.TimeoutMilliseconds, FailedRoundsToNotify: Draft.FailedRounds,
            PollSeconds: Draft.PollSeconds);
        var definition = new LicenseDefinition(Draft.Id, Draft.Name,
            new ResourceDescriptor(Draft.Id, new Revision(0), state.Location.MachineId, Draft.Host, Draft.RmsPort,
                Draft.RequireRules, Draft.RulesPort, Draft.UseService ? Draft.ServiceName : null,
                Draft.VmPath.Length > 0 ? Draft.VmPath : null, Draft.Enabled, policy));
        return new(DefinitionKind.License, License: definition);
    }

    private void Load(LicenseRow row)
    {
        var value = row.Definition;
        Draft = new LicenseEditor
        {
            Id = value.Id, Name = value.Name, Host = value.Resource.Host, RmsPort = value.Resource.RmsPort,
            RulesPort = value.Resource.RulesPort, RequireRules = value.Resource.RequireRules,
            Enabled = value.Resource.Enabled, UseService = value.Resource.ServiceName is not null,
            ServiceName = value.Resource.ServiceName ?? "", VmPath = value.Resource.VmPath ?? "",
            Attempts = value.Resource.Policy.AttemptsPerRound,
            FailedRounds = value.Resource.Policy.FailedRoundsToNotify,
            TimeoutMilliseconds = value.Resource.Policy.TimeoutMilliseconds,
            PollSeconds = value.Resource.Policy.PollSeconds
        };
    }

}