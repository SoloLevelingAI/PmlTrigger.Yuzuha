# AVEVA 安装定位与 EVAR 选择 / Installation discovery and EVAR selection

先确认用户是否要求修改 AVEVA 环境。仅注册本机 MCP 不需要修改 EVAR；用户明确要求不修改时，保持不动。

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
4. 修改前保留原始文件备份和编码、换行，按该文件实际语法做最小修改，并核对启动链中的后续设置是否覆盖它。`Evar.INIT` 若为 BAT 风格（批处理语法），可直接交给 `-EvarBat` 写入器处理；仅非批处理语法的 INIT 才按原格式手工最小修改。


环境或 Host 发生变化后，完全重启 AVEVA。MCP 安装、升级或客户端配置变化后，必须完全退出并重启 AI 客户端，仅新建聊天不够。

## English

Discover the selected AVEVA installation using PowerShell `Get-ItemProperty` or `reg query`, checking both registry views and validating the actual launcher/process path. Prefer the active `Evar.INIT`. Only fall back to `EVAR.BAT`/the actual `evars.bat` for PDMS/AM when that installation has no INIT and uses BAT. An inaccessible INIT is not an absent INIT. Preserve file syntax/encoding and backups; do not edit both. The `-EvarBat` writer handles BAT-style files: an `Evar.INIT` that is itself batch syntax can be passed to it directly (managed block appended at the end, with an automatic backup). 

## 没有 Codex 的客户端 / Clients without Codex

无需安装或模拟 Codex。先使用 `Install-YuzuhaAgent.ps1 -SkipMcpRegistration` 部署文件，再运行 `Register-YuzuhaMcp.ps1 -McpJsonPath <当前客户端的真实配置路径> -McpExecutable <安装后的执行程序路径> -KnowledgeExecutable <安装后的知识库程序路径>`。两个程序均为 MCP stdio 服务端。配置路径必须对应当前客户端，不猜测、不覆盖其他服务；更新同样可使用 `-SkipMcpRegistration` 后复用该注册步骤。通用客户端卸载前先从该客户端移除对应注册，避免遗留指向不存在程序的配置。

没有 EVAR.BAT/INIT 写权限时报告实际路径和权限错误，不创建替代环境文件、不改其他版本、不通过伪造 Codex 命令绕过失败。只注册 MCP 不传 AvevaProfile/EvarBat，因而不修改本机 EVAR。

No Codex CLI is required: deploy with -SkipMcpRegistration, then register both installed executable paths using -McpJsonPath for the actual client. Do not create a fake Codex command. Report inaccessible EVAR files instead of modifying another environment. No-EVAR instructions take precedence.
