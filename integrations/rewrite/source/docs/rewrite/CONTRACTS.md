# 接口、数据与所有权契约

[返回类索引](CLASS-DESIGN.md)。签名采用 C# 风格描述边界；DTO 为拟议类型，当前没有生成可编译声明。

## 统一约束

- UI 仅拿可绑定的不可变快照；内部快照可含 CredentialRef，不能直接返回页面或分享。
- SecretUpdate 只有 Keep / Replace(TransientSecret) / Clear。先保存新秘密引用，再提交配置；失败清理新引用，原有效引用保留至提交成功。已保存密码不回填。
- Start/Submit 的 CancellationToken 只控制接受请求；接受后由应用拥有运行令牌。关闭页面仅取消订阅；显式 Cancel/StopTracking 按 OperationId 执行。
- Singleton 不直接捕获 Page / Operation；工厂每次新建作用域，由任务租约释放。生产组合根装配普通 .NET 本机协调；Aspire 只在开发入口替换。
- Result<T> 承载业务失败；取消异常在用例边界映射。UI 显示 SafeMessage / ReasonCode，不记录含秘密参数或任意原始异常。
- 任务日志证明计划、意图和观测；部署回执证明写入与备份。进程创建与日志不能原子提交，崩溃后核对，不自动重放。
- Worker 请求限定版本、Kind、OperationId、PlanDigest、调用者和一次性会话；接收端重验计划和目标，不接受任意程序或登录秘密。

## 核心数据形状

| 类型 | 必须包含/保证 |
| --- | --- |
| `WorkspaceSnapshot` | Revision、Applications、Profiles、LicenseEndpoints、Tools、Templates；只读快照，集合不可由页面直接写入。 |
| `ProfileDraft` | ProfileId?、ApplicationId、Project、User、Mdb、Module、LicenseId、ToolIds；新增的 ID 由用例分配，密码不属于 DTO。 |
| `ProfileSnapshot` | ProfileId、Revision、DisplayFields、BindingStatus、CredentialStatus；供 UI 使用，不含密码和 CredentialRef。 |
| `OperationId / Revision` | 稳定标识与乐观并发版本；不以名称或文件路径作为实体主键。 |
| `LaunchPlan` | PlanId、WorkspaceRevision、ApplicationId、Entrypoint、InitFile、WorkingDirectory、ArgumentTokens、EnvironmentOverlay、CredentialRef?；令牌中的 SecretSlot 只在执行端展开。 |
| `RunSnapshot / RunEvent` | OperationId、Sequence、Kind、Stage、Outcome、ReasonCode、Evidence、ObservedAtUtc；只含脱敏事实。 |
| `ProbeSnapshot` | ResourceId、ObservedAtUtc、ValidUntilUtc、LastConfirmedReadyUtc、ConsecutiveFailedRounds、State、Evidence；VM 存活、服务和端口证据分别存储。 |
| `DeploymentPlan / ConfirmedPlan` | PlanId、SourceDigest、WorkspaceRevision、CanonicalTargets、Diff、VariableChanges、BackupPolicy；ConfirmedPlan 绑定 PlanDigest，不能用 bool true 代替确认。 计划包含有限 Action=Install/Recover/Rollback；恢复动作关联 OriginalOperationId，必须重新预览并确认。 |
| `DeploymentReceipt` | OperationId、PlanDigest、WriteAheadEntries、BackupLocation、ActualChanges、RecoveryState；与 UI 任务日志不同，供写入恢复使用。 |
| `VariableChangePlan` | OldName、OldExpectedValue、NewName、NewValue、Scope、ExpectedTargetState；只读计划，执行前比较原值。 |
| `ShareBundle / ImportProposal` | 分享数据与导入草稿分离；不含 CredentialRef、密码、回执、备份和本机变量应用状态。 |
| `TransientSecret / SecretLease` | 进程内短生命期、可释放且不可序列化；尽量清理内存，不宣称能够消除托管字符串或目标应用命令行的全部可见风险。 |
| `Result<T> / Error` | Success 或 Code、SafeMessage、RetryHint、Evidence；业务错误结构化返回；取消使用 CancellationToken / OperationCanceledException，由用例边界映射为 Cancelled。 |
| `TargetLease / TrackedProcess` | IAsyncDisposable 句柄；释放观察或锁不代表结束目标应用。 |
| `OperationRequest / OperationFact / OperationContext` | 仅 Archon 候选使用：有限 Kind、PlanRef、Revision 的请求；脱敏事实；由协调器提供的单次执行上下文。 |
| `OperationRequestSnapshot` | 已接受的本机不可变执行快照：OperationId、Kind、WorkspaceRevision、冻结配置及 PlanSnapshot、PlanDigest；不含明文秘密，不直接提供给 UI/导出。PlanRef 是该快照内计划摘要引用，不指向页面内存。 |

补充 DTO 约定：DefinitionDraft 是 Application / License / Tool / Template 的有限联合；LaunchConfiguration 为内部冻结登录与应用快照；Source / Target / Variable / Worker 请求带明确 Kind、稳定 ID 和修订。ConfirmedVariablePlan / WorkerEnvelope 不能用 bool 或任意字典代替确认。HandlerLease 拥有操作作用域；IOperationContext 只暴露 ReportAsync 与取消，不提供服务定位器。Unit 表示无数据成功。完整序列化 schema 在批准后的契约实现阶段补齐，不能直接序列化含秘密的运行时对象。

## 接口清单

### ICatalogService

调用者：UI / 应用用例。职责：统一实体 ID、修订与配置关联，负责保存，不执行启动/部署。的公开边界。

实现者：`WorkspaceCatalogService`。

```csharp
ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct);
ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(ProfileDraft draft, SecretUpdate change, Revision expected, CancellationToken ct);
ValueTask<Result<ProfileSnapshot>> CopyAsync(Guid sourceId, Revision expected, CancellationToken ct);
ValueTask<Result<Unit>> DeleteAsync(Guid profileId, Revision expected, CancellationToken ct);
ValueTask<Result<Revision>> SaveDefinitionAsync(DefinitionDraft draft, Revision expected, CancellationToken ct);
```

### IConfigurationShareService

调用者：UI / 应用用例。职责：导出允许字段；导入先校验、重映射 ID 与本机路径，再显式提交配置。的公开边界。

实现者：`ConfigurationShareService`。

```csharp
ValueTask<Result<ShareBundle>> ExportAsync(ShareSelection selection, CancellationToken ct);
ValueTask<Result<ImportProposal>> PreviewImportAsync(ShareBundle bundle, CancellationToken ct);
ValueTask<Result<Revision>> CommitAsync(ImportProposal proposal, Revision expected, CancellationToken ct);
```

### IResourceMonitor

调用者：UI / 应用用例。职责：统一多资源的轮询、时效、有界复查与通知去重。的公开边界。

实现者：`ResourceSupervisor`。

```csharp
ValueTask<Result<ProbeSnapshot>> RefreshAsync(Guid resourceId, CancellationToken ct);
ValueTask<Result<ProbeSnapshot>> RequireFreshAsync(Guid resourceId, CancellationToken ct);
IAsyncEnumerable<ProbeSnapshot> WatchAsync(CancellationToken ct);
```

### IOperationCommands

调用者：UI / 应用用例。职责：只管理任务外壳、意图、去重、取消和派发，业务规则在具体处理器。的公开边界。

实现者：`OperationCoordinator`。

```csharp
ValueTask<Result<OperationId>> SubmitAsync(OperationRequest request, CancellationToken ct);
ValueTask<Result<Unit>> CancelAsync(OperationId id, CancellationToken ct);
```

### IOperationHandlerRegistry

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：唯一把有限操作种类映射到具体处理器工厂的位置。的公开边界。

实现者：`OperationHandlerRegistry`。

```csharp
Result<HandlerLease> Create(OperationKind kind);
```

### IOperationHandler

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：有限操作处理器协议。

实现者：`LaunchOperationHandler`、`DeliveryOperationHandler`、`VariableOperationHandler`、`ImportConfigurationHandler`。

```csharp
ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequest request, IOperationContext context, CancellationToken ct);
```

### IToolSourceCatalog

调用者：UI / 应用用例。职责：来源刷新和选择策略；扫描/预览不执行内容。的公开边界。

实现者：`ToolSourceCatalog`。

```csharp
ValueTask<Result<SourceCatalog>> RefreshAsync(SourceQuery query, SourceSelection current, RefreshIntent intent, CancellationToken ct);
ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct);
```

### IWorkspaceStore

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：旧格式兼容读取、修订比较、锁、备份和原子保存。的公开边界。

实现者：`JsonWorkspaceStore`。

```csharp
ValueTask<Result<WorkspaceSnapshotInternal>> ReadAsync(CancellationToken ct);
ValueTask<Result<Revision>> CommitAsync(WorkspaceChange change, Revision expected, CancellationToken ct);
```

### IWorkspaceLocationResolver

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：显式 --config、portable、默认目录的唯一解析与打开入口。的公开边界。

实现者：`WorkspaceLocationResolver`。

```csharp
Result<WorkspaceLocation> Resolve(StartupOptions options, string executableDirectory);
ValueTask<Result<Unit>> OpenDirectoryAsync(WorkspaceLocation location, CancellationToken ct);
```

### ISecretVault

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：隔离 Windows 凭据 API，秘密只在本机短暂取得。的公开边界。

实现者：`WindowsSecretVault`。

```csharp
ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct);
ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct);
ValueTask<Result<Unit>> DeleteAsync(CredentialRef unreferenced, CancellationToken ct);
```

### IProcessGateway

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：执行结构化进程规格，观测子进程和窗口；不作项目成功判定。的公开边界。

实现者：`WindowsProcessGateway`。

```csharp
Result<EnvironmentSnapshot> CaptureEnvironment();
ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct);
IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process, CancellationToken ct);
```

### IResourceProbe

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：每次只探测一次 TCP、本机服务或 VMware 进程并返回分项证据。的公开边界。

实现者：`WindowsResourceProbe`。

```csharp
ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct);
```

### IPackageReader

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：只读发现和读取 ZIP / 7z、目录及文件映射。的公开边界。

实现者：`PackageReader`。

```csharp
ValueTask<Result<SourceCatalog>> DiscoverAsync(SourceQuery query, CancellationToken ct);
ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct);
```

### IFileDeliveryPort

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：目标检查和有限文件写入；文件回执与任务日志分开。的公开边界。

实现者：`FileDeliveryPort`。

```csharp
ValueTask<Result<TargetSnapshot>> InspectTargetAsync(TargetSpec target, CancellationToken ct);
ValueTask<Result<DeploymentReceipt>> ApplyAsync(ConfirmedPlan plan, TargetLease lease, CancellationToken ct);
ValueTask<Result<DeploymentReceipt>> ReadReceiptAsync(OperationId original, CancellationToken ct);
```

### ITargetLeaseProvider

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：对真实目录及其重叠范围提供跨操作互斥。的公开边界。

实现者：`CanonicalTargetLeaseProvider`。

```csharp
ValueTask<Result<TargetLease>> AcquireAsync(IReadOnlyList<CanonicalPath> targets, CancellationToken ct);
```

### IEnvironmentStore

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：读取变量并以原值比较方式执行有限重命名/写入。的公开边界。

实现者：`WindowsEnvironmentStore`。

```csharp
ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address, CancellationToken ct);
ValueTask<Result<Unit>> CompareExchangeAsync(ConfirmedVariablePlan plan, CancellationToken ct);
```

### IPrivilegeBroker

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：为已确认有限写入请求 UAC，关联调用者、一次性会话、计划摘要和回执。的公开边界。

实现者：`WindowsPrivilegeBroker`。

```csharp
ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request, CancellationToken ct);
```

### IRunJournal

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：唯一脱敏任务事实存储；按事件序号追加，快照可重建。的公开边界。

实现者：`JsonRunJournal`。

```csharp
ValueTask<Result<RunSnapshot>> AcceptAsync(OperationRequestSnapshot request, CancellationToken ct);
ValueTask<Result<OperationRequestSnapshot>> ReadRequestAsync(OperationId id, CancellationToken ct);
ValueTask<Result<RunSnapshot>> AppendAsync(OperationId id, long expectedSequence, RunEvent fact, CancellationToken ct);
ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct);
IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct);
```

### ITaskQueries

调用者：UI / 应用用例。职责：为 UI 提供只读任务快照与订阅，不维护第二份业务状态。的公开边界。

实现者：`OperationProjectionService`。

```csharp
ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct);
IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct);
```

### IDeploymentPlanningService

调用者：UI / 应用用例。职责：只读部署预览边界。。

实现者：`DeploymentPlanner`。

```csharp
ValueTask<Result<DeploymentPlan>> PreviewAsync(DeployRequest request, CancellationToken ct);
ValueTask<Result<DeploymentPlan>> PreviewRecoveryAsync(RecoveryRequest request, CancellationToken ct);
```

### IOperationContext

调用者：应用用例/受控基础设施；禁止页面直接访问。职责：本次执行的事实报告与释放边界，不是服务定位器。。

实现者：`OperationExecutionContext`。

```csharp
ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct);
ValueTask DisposeAsync();
```

### IVariablePlanningService

调用者：UI / 应用用例。职责：只读变量变更规划边界。。

实现者：`VariablePlanner`。

```csharp
ValueTask<Result<VariableChangePlan>> PreviewAsync(VariableRenameRequest request, CancellationToken ct);
```
