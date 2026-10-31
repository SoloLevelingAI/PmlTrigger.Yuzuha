# platform：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## WindowsSecretVault

- 预定文件：`rewrite/LaunchDesk.Windows/WindowsSecretVault.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`ISecretVault`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：隔离 Windows 凭据 API，秘密只在本机短暂取得。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR04。

### `ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct)`

行为与结果：新建引用，不原地覆盖有效旧引用。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct)`

行为与结果：返回可释放租约，不回填页面或进入日志。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Unit>> DeleteAsync(CredentialRef unreferenced, CancellationToken ct)`

行为与结果：仅清理已确认无引用凭据。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## WindowsProcessGateway

- 预定文件：`rewrite/LaunchDesk.Windows/WindowsProcessGateway.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`IProcessGateway`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：执行结构化进程规格，观测子进程和窗口；不作项目成功判定。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR09、AR10、AR11。

### `Result<EnvironmentSnapshot> CaptureEnvironment()`

行为与结果：返回父进程环境的独立只读副本；不修改或缓存为下一次启动的可变全局环境。

失败与取消：EnvironmentUnavailable。

### `ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct)`

行为与结果：仅执行时展开 SecretSlot，应用子进程变量，返回观察句柄。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process, CancellationToken ct)`

行为与结果：报告入口、子进程、窗口、退出；取消观察不能 Kill。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## WindowsResourceProbe

- 预定文件：`rewrite/LaunchDesk.Windows/WindowsResourceProbe.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`IResourceProbe`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：每次只探测一次 TCP、本机服务或 VMware 进程并返回分项证据。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR05、AR06、AR07、AR08。

### `ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct)`

行为与结果：提供可达性证据；重试与通知由资源服务负责。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## PackageReader

- 预定文件：`rewrite/LaunchDesk.Windows/PackageReader.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`IPackageReader`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：只读发现和读取 ZIP / 7z、目录及文件映射。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR12、AR13。

### `ValueTask<Result<SourceCatalog>> DiscoverAsync(SourceQuery query, CancellationToken ct)`

行为与结果：只列完整候选，排除下载中临时文件。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)`

行为与结果：校验条目路径、映射和摘要；禁止执行载荷。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## FileDeliveryPort

- 预定文件：`rewrite/LaunchDesk.Windows/FileDeliveryPort.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`IFileDeliveryPort`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：目标检查和有限文件写入；文件回执与任务日志分开。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR14、AR15、AR16、AR17、AR26。

### `ValueTask<Result<TargetSnapshot>> InspectTargetAsync(TargetSpec target, CancellationToken ct)`

行为与结果：解析真实路径，返回只读差异依据。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<DeploymentReceipt>> ApplyAsync(ConfirmedPlan plan, TargetLease lease, CancellationToken ct)`

行为与结果：核对计划与有效租约，先写回执日志再写文件；恢复/回滚由有限动作标识。

失败与取消：PlanExpired / TargetChanged / RecoveryRequired / RollbackConflict。

### `ValueTask<Result<DeploymentReceipt>> ReadReceiptAsync(OperationId original, CancellationToken ct)`

行为与结果：只读原任务写入和备份事实，供恢复/回滚规划。

失败与取消：ReceiptNotFound / InvalidReceipt。


## CanonicalTargetLeaseProvider

- 预定文件：`rewrite/LaunchDesk.Windows/CanonicalTargetLeaseProvider.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`ITargetLeaseProvider`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：对真实目录及其重叠范围提供跨操作互斥。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR14、AR15、AR16、AR17、AR26。

### `ValueTask<Result<TargetLease>> AcquireAsync(IReadOnlyList<CanonicalPath> targets, CancellationToken ct)`

行为与结果：路径别名和祖先/子目录冲突都互斥；按稳定顺序取得租约，释放安全。

失败与取消：TargetBusy / InvalidTarget；取消等待不撤销其他任务。


## WindowsEnvironmentStore

- 预定文件：`rewrite/LaunchDesk.Windows/WindowsEnvironmentStore.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`IEnvironmentStore`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：读取变量并以原值比较方式执行有限重命名/写入。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR18。

### `ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address, CancellationToken ct)`

行为与结果：区分不存在、空值和实际值。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Unit>> CompareExchangeAsync(ConfirmedVariablePlan plan, CancellationToken ct)`

行为与结果：重验原值、新名、作用域和保留字段，冲突拒绝覆盖。

失败与取消：VariableConflict / ReservedVariable / PermissionDenied。


## WindowsPrivilegeBroker

- 预定文件：`rewrite/LaunchDesk.Windows/WindowsPrivilegeBroker.cs`。
- 命名空间：`LaunchDesk.Windows`；项目：`LaunchDesk.Windows`。
- 实现：`IPrivilegeBroker`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：为已确认有限写入请求 UAC，关联调用者、一次性会话、计划摘要和回执。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得调用页面、决定项目已登录或扩大请求权限；秘密不能被序列化。
- 对应验收：AR14、AR15、AR16、AR17、AR26。

### `ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request, CancellationToken ct)`

行为与结果：只允许文件写入/恢复和变量应用，不接受任意命令行或登录秘密。

失败与取消：PermissionDenied / InvalidRequest / WorkerDisconnected。
