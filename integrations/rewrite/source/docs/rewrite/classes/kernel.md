# kernel：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## ConnectionHealthPolicy

- 预定文件：`rewrite/LaunchDesk.Kernel/ConnectionHealthPolicy.cs`。
- 命名空间：`LaunchDesk.Kernel`；项目：`LaunchDesk.Kernel`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Stateless**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：纯函数计算复查、过期和恢复状态，独立保留 VM/服务/端口证据。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得引用 UI、Windows、文件系统或编排框架。
- 对应验收：AR05、AR06、AR07、AR08。

### `ProbeSnapshot Reduce(ProbeSnapshot previous, ProbeRound round, DateTimeOffset now)`

行为与结果：连续三轮失败通知一次，成功清零；只接受显式传入时间。

失败与取消：无 IO；无效观测产生 Unknown 和原因。


## ProcessEnvironmentPolicy

- 预定文件：`rewrite/LaunchDesk.Kernel/ProcessEnvironmentPolicy.cs`。
- 命名空间：`LaunchDesk.Kernel`；项目：`LaunchDesk.Kernel`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Stateless**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：为每次启动创建新环境覆盖，保护许可变量，不修改父快照。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR05、AR18。

### `Result<EnvironmentOverlay> Build(EnvironmentSnapshot parent, LicenseBinding license, IReadOnlyList<ToolBinding> tools)`

行为与结果：工具变量先校验，再由本次许可填充专用变量；不调用全局 SetEnvironmentVariable。

失败与取消：ReservedVariable / DuplicateVariable / InvalidVariableName。


## OperationStatePolicy

- 预定文件：`rewrite/LaunchDesk.Kernel/OperationStatePolicy.cs`。
- 命名空间：`LaunchDesk.Kernel`；项目：`LaunchDesk.Kernel`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Stateless**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：验证通用外壳和类型专属证据，不把 Completed 等同于项目已登录。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR09、AR10、AR11、AR14、AR15、AR16、AR17、AR26。

### `Result<RunSnapshot> Apply(RunSnapshot previous, OperationFact fact)`

行为与结果：只允许规定状态迁移；窗口和会话证据分开。

失败与取消：InvalidTransition / EvidenceMismatch。
