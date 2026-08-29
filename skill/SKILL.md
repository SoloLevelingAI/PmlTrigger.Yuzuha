---
name: yuzuha-toolkit
description: "Operate the local YuzuhaToolkit bridge for AVEVA E3D: check heartbeat status, generate typed PML calls, read CE/DBREF/global object graphs, and execute an explicitly requested PML command on the AVEVA main thread. Use for E3D inspection, rename or attribute modification, macro execution, and the BOX or site spiral demonstrations. Execution has no preview safety gate; never auto-retry."
---

# YuzuhaToolkit E3D Skill / YuzuhaToolkit E3D 技能

## English

Use the `YuzuhaToolkit` MCP tools to inspect or operate AVEVA E3D. The stdio
server is a .NET 10 Native AOT executable; the .NET Framework 4.8 host is loaded
inside AVEVA and serves local named pipe `yuzuha.pml.command.v1`.

Treat the AVEVA session as live state. Inspect before changing and report the
exact command and observable result.

### Choose the tool

| Tool | Use | Side effects |
|---|---|---|
| `get_connection_status` | Heartbeat, latency, failure count, last error | none |
| `generate_pml_call` | Build a typed `!!Method(...)` string | none |
| `run_pml_command_list` | Execute a PML expression and return an array/object graph | runs PML |
| `run_pml_command` | Execute one PML command | may modify E3D |

1. Check `get_connection_status` when reachability is uncertain.
2. Use `generate_pml_call` for drafting or discussion.
3. Call either execution tool only after an explicit user request. Send each
   modifying command once and never automatically retry a timeout.
4. Before a mutation, verify the target and approved scope. Afterward, reread
   the model; transport success or heartbeat alone is not proof of completion.
5. Preserve `Success`, `Code`, `ErrorMessage`, `PmlCommand`, `RequestId`,
   `Items`, and `Unparsed` when reporting.

Use `!!YuzuhaTriggerCommand` for rename, attribute modification, one-line
commands, and macro-file execution. This is the name shipped in
`PMLLIB/Examples/YuzuhaTriggerCommand.pmlcmd`; do not use obsolete spellings.
Send `.InitArgs(...)` and
`.execute(...)` as two separate RPC calls; never join them with `,` or `;`.
For these operations, read
[the modification workflow](references/file-macro-workflow.md).

The site may provide `!!NewSpiral(...)` and `!!RotateMyspiral(...)` for the
spiral demonstration. Teach and use them when explicitly requested. If the
private functions are not loaded, report that fact; do not invent a substitute.
Preserve the exact case of verified model names. Never switch E3D modules as a
setup or recovery step; a module switch can restart the session and disconnect
the bridge.

Read detailed resources only when relevant:

- Tool schemas and response semantics: [references/mcp-tools.md](references/mcp-tools.md)
- Rename, attribute edits, and macros: [references/file-macro-workflow.md](references/file-macro-workflow.md)
- Installation and diagnostics: [references/deployment.md](references/deployment.md)

## 中文

使用 `YuzuhaToolkit` MCP 工具读取或操作 AVEVA E3D。stdio 服务是 .NET 10
Native AOT 可执行文件；.NET Framework 4.8 Host 加载在 AVEVA 内，通过本机
命名管道 `yuzuha.pml.command.v1` 提供服务。

把 AVEVA 会话视为实时状态：修改前先读取，执行后报告实际命令和可观察结果。

### 选择工具

| 工具 | 用途 | 副作用 |
|---|---|---|
| `get_connection_status` | 心跳、延迟、连续失败和最后错误 | 无 |
| `generate_pml_call` | 生成带类型参数的 `!!Method(...)` 文本 | 无 |
| `run_pml_command_list` | 执行返回数组/对象图的 PML 表达式 | 会运行 PML |
| `run_pml_command` | 执行一条 PML 命令 | 可能修改 E3D |

1. 连接不确定时先调用 `get_connection_status`。
2. 仅讨论或起草命令时使用 `generate_pml_call`。
3. 只有用户明确要求后才能调用执行型工具。修改命令只发送一次，超时后禁止自动重试。
4. 修改前确认目标和用户批准的范围，修改后重新读取模型；传输成功或心跳连通都不能
   单独证明操作完成。
5. 报告时保留 `Success`、`Code`、`ErrorMessage`、`PmlCommand`、`RequestId`、
   `Items` 和 `Unparsed`。

使用 `!!YuzuhaTriggerCommand` 执行改名、属性修改、单行命令和宏文件。
本项目由 `PMLLIB/Examples/YuzuhaTriggerCommand.pmlcmd` 提供该对象，不得使用旧拼写。
`.InitArgs(...)` 与 `.execute(...)` 必须作为两次独立 RPC 调用发送，不能用逗号或
分号合并。执行这些操作前阅读
[中文修改工作流](references/file-macro-workflow.zh-CN.md)。

站点可能提供 `!!NewSpiral(...)` 和 `!!RotateMyspiral(...)` 盘管演示函数。
用户明确要求演示时应教授并使用这些接口；私有函数未加载时如实报告，不得自行猜测替代命令。
模型名称按实际读取结果保持大小写。不得把切换 E3D 模块当作初始化或修复步骤；模块切换可能
重启会话并导致桥接断开。

仅在相关任务中读取详细资料：

- 工具参数和响应语义：[references/mcp-tools.zh-CN.md](references/mcp-tools.zh-CN.md)
- 改名、属性修改和宏：[references/file-macro-workflow.zh-CN.md](references/file-macro-workflow.zh-CN.md)
- 安装与诊断：[references/deployment.zh-CN.md](references/deployment.zh-CN.md)
