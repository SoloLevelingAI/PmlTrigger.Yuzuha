# coordinator：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## OperationCoordinator

- 预定文件：`rewrite/LaunchDesk.UseCases/Operations/OperationCoordinator.cs`。
- 命名空间：`LaunchDesk.UseCases.Operations`；项目：`LaunchDesk.UseCases`。
- 实现：`IOperationCommands`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：只管理任务外壳、意图、去重、取消和派发，业务规则在具体处理器。
- 注入依赖：`IRunJournal`（构造注入）、`IOperationHandlerRegistry`（构造注入）、`OperationExecutionContext`（每次新建的工厂，任务释放作用域）、`IWorkspaceStore`（构造注入）。
- 禁止：不得直接依赖具体 Handler、平台实现、WPF 或 Aspire，不重放不确定动作。
- 对应验收：AR09、AR10、AR11。

### `ValueTask<Result<OperationId>> SubmitAsync(OperationRequest request, CancellationToken ct)`

行为与结果：校验有限 Kind 和修订，登记意图，通过注册表创建操作级处理器与上下文。 ct 只控制提交阶段，接受后的任务令牌由应用持有，页面取消不结束任务。 受理前冻结配置与已确认 PlanSnapshot，调用 IRunJournal.AcceptAsync 持久化；PlanRef 不能引用页面内存。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Unit>> CancelAsync(OperationId id, CancellationToken ct)`

行为与结果：取消控制令牌，按回执核对已发生事实，不伪报回滚完成。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## OperationExecutionContext

- 预定文件：`rewrite/LaunchDesk.UseCases/Operations/OperationExecutionContext.cs`。
- 命名空间：`LaunchDesk.UseCases.Operations`；项目：`LaunchDesk.UseCases`。
- 实现：`IOperationContext`。
- 生命周期：**Operation**。每个 OperationId 独立实例，单写者变更；释放只影响本次。
- 唯一职责：单次任务的事实入口，校验序号和迁移后写入唯一任务日志。
- 注入依赖：`IRunJournal`（构造注入）、`OperationStatePolicy`（构造注入）。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR09、AR10、AR11。

### `ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct)`

行为与结果：调用纯状态策略再按 expected sequence 追加；处理器不得直接改日志。

失败与取消：InvalidTransition / SequenceConflict；无法提交事实则停止后续副作用并标待核对。

### `ValueTask DisposeAsync()`

行为与结果：释放本次订阅和控制资源，不结束用户程序。

失败与取消：重复释放安全。


## OperationProjectionService

- 预定文件：`rewrite/LaunchDesk.UseCases/Operations/OperationProjectionService.cs`。
- 命名空间：`LaunchDesk.UseCases.Operations`；项目：`LaunchDesk.UseCases`。
- 实现：`ITaskQueries`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：为 UI 提供只读任务快照与订阅，不维护第二份业务状态。
- 注入依赖：`IRunJournal`（构造注入）。
- 禁止：不得追加事件、决定阶段或直接调用处理器。
- 对应验收：AR09、AR10、AR11。

### `ValueTask<Result<RunSnapshot>> ReadAsync(OperationId id, CancellationToken ct)`

行为与结果：缺失记录返回明确错误，不假报完成。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `IAsyncEnumerable<RunSnapshot> WatchAsync(CancellationToken ct)`

行为与结果：先快照再增量；取消 UI 订阅不结束任务。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。
