# launch-handler：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## LaunchPlanBuilder

- 预定文件：`rewrite/LaunchDesk.UseCases/Launch/LaunchPlanBuilder.cs`。
- 命名空间：`LaunchDesk.UseCases.Launch`；项目：`LaunchDesk.UseCases`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Stateless**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：从冻结配置生成可变入口、初始化文件、参数令牌和子进程环境。
- 注入依赖：`ProcessEnvironmentPolicy`（构造注入）。
- 禁止：不得展开密码、启动应用、改写全局环境或猜测项目已登录。
- 对应验收：AR02、AR03、AR27。

### `Result<LaunchPlan> Build(LaunchConfiguration snapshot, EnvironmentSnapshot parent)`

行为与结果：区分 mon、Marine 批处理、普通 EXE；SecretSlot 仅是占位，普通 EXE 用参数列表，cmd 专门编码。

失败与取消：MissingEntrypoint / MissingInitFile / InvalidArguments。


## LaunchOperationHandler

- 预定文件：`rewrite/LaunchDesk.UseCases/Launch/LaunchOperationHandler.cs`。
- 命名空间：`LaunchDesk.UseCases.Launch`；项目：`LaunchDesk.UseCases`。
- 实现：`IOperationHandler`。
- 生命周期：**Operation**。每个 OperationId 独立实例，单写者变更；释放只影响本次。
- 唯一职责：只处理 Launch 种类的真实启动与证据观察。
- 注入依赖：`LaunchPlanBuilder`（构造注入）、`IResourceMonitor`（构造注入）、`ISecretVault`（构造注入）、`IProcessGateway`（构造注入）。
- 禁止：不得直接追加协调器日志、把窗口当登录成功或在取消/释放时 Kill 用户应用。
- 对应验收：AR09、AR10、AR11、AR04。

### `ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequest request, IOperationContext context, CancellationToken ct)`

行为与结果：构建计划，检查新鲜资源，短暂解析秘密；入口交接、窗口、会话和退出分别经上下文报告；90 秒未确认，观察最多 5 分钟。 用 IProcessGateway.CaptureEnvironment 取得独立环境快照，先构建计划，再执行；绝不复用上一任务的覆盖字典。 五分钟上限只适用于未观察到窗口的阶段；已出现关联窗口则持续观察进程直到 Exited 或显式停止。

失败与取消：WrongOperationKind / PlanExpired / PermissionDenied / Indeterminate；不自动重放副作用。
