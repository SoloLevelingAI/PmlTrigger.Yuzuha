# 部署 — PID 绑定的 MCP 与 AVEVA 宿主
> 中文版（供作者审阅）。英文版 / English: [deployment.md](deployment.md)

发布压缩包采用以下目录布局：

```text
PmlTrigger.Yuzuha/
├─ PMLLIB/
├─ PMLUI/
├─ runtime/
│  ├─ profiles/
│  │  ├─ AM/net35/
│  │  ├─ PDMS/net35/
│  │  ├─ E3D2.1/net48/
│  │  ├─ E3D3.1.0/net48/
│  │  └─ E3D3.1.6/net48/
│  └─ net10/            YuzuhaToolkit.Mcp.exe
│                       YuzuhaToolkit.Knowledge.exe (+ e_sqlite3.dll)
└─ skill/
```

AVEVA 专有程序集不会被重新分发，本地知识库（`knowledge\*.sqlite3`）在运行时构建，绝不打包。

受保护的 Agent 安装、更新与卸载，请使用 [lifecycle.md](lifecycle.md) 中描述的生命周期脚本。这些脚本会安装到稳定的本地路径，管理 Skill，校验 MCP 归属，并拒绝覆盖或删除未打标记的目录。

## 启动 AVEVA 宿主

在启动 AVEVA 之前，将 `PMLLIB` 和 `PMLUI` 加入对应的 AVEVA 环境路径，并设置匹配的 Profile：

```bat
set Yuzuha=E3D2.1
```

该自定义 EVAR 变量名严格为 `Yuzuha`，不要添加下划线。

引导程序使用以下方式读取当前模块：

```pml
!!YuzuhaModel = !!fmsys.FMINFO()[0].SPLIT()[3]
```

它会把这个值（例如 `Design`）传入 PMLNet 宿主。默认管道绑定到实际的 AVEVA 进程 ID：

```text
yuzuha.pml.command.v1.pid-<AVEVA-PID>
```

可以通过显式设置 `YUZUHA_PML_PIPE` 覆盖该名称，但 MCP 在每次执行前仍会校验宿主上报的 PID、进程启动时间和模块。

在 AVEVA 中验证：

```pml
!!YuzuhaRpcHost.GetRpcServerStatus()
```

预期结果为 `RUNNING`。

## 注册一个通用的 Codex MCP

Net10 MCP 是一个精简的、自包含的 Windows x64 单文件可执行程序。它启动时处于未连接状态，不使用 PID/模块环境变量。只需注册一次：

```powershell
.\scripts\Register-YuzuhaMcp.ps1 `
  -McpExecutable '.\runtime\net10\YuzuhaToolkit.Mcp.exe'
```

在安装或更新 Skill 时重复运行该注册脚本是安全的。它会先检查 `codex mcp list --json`：

- 已存在相同的、处于启用状态的 stdio 可执行文件且无参数：直接复用，不做写入。
- 无条目：添加它。
- 同名但命令、参数、传输方式或禁用状态不同，或疑似存在以其他名称注册的 Yuzuha 条目：停止并报告发现的配置。它绝不会自动添加重复项、移除或覆盖任何 MCP。

如果冲突是有意为之，请先检查它，并使用 `codex mcp remove YuzuhaToolkit` 显式移除；然后重新运行脚本。当安装包附带知识库服务器时，同一脚本会按相同的冲突规则注册 `YuzuhaToolkitKnowledge`（可执行文件 `runtime\net10\YuzuhaToolkit.Knowledge.exe`）；其工具与版权规则见 [knowledge-base.md](knowledge-base.md)。

对于旧版 AM 或 PDMS，同一脚本还可以备份并更新 `evar.bat` 或 `evars.bat`。由于 AVEVA 安装布局各不相同，需要显式提供该文件：

```powershell
.\scripts\Register-YuzuhaMcp.ps1 `
  -McpExecutable '.\runtime\net10\YuzuhaToolkit.Mcp.exe' `
  -AvevaProfile PDMS `
  -EvarBat 'D:\AVEVA\Plant\PDMS12.1.SP4\evars.bat'
```

AM 请使用 `-AvevaProfile AM`。托管代码块是幂等的：它会设置 `Yuzuha`，将本安装包的 `PMLLIB` 和 `PMLUI` 前置插入，并在修改文件前创建带时间戳的备份。若要配置 EVAR 但不改动 Codex MCP 注册，请加上 `-SkipMcpRegistration` 并省略 `-McpExecutable`。之后请完全重启 AM 或 PDMS。

运行时流程：

1. 调用 `list_aveva_sessions`。它会在不打开 RPC 连接的情况下读取可见的 AVEVA 顶层窗口标题和进程元数据。
2. 从 `WindowTitle` 识别目标 `Product` 和 `Project`。如果零个或多个会话都可能符合，停下来等待明确选择；绝不猜测。
3. 使用发现阶段返回的某一个确切 PID 调用 `select_aveva_session`。它只连接 `yuzuha.pml.command.v1.pid-<PID>`，并校验 PID、进程启动时间、管道以及可选的预期模块。
4. 调用 `get_connection_status`；仅当 `TargetVerified=true` 时才继续。

旧的共享管道 `yuzuha.pml.command.v1` 绝不会作为回退使用。执行类工具可能修改当前活动的模型；仅针对明确的请求调用它们，并且绝不自动重试超时。

窗口发现要求 MCP 与 AVEVA 运行在同一个交互式 Windows 会话中。如果 AVEVA 以提升权限运行，请让 MCP 以兼容的完整性级别运行，以便它能够读取窗口并打开其本地管道。
