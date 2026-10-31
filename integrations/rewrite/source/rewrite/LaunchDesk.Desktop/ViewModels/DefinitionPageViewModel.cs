namespace LaunchDesk.Desktop;

/// <summary>复用定义页选择、独立草稿及新建/保存/删除命令注册；定义和确认文案由页面提供。</summary>
/// <typeparam name="TItem">列表中的选择类型。</typeparam>
/// <typeparam name="TDraft">可独立创建的编辑草稿类型。</typeparam>
/// <param name="catalog">公开配置目录端口。</param>
/// <param name="state">公开页面状态。</param>
/// <param name="dialogs"><include file="../../Comments.xml" path="doc/Dialogs/param[@name='dialogs']/*"/></param>
public abstract class DefinitionPageViewModel<TItem, TDraft>(
    ICatalogService catalog, WorkspacePresentationState state, IPresentationDialogs dialogs) : CatalogPageBase(catalog, state)
    where TItem : class where TDraft : new()
{
    /// <summary>选择字段；新建和工具保存可显式通知而不触发选择器的相等检查。</summary>
    protected TItem? selected;
    private TDraft draft = new();
    private Action<TItem>? load;
    private Action<TDraft>? draftAssigned;

    /// <summary>页面当前选中条目。</summary>
    public TItem? Selected
    {
        get => selected;
        set { if (Set(ref selected, value) && value is not null) load?.Invoke(value); }
    }

    /// <summary>未提交的独立编辑草稿。</summary>
    public TDraft Draft
    {
        get => draft;
        protected set { Set(ref draft, value); draftAssigned?.Invoke(value); }
    }

    /// <summary>新增独立条目。</summary>
    public RelayCommand NewCommand { get; private set; } = null!;
    /// <summary>保存当前草稿。</summary>
    public AsyncCommand SaveCommand { get; private set; } = null!;
    /// <summary>移除管理记录。</summary>
    public AsyncCommand DeleteCommand { get; private set; } = null!;

    /// <summary>一次登记三条独立命令；未决提交禁写，保存前冻结原实体身份，核验成功恢复选择；删除须有当前选择且明确确认。</summary>
    /// <param name="identity">当前草稿的原实体身份；新草稿为 Guid.Empty。</param>
    /// <param name="define">构造当前草稿保存/已确认选择删除意图，不直接写入。</param>
    /// <param name="load">将非空新选择加载为独立草稿。</param>
    /// <param name="selectSaved">按冻结的实体身份恢复选择；工具页保留强制重载。</param>
    /// <param name="confirmation">原页面的删除说明和标题；不清理文件、服务或虚拟机。</param>
    /// <param name="newDraftCreated">基础新建完成后的页面候选、计划和导航重置。</param>
    /// <param name="draftAssigned">草稿每次赋值后订阅，含相等值重复赋值；初始草稿不订阅。</param>
    protected void RegisterDefinition(Func<Guid> identity, Func<bool, DefinitionDraft> define, Action<TItem> load,
        Action<Guid> selectSaved, (string Message, string Title) confirmation,
        Action? newDraftCreated = null, Action<TDraft>? draftAssigned = null)
    {
        if (SaveCommand is not null) throw new InvalidOperationException("Definition commands are already registered.");
        this.load = load;
        this.draftAssigned = draftAssigned;
        NewCommand = Sync(() =>
        {
            selected = null;
            Raise(nameof(Selected));
            Draft = new TDraft();
            newDraftCreated?.Invoke();
        }, () => !state.HasPendingCommit);
        SaveCommand = Async(async () =>
        {
            var old = identity();
            var saved = await SaveDefinition(define(false));
            if (saved.HasValue) selectSaved(old == Guid.Empty ? saved.Value : old);
        }, () => !state.HasPendingCommit);
        DeleteCommand = Async(async () =>
        {
            if (Selected is not null && dialogs.Confirm(confirmation.Message, confirmation.Title))
                await SaveDefinition(define(true));
        }, () => Selected is not null && !state.HasPendingCommit);
    }

}
