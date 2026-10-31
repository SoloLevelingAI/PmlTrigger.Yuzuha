# bootstrap：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## OperationHandlerRegistry

- 预定文件：`rewrite/LaunchDesk.Desktop/Bootstrap/OperationHandlerRegistry.cs`。
- 命名空间：`LaunchDesk.Desktop.Bootstrap`；项目：`LaunchDesk.Desktop`。
- 实现：`IOperationHandlerRegistry`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：唯一把有限操作种类映射到具体处理器工厂的位置。
- 注入依赖：`LaunchOperationHandler`（每次新建的工厂，任务释放作用域）、`DeliveryOperationHandler`（每次新建的工厂，任务释放作用域）、`VariableOperationHandler`（每次新建的工厂，任务释放作用域）、`ImportConfigurationHandler`（每次新建的工厂，任务释放作用域）。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR24、AR25。

### `Result<HandlerLease> Create(OperationKind kind)`

行为与结果：支持 Launch / Deploy / ApplyVariables / ImportConfiguration，每次返回带独立依赖作用域的租约。

失败与取消：UnsupportedOperationKind；禁止反射任意类型或动态加载 DLL。


## DesktopCompositionRoot

- 预定文件：`rewrite/LaunchDesk.Desktop/Bootstrap/DesktopCompositionRoot.cs`。
- 命名空间：`LaunchDesk.Desktop.Bootstrap`；项目：`LaunchDesk.Desktop`。
- 实现：内部协作类，无额外公开服务接口。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：唯一装配入口；选择具体平台、用例及本机运行生命周期。
- 注入依赖：`WorkspaceCatalogService`（构造注入）、`ResourceSupervisor`（构造注入）、`OperationCoordinator`（构造注入）、`OperationHandlerRegistry`（构造注入）、`OperationProjectionService`（构造注入）、`JsonWorkspaceStore`（构造注入）、`JsonRunJournal`（构造注入）、`WindowsProcessGateway`（构造注入）、`ThemeResourceCatalog`（构造注入）。
- 禁止：装配例外只在 Bootstrap 命名空间；生产入口不得引用 DevHost。
- 对应验收：AR24、AR25。

### `Result<DesktopRuntime> Build(RuntimeOptions options)`

行为与结果：注册应用单例、页面和操作工厂；停止本机协调不结束用户外部资源。

失败与取消：ConfigurationUnavailable / DuplicateRegistration，不静默新建空数据。
