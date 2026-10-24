# 架构与守卫

[打开交互式 LikeC4 架构图](../../artifacts/architecture/index.html)。图中包含项目依赖、运行时与开发宿主三个视图。

`docs/architecture/dependencies.json` 定义生产项目的引用白名单，包含 `src/` 及 `installer/SetupBridge`。`scripts/architecture.mjs` 生成 `projects.c4`；ArchUnitNET / xUnit读取同一白名单，检查实际csproj项目集合、精确引用和循环，并检查约定的.NET 10程序集依赖方向。新增源项目必须进入白名单，不能被测试静默遗漏。

当前主入口 `PmlTwin.Phayton.Cli` 引用共享操作内核和执行适配核心；后者直接复用RPC、Workspace与Knowledge.Core。Desktop也直接复用这些服务。程序集名 `Mcp.Core` 保留兼容，不表示启动MCP服务；`YuzuhaToolkit.CommandLine` 中的旧适配实现不是当前CLI主路线。精确项目引用以白名单和生成图为准。NET35/NET48 Host通过仓库RPC DLL与本机AVEVA SDK编译，经Named Pipe接收已核验请求。

`runtime.c4` 是人工维护的通信视图：AI/终端 → 原生CLI → 共享操作与执行适配器 → RPC → NET35/NET48 Host → AVEVA主线程；知识适配器 → 本地SQLite。没有stdio服务或MCP子进程。它不是从代码推导的调用图；编译视图仅显示 `build` 关系，运行时视图排除这种关系。

守卫还检查开发处理器不依赖会话桥、信任存储和知识仓库；知识搜索和切分算法不依赖仓库；生产后端不引用 Scalar、ASP.NET、xUnit 或 ArchUnitNET。负例使用真实编译的方法体证明 ArchUnitNET 能检测禁止的文件访问，不执行该方法。

```powershell
npm run architecture:generate  # 修改白名单后生成并审阅
npm run architecture:check
npm run architecture:validate
npm run architecture:build
```

更改白名单意味着更改架构，应与代码一起审阅。NET35 / NET48 与安装器执行项目引用和源码注释检查，未纳入 .NET 10 的 IL 检查；真实 Host 构建和 AVEVA 联测仍使用原有流程。
