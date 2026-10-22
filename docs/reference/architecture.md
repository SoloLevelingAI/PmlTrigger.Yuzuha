# 架构与守卫

[打开交互式 LikeC4 架构图](../../artifacts/architecture/index.html)。图中包含项目依赖、运行时与开发宿主三个视图。

`docs/architecture/dependencies.json` 定义十个生产项目的引用白名单，包含 `src/` 及 `installer/SetupBridge`。`scripts/architecture.mjs` 生成 `projects.c4`；ArchUnitNET / xUnit 读取同一白名单，检查实际 csproj 项目集合、精确引用和循环，并对六个 .NET 10 编译程序集检查依赖方向。新增源项目必须进入白名单，不能被测试静默遗漏。

允许的编译引用为：执行 MCP → RPC、CommandLine、Workspace；知识 MCP → CommandLine、Workspace；CommandLine → Workspace；Desktop → Workspace；DevHost → 执行 MCP、知识 MCP。CommandLine 仅负责参数和协议适配，通过同一程序的 MCP 子进程复用工具。两个旧 Framework Host 通过随仓库提供的 RPC DLL 与本机 AVEVA SDK 编译，与执行 MCP 之间通过管道通信。

`runtime.c4` 是人工维护的通信视图：MCP 客户端 → 两个 stdio 服务，执行 MCP → RPC → NET35 / NET48 Host → AVEVA 主线程；知识服务 → 本地 SQLite。它不是从代码推导的调用图。编译视图仅显示 `build` 关系，运行时视图排除这种关系。

守卫还检查开发处理器不依赖会话桥、信任存储和知识仓库；知识搜索和切分算法不依赖仓库；生产后端不引用 Scalar、ASP.NET、xUnit 或 ArchUnitNET。负例使用真实编译的方法体证明 ArchUnitNET 能检测禁止的文件访问，不执行该方法。

```powershell
npm run architecture:generate  # 修改白名单后生成并审阅
npm run architecture:check
npm run architecture:validate
npm run architecture:build
```

更改白名单意味着更改架构，应与代码一起审阅。NET35 / NET48 与安装器执行项目引用和源码注释检查，未纳入 .NET 10 的 IL 检查；真实 Host 构建和 AVEVA 联测仍使用原有流程。
