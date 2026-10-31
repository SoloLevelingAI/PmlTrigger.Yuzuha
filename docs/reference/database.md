# 应用数据库修订边界

状态：2026-10-31 配置/文档修订基础设施切片。`PmlTwin.Database.Contracts.dll` 定义固定范围、文本修订和批次比较交换；`PmlTwin.Database.dll` 在 net10 管理进程实现 SQLite 存储。未切换现有 Manager/Workspace/Ribbon 活动数据，不代表整体平台已迁移。

## 所有者与依赖

依赖方向为 UI/业务 → Database.Contracts ← Database，装配层显式调用 `SqliteRevisionStore.Open` 并注入 `IRevisionSession`。实现 DLL 不引用 AVEVA、WPF、Workspace、Manager 或安装器；装配不会建目录、启动进程、修改 EVARS 或连接工程。NET35/NET48 Host 不加载这两个 net10 DLL。

**活动数据只能有一个权威写者。** 当前 Workspace 配置继续由 `WorkspaceConfigurationStore` 写入，启动计划/回执继续由既有操作存储写入；新数据库是隔离的显式调用接口。接入 Feature 时必须先交付只读导入、预览、验证、明确切换和恢复，再替换原写入路径；禁止一边写 JSON 一边写本库。SQLite 对同一数据库序列化写事务；这不是另一个业务操作协调器。

## 调用与修订

```csharp
using PmlTwin.Database;
using PmlTwin.Database.Contracts;

// 装配层提供已存在的父目录和数据库路径；UI 不接触存储文件布局。
IRevisionSession configuration = SqliteRevisionStore.Open(
    applicationDatabasePath,
    new DataScope("E3D", "2.1", "APS", "configuration"));
var current = configuration.Read("launch-profile");
var commit = configuration.Commit([
    new RevisionWrite("launch-profile", current?.Revision ?? 0,
        "{\"module\":\"DESIGN\"}", "用户保存草稿")
]);
if (!commit.Committed)
{
    // 显示 commit.Current，重新编辑后再明确提交；不要自动覆盖或盲重试。
}
```

`DataScope` 的 product/version/workspace/kind 四字段均按原样、区分大小写隔离；`configuration` 与 `document` 可使用相同 identity 而互不覆盖。scope 和 identity 各为 1–256 字符且不含控制字符。内容是非秘密 UTF-8 文本，单条最多 1 MiB；原因 1–1000 字符；每批 1–100 个不同对象。调用方负责内容格式、业务校验与秘密剔除，数据库不接收口令或把凭据引用当授权。

修订从 1 递增，预期 0 表示不存在。SQLite `BEGIN IMMEDIATE` 在读取当前修订前获得写事务，同一预期修订的并发请求仅一个成功；失败者收到当前快照。批次中后续对象冲突或 SQL 执行失败会回滚前面已插入的对象。各历史修订不可经接口修改；读取当前/历史均重算 SHA-256，损坏时抛出 `InvalidDataException`。恢复历史仅返回内容，调用方须使用最新预期修订明确提交为新修订。

## 只读、版本与恢复边界

`Open(path, scope, readOnly: true)` 使用 SQLite 只读打开，文件必须已存在；Commit 明确拒绝。既有文件先只读检查 `PRAGMA user_version`，当前仅接受格式 1，并验证 `application_id=0x504D4C54` 和 revisions 的完整列/类型/非空/组合主键；较新格式、旧格式或其他用途 SQLite 均拒绝，不自动降级写入。每次操作重新验证身份与结构；提交取得写事务后再次验证。首次创建不建父目录。若首次创建在格式事务提交前终止，可能留下空格式 0 文件；重开仍明确拒绝，须由调用方确认这是本次未初始化产物后另选新路径或显式移除空文件，不自动接管。打开/提交失败不代表业务动作已经执行。

使用 SQLite 默认回滚日志与提交语义，不主动启用 WAL。每次调用单独开关连接，不保留供应商连接在 UI；10 秒 SQLite 锁等待届满会返回异常，由业务层明确处理。意外终止未提交事务后，SQLite 下次打开恢复最后提交状态；进程夹具对此作真实 kill 验证。该切片没有备份/恢复/显式迁移 API，不能作为生产迁移操作手册。未来 WAL 一致备份须用数据库快照接口，不能直接复制正在使用的 db 文件。

文件/blob 与数据库没有全局原子事务。本片不实现大型资产、文件部署、操作受理/阶段事实，也不改变已有启动协调器的幂等、unknown 或恢复策略。后续应向既有协调器提供事实持久化端口，而非新增协调者；资产需先落受限根的暂存文件、验证大小/哈希、登记阶段，再发布索引，并针对各中断阶段恢复。

## 验证入口

```powershell
& C:/Users/26648/.dotnet/dotnet.exe test tests/PmlTwin.Database.Tests/PmlTwin.Database.Tests.csproj
```

独立测试覆盖跨连接并发、后段冲突全回滚、SQLite 触发器失败全回滚、产品/版本/工作区/用途隔离、历史重开、只读拒写、新版本拒写且文件字节不变、格式 1 外来库/缺列/错误主键拒绝、空格式 0 显式恢复边界、哈希篡改检测、子进程提交与强制终止事务恢复，以及两个程序集的引用边界。2026-10-31 共 14/14 通过，具体执行结果见 Database Feature 交接；未通过执行的测试不能当作验收证据。
