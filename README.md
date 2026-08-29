# PmlTrigger.Yuzuha

[中文](#中文) | [English](#english)

## 中文

PmlTrigger.Yuzuha 是一个本机桥接工具，让 Agent 能够通过 PML 读取和操作
AVEVA E3D。Native AOT 的 .NET 10 MCP 进程通过版本化 Named Pipe 与加载在
AVEVA 主线程中的 .NET Framework 4.8 PMLNet Host 通信。

> 当前为 v0.1 Preview。执行型工具能够直接修改活动模型，只允许可信本机用户在
> 明确请求后使用；超时后禁止自动重试。

### 主要功能

- 生成带类型参数的 PML 全局方法调用文本，不接触 E3D。
- 执行用户明确要求的单条 PML 命令。
- 将 CE、DBREF 或全局 PML 对象图读取为结构化 JSON。
- 通过 2 秒心跳和无副作用的 `get_connection_status` 查看连接状态。
- 通过 `!!YuzuhaTriggerCommand` 执行改名、属性修改和宏文件。
- 向 Agent 提供 BOX 与站点盘管演示接口说明；示例不会在 Addin 启动时自动执行。

### 架构与目录

```text
Agent / MCP 客户端
        │ stdio
        ▼
YuzuhaToolkit.Mcp.exe       .NET 10 Native AOT
        │ Named Pipe: yuzuha.pml.command.v1
        ▼
YuzuhaToolkit.PmlHost.Net48.dll
        │ AVEVA 主线程 / PMLNet
        ▼
AVEVA E3D
```

```text
PMLLIB/   Addin、启动、遍历、命令与演示定义
PMLUI/    CAT、DES、DRA、ISO 的 Addin 注册
src/      MCP 与 Net48 Host 源码
skill/    Agent Skill 与按需加载的参考资料
docs/     API、构建、发布和 Agent 文档
```

### 构建

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj --configfile src\NuGet.config
dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj -c Release --no-restore
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot --no-restore
```

Net48 Host 必须使用用户已安装且获得许可的 AVEVA SDK 构建。AVEVA 专有程序集
不会进入仓库或 Release。

### 安装与使用

推荐使用 Release 中的复制安装脚本：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-Yuzuha.ps1 -RegisterCodex -InstallCodexSkill
```

安装后重启 E3D，确认 `!!YuzuhaRpcHost.GetRpcServerStatus()` 返回 `RUNNING`，
然后调用 `get_connection_status`。详细说明见
[中文发布文档](docs/publishing.zh-CN.md)、[中文 PML API](docs/pml-api.zh-CN.md)
和 [Agent 操作说明](docs/Agent.PmlTrigger.zh-CN.md)。

## English

PmlTrigger.Yuzuha is a local bridge that lets agents inspect and operate AVEVA
E3D through PML. A .NET 10 Native AOT MCP process communicates over a versioned
named pipe with a .NET Framework 4.8 PMLNet host attached to the AVEVA main
thread.

> This is a v0.1 preview. Execution tools can modify the active model. Use them
> only for explicit requests from trusted local users, and never automatically
> retry a timed-out execution.

### Features

- Build typed PML global-method calls without touching E3D.
- Execute one explicitly requested PML command.
- Read CE, DBREF, or global PML object graphs as structured JSON.
- Observe reachability through a 2-second heartbeat and the side-effect-free
  `get_connection_status` tool.
- Perform rename, attribute-edit, and macro-file workflows through
  `!!YuzuhaTriggerCommand`.
- Teach agents the BOX and site-provided spiral demonstrations without running
  examples during Addin startup.

### Architecture and layout

```text
Agent / MCP client
        │ stdio
        ▼
YuzuhaToolkit.Mcp.exe       .NET 10 Native AOT
        │ Named Pipe: yuzuha.pml.command.v1
        ▼
YuzuhaToolkit.PmlHost.Net48.dll
        │ AVEVA main thread / PMLNet
        ▼
AVEVA E3D
```

```text
PMLLIB/   Addin, bootstrap, traversal, command, and demonstration definitions
PMLUI/    Addin registration for CAT, DES, DRA, and ISO
src/      MCP and Net48 host source
skill/    Agent skill and progressively loaded references
docs/     API, build, publishing, and agent documentation
```

### Build

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj --configfile src\NuGet.config
dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj -c Release --no-restore
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot --no-restore
```

Build the Net48 host against the user's licensed local AVEVA SDK. Proprietary
AVEVA assemblies are never committed or included in Release assets.

### Install and use

Use the copy-mode installer supplied with the Release:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-Yuzuha.ps1 -RegisterCodex -InstallCodexSkill
```

Restart E3D, verify that `!!YuzuhaRpcHost.GetRpcServerStatus()` returns
`RUNNING`, then call `get_connection_status`. See the
[English publishing guide](docs/publishing.en.md),
[English PML API](docs/pml-api.en.md), and
[agent guide](docs/Agent.PmlTrigger.en.md).

## License / 许可证

Project source is licensed under Apache-2.0. Bundled dependencies retain their
own licenses; see [THIRD-PARTY.md](THIRD-PARTY.md).

项目源码采用 Apache-2.0 许可证；随附依赖保留各自许可证，详见
[THIRD-PARTY.md](THIRD-PARTY.md)。
