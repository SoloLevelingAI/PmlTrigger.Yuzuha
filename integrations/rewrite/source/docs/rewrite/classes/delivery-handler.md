# delivery-handler：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## DeliveryOperationHandler

- 预定文件：`rewrite/LaunchDesk.UseCases/Delivery/DeliveryOperationHandler.cs`。
- 命名空间：`LaunchDesk.UseCases.Delivery`；项目：`LaunchDesk.UseCases`。
- 实现：`IOperationHandler`。
- 生命周期：**Operation**。每个 OperationId 独立实例，单写者变更；释放只影响本次。
- 唯一职责：只处理 Deploy/恢复/回滚的有限计划动作，不拥有另一份任务状态。
- 注入依赖：`IFileDeliveryPort`（构造注入）、`ITargetLeaseProvider`（构造注入）、`IPrivilegeBroker`（构造注入）。
- 禁止：不得跳过目标租约/计划重验、直接修改任务外壳或自动重放中断写入。
- 对应验收：AR14、AR15、AR16、AR17、AR26。

### `ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequest request, IOperationContext context, CancellationToken ct)`

行为与结果：取得真实目标租约，重验确认摘要，执行受控写入，回执用于恢复，事实通过上下文报告。

失败与取消：WrongOperationKind / PlanExpired / PermissionDenied / Indeterminate；不自动重放副作用。


## VariableOperationHandler

- 预定文件：`rewrite/LaunchDesk.UseCases/Delivery/VariableOperationHandler.cs`。
- 命名空间：`LaunchDesk.UseCases.Delivery`；项目：`LaunchDesk.UseCases`。
- 实现：`IOperationHandler`。
- 生命周期：**Operation**。每个 OperationId 独立实例，单写者变更；释放只影响本次。
- 唯一职责：处理显式确认的持久变量应用，配置保存与进程注入另行负责。
- 注入依赖：`IEnvironmentStore`（构造注入）、`IPrivilegeBroker`（构造注入）。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR18。

### `ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequest request, IOperationContext context, CancellationToken ct)`

行为与结果：核对原值、新名、作用域及保留变量，系统范围按需提权；上下文只收到脱敏结果。

失败与取消：WrongOperationKind / PlanExpired / PermissionDenied / Indeterminate；不自动重放副作用。


## ToolSourceCatalog

- 预定文件：`rewrite/LaunchDesk.UseCases/Delivery/ToolSourceCatalog.cs`。
- 命名空间：`LaunchDesk.UseCases.Delivery`；项目：`LaunchDesk.UseCases`。
- 实现：`IToolSourceCatalog`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：来源刷新和选择策略；扫描/预览不执行内容。
- 注入依赖：`IPackageReader`（构造注入）。
- 禁止：不得在刷新/选择时保存工具定义、部署文件或执行来源内容。
- 对应验收：AR12、AR13。

### `ValueTask<Result<SourceCatalog>> RefreshAsync(SourceQuery query, SourceSelection current, RefreshIntent intent, CancellationToken ct)`

行为与结果：KeepSelection 保留仍有效旧版；SelectNewest 重新扫描完整 ZIP / 7z 并按修改时间选择。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<SourceSnapshot>> InspectAsync(PackageSource source, CancellationToken ct)`

行为与结果：支持目录、普通文件与映射，不要求先压缩。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## DeploymentPlanner

- 预定文件：`rewrite/LaunchDesk.UseCases/Delivery/DeploymentPlanner.cs`。
- 命名空间：`LaunchDesk.UseCases.Delivery`；项目：`LaunchDesk.UseCases`。
- 实现：`IDeploymentPlanningService`。
- 生命周期：**Stateless**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：生成只读部署计划，绑定来源摘要、真实目标、修订、差异和备份策略。
- 注入依赖：`IPackageReader`（构造注入）、`IFileDeliveryPort`（构造注入）。
- 禁止：不得为预览写目标、请求 UAC 或执行来源程序。
- 对应验收：AR12、AR13、AR14、AR15、AR16、AR17、AR26。

### `ValueTask<Result<DeploymentPlan>> PreviewAsync(DeployRequest request, CancellationToken ct)`

行为与结果：检查路径穿越、映射、目标与记录根；计算 PlanDigest，不写入。

失败与取消：UnsafeArchiveEntry / SourceUnavailable / TargetUnavailable / InvalidPath。

### `ValueTask<Result<DeploymentPlan>> PreviewRecoveryAsync(RecoveryRequest request, CancellationToken ct)`

行为与结果：读取原回执并检查外部修改，生成 Action=Recover/Rollback 且关联 OriginalOperationId 的只读计划。

失败与取消：ReceiptNotFound / BackupUnavailable / RollbackConflict。


## VariablePlanner

- 预定文件：`rewrite/LaunchDesk.UseCases/Delivery/VariablePlanner.cs`。
- 命名空间：`LaunchDesk.UseCases.Delivery`；项目：`LaunchDesk.UseCases`。
- 实现：`IVariablePlanningService`。
- 生命周期：**Stateless**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：只规划变量重命名/写入，执行留给有限操作处理器。
- 注入依赖：`IEnvironmentStore`（构造注入）。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR18。

### `ValueTask<Result<VariableChangePlan>> PreviewAsync(VariableRenameRequest request, CancellationToken ct)`

行为与结果：读取原值和新名，验证作用域和保留字段，形成待确认计划；不写 Windows。

失败与取消：VariableConflict / ReservedVariable / VariableUnavailable。
