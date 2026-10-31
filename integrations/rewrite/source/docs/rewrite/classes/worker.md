# worker：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## PrivilegedWorkerHost

- 预定文件：`rewrite/LaunchDesk.Worker/PrivilegedWorkerHost.cs`。
- 命名空间：`LaunchDesk.Worker`；项目：`LaunchDesk.Worker`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Executable**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：验证有限请求后执行受保护动作，写回同一 OperationId 的回执。
- 注入依赖：`IFileDeliveryPort`（构造注入）、`IEnvironmentStore`（构造注入）。
- 禁止：不得接收任意脚本、程序路径、登录密码或结束真实 VM/AVEVA。
- 对应验收：AR14、AR15、AR16、AR17、AR26。

### `ValueTask<int> RunAsync(WorkerEnvelope request, CancellationToken ct)`

行为与结果：验证版本、调用者、一次性绑定、确认摘要、目标和操作种类。

失败与取消：InvalidRequest / CallerMismatch / PlanExpired / PermissionDenied；未知操作拒绝。
