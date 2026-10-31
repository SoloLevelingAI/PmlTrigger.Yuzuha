namespace LaunchDesk.Desktop;

/// <summary>页面命令的公共装配：同步命令交给 WPF，异步命令串行执行并把失败转为页面提示。</summary>
/// <remarks>各命令保留自己的可执行条件；配置、持久操作和外部副作用仍经已装配用例端口提交。</remarks>
/// <param name="presentation">各页面共享的公开工作空间状态。</param>
public abstract class PageViewModel(WorkspacePresentationState presentation) : ObservableObject
{
    /// <summary>各页面共享的公开工作空间状态。</summary>
    protected readonly WorkspacePresentationState state = presentation;

    /// <summary>装配同步页面命令；不额外改变调用方的可执行条件。</summary>
    /// <param name="action">同步页面操作。</param>
    /// <param name="canExecute">可选的可执行条件。</param>
    /// <returns>交给 WPF 查询和执行的命令。</returns>
    protected RelayCommand Sync(Action action, Func<bool>? canExecute = null) => new(action, canExecute);

    /// <summary>装配异步页面命令；复用取消、异常提示及运行状态恢复，保持每条命令独立防重入。</summary>
    /// <param name="action">异步页面操作。</param>
    /// <param name="canExecute">可选的可执行条件。</param>
    /// <returns>串行执行自身操作的命令；其他命令不共享运行锁。</returns>
    protected AsyncCommand Async(Func<Task> action, Func<bool>? canExecute = null) => new(action, Error, canExecute);

    /// <summary>显示用例返回的脱敏失败提示。</summary>
    /// <param name="message">可展示的脱敏说明。</param>
    protected void Error(string message) => state.Message = message;

    /// <summary>刷新公开快照；读取失败时按原页面策略显示错误或保留现有提示，不代替提交核验。</summary>
    /// <param name="catalog">公开配置目录端口。</param>
    /// <param name="ct">取消当前读取。</param>
    /// <param name="reportFailure">是否显示读取失败；提交后的独立读取可保留原结果提示。</param>
    /// <returns>快照读取与投影更新的完成任务。</returns>
    protected async Task RefreshWorkspace(ICatalogService catalog, CancellationToken ct = default, bool reportFailure = false)
    {
        var result = await catalog.ReadAsync(ct);
        if (result.IsSuccess) state.Update(result.Value);
        else if (reportFailure) Error(result.Error!.SafeMessage);
    }

    /// <summary>只读核验原提交；仅Committed/NotCommitted解除待核验并独立刷新，其他状态保留原号，不重放写入。</summary>
    /// <param name="id">需核验的原提交号。</param>
    /// <param name="inspect">所属用例的原提交查询端口。</param>
    /// <param name="catalog">刷新当前公开配置的目录端口。</param>
    /// <param name="messages">原页面的成功、未写入和待核验提示。</param>
    /// <param name="ct">取消当前核验与读取。</param>
    /// <param name="reportReadFailure">是否在独立读取失败时先显示读取错误。</param>
    /// <param name="clearOnlyMatching">仅解除匹配的待核验号；设置页仍无条件解除。</param>
    /// <returns>原回执核验、可选刷新及提示完成的任务。</returns>
    protected async ValueTask VerifyCommit(CommitId id,
        Func<CommitId, CancellationToken, ValueTask<Result<CommitReceipt>>> inspect, ICatalogService catalog,
        (string Committed, string NotCommitted, string Pending) messages, CancellationToken ct = default,
        bool reportReadFailure = false, bool clearOnlyMatching = true)
    {
        var result = await inspect(id, ct);
        if (!result.IsSuccess) { Error(result.Error!.SafeMessage); return; }
        if (result.Value.State is CommitState.Committed or CommitState.NotCommitted)
        {
            if (!clearOnlyMatching || state.PendingCommitId == id) state.PendingCommitId = null;
            await RefreshWorkspace(catalog, ct, reportReadFailure);
            state.Message = result.Value.State == CommitState.Committed ? messages.Committed : messages.NotCommitted;
        }
        else state.Message = messages.Pending;
    }
}
