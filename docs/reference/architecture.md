# 架构与守卫

[打开交互式 LikeC4 架构图](../../artifacts/architecture/index.html)。图中包含项目依赖、运行时与开发宿主三个视图。

`docs/architecture/dependencies.json` 定义生产项目的引用白名单，包含 `src/` 及 `installer/SetupBridge`。`scripts/architecture.mjs` 生成 `projects.c4`；ArchUnitNET / xUnit读取同一白名单，检查实际csproj项目集合、精确引用和循环，并检查约定的.NET 10程序集依赖方向。新增源项目必须进入白名单，不能被测试静默遗漏。

当前分支有两个 CLI 边界：`YuzuhaToolkit.Manager.Cli` 组合 `Manager.Core`，提供部署、EVARS、启动、复制、用户数据、知识与可选插件操作；`PmlTwin.Phayton.Cli` 的 `--manager-protocol` 入口只组合通信与诊断操作，供管理器启动独立子进程。通信 Core 引用 RPC、Workspace 公共服务、Communication.Contracts 和无存储依赖的 Builtins，不引用 Manager.Core、Knowledge.Core、SQLite 或旧 CommandLine。

`Cli.Compatibility` 保留历史 `NativeCliHost` / `NativeOperationCatalog`，为原生 `op` 入口组合原有 46 个操作；其中管理操作转接同一 Manager 服务。`Mcp.Core` 的程序集名保留兼容，独立 MCP 服务仍禁用。现有 WPF 的会话、对象、函数调用与开发流程已走通信子进程；Desktop 仍引用 Core 的 DTO、函数登记和信任类型，但不构造 RPC 实现。新管理页面仍待接线与视觉验收，不能从依赖声明推断界面已交付。操作说明见[管理器 CLI](manager-cli.md)。

运行时主链为 GUI/管理 CLI → Manager 服务 → 独立通信 CLI → PID 专用 RPC → NET35/NET48 Host → AVEVA 主线程；本地知识由 Manager/知识服务访问 SQLite，可选许可与 VM 包由独立 worker 执行。Host 使用仓库 RPC DLL 和对应 AVEVA SDK 编译，目标调用逐次核验 PID、启动时间、模块与协议。

`runtime.c4` 是人工维护的运行时视图，不是从代码推导的调用图；其中仍有拆分前的直接调用关系，须同步后才能作为本轮交付图，当前边界以上述说明为准。编译视图仅显示 `build` 关系，运行时视图排除这种关系。精确 ProjectReference 以白名单和生成图为准，进程边界另由架构测试检查，不能把编译引用当作实际 RPC 调用。

守卫还检查开发处理器不依赖会话桥、信任存储和知识仓库；知识搜索和切分算法不依赖仓库；生产后端不引用 Scalar、ASP.NET、xUnit 或 ArchUnitNET。负例使用真实编译的方法体证明 ArchUnitNET 能检测禁止的文件访问，不执行该方法。

```powershell
npm run architecture:generate  # 修改白名单后生成并审阅
npm run architecture:check
npm run architecture:validate
npm run architecture:build
```

更改白名单意味着更改架构，应与代码一起审阅。NET35 / NET48 与安装器执行项目引用和源码注释检查，未纳入 .NET 10 的 IL 检查；真实 Host 构建和 AVEVA 联测仍使用原有流程。
