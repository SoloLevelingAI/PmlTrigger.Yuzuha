# AVEVA 安装定位与 EVAR 选择 / Installation discovery and EVAR selection

先确认用户是否要求修改 AVEVA 环境。安装发现、CLI 元数据查询与客户端工具配置不需要修改 EVAR；只有用户授权部署运行载荷或修改环境时才应用对应计划。用户明确要求不修改时，保持不动。当前独立 Yuzuha MCP 服务已禁用，现行入口见[原生 CLI](reference/cli.md)与[管理器 CLI](reference/manager-cli.md)。

## 先查询注册表，确认安装版本与路径

可以使用 PowerShell 的 `Get-ItemProperty` 或命令行的 `reg query`。需要同时考虑 32 位和 64 位注册表视图；不要根据默认目录名猜测安装版本。下面 AVEVA 键是候选查询位置，缺失不表示软件一定未安装。

```powershell
$avevaRegistryRoots = @(
  'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\AVEVA',
  'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\AVEVA',
  'Registry::HKEY_CURRENT_USER\SOFTWARE\AVEVA'
)
foreach ($avevaRegistryRoot in $avevaRegistryRoots) {
  if (Test-Path -LiteralPath $avevaRegistryRoot) {
    Get-ItemProperty -LiteralPath $avevaRegistryRoot
    Get-ChildItem -LiteralPath $avevaRegistryRoot -Recurse |
      ForEach-Object { Get-ItemProperty -LiteralPath $_.PSPath }
  }
}
```

```bat
reg query "HKLM\SOFTWARE\AVEVA" /s /reg:64
reg query "HKLM\SOFTWARE\AVEVA" /s /reg:32
reg query "HKCU\SOFTWARE\AVEVA" /s
```

若候选键没有安装信息，可检查 Windows 卸载项中的 DisplayName / InstallLocation，或从运行进程路径及实际启动快捷方式核对。注册表路径只是证据：多个版本并存时，应匹配用户选择的产品、版本、进程和启动入口，不修改其他版本。访问被拒绝时应报告，不推断为未安装。

## 修改顺序：INIT 优先，BAT 仅作兼容回退

1. 在已确认的目标安装和实际启动配置中查找 `Evar.INIT`，确认启动链确实读取它；找到适用的 INIT 时优先修改该文件。
2. 对 PDMS 或 AM，只有确认该目标没有 `Evar.INIT`、实际只使用 BAT 启动环境时，才修改 `EVAR.BAT`（部分安装的实际文件名可能是 `evars.bat`，以文件和启动链为准），E3D始终修改`Evar.INIT`。
3. INIT 存在但无法访问或不清楚是否生效，不等于“没有 INIT”；不要因此自动回退去修改 BAT。不要同时修改 INIT 和 BAT。
4. 修改前保留原始文件备份和编码、换行，按该文件实际语法做最小修改，并核对启动链中的后续设置是否覆盖它。当前管理器用 `evars.preview` / `deployment.preview` 冻结所选入口、有效环境文件与原内容；`evars.apply` / `deployment.apply` 使用计划引用并要求 `--allow-write`。不支持的语法、不可读文件或不唯一入口应拒绝应用，不用另一环境文件绕过。输入、恢复与代码页规则见[管理器 CLI](reference/manager-cli.md#预览与应用)。


环境或 Host 发生变化后，完全重启相应 AVEVA，再重新发现目标并核验 Host；文件部署成功不证明已运行的进程加载了新文件。CLI 更新后重新创建通信客户端并核对能力/内容指纹。若 AI 客户端缓存了工具路径或配置，按该客户端要求重新加载，不能只凭新建聊天推定配置生效。

## English

Discover the selected AVEVA installation using PowerShell `Get-ItemProperty` or `reg query`, checking both registry views and validating the actual launcher/process path. Prefer the active `Evar.INIT`. Only fall back to `EVAR.BAT`/the actual `evars.bat` for PDMS/AM when that installation has no INIT and uses BAT. An inaccessible INIT is not an absent INIT. Preserve syntax, encoding and backups; do not edit both. Use the manager CLI's preview/apply/restore operations with a frozen plan. Unsupported syntax, unreadable files and ambiguous entry points must be rejected rather than bypassed. Restart the selected AVEVA process after changing its environment or Host, then discover and verify it again.

## CLI 接入与客户端选择 / CLI integration

无需安装或模拟 Codex。现行发行版通过 `runtime/cli/PmlTwin.Phayton.Cli.exe op list/describe/invoke` 提供操作；默认/旧 MCP 服务入口返回 `MCP_DISABLED`，不能将旧执行与知识程序注册为 stdio 服务。安装参数以[安装说明](../installer/INSTALL.md)为准。客户端需要支持本机命令调用，或另行提供明确的适配层；不能假称所有 MCP-only 客户端可直接接入。

当前分支新增的 Manager CLI 验证包单独提供部署与管理操作，不包含完整 Host/PML 安装载荷，尚未替换本机安装。用 `installation.discover` 选择明确产品根，以 `evars.preview` 或 `deployment.preview` 查看差异；未经授权不执行 apply。没有 EVAR.BAT/INIT 写权限时报告实际路径和权限错误，不创建替代环境文件、不改其他版本、不通过伪造 Codex 命令绕过失败。

No Codex CLI is required. Current releases expose native CLI operations; legacy standalone MCP service entry points return `MCP_DISABLED`. Use a client capable of local command execution or an explicitly supplied adapter. The new manager CLI verification package is separate from a complete Host/PML installation. Preview the selected product's environment and deployment changes before an authorized apply; report inaccessible EVAR files rather than modifying another environment.

### 历史脚本说明

[`Install-YuzuhaAgent.ps1`](../scripts/Install-YuzuhaAgent.ps1) 与 [`Register-YuzuhaMcp.ps1`](../scripts/Register-YuzuhaMcp.ps1) 保留旧版安装/注册兼容及取证用途；其 `-SkipMcpRegistration`、`-McpJsonPath`、`-EvarBat` 等旧用法不是当前 CLI 接入指南。历史文档中的“双 MCP stdio 服务”描述不适用于现行发行版，也不能据旧文件名推断本机已升级。
