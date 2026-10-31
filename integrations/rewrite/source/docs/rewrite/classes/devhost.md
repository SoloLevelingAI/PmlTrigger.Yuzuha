# devhost：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## AspireDevelopmentHost

- 预定文件：`rewrite/LaunchDesk.DevHost/Aspire/AspireDevelopmentHost.cs`。
- 命名空间：`LaunchDesk.DevHost.Aspire`；项目：`LaunchDesk.DevHost`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**DevOnly**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：可选开发入口，仅编排隔离桌面和模拟资源，SDK 类型仅出现在此项目。
- 注入依赖：`DesktopCompositionRoot`（构造注入）。
- 禁止：不得成为发布物必需依赖或决定业务任务状态。
- 对应验收：AR24、AR25。

### `ValueTask<int> RunAsync(DevelopmentOptions options, CancellationToken ct)`

行为与结果：使用隔离工作空间，停止仅回收自己创建的夹具。

失败与取消：DevelopmentResourceUnavailable；不结束真实许可/VM/AVEVA。
