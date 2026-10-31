# 类与职责设计：archon

状态：待审阅。所有路径均为未来重写目录下的预定文件，尚未创建 C# / XAML。`class-contracts.json` 是本份类目录与接口的单一来源。

先看索引，再点进相关模块。每个类规定职责、接口、C# 风格签名、输入输出、错误、依赖、生命周期与场景；这些是设计边界，不是已可编译的应用骨架。

## 规定到什么程度

类名、命名空间、唯一职责、公开方法语义和依赖方向作为重写基线。内部辅助类可在实现阶段补充；合并职责、更改接口或增加跨模块依赖必须同步本设计和检查，不能用万能 Service / ViewModel 绕过边界。

同模块可协作，跨功能通过契约调用。所有权与时序见 [接口与数据契约](CONTRACTS.md)。

| 预定义类 | 单一职责 | 生命周期 | 详情 |
| --- | --- | --- | --- |
| `WorkspaceCatalogService` | 统一实体 ID、修订与配置关联，负责保存，不执行启动/部署。 | Singleton | [方法与依赖](classes/catalog.md#workspacecatalogservice) |
| `ConfigurationShareService` | 导出允许字段；导入先校验、重映射 ID 与本机路径，再显式提交配置。 | Singleton | [方法与依赖](classes/catalog.md#configurationshareservice) |
| `ResourceSupervisor` | 统一多资源的轮询、时效、有界复查与通知去重。 | Singleton | [方法与依赖](classes/supervisor.md#resourcesupervisor) |
| `ConnectionHealthPolicy` | 纯函数计算复查、过期和恢复状态，独立保留 VM/服务/端口证据。 | Stateless | [方法与依赖](classes/kernel.md#connectionhealthpolicy) |
| `ProcessEnvironmentPolicy` | 为每次启动创建新环境覆盖，保护许可变量，不修改父快照。 | Stateless | [方法与依赖](classes/kernel.md#processenvironmentpolicy) |
| `LaunchPlanBuilder` | 从冻结配置生成可变入口、初始化文件、参数令牌和子进程环境。 | Stateless | [方法与依赖](classes/launch-handler.md#launchplanbuilder) |
| `OperationCoordinator` | 只管理任务外壳、意图、去重、取消和派发，业务规则在具体处理器。 | Singleton | [方法与依赖](classes/coordinator.md#operationcoordinator) |
| `OperationStatePolicy` | 验证通用外壳和类型专属证据，不把 Completed 等同于项目已登录。 | Stateless | [方法与依赖](classes/kernel.md#operationstatepolicy) |
| `OperationExecutionContext` | 单次任务的事实入口，校验序号和迁移后写入唯一任务日志。 | Operation | [方法与依赖](classes/coordinator.md#operationexecutioncontext) |
| `OperationHandlerRegistry` | 唯一把有限操作种类映射到具体处理器工厂的位置。 | Singleton | [方法与依赖](classes/bootstrap.md#operationhandlerregistry) |
| `LaunchOperationHandler` | 只处理 Launch 种类的真实启动与证据观察。 | Operation | [方法与依赖](classes/launch-handler.md#launchoperationhandler) |
| `DeliveryOperationHandler` | 只处理 Deploy/恢复/回滚的有限计划动作，不拥有另一份任务状态。 | Operation | [方法与依赖](classes/delivery-handler.md#deliveryoperationhandler) |
| `VariableOperationHandler` | 处理显式确认的持久变量应用，配置保存与进程注入另行负责。 | Operation | [方法与依赖](classes/delivery-handler.md#variableoperationhandler) |
| `ImportConfigurationHandler` | 仅提交已审阅的导入提案。 | Operation | [方法与依赖](classes/catalog.md#importconfigurationhandler) |
| `ToolSourceCatalog` | 来源刷新和选择策略；扫描/预览不执行内容。 | Singleton | [方法与依赖](classes/delivery-handler.md#toolsourcecatalog) |
| `DeploymentPlanner` | 生成只读部署计划，绑定来源摘要、真实目标、修订、差异和备份策略。 | Stateless | [方法与依赖](classes/delivery-handler.md#deploymentplanner) |
| `JsonWorkspaceStore` | 旧格式兼容读取、修订比较、锁、备份和原子保存。 | Singleton | [方法与依赖](classes/storage.md#jsonworkspacestore) |
| `WorkspaceLocationResolver` | 显式 --config、portable、默认目录的唯一解析与打开入口。 | Singleton | [方法与依赖](classes/storage.md#workspacelocationresolver) |
| `WindowsSecretVault` | 隔离 Windows 凭据 API，秘密只在本机短暂取得。 | Singleton | [方法与依赖](classes/platform.md#windowssecretvault) |
| `WindowsProcessGateway` | 执行结构化进程规格，观测子进程和窗口；不作项目成功判定。 | Singleton | [方法与依赖](classes/platform.md#windowsprocessgateway) |
| `WindowsResourceProbe` | 每次只探测一次 TCP、本机服务或 VMware 进程并返回分项证据。 | Singleton | [方法与依赖](classes/platform.md#windowsresourceprobe) |
| `PackageReader` | 只读发现和读取 ZIP / 7z、目录及文件映射。 | Singleton | [方法与依赖](classes/platform.md#packagereader) |
| `FileDeliveryPort` | 目标检查和有限文件写入；文件回执与任务日志分开。 | Singleton | [方法与依赖](classes/platform.md#filedeliveryport) |
| `CanonicalTargetLeaseProvider` | 对真实目录及其重叠范围提供跨操作互斥。 | Singleton | [方法与依赖](classes/platform.md#canonicaltargetleaseprovider) |
| `WindowsEnvironmentStore` | 读取变量并以原值比较方式执行有限重命名/写入。 | Singleton | [方法与依赖](classes/platform.md#windowsenvironmentstore) |
| `WindowsPrivilegeBroker` | 为已确认有限写入请求 UAC，关联调用者、一次性会话、计划摘要和回执。 | Singleton | [方法与依赖](classes/platform.md#windowsprivilegebroker) |
| `JsonRunJournal` | 唯一脱敏任务事实存储；按事件序号追加，快照可重建。 | Singleton | [方法与依赖](classes/storage.md#jsonrunjournal) |
| `OperationProjectionService` | 为 UI 提供只读任务快照与订阅，不维护第二份业务状态。 | Singleton | [方法与依赖](classes/coordinator.md#operationprojectionservice) |
| `ShellViewModel` | 导航、版本筛选与任务摘要；不拥有任务控制令牌。 | Singleton | [方法与依赖](classes/desktop.md#shellviewmodel) |
| `ProfilesPageViewModel` | 列表、搜索、新增/复制入口及提交启动请求。 | Page | [方法与依赖](classes/desktop.md#profilespageviewmodel) |
| `ProfileEditorViewModel` | 编辑本页草稿和字段校验，保存成功清理临时输入。 | Page | [方法与依赖](classes/desktop.md#profileeditorviewmodel) |
| `LicensePageViewModel` | 分别显示服务、端口和 VM 证据，发起手动复查。 | Page | [方法与依赖](classes/desktop.md#licensepageviewmodel) |
| `ToolPageViewModel` | 工具编辑、来源选择、预览与确认，不写文件。 | Page | [方法与依赖](classes/desktop.md#toolpageviewmodel) |
| `PasswordEntryBehavior` | 仅管理输入期间的显隐、移交和清理。 | Page | [方法与依赖](classes/desktop.md#passwordentrybehavior) |
| `ThemeResourceCatalog` | 固定设计稿的颜色、图标、间距、圆角和控件状态键。 | Singleton | [方法与依赖](classes/desktop.md#themeresourcecatalog) |
| `PrivilegedWorkerHost` | 验证有限请求后执行受保护动作，写回同一 OperationId 的回执。 | Executable | [方法与依赖](classes/worker.md#privilegedworkerhost) |
| `DesktopCompositionRoot` | 唯一装配入口；选择具体平台、用例及本机运行生命周期。 | Singleton | [方法与依赖](classes/bootstrap.md#desktopcompositionroot) |
| `AspireDevelopmentHost` | 可选开发入口，仅编排隔离桌面和模拟资源，SDK 类型仅出现在此项目。 | DevOnly | [方法与依赖](classes/devhost.md#aspiredevelopmenthost) |
| `VariablePlanner` | 只规划变量重命名/写入，执行留给有限操作处理器。 | Stateless | [方法与依赖](classes/delivery-handler.md#variableplanner) |

## 实现时怎样验收

按预定项目/命名空间建立新骨架，再实现最小边界，加入真实编译依赖与行为测试。设计检查不证明运行正确，也不代替用户批准。

`npm run classes:check` 检查接口完整、依赖合法、UI 不访问平台端口、单例不捕获页面/操作实例、构造依赖无环和场景归属，且文档没有漂移。`npm run classes:render` 在修改类契约后重新生成详情。
