using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LaunchDesk.Desktop;

/// <summary>为 WPF 页面模型提供属性变更通知和相等值检查。</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>属性值发生变化时通知 WPF 绑定订阅者。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>向 WPF 绑定订阅者通知指定属性发生变化。</summary>
    /// <param name="name">显示名或配置名</param>
    protected void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>按给定顺序通知同一次投影更新涉及的属性；不合并或跳过事件。</summary>
    /// <param name="names">需刷新的绑定属性名称。</param>
    protected void Notify(params string[] names)
    {
        foreach (var name in names) Raise(name);
    }

    /// <summary>仅在字段值发生变化时更新字段并发出属性通知。</summary>
    /// <include file="../../Comments.xml" path="doc/T/*"/>
    /// <param name="field">属性通知前保存的字段引用。</param>
    /// <include file="../../Comments.xml" path="doc/Value/*"/>
    /// <param name="name">显示名或配置名</param>
    /// <returns>是否满足所述条件。</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>执行同步页面操作并将可执行状态交给 WPF 命令系统。</summary>
/// <param name="action">命令的同步/异步操作。</param>
/// <param name="canExecute">可选的可执行状态判断。</param>
public sealed class RelayCommand(Action action, Func<bool>? canExecute = null) : ICommand
{
    /// <summary>查询同步命令回调的当前可执行条件。</summary>
    /// <param name="parameter">本命令忽略WPF命令参数。</param>
    /// <returns>未提供条件时为 true，否则使用条件回调的结果。</returns>
    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke() ?? true;
    }

    /// <summary>执行同步回调；命令参数不参与本回调。</summary>
    /// <param name="parameter">本命令忽略WPF命令参数。</param>
    public void Execute(object? parameter)
    {
        action();
    }

    /// <summary>可执行状态变化时通知WPF重评按钮。</summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>串行执行一次页面异步操作，将失败转为可展示提示。</summary>
/// <param name="action">命令的同步/异步操作。</param>
/// <param name="onError">接收脱敏失败提示的页面回调。</param>
/// <param name="canExecute">可选的可执行状态判断。</param>
public sealed class AsyncCommand(Func<Task> action, Action<string> onError, Func<bool>? canExecute = null)
    : ObservableObject, ICommand
{
    private bool running;

    /// <summary>页面异步命令是否正在运行。</summary>
    public bool IsRunning
    {
        get => running;
        private set
        {
            Set(ref running, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>仅在没有命令运行且附加条件成立时允许再次执行。</summary>
    /// <param name="parameter">本命令忽略WPF命令参数。</param>
    /// <returns>当前没有运行中的命令，并且可选条件回调允许执行时为 true。</returns>
    public bool CanExecute(object? parameter)
    {
        return !running && (canExecute?.Invoke() ?? true);
    }

    /// <summary>从 WPF 命令入口等待同一串行异步执行流程。</summary>
    /// <param name="parameter">本命令忽略WPF命令参数。</param>
    public async void Execute(object? parameter) => await ExecuteAsync();

    /// <summary>在当前页面串行运行异步操作，将取消或异常转换为页面提示并恢复可执行状态。</summary>
    /// <returns>表示本次页面命令执行及状态清理完成的任务。</returns>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        IsRunning = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            onError("操作已停止，已有结果仍可核验。");
        }
        catch (Exception)
        {
            onError("操作未完成，请检查输入或查看原操作状态。");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>可执行状态变化时通知WPF重评按钮。</summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>桌面工作空间的页面标识。</summary>
public enum PageId
{
    /// <summary>工作台页面。</summary>
    Dashboard,
    /// <summary>登录配置页面。</summary>
    Profiles,
    /// <summary>许可监测页面。</summary>
    Licenses,
    /// <summary>工具库页面。</summary>
    Tools,
    /// <summary>更新回执页面。</summary>
    History,
    /// <summary>应用与版本页面。</summary>
    Applications,
    /// <summary>分享与导入页面。</summary>
    Sharing,
    /// <summary>数据与迁移页面。</summary>
    Settings,
    /// <summary>来源及部署预览页面。</summary>
    Deployment,
    /// <summary>工具信息和变量设置页面。</summary>
    ToolSettings
}

/// <summary>侧栏页面标识、标题及图标名称。</summary>
/// <param name="Id">稳定标识</param>
/// <param name="Label">界面显示文字</param>
/// <param name="Icon">几何图标的资源名称</param>
public sealed record NavigationItem(PageId Id, string Label, string Icon);

/// <summary>页面使用的文件选择和明确确认交互，便于隔离外部副作用测试。</summary>
public interface IPresentationDialogs
{
    /// <summary>请求用户选择单个本机文件；取消时返回空值。</summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="filter">文件选择器接受的扩展名规则。</param>
    /// <returns>用户选择的本机文件路径；取消时为空值。</returns>
    string? OpenFile(string title, string filter = "所有文件|*.*");
    /// <summary>请求用户选择普通文件集合；取消时返回空集合。</summary>
    /// <param name="title">对话框标题。</param>
    /// <returns>用户选择的文件路径数组；取消时为空数组。</returns>
    string[] OpenFiles(string title);
    /// <summary>请求用户明确选择分享文件保存位置；取消时返回空值。</summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="suggestedName">分享文件建议保存的文件名。</param>
    /// <returns>用户明确选择的 JSON 文件保存路径；取消时为空值。</returns>
    string? SaveFile(string title, string suggestedName);
    /// <summary>请求用户选择本机目录；取消时返回空值。</summary>
    /// <param name="title">对话框标题。</param>
    /// <returns>用户选择的本机目录路径；取消时为空值。</returns>
    string? OpenFolder(string title);
    /// <summary>固定用户明确确认时的计划及其摘要，或等待对话框的明确确认。</summary>
    /// <param name="message">用户可见且无秘密的提示。</param>
    /// <param name="title">对话框标题。</param>
    /// <returns>是否满足所述条件。</returns>
    bool Confirm(string message, string title);
}

/// <summary>持有公开配置、探测及操作投影；不拥有平台执行能力。</summary>
/// <param name="machineName">供界面显示的当前计算机名称。</param>
/// <param name="location"><include file="../../Comments.xml" path="doc/Location/param[@name='location']/*"/></param>
public sealed class WorkspacePresentationState(string machineName, WorkspaceLocation location) : ObservableObject
{
    private WorkspaceSnapshot snapshot = new(Guid.Empty, new Revision(0), [], [], [], [], []);
    private string message = "正在读取工作空间…";
    private CommitId? pendingCommitId;
    /// <summary>供界面显示的计算机名称。</summary>
    public string MachineName { get; } = machineName;
    /// <summary>当前独立工作空间的位置。</summary>
    public WorkspaceLocation Location { get; } = location;
    /// <summary>当前不可变状态快照。</summary>
    public WorkspaceSnapshot Snapshot => snapshot;

    /// <summary>页面最近的脱敏操作提示。</summary>
    public string Message
    {
        get => message;
        set => Set(ref message, value);
    }

    /// <summary>未确定写入结果的原提交号；未完成核验前阻止新配置写入。</summary>
    public CommitId? PendingCommitId
    {
        get => pendingCommitId;
        set
        {
            Set(ref pendingCommitId, value);
            Raise(nameof(HasPendingCommit));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>是否有须按原编号核验的未确定提交。</summary>
    public bool HasPendingCommit => PendingCommitId.HasValue;

    /// <summary>保留未确定提交的原编号，并显示脱敏失败说明。</summary>
    /// <include file="../../Comments.xml" path="doc/Id/*"/>
    /// <param name="error">失败时的脱敏错误</param>
    public void CommitFailed(CommitId id, Error error)
    {
        if (error.Code is "CommitIndeterminate" or "CommitPending") PendingCommitId = id;
        Message = error.SafeMessage + (HasPendingCommit ? " 提交编号：" + PendingCommitId : "");
    }

    /// <summary>公开工作空间快照变化后通知页面刷新派生集合。</summary>
    public event Action? WorkspaceChanged;
    /// <summary>页面模型请求外壳导航到指定页面时发出通知。</summary>
    public event Action<PageId>? NavigateRequested;
    /// <summary>已观察到的原操作投影列表。</summary>
    public ObservableCollection<RunRow> Runs { get; } = [];
    /// <summary>工作空间中各个许可连接的独立投影。</summary>
    public ObservableCollection<LicenseRow> Resources { get; } = [];

    /// <summary>请求外壳切换到指定页面，不执行页面的持久写入。</summary>
    /// <include file="../../Comments.xml" path="doc/Id/*"/>
    public void Navigate(PageId id)
    {
        NavigateRequested?.Invoke(id);
    }

    /// <summary>投影新快照；过期或错序事实不得覆盖更新状态。</summary>
    /// <include file="../../Comments.xml" path="doc/Value/*"/>
    public void Update(WorkspaceSnapshot value)
    {
        snapshot = value;
        Raise(nameof(Snapshot));
        var previous = Resources.ToDictionary(r => r.Definition.Id);
        Resources.Clear();
        foreach (var license in value.LicenseEndpoints)
        {
            var row = new LicenseRow(license);
            if (previous.TryGetValue(license.Id, out var old) &&
                old.Probe?.Resource.IdentityKey == license.Resource.IdentityKey) row.Update(old.Probe);
            Resources.Add(row);
        }

        WorkspaceChanged?.Invoke();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>仅接收匹配端点身份且轮次更新的探测结果，避免迟到事件回退界面。</summary>
    /// <param name="probe">最近探测快照/可替换探针端口</param>
    /// <returns>是否满足所述条件。</returns>
    public bool UpdateProbe(ProbeSnapshot probe)
    {
        var row = Resources.FirstOrDefault(r =>
            r.Definition.Id == probe.Resource.ResourceId &&
            r.Definition.Resource.IdentityKey == probe.Resource.IdentityKey);
        if (row is null) return false;
        if (row.Probe is not null && row.Probe.RoundSequence >= probe.RoundSequence) return false;
        if (probe.NotifyDisconnect && (row.Probe is null || row.Probe.RoundSequence < probe.RoundSequence))
            Message = row.Name + " 连续复查失败，请分别查看端口、服务和虚拟机证据。";
        else if (row.Probe?.State == ResourceState.Unreachable && probe.State == ResourceState.Reachable)
            Message = row.Name + " 的连通性已恢复。";
        row.Update(probe);
        return true;
    }

    /// <summary>合并指定原操作的阶段投影，拒绝以旧序号覆盖新观察。</summary>
    /// <param name="run">带原操作号及单调序号的脱敏持久状态。</param>
    public void UpdateRun(RunSnapshot run)
    {
        var row = Runs.FirstOrDefault(r => r.Snapshot.Id == run.Id);
        if (row is null) Runs.Insert(0, new RunRow(run));
        else row.Update(run);
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>将持久操作投影转换为可绑定的阶段、详情、时间及语义颜色。</summary>
/// <param name="snapshot">不可变配置、操作或探测快照。</param>
public sealed class RunRow(RunSnapshot snapshot) : ObservableObject
{
    /// <summary>当前不可变状态快照。</summary>
    public RunSnapshot Snapshot { get; private set; } = snapshot;

    /// <summary>定义、入口、来源或操作的有限类别。</summary>
    public string Kind => Snapshot.Kind switch
    {
        OperationKind.Launch => "应用启动", OperationKind.Deploy => "工具交付", OperationKind.ApplyVariables => "变量应用",
        _ => "配置迁移"
    };

    /// <summary>操作或回执的可读阶段文字。</summary>
    public string Status => StageLabel(Snapshot.Stage);

    /// <summary>操作或回执的脱敏详情。</summary>
    public string Detail => string.IsNullOrEmpty(Snapshot.SubjectName)
        ? Snapshot.SafeMessage
        : Snapshot.SubjectName + " · " + Snapshot.SafeMessage;

    /// <summary>原操作号的可展示形式。</summary>
    public string OperationId => Snapshot.Id.ToString();
    /// <summary>观察或交付开始时间的本地显示文字。</summary>
    public string Time => Snapshot.ObservedAtUtc.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>状态对应的语义前景色。</summary>
    public string Color => Snapshot.Stage is RunStage.Failed or RunStage.NeedsVerification ? "#B65364" :
        Snapshot.Stage is RunStage.Completed or RunStage.SessionConfirmed ? "#238266" : "#9D762D";

    /// <summary>投影新快照；过期或错序事实不得覆盖更新状态。</summary>
    /// <include file="../../Comments.xml" path="doc/Value/*"/>
    public void Update(RunSnapshot value)
    {
        if (value.Id != Snapshot.Id || value.Sequence < Snapshot.Sequence) return;
        Snapshot = value;
        Notify(nameof(Snapshot), nameof(Status), nameof(Detail), nameof(Time), nameof(Color));
    }

    /// <summary>把持久阶段转换为明确区分窗口观察与项目登录的页面文字。</summary>
    /// <param name="stage">需转换为页面说明的已持久阶段。</param>
    /// <returns>可向用户展示的中文阶段说明；未知阶段使用枚举名称。</returns>
    public static string StageLabel(RunStage stage)
    {
        return stage switch
        {
            RunStage.Accepted => "已受理", RunStage.Executing => "正在准备", RunStage.Checking => "正在检查",
            RunStage.HandedOff => "已交接 · 观察中",
            RunStage.WindowObserved => "窗口出现 · 登录待确认", RunStage.SessionConfirmed => "会话已确认",
            RunStage.Completed => "操作完成",
            RunStage.Failed => "操作失败", RunStage.Cancelled => "已取消", RunStage.CancelRequested => "正在停止",
            RunStage.Exited => "已观察到退出",
            RunStage.NeedsVerification => "需要核验", _ => "结果未确认"
        };
    }
}

/// <summary>展示同一许可资源的地址、连接状态与分项证据。</summary>
/// <param name="definition">从当前快照取得的有限配置定义。</param>
public sealed class LicenseRow(LicenseDefinition definition) : ObservableObject
{
    /// <summary>此投影对应的不可变配置定义。</summary>
    public LicenseDefinition Definition { get; } = definition;
    /// <summary>最近探测快照/可替换探针端口。</summary>
    public ProbeSnapshot? Probe { get; private set; }
    /// <summary>显示名或配置名。</summary>
    public string Name => Definition.Name;
    /// <summary>变量名称与作用域。</summary>
    public string Address => $"{Definition.Resource.RmsPort}@{Definition.Resource.Host}";

    /// <summary>最近归约的可读连接或提交状态。</summary>
    public string State => !Definition.Resource.Enabled
        ? "已暂停"
        : Probe?.State switch
        {
            ResourceState.Reachable => "连通性就绪", ResourceState.Unreachable => "持续不可达",
            ResourceState.Rechecking => "正在复查", _ => "尚未检测"
        };

    /// <summary>状态对应的语义前景色。</summary>
    public string Color => Probe?.State == ResourceState.Reachable ? "#238266" :
        Probe?.State == ResourceState.Unreachable ? "#B65364" : "#8C7B53";

    /// <summary>分项探测或观察证据。</summary>
    public string Evidence => Probe is null
        ? "服务、TCP 与虚拟机状态分别采集"
        : string.Join("\n",
            Probe.Evidence.Select(e =>
                e.Kind switch
                {
                    EvidenceKind.VirtualMachine => "虚拟机：", EvidenceKind.LocalService => "服务：", _ => "TCP："
                } + e.SafeMessage));

    /// <summary>投影新快照；过期或错序事实不得覆盖更新状态。</summary>
    /// <param name="probe">最近探测快照/可替换探针端口</param>
    public void Update(ProbeSnapshot probe)
    {
        Probe = probe;
        Notify(nameof(Probe), nameof(State), nameof(Color), nameof(Evidence));
    }
}

/// <summary>展示登录字段及其在当前快照中的应用、许可和工具关联。</summary>
/// <param name="profile">登录配置或内部记录</param>
/// <param name="workspace">工作空间存储端口。</param>
public sealed class ProfileRow(ProfileSnapshot profile, WorkspaceSnapshot workspace)
{
    /// <summary>登录配置或内部记录。</summary>
    public ProfileSnapshot Profile { get; } = profile;
    /// <summary>显示名或配置名。</summary>
    public string Name => Profile.DisplayFields.Name;

    /// <summary>不含密码的项目、用户、MDB 与模块摘要。</summary>
    public string Summary =>
        $"{Profile.DisplayFields.Project} / {Profile.DisplayFields.User} / {Profile.DisplayFields.Mdb} / {Profile.DisplayFields.Module}";

    /// <summary>版本名称或策略版本。</summary>
    public string Version => Profile.DisplayFields.VersionGroup;

    /// <summary>登录版本的短标记。</summary>
    public string Mark => Version.Contains("AM", StringComparison.OrdinalIgnoreCase) ? "AM" :
        Version.Contains("3.1", StringComparison.OrdinalIgnoreCase) ? "3.1" :
        Version.Contains("2.1", StringComparison.OrdinalIgnoreCase) ? "2.1" : "APP";
    /// <summary>版本短标记的语义底色。</summary>
    public string MarkBackground => Mark == "AM" ? "#E9F6F0" : "#EDF0FF";
    /// <summary>版本短标记的语义前景色。</summary>
    public string MarkColor => Mark == "AM" ? "#238266" : "#4E5DDB";

    /// <summary>应用入口定义。</summary>
    public string Application =>
        workspace.Applications.FirstOrDefault(a => a.Id == Profile.DisplayFields.ApplicationId)?.Name ?? "缺少应用";

    /// <summary>关联许可连接。</summary>
    public string License =>
        workspace.LicenseEndpoints.FirstOrDefault(l => l.Id == Profile.DisplayFields.LicenseId)?.Name ?? "不使用许可连接";

    /// <summary>独立工具或关联工具集。</summary>
    public string Tools => string.Join(" · ",
        workspace.Tools.Where(t => Profile.DisplayFields.ToolIds.Contains(t.Id)).Select(t => t.Name));
}

/// <summary>可选择的登录或工具条目，按稳定标识保存勾选状态。</summary>
/// <param name="id"><include file="../../Comments.xml" path="doc/Id/param[@name='id']/*"/></param>
/// <param name="name">显示名或配置名</param>
/// <param name="selected">条目初始是否被明确选择。</param>
public sealed class ToolChoice(Guid id, string name, bool selected) : ObservableObject
{
    /// <summary>稳定标识。</summary>
    public Guid Id { get; } = id;
    /// <summary>显示名或配置名。</summary>
    public string Name { get; } = name;
    private bool selected = selected;

    /// <summary>页面当前选中条目。</summary>
    public bool Selected
    {
        get => selected;
        set => Set(ref selected, value);
    }
}
