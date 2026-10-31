# storage：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## JsonWorkspaceStore

- 预定文件：`rewrite/LaunchDesk.Windows/Storage/JsonWorkspaceStore.cs`。
- 命名空间：`LaunchDesk.Windows.Storage`；项目：`LaunchDesk.Windows`。
- 实现：`IWorkspaceStore`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：旧格式兼容读取、修订比较、锁、备份和原子保存。
- 注入依赖：`IWorkspaceLocationResolver`（构造注入）。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR20、AR21、AR22。

### `ValueTask<Result<WorkspaceSnapshotInternal>> ReadAsync(CancellationToken ct)`

行为与结果：损坏、缺失和空配置分开报告，不静默创建伪造数据。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Revision>> CommitAsync(WorkspaceChange change, Revision expected, CancellationToken ct)`

行为与结果：保留 ID、关联、凭据引用和历史根，失败保留原有效文件。

失败与取消：RevisionConflict / StoreUnavailable / MigrationFailed。


## WorkspaceLocationResolver

- 预定文件：`rewrite/LaunchDesk.Windows/Storage/WorkspaceLocationResolver.cs`。
- 命名空间：`LaunchDesk.Windows.Storage`；项目：`LaunchDesk.Windows`。
- 实现：`IWorkspaceLocationResolver`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：显式 --config、portable、默认目录的唯一解析与打开入口。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR20、AR21、AR22。

### `Result<WorkspaceLocation> Resolve(StartupOptions options, string executableDirectory)`

行为与结果：按优先级选择；相对 portable 路径基于 EXE，不受工作目录影响。

失败与取消：InvalidLocation / AccessDenied，不静默切换另一个空工作空间。

### `ValueTask<Result<Unit>> OpenDirectoryAsync(WorkspaceLocation location, CancellationToken ct)`

行为与结果：创建缺失目录并以目录对象打开，不伪造配置。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## JsonRunJournal

- 预定文件：`rewrite/LaunchDesk.Windows/Storage/JsonRunJournal.cs`。
- 命名空间：`LaunchDesk.Windows.Storage`；项目：`LaunchDesk.Windows`。
- 实现：`IRunJournal`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：唯一脱敏任务事实存储；按事件序号追加，快照可重建。
- 注入依赖：`IWorkspaceLocationResolver`（构造注入）。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR09、AR10、AR11。

### `ValueTask<Result<RunSnapshot>> AcceptAsync(OperationRequestSnapshot request, CancellationToken ct)`

行为与结果：去重 OperationId 并持久化不可变执行快照与首次意图；本机快照可含凭据引用，绝无明文秘密；UI 查询不返回请求载荷。

失败与取消：DuplicateOperation / JournalUnavailable。

### `ValueTask<Result<OperationRequestSnapshot>> ReadRequestAsync(OperationId id, CancellationToken ct)`

行为与结果：仅应用用例读取已接受的冻结快照供核对，不能自动重放。

失败与取消：RunNotFound / JournalUnavailable。

### `ValueTask<Result<RunSnapshot>> AppendAsync(OperationId id, long expectedSequence, RunEvent fact, CancellationToken ct)`

行为与结果：原子追加并提交快照；不确定的外部动作保留核对状态。

失败与取消：SequenceConflict / JournalUnavailable。

### `ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)`

行为与结果：缺失记录返回明确错误，不假报完成。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)`

行为与结果：先快照再增量；取消 UI 订阅不结束任务。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。
