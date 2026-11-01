# Yuzuha 管理器 CLI

管理器 CLI 已提供启动配置、目录状态、通信入口、EVARS 预览/恢复、运行目录部署、数据迁移、版本隔离知识库及可选插件入口。实时 AVEVA 通信经过独立 `PmlTwin.Phayton.Cli` 子进程。WPF“安装与环境准备”已复用同一执行器；[完整八阶段契约](install-to-operation.zh-CN.md)关联脚本、DocFX、原生页面和验收边界。更广的管理页面与跨产品实机验收仍按OpenSpec逐项登记。

安装包管理入口为 `runtime/manager/Yuzuha.Manager.Cli.exe`；`op invoke manager.contracts --input-stdin` 可读取实际契约和操作目录。只读HTTP元数据、OpenAPI及TS/JS适配的生成来源和授权边界见完整契约；安装后docs-only文档宿主不开放开发API。

## 开发运行

需要 global.json 指定的 .NET 10 SDK。脚本只调整当前进程环境，不更改系统 PATH：

```powershell
. ./scripts/Use-YuzuhaDotnet.ps1
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op list
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op describe launch.profile.save
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op invoke manager.status
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op invoke launch.profile.list --input '{"query":"","scope":"all"}'
```

list/describe/status 不打开 GUI、不创建用户资料或连接 AVEVA。配置的预览、保存、历史、恢复、收藏和启动沿用现有 `launch.*` ID；保存需 `--allow-write`，启动另需 `--allow-launch`，预览不隐式执行。

## 连接到独立通信 CLI

生产安装默认使用安装根下 `runtime/cli/PmlTwin.Phayton.Cli.exe`。开发时明确指定 DLL 和 dotnet host：

```powershell
$env:YUZUHA_COMMUNICATION_CLI = (Resolve-Path 'src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.dll').Path
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op invoke connection.capabilities
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op invoke connection.sessions
```

从发现结果取得 PID、启动时间与模块后再测试目标，不使用示例数字猜测进程：

```powershell
# 将下列变量设为本次发现结果中的准确值。
dotnet run --project src/YuzuhaToolkit.Manager.Cli -c Release -- op invoke connection.test --pid $targetPid --expected-start-time-utc-ticks $targetStartTicks --expected-model $targetModule
```

无会话时先启动工程再发现。测试只进行身份和协议核验，不执行用户 PML。连接结果可过期，不替代下一次执行时的核验和授权。

`connection.select`、`connection.status`、`connection.test` 均要求 PID/启动时间/模块，每次调用创建独立通信进程，不继承上次选择；分别转接原生 select/status/status。它们额外接受 `profile`、`runtimeRoot`、`environmentFile`（建议用 JSON 文件输入）。完整绿色要求目标 PID/启动时间/模块、专用管道、Host 框架、查询协议 v1 及运行内容绑定均通过；不提供产品/运行目录时保留黄色未绑定项。结果中 `communication` 保留原通信回执，`evidence` 给出各灯、原因和 60 秒过期时间。发现窗口、创建启动入口、许可端口可达都不能点亮 Host 成功灯。目标退出/重用、模块/部署变化或证据过期必须清除旧绿色；前端复用 `ConnectionEvidence.Revalidate`。

## 进程协议与失败

管理器以 ArgumentList 选择程序，JSON 经 stdin 传入，stdout 返回一份带协议版本及 requestId 的 JSON，stderr 独立且有界。协议 v1 仅开放通信操作；不接受管理操作。每次目标调用必须带本次发现的启动身份，PID 重用在连接前拒绝。

通信 CLI 的内容指纹变化需要重新创建客户端。请求、两个输出流和等待时间均有预算；协议不匹配、非 JSON、退出码矛盾、输出超限和超时不会变成成功。超时取消后等待自有子进程终止；AVEVA 已收到的操作可能仍在执行，`mayHaveExecuted` 保留，不自动重发。

CLI 使用 UTF-8 JSON。内联、文件、stdin 三种输入互斥；批处理先校验全部外层输入，按顺序执行，首个失败后停止并保留先前结果。管理/通信 CLI 的 argv 不承载密码。

## 预览与应用

`evars.preview`、`deployment.preview`、`knowledge.import.preview`、`data.migration.preview` 返回 `planId`、`sha256` 和摘要。完整冻结计划留在 StateDirectory/ManagerPlans，环境文件原文不会输出到终端。后续 `.apply` 用同一对字段，并显式加 `--allow-write`；文件或来源变化会要求重新预览。

部署预览把 installationRoot/product 与该根唯一入口绑定；多入口时必须传 installationExecutable。可传 installation.discover 返回的 expectedInstallationId，再核对入口和有效环境文件内容。摘要带完整安装身份，计划冻结后入口或环境变化会拒绝应用；目录版本提示不冒充厂商版本证明。E3D 只选 INIT；AM/PDMS 仅在 INIT 确实不存在时选 BAT，INIT 被占用/不可读不回退。

EVARS 输入示例保存为 JSON 文件，用 `op invoke evars.preview --input-file <文件>` 执行：

```json
{"installationRoot":"C:\\Fixtures\\E3D","product":"E3D","runtimeRoot":"C:\\Fixtures\\YuzuhaRuntime","profile":"E3D2.1","framework":"net48","codePage":65001}
```

`evars.apply` 返回另一份回执引用，供 `evars.restore` 条件恢复。`deployment.preview` 输入还需 `packageRoot` 和 `mode`（`copy` 或 `reference`）；copy 按包清单复制 PMLLIB/PMLUI/runtime，reference 要求 runtimeRoot 等于 packageRoot。两者均检查 bootstrap 的根目录识别、产品 profile/framework，并修改生效的环境文件。恢复使用 `deployment.restore` 的回执引用，外部新改动会列为未恢复，不会被强行覆盖。

`setup.handoff.inspect` 只读核对安装清单及所有权。`setup.legacy.preview` 冻结所选旧v2环境记录，`setup.legacy.transfer` 经写入授权仅移交该环境的旧卸载所有权；保留原记录备份，不写EVARS，不支持格式或漂移时拒绝。写入或恢复丢回包保留 `mayHaveExecuted`；有回执时按本次引用核查恢复，无回执时保留人工备份定位，不自动重试。

安装根中的有效 INIT 优先于 BAT；不猜测多候选文件，无法读取不会当作不存在。编辑保留 BOM、代码页和换行；无 BOM 的非 ASCII 路径须指定代码页。部署不会打包活动库、个人资料或未登记的文件。实际安装另需具体预览和占用核对，合成示例不会修改本机 AVEVA。

## 用户资料与知识

`manager.status` 显示各目录的实际解析值；环境覆盖优先。`data.migration.preview` 的输入为 `{"root":"C:\\Fixtures\\NewUserRoot"}`；迁移沿用已有哈希、WAL/SHM、冲突和回退规则，保留旧源，环境覆盖存在时拒绝隐式切换。

知识双路导入、检查分层及版本检索见 [知识导入说明](manager-knowledge.md)。新库必须带 product/version/role/name；不会覆盖 personal/project/experience，也不会自动扫描未选择的产品或资料。

显式可选包、启停、版本/权限握手与工作进程调用见 [插件说明](manager-plugins.md)。插件未安装不会触发自动安装，也不会阻断基础功能。

稳定退出码：成功 0、输入错误 2、未知操作 3、授权不足 4、目标身份 5、Host 能力 6、I/O/通信 7、取消 8、内部错误 9、配置修订冲突 10。检查命令成功返回报告不等于库可启用，必须读 `canEnable` 及各项状态。

## 验证与清单

`scripts/Build-Manager.ps1` 在唯一 artifacts/manager/packages 子目录生成可审阅的 Windows x64 管理 CLI 包，要求本机 .NET 10 运行时。manager-package.json 带入口、运行时和逐文件 SHA256；发布后验证操作清单及空插件登记。基础包拒绝 RPC/旧 CLI 组合层/可选工作包依赖，以及工程库、VM 镜像和用户配置。通信程序按上文单独指定；插件用 Build-ManagerPlugins.ps1 单独生成。本脚本不安装、不切换活动资料根，也不把旧 GUI 标为新管理界面。

```powershell
./scripts/Test-Manager.ps1
./scripts/Test-Manager.ps1 -FullOffline -SevenZipTool '本机明确选定的7zr.exe绝对路径'
./scripts/Export-ManagerOperations.ps1
./scripts/Test-ManagerExamples.ps1
```

隔离测试使用真实子进程与合成数据，覆盖中文/空格路径、stdin、日志分离、版本/关联错误、退出码矛盾、取消、输出上限、超时不重发、目标身份和配置冲突。它们不构成 AM/E3D 实机验收。

清单位于 `artifacts/manager/operation-inventory.json`，包含原 CLI 全部操作/schema、归属、管理器当前可用状态和稳定退出码。兼容期旧启动入口转接管理服务，原输入输出及授权保持。

通信 Core 仅引用 RPC、通信契约、工作区公共服务和无存储依赖的 Builtins；不引用 Manager.Core、Knowledge.Core、SQLite 或旧 CommandLine。历史 NativeCliHost/NativeOperationCatalog 放在 Cli.Compatibility 组合层，旧入口仍保留 46 个操作。独立进程协议只注册通信能力。管理内核通过进程适配器通信；架构守卫同时检查编译依赖图和桌面源码不能引用 RPC 实现类型。

桌面初次只读选择绑定发现所得 PID/启动时间，并固定 Host 返回模块；随后每个进程都传完整目标。发现通过异步调用等待并使用共用 busy 状态，目标变更/失败即清除可调用目标；函数权限与源码检查继续在调度前进行。读取回执丢失保留 unknown，不重发。

示例脚本逐条解析 stdout JSON，核对 0/2/3/4 退出码，并在唯一临时目录验证环境恢复和知识导入；不会启动 GUI 或连接真实 AVEVA。
