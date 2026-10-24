# 工作台的开发、测试与使用约定

2026-10-24 · 0.3.4.2 源码实施记录。首个交付切面为启动配置；知识源优先级、其他工作台页面仍按各自进度验收。

一个功能由可独立调用的应用服务实现，代码测试、原生 CLI、JSON/TOML 场景和 WPF 调用同一服务。配置不留在界面事件里，测试保存证据，文档同时面向开发、测试和使用。独立 MCP 程序已禁用，CLI 不再创建 MCP 子进程。

## 已实现的调用路线

```text
WPF 启动配置页 ───────────────────────┐
CLI / JSON 文件 / stdin → OperationExecutor → LaunchProfileService
TOML → 规范 JSON → 每步独立 CLI 进程 ─┘             ↓
代码测试 → 同一个服务                  IConfigurationStore / IProductModuleCatalog
```

`src/YuzuhaToolkit.Workspace` 是配置业务边界，`src/YuzuhaToolkit.Mcp.Core/LaunchOperationCatalog.cs` 是原生 CLI 的适配层（程序集历史名称保留）。`MainWindow.LaunchLifecycle.xaml.cs` 绑定该服务，不另写保存规则。`OperationDescriptor` 提供操作 ID、契约版本、输入 Schema 及 PID、执行、写入和启动要求；输出通过 `OperationResult` 返回。输出 JSON Schema 尚未单独导出。

## 可扩展接点

实际接口如下，完整类型说明由 XML 注释进入 DocFX API：

```csharp
public interface IConfigurationStore<T>
{
    ConfigSnapshot<T> Read();
    ConfigCommit<T> Commit(T candidate, string expectedRevision,
        ConfigurationChangeReason reason);
    ConfigHistoryEntry[] History(int limit = 100);
    ConfigSnapshot<T> ReadRevision(string revision);
}

public interface IProductModuleCatalog
{
    ProductModuleSet Get(InstalledProductIdentity installation);
}
```

通过 `new LaunchProfileService(store, modules)` 注入替代实现。新增配置字段先扩展模型、校验和序列化元数据；新增存储或模块来源再实现对应接口。无需改动 CLI 的业务流程，也不要求每个类建立接口。

`LaunchProfileService` 提供读取、组合筛选、校验、差异预览、保存、字段更新、复制、收藏、默认、删除、历史和恢复。`ConfigSnapshot` 包含独立值、修订、Schema 版本、文件位置和存在状态；提交返回新快照、字段差异及生效说明。密码只通过本机凭据引用关联，不进入配置历史。

## 启动配置的完整使用流程

1. 打开正式工作台的启动配置页，用产品、收藏/范围下拉框及关键词组合筛选。
2. 新建或选择配置，指定 AM、E3D 或 PDMS 和安装入口，模块下拉框随产品切换。AM 提供 `Outfitting`、`HullDesign`、`OutfittingDraft`、`MarineDrafting` 等原生值，不接受 `Design`；E3D 使用 `Design`、`Draw` 等，PDMS 使用 `Design`、`Draft` 等。
3. 修改工程、MDB、用户和启动参数，查看字段校验、保存路径与差异后保存。重新打开窗口或用新 CLI 进程读取，结果来自同一份文件。
4. 如果其他窗口、CLI 或外部编辑器已修改配置，保存返回冲突；界面保留草稿，重新读取并比较后再保存，不静默覆盖。
5. 在历史区选择修订、预览恢复差异并恢复。恢复产生一个新修订，保留旧历史，只恢复目标配置，不撤回其他配置的新修改。
6. 单独执行启动。创建入口进程只表示启动请求已发出，实际登录、模块和 Host 连接仍由会话状态核验。

模块的 `Verified` 表示安装目录元数据确认，不表示许可或实际登录通过。无需经过 LaunchDesk 导入。已有配置保存位置显示在界面；`YUZUHA_STATE_DIR` 可为测试选择独立目录。

Monitor 参数顺序保持 `PROD E3D init <launch.init> <工程> <用户/密码> <MDB> <模块>`。密码运行时交给进程，不写入公开日志、配置历史或测试示例。

配置中的“许可与启动动作”可按环境启用或关闭 REHASH，扩展接点见[启动策略 Object](startup-policy.md)。

### 运行时初始化与 Host 选择

PML 初始化执行 `!!PhaytonSession = Current Session`，通过 `!!PhaytonSession.module()` 取得模块；原生 `VAR !banner BANNER` 获取产品与版本。SESSION 没有已确认的版本方法，不调用猜测的 `Session.version()`。Host 创建后使用当前模块刷新身份。

| 产品系列 | Host |
| --- | --- |
| E3D 2.1 | E3D2.1 / NET48 |
| E3D 3.1，各小版本 | E3D3.1.6 / NET48 |
| AM | 已编译 AM / NET35 |
| PDMS | 已编译 PDMS / NET35 |

保留实际版本用于显示与诊断，Host 选择不再区分 E3D 3.1 小版本。具体实机覆盖见验证报告，不由映射表推断兼容性。

## 保存、历史和中断语义

`WorkspaceConfigurationStore` 对 `workspace.json` 整份文件维护内容修订。提交使用跨进程文件锁，再次核对磁盘内容及 `expectedRevision`。保存保留无关配置和 JSON 扩展字段；重复字段、未知高版本 Schema 被拒绝。当前兼容旧配置并保存为支持的 Schema。

历史快照、待提交记录与原子替换组成恢复流程；重开时核对内容确定已提交状态。恢复通过当前校验并生成新修订。代码测试覆盖外部编辑、并发、历史损坏与提交中断。历史上限参数控制返回数量，当前没有分页游标接口。

不同配置共用文件修订；另一配置变化或 `LastStartedUtc` 更新也可能触发冲突。这是当前一致性边界。文件配置、Windows 凭据和 AVEVA 进程不构成跨系统事务；恢复不会重建已删除的凭据、启动进程或执行 PML。

## JSON 与 TOML 场景测试

开发工具 `scripts/run-workbench-scenario.py` 使用 Python 3.11+ 的标准库 `tomllib`。最终桌面与 CLI 用户不需要安装 Python。两种格式先规范为 JSON，然后调用真实 CLI，每一步都是新进程，不依赖内存缓存证明持久化。

仓库中可直接执行的完整案例：

- `tests/scenarios/launch-lifecycle.toml`：手写作者格式。
- `tests/scenarios/launch-lifecycle.json`：同等内容的 JSON。
- `tests/workbench-scenario.test.py`：解析、绑定、授权边界及失败证据测试。

```powershell
python scripts/run-workbench-scenario.py --scenario tests/scenarios/launch-lifecycle.toml --cli artifacts/native-cli-release-final/PmlTwin.Phayton.Cli.exe --output artifacts/scenarios/toml
python scripts/run-workbench-scenario.py --scenario tests/scenarios/launch-lifecycle.json --cli artifacts/native-cli-release-final/PmlTwin.Phayton.Cli.exe --output artifacts/scenarios/json
python tests/workbench-scenario.test.py
```

运行目录必须是新目录。案例包含 15 步：产品模块、读取、修改、重开、过期修订拒绝、AM 模块校验、历史、恢复预览、恢复、再重开和组合筛选。隔离夹具创建临时启动入口文件，仅用于路径校验，**不启动 AVEVA**。

实际场景格式示例（须置于包含版本、夹具、前序读取步骤的完整案例中）：

```toml
[[steps]]
id = "save"
operation = "launch.profile.update"
permissions = ["write"]
[steps.input]
profileId = "sample-e3d"
[steps.input.changes]
mdb = "/REVIEW"
[steps.bindings.expectedRevision]
step = "before"
pointer = "/data/revision"
[[steps.assertions]]
pointer = "/success"
equals = true
```

仅支持已完成步骤的 JSON Pointer 绑定，不支持任意表达式。重复键、非有限数字、TOML 日期类型、未知字段、前向引用、覆盖现有输入、未支持的操作或权限均失败。隔离运行器只允许启动配置文件操作，不能通过案例自授 PML 或进程启动权限。

每次保存原始案例、规范 JSON、输入/输出/标准错误、断言、修订、CLI 哈希、源码版本及 dirty 状态、耗时，以及 `run.json` 和可独立打开的 `report.html`。失败、中断和零执行不会标为通过。正式 CLI 的每次调用另有本地运行记录。

## 三类文档与验收

| 阶段 | 可用入口 | 实际证据 |
| --- | --- | --- |
| 开发 | DocFX API、架构、服务与存储接口 | xUnit / 架构 TRX |
| 测试 | 本页、完整 JSON/TOML、测试目录与复刻命令 | 每步 JSON、HTML 报告、TRX |
| 使用与调试 | 正式工作台、[CLI 使用](cli.md)、[桌面工作台](workspace.md) | 配置历史、运行日志、目标身份与错误码 |

启动配置的 WPF 专项实际检查筛选、AM 模块、保存、冲突保留草稿、恢复、窗口重开和两主题；完整桌面回归另外检查其他页面。该结果不代替 AM 的实际启动验收。

所有已有底层操作都应进入能力报告。真实服务配合临时文件/合成 Host 的通过、真实 E3D 的通过、旧 Host 不支持、未安装平台分开列出；不得将离线通过写成全平台实机通过。原始用户模型、登录名及知识库不放入公开报告。

这次已接通启动配置切面。官方 HTML/源码默认优先检索、其他配置的编辑界面及完整工作台流程还需各自实现；新增接口为它们提供复用点，并不表示这些业务已完成。
