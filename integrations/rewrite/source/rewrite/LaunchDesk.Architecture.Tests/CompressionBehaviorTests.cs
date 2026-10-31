using LaunchDesk.Desktop;
using LaunchDesk.Kernel;
using LaunchDesk.OperationContracts;

namespace LaunchDesk.Architecture.Tests;

/// <summary>验证复用后的失败传播、页面命令隔离和绑定通知语义。</summary>
public sealed class CompressionBehaviorTests
{
    /// <summary>失败转换不触发后续行为，并保留同一错误对象、原因码及重试提示。</summary>
    [Fact]
    public void FailedResultsDoNotEvaluateMappings()
    {
        var error = new Error("NeedsVerification", "按原编号核验。", true);
        var failed = Result<int>.Fail(error);
        Assert.Same(error, failed.Map<string>(_ => throw new InvalidOperationException()).Error);
        Assert.Same(error, failed.Bind<string>(_ => throw new InvalidOperationException()).Error);
    }

    /// <summary>成功转换执行一次；Bind 可保留后续失败，转换异常不能被隐式吞掉。</summary>
    [Fact]
    public void SuccessfulResultsPreserveContinuationBehavior()
    {
        var calls = 0;
        var source = Result<int>.Ok(7);
        Assert.Equal(8, source.Map(value => { calls++; return value + 1; }).Value);
        Assert.Equal(1, calls);
        var rejected = Result<string>.Fail("Rejected", "后续拒绝。", true);
        Assert.Same(rejected, source.Bind(_ => rejected));
        Assert.Throws<InvalidOperationException>(() => source.Map<int>(_ => throw new InvalidOperationException()));
        Assert.Throws<InvalidOperationException>(() => source.Bind<int>(_ => throw new InvalidOperationException()));
    }

    /// <summary>共用装配不改各条命令的条件，也不把防重入锁扩大为整页互斥。</summary>
    [Fact]
    public async Task PageCommandsRemainIndependentAndRecoverAfterFailure()
    {
        var page = new ProbePage();
        var allowed = false;
        var calls = 0;
        var blocked = page.Command(() => { calls++; return Task.CompletedTask; }, () => allowed);
        await blocked.ExecuteAsync();
        Assert.Equal(0, calls);
        allowed = true;
        await blocked.ExecuteAsync();
        Assert.Equal(1, calls);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = page.Command(() => pending.Task);
        var execution = running.ExecuteAsync();
        Assert.True(running.IsRunning);
        Assert.False(running.CanExecute(null));
        Assert.True(blocked.CanExecute(null));
        await running.ExecuteAsync();
        pending.SetResult();
        await execution;
        Assert.False(running.IsRunning);
        var failed = page.Command(() => throw new InvalidOperationException("secret"));
        await failed.ExecuteAsync();
        Assert.DoesNotContain("secret", page.Message);
        Assert.False(failed.IsRunning);
        Assert.True(failed.CanExecute(null));
        var canceled = page.Command(() => Task.FromCanceled(new CancellationToken(true)));
        await canceled.ExecuteAsync();
        Assert.Equal("操作已停止，已有结果仍可核验。", page.Message);
        Assert.False(canceled.IsRunning);
    }

    /// <summary>批量通知保持原有顺序和事件次数，含重复的同名通知。</summary>
    [Fact]
    public void ProjectionNotificationsKeepOrderAndDuplicates()
    {
        var page = new ProbePage();
        var names = new List<string?>();
        page.PropertyChanged += (_, args) => names.Add(args.PropertyName);
        page.Project("Title", "Subtitle", "Title");
        Assert.Equal(new[] { "Title", "Subtitle", "Title" }, names);
    }

    /// <summary>仅确定结果才刷新；保留匹配解除及设置页无条件解除两种策略，不随流程复用合并。</summary>
    /// <param name="completion">原回执状态。</param>
    /// <param name="matching">待核验编号是否匹配原编号。</param>
    /// <param name="onlyMatching">是否只解除匹配编号。</param>
    /// <param name="cleared">是否应解除待核验。</param>
    /// <param name="reads">应发生的独立刷新次数。</param>
    [Theory]
    [InlineData(CommitState.Pending, true, true, false, 0)]
    [InlineData(CommitState.Indeterminate, true, false, false, 0)]
    [InlineData(CommitState.Committed, true, true, true, 1)]
    [InlineData(CommitState.NotCommitted, false, true, false, 1)]
    [InlineData(CommitState.Committed, false, false, true, 1)]
    public async Task CommitVerificationKeepsPagePolicies(CommitState completion, bool matching, bool onlyMatching, bool cleared, int reads)
    {
        var original = CommitId.New();
        var page = new ProbePage { Pending = matching ? original : CommitId.New() };
        var catalog = new ReadCatalog();
        await page.Verify(original, (_, _) => ValueTask.FromResult(Result<CommitReceipt>.Ok(
            new(original, "digest", completion, new(0), Guid.Empty, null, null, "fixture"))), catalog, onlyMatching);
        Assert.Equal(cleared, page.Pending is null);
        Assert.Equal(reads, catalog.Reads);
        Assert.Equal(completion switch { CommitState.Committed => "committed", CommitState.NotCommitted => "not-committed", _ => "pending" }, page.Message);
    }

    /// <summary>独立读取失败的中间提示按页面策略保留；查询失败不解除待核验，也不刷新或重放写入。</summary>
    [Fact]
    public async Task CommitVerificationPreservesFailureMessages()
    {
        var original = CommitId.New();
        var page = new ProbePage { Pending = original };
        var catalog = new ReadCatalog { Fail = true };
        var messages = new List<string>();
        page.ObserveMessage(messages.Add);
        ValueTask<Result<CommitReceipt>> Inspect(CommitId id, CancellationToken ct) => ValueTask.FromResult(
            Result<CommitReceipt>.Ok(new(id, "digest", CommitState.Committed, new(0), Guid.Empty, null, null, "fixture")));
        await page.Verify(original, Inspect, catalog, true, reportReadFailure: true);
        Assert.Equal(new[] { "read-failed", "committed" }, messages);
        page.Pending = original;
        messages.Clear();
        await page.Verify(original, (_, _) => ValueTask.FromResult(Result<CommitReceipt>.Fail("Unavailable", "inspect-failed")), catalog, true);
        Assert.Equal(original, page.Pending);
        Assert.Equal(1, catalog.Reads);
        Assert.Equal(new[] { "inspect-failed" }, messages);
    }

    private sealed class ProbePage() : PageViewModel(new WorkspacePresentationState("fixture", null!))
    {
        internal string Message => state.Message;
        internal AsyncCommand Command(Func<Task> action, Func<bool>? canExecute = null) => Async(action, canExecute);
        internal void Project(params string[] names) => Notify(names);
        internal CommitId? Pending { get => state.PendingCommitId; set => state.PendingCommitId = value; }
        internal ValueTask Verify(CommitId id, Func<CommitId, CancellationToken, ValueTask<Result<CommitReceipt>>> inspect,
            ICatalogService catalog, bool onlyMatching, bool reportReadFailure = false) =>
            VerifyCommit(id, inspect, catalog, ("committed", "not-committed", "pending"),
                reportReadFailure: reportReadFailure, clearOnlyMatching: onlyMatching);
        internal void ObserveMessage(Action<string> observe) => state.PropertyChanged += (_, args) =>
        { if (args.PropertyName == nameof(state.Message)) observe(state.Message); };
    }

    private sealed class ReadCatalog : ICatalogService
    {
        internal int Reads;
        internal bool Fail;
        public ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct)
        {
            Reads++;
            return ValueTask.FromResult(Fail ? Result<WorkspaceSnapshot>.Fail("Unavailable", "read-failed") :
                Result<WorkspaceSnapshot>.Ok(new(Guid.Empty, new(0), [], [], [], [], [])));
        }
        public ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(CommitId commitId, ProfileDraft draft, SecretUpdate change, Revision expected, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<Result<ProfileSnapshot>> CopyAsync(CommitId commitId, Guid sourceId, Revision expected, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<Result<Unit>> DeleteAsync(CommitId commitId, Guid profileId, Revision expected, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<Result<Revision>> SaveDefinitionAsync(CommitId commitId, DefinitionDraft draft, Revision expected, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<Result<WorkspaceSnapshot>> RefreshDiscoveryAsync(CommitId commitId, Guid applicationId, Revision expected, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<Result<CommitReceipt>> InspectCommitAsync(CommitId commitId, CancellationToken ct) => throw new NotSupportedException();
    }
}
