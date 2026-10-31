# supervisor：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## ResourceSupervisor

- 预定文件：`rewrite/LaunchDesk.UseCases/Resources/ResourceSupervisor.cs`。
- 命名空间：`LaunchDesk.UseCases.Resources`；项目：`LaunchDesk.UseCases`。
- 实现：`IResourceMonitor`。
- 生命周期：**Singleton**。同资源单轮互斥，不同资源并发；应用退出取消轮询，不结束外部资源。
- 唯一职责：统一多资源的轮询、时效、有界复查与通知去重。
- 注入依赖：`IResourceProbe`（构造注入）、`ConnectionHealthPolicy`（构造注入）。
- 禁止：不得把端口成功当许可签出，或把端口失败称作虚拟机丢失。
- 对应验收：AR05、AR06、AR07、AR08。

### `ValueTask<Result<ProbeSnapshot>> RefreshAsync(Guid resourceId, CancellationToken ct)`

行为与结果：每轮最多三次，间隔 350ms，TCP 单次最多 2s；按已完成轮次计数。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<ProbeSnapshot>> RequireFreshAsync(Guid resourceId, CancellationToken ct)`

行为与结果：启动前确认未过期的成功证据，不能无限沿用旧 Ready。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `IAsyncEnumerable<ProbeSnapshot> WatchAsync(CancellationToken ct)`

行为与结果：取消订阅只停止本订阅，不停止全局监测。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。
