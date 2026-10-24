# Phayton：AI 客户端接入 / AI client setup

0.3.4.2 通过 `runtime/cli/PmlTwin.Phayton.Cli.exe` 接入 AI 客户端。客户端需要能够在 AVEVA 所在的 Windows 环境运行本地命令、传入 JSON 并读取 stdout 和退出码。CLI 直接调用业务服务或 AVEVA Host，独立 MCP 服务已禁用，无需配置 `mcpServers`。

## 安装与定位

使用正式 Inno 发行包 `Phayton-0.3.4.2-Windows-Setup.exe`，或包含该安装器的 `Phayton-0.3.4.2-Windows-Agent.zip`。按发行包中的 `INSTALL.md` 确认安装目录、AVEVA 环境和可选 Skill，并审阅具体计划后执行。开发/源码补充 ZIP 不提供安装或接入入口；旧 PS1 安装、更新及 MCP 注册步骤不适用。

从安装目录找到 `runtime/cli/PmlTwin.Phayton.Cli.exe`；也可读取 `HKCU\Software\YuzuhaToolkit\Installations` 中的安装记录，核对实际路径和文件。将该绝对路径提供给客户端。支持 Skill 的客户端可加载随包 `yuzuha-toolkit`，其他客户端可将本页和 [CLI 契约](reference/cli.md) 作为调用说明。不同客户端不会自动共享 Skill；若客户端缓存了旧 Skill，按该客户端的方式重新加载后再核对当前入口。

## 先验证可用契约

将下面路径替换为已经核对的安装路径：

```powershell
$cli = 'C:\实际安装目录\PmlTrigger.Yuzuha\runtime\cli\PmlTwin.Phayton.Cli.exe'
& $cli op list
& $cli op describe aveva.session.status
& $cli op invoke builtin.usage --input '{"query":"当前元素"}'
& $cli op invoke aveva.sessions.list
```

`op list` 和 `op describe` 提供实际操作 ID 与输入 Schema。返回零个 AVEVA 会话表示当前未发现目标，不等于 CLI 安装失败。知识查询同样通过此 CLI 的 `knowledge.*` 操作完成；普通检索与安装不会自动重建知识库。

从发现结果选择正确 PID 和模块，再调用 `aveva.session.status --pid <实际PID> --expected-model <实际模块>`。AM 与 E3D 的模块不同，不要把 `DESIGN` 当作所有产品的固定值。每次 CLI 调用均独立核验目标，不共享前一进程中的选择状态。Host 或 AVEVA 环境配置更新后，先完整重启对应 AVEVA，再重新发现和核验。

## JSON 输入与权限

客户端可以通过 `--input-file` 或 `--input-stdin` 传入复杂 JSON。例如 `query.json` 内容为 `{"query":"当前元素"}`：

```powershell
& $cli op invoke builtin.usage --input-file query.json
Get-Content query.json -Raw | & $cli op invoke builtin.usage --input-stdin
```

一次调用只使用一种输入来源。持久化写入、启动程序和执行 PML 分别需要 `--allow-write`、`--allow-launch`、`--allow-execution`；仅在用户已授权对应操作时传入。JSON 字段本身不能授予这些权限。

保留 JSON 回执及退出码，检查 `success`、`error` 和底层 `data`；传输成功不等于业务成功。遇到超时、连接中断或 `mayHaveExecuted=true`，先核验实际结果，不自动重试可能已执行的命令。完整操作、批处理及退出码见 [CLI 文档](reference/cli.md)。

CLI 和 AVEVA 必须位于同一台 Windows 环境；虚拟机内的 AVEVA 应由虚拟机内的 CLI 访问。排障时记录 CLI 绝对路径、操作 ID、目标 PID/模块和脱敏回执。不要重新启用旧 MCP 作为排障手段。

## English quick setup

Install the formal Inno distribution and follow its `INSTALL.md` preview/plan workflow. Locate the installed `runtime/cli/PmlTwin.Phayton.Cli.exe` and give its absolute path to a client capable of invoking local Windows commands. Load the optional Skill if supported, then inspect `op list` and `op describe` before invoking operations with JSON. There is no MCP registration step; independent MCP servers are disabled.

Discover the actual AVEVA PID and module on the same Windows machine, verify them on each target call, and preserve both JSON receipts and exit codes. Write, launch and PML execution flags are separate and require authorization. Restart AVEVA after Host/environment changes. See the [CLI contract](reference/cli.md) for input formats, permissions and failure handling.
