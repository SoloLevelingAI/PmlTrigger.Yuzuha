# desktop：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## ShellViewModel

- 预定文件：`rewrite/LaunchDesk.Desktop/ShellViewModel.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Singleton**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：导航、版本筛选与任务摘要；不拥有任务控制令牌。
- 注入依赖：`ITaskQueries`（构造注入）。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR01、AR11、AR23。

### `ValueTask ActivateAsync(CancellationToken ct)`

行为与结果：只订阅不可变摘要，离开时取消自身订阅。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## ProfilesPageViewModel

- 预定文件：`rewrite/LaunchDesk.Desktop/ProfilesPageViewModel.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Page**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：列表、搜索、新增/复制入口及提交启动请求。
- 注入依赖：`ICatalogService`（构造注入）、`IOperationCommands`（构造注入）。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR02、AR03、AR27。

### `ValueTask LaunchSelectedAsync(CancellationToken ct)`

行为与结果：以选中 ID 与修订提交，不创建 Process。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## ProfileEditorViewModel

- 预定文件：`rewrite/LaunchDesk.Desktop/ProfileEditorViewModel.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Page**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：编辑本页草稿和字段校验，保存成功清理临时输入。
- 注入依赖：`ICatalogService`（构造注入）。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR04。

### `ValueTask SaveAsync(SecretUpdate secretChange, CancellationToken ct)`

行为与结果：仅移交本次临时秘密与草稿，不回填已保存密码。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## LicensePageViewModel

- 预定文件：`rewrite/LaunchDesk.Desktop/LicensePageViewModel.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Page**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：分别显示服务、端口和 VM 证据，发起手动复查。
- 注入依赖：`IResourceMonitor`（构造注入）。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR05、AR06、AR07、AR08。

### `ValueTask RefreshAsync(Guid resourceId, CancellationToken ct)`

行为与结果：不自行统计失败轮数，不直接探测。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## ToolPageViewModel

- 预定文件：`rewrite/LaunchDesk.Desktop/ToolPageViewModel.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Page**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：工具编辑、来源选择、预览与确认，不写文件。
- 注入依赖：`ICatalogService`（构造注入）、`IToolSourceCatalog`（构造注入）、`IOperationCommands`（构造注入）、`IDeploymentPlanningService`（构造注入）、`IVariablePlanningService`（构造注入）。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR12、AR13。

### `ValueTask PreviewDeploymentAsync(DeployRequest request, CancellationToken ct)`

行为与结果：调用只读规划契约获取计划；用户确认后再通过 IOperationCommands 提交。

失败与取消：SourceUnavailable / InvalidPath；不创建执行任务。

### `ValueTask PreviewVariableAsync(VariableRenameRequest request, CancellationToken ct)`

行为与结果：只请求变量预览，确认后提交 ApplyVariables 请求。

失败与取消：VariableConflict / ReservedVariable；不隐式写系统。

### `ValueTask RefreshSourcesAsync(RefreshIntent intent, CancellationToken ct)`

行为与结果：刷新最新与保留旧选择传不同意图。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask SubmitConfirmedAsync(ConfirmedPlan plan, CancellationToken ct)`

行为与结果：只提交确认过的计划，展示任务 ID 和持续结果。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## PasswordEntryBehavior

- 预定文件：`rewrite/LaunchDesk.Desktop/PasswordEntryBehavior.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Page**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：仅管理输入期间的显隐、移交和清理。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR04。

### `void RevealWhileEditing()`

行为与结果：只显示本次新输入，离焦/保存/离页隐藏。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `TransientSecret DetachInput()`

行为与结果：移交输入后清空控件；不公开可绑定 Password 字符串属性。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## ThemeResourceCatalog

- 预定文件：`rewrite/LaunchDesk.Desktop/ThemeResourceCatalog.cs`。
- 命名空间：`LaunchDesk.Desktop`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Singleton**。UI Dispatcher 更新属性，订阅切回 Dispatcher；页面只取消自身等待，不结束应用任务。
- 唯一职责：固定设计稿的颜色、图标、间距、圆角和控件状态键。
- 注入依赖：无；仅使用显式方法参数。
- 禁止：不得引用平台端口/具体业务类；不得用页面作为任务唯一状态源；不得阻塞 UI。
- 对应验收：AR01、AR11、AR23。

### `ThemeSnapshot Resolve(ThemePreference preference)`

行为与结果：按钮、下拉、卡片、图标由集中命名令牌引用，逐页核对。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。
