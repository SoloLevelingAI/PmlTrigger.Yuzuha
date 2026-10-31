# catalog：类职责与方法

[返回类索引](../CLASS-DESIGN.md)。以下均为待审阅的设计契约，不包含方法实现。

## WorkspaceCatalogService

- 预定文件：`rewrite/LaunchDesk.UseCases/Catalog/WorkspaceCatalogService.cs`。
- 命名空间：`LaunchDesk.UseCases.Catalog`；项目：`LaunchDesk.UseCases`。
- 实现：`ICatalogService`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：统一实体 ID、修订与配置关联，负责保存，不执行启动/部署。
- 注入依赖：`IWorkspaceStore`（构造注入）、`ISecretVault`（构造注入）。
- 禁止：不得保存明文密码、启动进程、部署文件或隐式应用变量。
- 对应验收：AR02、AR03、AR27。

### `ValueTask<Result<WorkspaceSnapshot>> ReadAsync(CancellationToken ct)`

行为与结果：返回供 UI 读取的不可变快照，隐藏 CredentialRef。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<ProfileSnapshot>> SaveProfileAsync(ProfileDraft draft, SecretUpdate change, Revision expected, CancellationToken ct)`

行为与结果：校验引用；新秘密先存新引用，配置提交失败清理新引用，提交成功才清理无引用的旧凭据。

失败与取消：InvalidReference / RevisionConflict / CredentialWriteFailed；清理失败留下脱敏诊断，不能丢失原有效凭据。

### `ValueTask<Result<ProfileSnapshot>> CopyAsync(Guid sourceId, Revision expected, CancellationToken ct)`

行为与结果：分配新 ID，保留可编辑字段及本机关联，不复制秘密。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Unit>> DeleteAsync(Guid profileId, Revision expected, CancellationToken ct)`

行为与结果：只删管理记录并核对关联；不卸载应用或删除工具文件。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Revision>> SaveDefinitionAsync(DefinitionDraft draft, Revision expected, CancellationToken ct)`

行为与结果：只支持应用/许可/工具/模板等有限定义种类；变量定义保存不等于 Windows 应用。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## ConfigurationShareService

- 预定文件：`rewrite/LaunchDesk.UseCases/Catalog/ConfigurationShareService.cs`。
- 命名空间：`LaunchDesk.UseCases.Catalog`；项目：`LaunchDesk.UseCases`。
- 实现：`IConfigurationShareService`。
- 生命周期：**Singleton**。不保存当前页面/当前登录等请求级字段；共享状态显式同步。
- 唯一职责：导出允许字段；导入先校验、重映射 ID 与本机路径，再显式提交配置。
- 注入依赖：`IWorkspaceStore`（构造注入）。
- 禁止：不得导出密码、凭据引用、任务记录、备份或变量应用状态。
- 对应验收：AR19。

### `ValueTask<Result<ShareBundle>> ExportAsync(ShareSelection selection, CancellationToken ct)`

行为与结果：构造新的分享对象，不直接序列化本机完整配置。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<ImportProposal>> PreviewImportAsync(ShareBundle bundle, CancellationToken ct)`

行为与结果：生成待绑定草稿，不写系统或执行内容。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。

### `ValueTask<Result<Revision>> CommitAsync(ImportProposal proposal, Revision expected, CancellationToken ct)`

行为与结果：只保存已确认配置，不部署、启动或应用变量。

失败与取消：返回结构化业务错误；取消由用例边界记录，禁止吞错或盲目重试。


## ImportConfigurationHandler

- 预定文件：`rewrite/LaunchDesk.UseCases/Catalog/ImportConfigurationHandler.cs`。
- 命名空间：`LaunchDesk.UseCases.Catalog`；项目：`LaunchDesk.UseCases`。
- 实现：`IOperationHandler`。
- 生命周期：**Operation**。每个 OperationId 独立实例，单写者变更；释放只影响本次。
- 唯一职责：仅提交已审阅的导入提案。
- 注入依赖：`IConfigurationShareService`（构造注入）。
- 禁止：不得越过本类声明的 IO、权限或状态所有权边界。
- 对应验收：AR19、AR27。

### `ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequest request, IOperationContext context, CancellationToken ct)`

行为与结果：调用分享契约提交配置，不启动、部署或写 Windows 变量。

失败与取消：WrongOperationKind / PlanExpired / PermissionDenied / Indeterminate；不自动重放副作用。
