# 部署——Net10 Native AOT MCP 与 Net48 AVEVA Host

中文 | [English](deployment.md)

## Release 结构

```text
PmlTrigger.Yuzuha/
├─ PMLLIB/
├─ PMLUI/
├─ runtime/
│  ├─ net48/
│  └─ win-x64-nativeaot/
├─ docs/
└─ skill/
```

目标机需要 Windows x64、PowerShell 5.1+，以及已获得许可的 AVEVA E3D
2.1/.NET Framework 4.8/PMLNet 环境。Native AOT MCP 不要求安装 .NET 10。
AVEVA 专有程序集不随项目分发。

## 复制安装

在 Release 根目录运行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-Yuzuha.ps1 -RegisterCodex -InstallCodexSkill
```

当前脚本的 `-InstallCodexSkill` 只安装 Codex Skill，不会自动安装 DeepSeek
Harness Skill。将 Release 交给 Agent 安装时，先让它阅读 Release 根目录的
`INSTALL-SKILL.zh-CN.md`。DeepSeek 用户级目标结构应为：

```text
%USERPROFILE%\.dsh\skills\yuzuha-toolkit\
├─ SKILL.md
└─ references\
```

必须复制整个 `skill` 目录的内容，不能只复制 `SKILL.md`。安装后重新启动 Harness
或新建会话，让它重建 Skill 目录。

默认安装目录为 `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha`。安装器在修改
`evars.init` 前创建带时间戳的备份，然后注册安装目录中的 `PMLUI` 和 `PMLLIB`。

非标准环境可指定：

```powershell
.\Install-Yuzuha.ps1 `
  -EvarsInitPath 'D:\AVEVA\Everything3D2.10\evars.init' `
  -RegisterCodex -InstallCodexSkill
```

## AVEVA Host 启动

`YuzuhaAddin` 加载 `!!YuzuhaRpcCommand`。命令构造过程解析并导入
`runtime/net48/YuzuhaToolkit.PmlHost.Net48.dll`，在 AVEVA 主线程构造 Host，
然后启动管道 `yuzuha.pml.command.v1`。重复构造由 Host 端幂等处理。

在 AVEVA 中验证：

```pml
!!YuzuhaRpcHost.GetRpcServerStatus()
```

期望结果为 `RUNNING`。随后在 Agent 中调用 `get_connection_status`；只有
`Connected` 表示近期心跳成功。

## 手工注册 MCP

如果安装时没有使用 `-RegisterCodex`：

```powershell
codex mcp add YuzuhaToolkit -- `
  "$env:LOCALAPPDATA\YuzuhaToolkit\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe"
```

MCP EXE 和 AVEVA 应运行在同一 Windows 用户会话，并使用相同的
`YUZUHA_PML_PIPE`；未设置时默认使用 `yuzuha.pml.command.v1`。

## 排错顺序

1. 检查 `evars.init` 是否包含正确的 PMLUI/PMLLIB 路径。
2. 检查 `!!YuzuhaRuntimePath` 和 `!!YuzuhaAutoSetup`。
3. 检查 `GetRpcServerStatus()` 是否为 `RUNNING`。
4. 调用 `get_connection_status`，查看最后错误和连续失败次数。
5. 只在连接成功后执行只读查询；不要用创建 BOX 或盘管来测试连接。
6. 确认当前加载的是 `!!YuzuhaTriggerCommand`，其文件为
   `PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd`。如果不可用，检查 `evars.init`
   中的 PMLLIB 顺序并完全重启 E3D。
