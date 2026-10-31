namespace LaunchDesk.Desktop;

/// <summary>普通文件到目标相对路径的可观察映射草稿。</summary>
public sealed class MappingEditor : ObservableObject
{
    private string source = "", relative = "";
    /// <summary>明确选择的源文件路径。</summary>
    public string SourcePath { get => source; set => Set(ref source, value); }
    /// <summary>相对目标根的路径。</summary>
    public string RelativePath { get => relative; set => Set(ref relative, value); }
}

/// <summary>工具变量的名称、值与作用域草稿。</summary>
public sealed class VariableEditor : ObservableObject
{
    /// <summary>稳定标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    private string name = "ToolRoot", value = "{Target}";
    private VariableScope scope;

    /// <summary>显示名或配置名。</summary>
    public string Name
    {
        get => name;
        set => Set(ref name, value);
    }

    /// <summary>当前变量草稿中准备设置的非秘密文本值。</summary>
    public string Value
    {
        get => value;
        set => Set(ref this.value, value);
    }

    /// <summary>变量作用域。</summary>
    public VariableScope Scope
    {
        get => scope;
        set => Set(ref scope, value);
    }
}

/// <summary>工具来源、目标、文件映射及变量的可观察草稿。</summary>
public sealed class ToolEditor : ObservableObject
{
    /// <summary>稳定标识。</summary>
    public Guid Id { get; set; }
    private string name = "新工具", group = "自定义工具", target = "", source = "", search = "";
    private PackageKind kind = PackageKind.Folder;
    private bool flatten = true, backup = true;

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

    /// <summary>探测目标或配置目标路径。</summary>
    public string Target
    {
        get => target;
        set => Set(ref target, value);
    }

    /// <summary>工具来源或来源快照。</summary>
    public string Source
    {
        get => source;
        set { Set(ref source, value); Raise(nameof(SourceLabel)); }
    }

    /// <summary>刷新工具来源时扫描的目录。</summary>
    public string SearchDirectory
    {
        get => search;
        set => Set(ref search, value);
    }

    /// <summary>定义、入口、来源或操作的有限类别。</summary>
    public PackageKind Kind
    {
        get => kind;
        set { Set(ref kind, value); Raise(nameof(SourceLabel)); }
    }

    /// <summary>是否展开压缩包中唯一的顶层目录。</summary>
    public bool FlattenRoot
    {
        get => flatten;
        set => Set(ref flatten, value);
    }

    /// <summary>是否保留交付前文件备份。</summary>
    public bool KeepBackup
    {
        get => backup;
        set => Set(ref backup, value);
    }

    /// <summary>旧版回执所在目录，只读兼容使用。</summary>
    public string? LegacyRecordsDirectory { get; set; }
    /// <summary>已检查的文件/普通文件映射集。</summary>
    public ObservableCollection<MappingEditor> Files { get; } = [];
    /// <summary>工具变量定义或确认后的变量计划。</summary>
    public ObservableCollection<VariableEditor> Variables { get; } = [];
    /// <summary>当前来源路径或普通文件包数量说明。</summary>
    public string SourceLabel => Kind == PackageKind.Files ? $"普通文件包 · {Files.Count} 个文件" : Source;
    /// <summary>创建工具草稿，并订阅文件集合变化以刷新来源标签。</summary>
    public ToolEditor() { Files.CollectionChanged += (_, _) => Raise(nameof(SourceLabel)); }

    /// <summary>从当前草稿或冻结输入构造配置定义、计划或独立子进程环境。</summary>
    /// <returns>由当前来源、映射、目标和变量字段构建的独立工具定义。</returns>
    public ToolDefinition Build()
    {
        return new ToolDefinition(Id, Name, Group, Target,
            new PackageSource(Kind, Source,
                Files.Select(f => new ToolFile(f.SourcePath, f.RelativePath)).ToImmutableArray()),
            SearchDirectory, Variables.Select(v => new ToolVariable(v.Id, v.Name, v.Value, v.Scope)).ToImmutableArray(),
            FlattenRoot, KeepBackup, LegacyRecordsDirectory);
    }
}

/// <summary>将部署回执转换为更新记录页面的可读信息。</summary>
/// <param name="Receipt">Worker 或单次交付的有限执行回执。</param>
public sealed record ReceiptRow(DeploymentReceipt Receipt)
{
    /// <summary>操作或回执的可读阶段文字。</summary>
    public string Status => Receipt.State switch
    {
        "Completed" => "已完成", "RolledBack" => "已回滚", "LegacyReadOnly" => "旧版记录 · 只读",
        "LegacyUnavailable" => "旧记录路径待修复", "NeedsVerification" => "需要核验", "Applying" => "写入未结束", _ => "已准备"
    };

    /// <summary>操作或回执的脱敏详情。</summary>
    public string Detail => Receipt.ReasonCode;
    /// <summary>观察或交付开始时间的本地显示文字。</summary>
    public string Time => Receipt.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    /// <summary>探测目标或配置目标路径。</summary>
    public string Target => Receipt.TargetDirectory;
    /// <summary>稳定标识。</summary>
    public string Id => Receipt.Id.ToString();
    /// <summary>回执中的文件数量说明。</summary>
    public string Count => Receipt.Files.Length + " 个文件";
}

public sealed partial class ToolPageViewModel : DefinitionPageViewModel<ToolDefinition, ToolEditor>
{
    private readonly IToolSourceCatalog sources;
    private readonly IDeploymentPlanningService planning;
    private readonly IVariablePlanningService variablePlanning;
    private readonly IOperationCommands operations;
    private readonly IDeploymentHistoryQueries history;
    private readonly IPresentationDialogs dialogs;
    private SourceCandidate? selectedSource;
    private DeploymentPlan? plan;
    private VariableChangePlan? variablePlan;
    private string search = "", oldName = "ToolRoot", newName = "ToolRoot", variableValue = "{Target}";
    private VariableScope variableScope;
    private VariableEditor? selectedVariable;
    private bool useAdmin;
    /// <summary>Workspace。</summary>
    public WorkspacePresentationState Workspace => State;

    /// <summary>独立工具或关联工具集。</summary>
    public IEnumerable<ToolDefinition> Tools => State.Snapshot.Tools.Where(t =>
        Search.Length == 0 ||
        $"{t.Name} {t.Group} {t.TargetDirectory}".Contains(Search, StringComparison.OrdinalIgnoreCase));

    /// <summary>用于筛选名称、版本或目标的搜索文字。</summary>
    public string Search
    {
        get => search;
        set
        {
            Set(ref search, value);
            Raise(nameof(Tools));
        }
    }

    /// <summary>可选择的压缩包、目录或普通文件来源类别。</summary>
    public PackageKind[] SourceKinds { get; } = Enum.GetValues<PackageKind>();
    /// <summary>可选择的变量作用域。</summary>
    public VariableScope[] Scopes { get; } = Enum.GetValues<VariableScope>();
    /// <summary>本轮刷新发现的来源候选。</summary>
    public ObservableCollection<SourceCandidate> Sources { get; } = [];

    /// <summary>用户明确选择或按原路径保留的候选。</summary>
    public SourceCandidate? SelectedSource
    {
        get => selectedSource;
        set
        {
            if (Set(ref selectedSource, value) && value is not null)
            {
                Draft.Source = value.Path;
                Draft.Kind = value.Kind;
            }
        }
    }

    /// <summary>普通文件映射编辑器当前选中的条目。</summary>
    public MappingEditor? SelectedMapping { get; set; }

    /// <summary>当前工具正在编辑的变量草稿。</summary>
    public VariableEditor? SelectedVariable
    {
        get => selectedVariable;
        set
        {
            if (Set(ref selectedVariable, value) && value is not null)
            {
                OldName = value.Name;
                NewName = value.Name;
                VariableValue = value.Value;
                VariableScope = value.Scope;
            }
        }
    }

    /// <summary>冻结的执行计划。</summary>
    public DeploymentPlan? Plan
    {
        get => plan;
        private set
        {
            Set(ref plan, value);
            Notify(nameof(PlanSummary), nameof(Changes));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>本次冻结部署计划的文件变更集合。</summary>
    public IEnumerable<DeploymentFile> Changes => Plan?.Files ?? [];

    /// <summary>当前预览的动作、变更数量和确认提示。</summary>
    public string PlanSummary => Plan is null
        ? "选择来源和目标后生成预览。预览不会写入目标目录。"
        : $"{(Plan.Action == DeploymentAction.Rollback ? "回滚" : Plan.Action == DeploymentAction.Recover ? "恢复" : "部署")} · {Plan.Files.Length} 个变更 · 备份与回执保留";

    /// <summary>用户是否明确选择通过受信任 Worker 执行计划。</summary>
    public bool UseAdmin
    {
        get => useAdmin;
        set => Set(ref useAdmin, value);
    }

    /// <summary>待改名变量的原名称。</summary>
    public string OldName
    {
        get => oldName;
        set
        {
            Set(ref oldName, value);
            VariablePlan = null;
        }
    }

    /// <summary>用户确认的新变量名称。</summary>
    public string NewName
    {
        get => newName;
        set
        {
            Set(ref newName, value);
            if (selectedVariable is not null) selectedVariable.Name = value;
            Raise(nameof(Draft));
            VariablePlan = null;
        }
    }

    /// <summary>当前变量草稿准备应用的值。</summary>
    public string VariableValue
    {
        get => variableValue;
        set
        {
            Set(ref variableValue, value);
            if (selectedVariable is not null) selectedVariable.Value = value;
            Raise(nameof(Draft));
            VariablePlan = null;
        }
    }

    /// <summary>当前变量草稿准备应用的作用域。</summary>
    public VariableScope VariableScope
    {
        get => variableScope;
        set
        {
            Set(ref variableScope, value);
            if (selectedVariable is not null) selectedVariable.Scope = value;
            Raise(nameof(Draft));
            VariablePlan = null;
        }
    }

    /// <summary>读取旧值及目标基线后冻结的变量预览。</summary>
    public VariableChangePlan? VariablePlan
    {
        get => variablePlan;
        private set
        {
            Set(ref variablePlan, value);
            Raise(nameof(VariablePreview));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>当前变量计划的可读差异。</summary>
    public string VariablePreview => VariablePlan is null
        ? "先读取当前作用域，生成改名或应用预览。保存工具配置不会修改 Windows 环境。"
        : $"旧名：{VariablePlan.Before.Address.Name}\n当前值：{VariablePlan.Before.Value ?? "（不存在）"}\n新名：{VariablePlan.NewName}\n新值：{VariablePlan.NewValue}\n作用域：{VariablePlan.Before.Address.Scope}";

    /// <summary>当前工作空间的交付回执投影。</summary>
    public ObservableCollection<ReceiptRow> History { get; } = [];
    private ReceiptRow? selectedReceipt;
    /// <summary>当前选中的原部署回执。</summary>
    public ReceiptRow? SelectedReceipt { get => selectedReceipt; set { Set(ref selectedReceipt, value); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
    /// <summary>选择目标目录。</summary>
    public RelayCommand BrowseTargetCommand { get; }
    /// <summary>选择来源文件或目录。</summary>
    public RelayCommand BrowseSourceCommand { get; }
    /// <summary>选择来源查找目录。</summary>
    public RelayCommand BrowseSearchCommand { get; }
    /// <summary>选择普通文件并编辑目标映射。</summary>
    public RelayCommand AddFilesCommand { get; }
    /// <summary>移除所选文件映射。</summary>
    public RelayCommand RemoveFileCommand { get; }
    /// <summary>新增当前工具的变量草稿。</summary>
    public RelayCommand AddVariableCommand { get; }
    /// <summary>移除所选变量草稿。</summary>
    public RelayCommand RemoveVariableCommand { get; }
    /// <summary>刷新候选并保留人工选择。</summary>
    public AsyncCommand RefreshSourcesCommand { get; }
    /// <summary>明确选择最新来源。</summary>
    public AsyncCommand LatestSourceCommand { get; }
    /// <summary>生成只读预览。</summary>
    public AsyncCommand PreviewCommand { get; }
    /// <summary>明确确认当前冻结计划。</summary>
    public AsyncCommand ExecuteCommand { get; }
    /// <summary>读取旧变量并冻结差异。</summary>
    public AsyncCommand PreviewVariableCommand { get; }
    /// <summary>明确应用已预览变量。</summary>
    public AsyncCommand ApplyVariableCommand { get; }
    /// <summary>刷新只读交付历史。</summary>
    public AsyncCommand RefreshHistoryCommand { get; }
    /// <summary>生成原交付的恢复预览。</summary>
    public AsyncCommand RecoverCommand { get; }
    /// <summary>生成原交付的回滚预览。</summary>
    public AsyncCommand RollbackCommand { get; }
    /// <summary>导出明确选择的非秘密定义。</summary>
    public AsyncCommand ExportCommand { get; }

    /// <summary>连接工具草稿、来源选择、部署预览、变量计划与历史查询。</summary>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="sources">来源发现与检查端口。</param>
    /// <param name="operations">有限持久操作提交端口。</param>
    /// <param name="planning">部署预览端口。</param>
    /// <param name="variablePlanning">变量预览端口。</param>
    /// <param name="history">回执只读查询端口。</param>
    /// <param name="state">公开页面状态。</param>
    /// <include file="../../Comments.xml" path="doc/Dialogs/*"/>
    /// <param name="sharing">分享配置用例端口。</param>
    /// <param name="documents">分享文件读写端口。</param>
    public ToolPageViewModel(ICatalogService catalog, IToolSourceCatalog sources, IOperationCommands operations,
        IDeploymentPlanningService planning, IVariablePlanningService variablePlanning,
        IDeploymentHistoryQueries history,
        WorkspacePresentationState state, IPresentationDialogs dialogs,
        IConfigurationShareService? sharing = null, IConfigurationDocuments? documents = null) : base(catalog, state, dialogs)
    {
        RegisterDefinition(() => Draft.Id, Define, Load, SelectSaved,
            ("仅移除工具登记，目标文件、备份与历史记录保留。", "移除工具"), NewDraftCreated, DraftAssigned);
        this.sources = sources;
        this.operations = operations;
        this.planning = planning;
        this.variablePlanning = variablePlanning;
        this.history = history;
        this.dialogs = dialogs;
        ExportCommand = Async(async () =>
        {
            if (Draft.Id == Guid.Empty || sharing is null || documents is null) return;
            var path = dialogs.SaveFile("导出工具配置", "LaunchDesk-tool.json");
            if (path is null) return;
            var bundle = await sharing.ExportAsync(new ShareSelection([], true, [Draft.Id]), default);
            if (!bundle.IsSuccess) { Error(bundle.Error!.SafeMessage); return; }
            var result = await documents.WriteShareAsync(path, bundle.Value, default);
            State.Message = result.IsSuccess ? "已导出独立工具配置。" : result.Error!.SafeMessage;
        }, () => Draft.Id != Guid.Empty && sharing is not null && documents is not null);
        BrowseTargetCommand = Sync(() =>
        {
            var path = dialogs.OpenFolder("选择目标目录");
            if (path is not null) Draft.Target = path;
        });
        BrowseSearchCommand = Sync(() =>
        {
            var path = dialogs.OpenFolder("选择来源查找目录");
            if (path is not null) Draft.SearchDirectory = path;
        });
        BrowseSourceCommand = Sync(() =>
        {
            if (Draft.Kind == PackageKind.Folder)
            {
                var path = dialogs.OpenFolder("选择完整文件夹");
                if (path is not null) Draft.Source = path;
            }
            else
            {
                var path = dialogs.OpenFile("选择来源文件", "来源文件|*.zip;*.7z;*.*");
                if (path is not null)
                {
                    Draft.Source = path;
                    var extension = System.IO.Path.GetExtension(path);
                    Draft.Kind = extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ? PackageKind.Zip :
                        extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ? PackageKind.SevenZip :
                        PackageKind.Files;
                }
            }
        });
        AddFilesCommand = Sync(() =>
        {
            foreach (var file in dialogs.OpenFiles("选择普通文件"))
                Draft.Files.Add(
                    new MappingEditor { SourcePath = file, RelativePath = System.IO.Path.GetFileName(file) });
            Draft.Kind = PackageKind.Files;
            Plan = null;
            IsEditingFiles = true;
        });
        RemoveFileCommand = Sync(() =>
        {
            if (SelectedMapping is not null) Draft.Files.Remove(SelectedMapping);
            Plan = null;
        });
        AddVariableCommand = Sync(() =>
        {
            var variable = new VariableEditor();
            Draft.Variables.Add(variable);
            SelectedVariable = variable;
            IsEditingVariable = true;
        });
        RemoveVariableCommand = Sync(() =>
        {
            if (SelectedVariable is not null) Draft.Variables.Remove(SelectedVariable);
            SelectedVariable = null;
            VariablePlan = null;
        });
        RefreshSourcesCommand = Async(async () => await RefreshSources(RefreshIntent.RefreshSources));
        LatestSourceCommand = Async(async () => await RefreshSources(RefreshIntent.SelectLatest));
        PreviewCommand = Async(Preview, () => Draft.Id != Guid.Empty);
        ExecuteCommand = Async(Execute, () => Plan is not null);
        PreviewVariableCommand = Async(async () =>
        {
            var result = await variablePlanning.PreviewAsync(
                new VariableRenameRequest(OldName, NewName,
                    VariableValue.Replace("{Target}", Draft.Target, StringComparison.OrdinalIgnoreCase), VariableScope),
                default);
            if (result.IsSuccess) VariablePlan = result.Value;
            else Error(result.Error!.SafeMessage);
        });
        ApplyVariableCommand = Async(async () =>
        {
            if (VariablePlan is null ||
                !dialogs.Confirm(VariablePreview + (UseAdmin ? "\n将请求管理员授权。" : ""), "明确应用持久变量")) return;
            var result = await operations.SubmitAsync(
                new OperationRequest(OperationId.New(), OperationKind.ApplyVariables, state.Snapshot.Revision,
                    Variables: new ConfirmedVariablePlan(VariablePlan, VariablePlan.PlanDigest),
                    ElevationRequested: UseAdmin), default);
            state.Message = result.IsSuccess ? "变量请求已受理，保存配置与实际应用状态分别显示。" : result.Error!.SafeMessage;
        }, () => VariablePlan is not null && VariableScope != VariableScope.Process);
        RefreshHistoryCommand = Async(async () => await RefreshHistoryAsync());
        RecoverCommand = Async(async () => await PreviewRecovery(DeploymentAction.Recover),
            () => SelectedReceipt is not null && SelectedReceipt.Receipt.PlanId != Guid.Empty &&
                  SelectedReceipt.Receipt.State != "RolledBack");
        RollbackCommand = Async(async () => await PreviewRecovery(DeploymentAction.Rollback),
            () => SelectedReceipt is not null && SelectedReceipt.Receipt.PlanId != Guid.Empty &&
                  SelectedReceipt.Receipt.State != "RolledBack");
        state.WorkspaceChanged += () =>
        {
            Raise(nameof(Tools));
            Plan = null;
        };
    }

    private DefinitionDraft Define(bool delete) =>
        new(DefinitionKind.Tool, Tool: delete ? Selected : Draft.Build(), Delete: delete);
    private void SelectSaved(Guid id)
    {
        var tool = state.Snapshot.Tools.FirstOrDefault(t => t.Id == id);
        if (tool is null) return;
        selected = tool;
        Raise(nameof(Selected));
        Load(tool);
    }
    private void NewDraftCreated()
    {
        SelectedVariable = null;
        SelectedMapping = null;
        IsEditingVariable = false;
        IsEditingFiles = false;
        Sources.Clear();
        selectedSource = null;
        Raise(nameof(SelectedSource));
        Plan = null;
        VariablePlan = null;
        State.Navigate(PageId.ToolSettings);
    }
    private void DraftAssigned(ToolEditor value)
    {
        value.PropertyChanged += (_, _) =>
        {
            Plan = null;
            VariablePlan = null;
        };
        value.Files.CollectionChanged += (_, args) =>
        {
            Plan = null;
            if (args.NewItems is not null) foreach (MappingEditor mapping in args.NewItems)
                mapping.PropertyChanged += (_, _) => Plan = null;
        };
        foreach (var mapping in value.Files) mapping.PropertyChanged += (_, _) => Plan = null;
    }

    private void Load(ToolDefinition tool)
    {
        var value = new ToolEditor
        {
            Id = tool.Id, Name = tool.Name, Group = tool.Group, Target = tool.TargetDirectory, Kind = tool.Source.Kind,
            Source = tool.Source.Path, SearchDirectory = tool.SearchDirectory, FlattenRoot = tool.FlattenRoot,
            KeepBackup = tool.KeepBackup, LegacyRecordsDirectory = tool.LegacyRecordsDirectory
        };
        foreach (var file in tool.Source.Files)
            value.Files.Add(new MappingEditor { SourcePath = file.SourcePath, RelativePath = file.RelativePath });
        foreach (var variable in tool.Variables)
            value.Variables.Add(new VariableEditor
                { Id = variable.Id, Name = variable.Name, Value = variable.Value, Scope = variable.Scope });
        Draft = value;
        SelectedVariable = null;
        SelectedMapping = null;
        IsEditingVariable = false;
        IsEditingFiles = false;
        Sources.Clear();
        selectedSource = null;
        Notify(nameof(SelectedSource), nameof(Associations));
        Plan = null;
        VariablePlan = null;
    }

    private async Task RefreshSources(RefreshIntent intent)
    {
        var current = Draft.Source;
        var result = await sources.RefreshAsync(new SourceQuery(Draft.SearchDirectory), new SourceSelection(current),
            intent, default);
        if (!result.IsSuccess)
        {
            Error(result.Error!.SafeMessage);
            return;
        }

        Sources.Clear();
        foreach (var source in result.Value.Candidates) Sources.Add(source);
        if (result.Value.Selection is not null)
        {
            SelectedSource = result.Value.Selection;
        }
        else
        {
            Draft.Source = current;
            State.Message = "来源列表已刷新，仍保留人工选择；请检查原路径是否可用。";
        }
    }

    private async Task Preview()
    {
        var source = await sources.InspectAsync(Draft.Build().Source, default);
        if (!source.IsSuccess)
        {
            Error(source.Error!.SafeMessage);
            return;
        }

        var result = await planning.PreviewAsync(
            new DeployRequest(State.Snapshot.Revision, Draft.Id, source.Value, new TargetSpec(Draft.Target),
                Draft.KeepBackup, Draft.FlattenRoot), default);
        if (!result.IsSuccess)
        {
            Error(result.Error!.SafeMessage);
            return;
        }

        Plan = result.Value;
        State.Navigate(PageId.Deployment);
        State.Message = "预览已生成；只有点击确认执行才会写入目标。";
    }

    private async Task Execute()
    {
        var current = Plan;
        if (current is null ||
            !dialogs.Confirm(PlanSummary + "\n目标：" + current.TargetDirectory + (UseAdmin ? "\n将请求管理员授权。" : ""),
                "确认本次计划")) return;
        var result = await operations.SubmitAsync(
            new OperationRequest(OperationId.New(), OperationKind.Deploy, current.WorkspaceRevision,
                Deployment: ConfirmedPlan.Confirm(current), ElevationRequested: UseAdmin), default);
        State.Message = result.IsSuccess ? "交付请求已受理；结果和回执将持续显示在更新记录中。" : result.Error!.SafeMessage;
        if (result.IsSuccess)
        {
            Plan = null;
            State.Navigate(PageId.History);
        }
    }

    /// <summary>通过历史只读端口刷新回执列表，不重新执行原部署。</summary>
    /// <returns>异步完成任务。</returns>
    public async ValueTask RefreshHistoryAsync()
    {
        var result = await history.ReadAsync(default);
        if (!result.IsSuccess)
        {
            Error(result.Error!.SafeMessage);
            return;
        }

        History.Clear();
        foreach (var receipt in result.Value) History.Add(new ReceiptRow(receipt));
    }

    private async Task PreviewRecovery(DeploymentAction action)
    {
        if (SelectedReceipt is null) return;
        var result =
            await planning.PreviewRecoveryAsync(
                new RecoveryRequest(State.Snapshot.Revision, SelectedReceipt.Receipt.Id, action), default);
        if (!result.IsSuccess)
        {
            Error(result.Error!.SafeMessage);
            return;
        }

        Plan = result.Value;
        State.Navigate(PageId.Deployment);
        State.Message = "恢复或回滚也需要确认当前差异，不会重放旧操作。";
    }

}
