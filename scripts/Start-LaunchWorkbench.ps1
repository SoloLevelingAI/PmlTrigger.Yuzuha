<#
.SYNOPSIS
使用仓库固定 SDK 打开本机启动工作台。
.DESCRIPTION
默认执行增量 Debug 构建并打开浏览器；不发布、不启动 AVEVA、不修改厂商环境。
关闭服务请在当前 PowerShell 窗口按 Ctrl+C。
.EXAMPLE
.\scripts\Start-LaunchWorkbench.ps1
.EXAMPLE
.\scripts\Start-LaunchWorkbench.ps1 -NoBrowser -Port 51820
#>
[CmdletBinding()]
param(
    [switch] $NoBrowser,
    [ValidateRange(0, 65535)] [int] $Port = 0,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskWorkbenchRepo = Split-Path -Parent $PSScriptRoot
$taskWorkbenchProject = Join-Path $taskWorkbenchRepo 'src/YuzuhaToolkit.Manager.Cli/YuzuhaToolkit.Manager.Cli.csproj'
$taskWorkbenchCli = Join-Path $taskWorkbenchRepo "src/YuzuhaToolkit.Manager.Cli/bin/$Configuration/net10.0/Yuzuha.Manager.Cli.dll"
Push-Location -LiteralPath $taskWorkbenchRepo
try {
    if (-not $NoBuild) {
        & $env:DOTNET_HOST_PATH build $taskWorkbenchProject --configuration $Configuration --nologo --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw 'Manager CLI 构建失败；工作台尚未启动。' }
    }
    if (-not (Test-Path -LiteralPath $taskWorkbenchCli)) { throw '缺少 Manager CLI；请移除 -NoBuild 重新运行。' }
    $taskWorkbenchArgs = @($taskWorkbenchCli, 'workbench', '--port', "$Port")
    if ($NoBrowser) { $taskWorkbenchArgs += '--no-browser' }
    Write-Host '工作台仅监听本机。按 Ctrl+C 关闭服务；产品启动仍需在页面明确确认。'
    & $env:DOTNET_HOST_PATH @taskWorkbenchArgs
    if ($LASTEXITCODE -ne 0) { throw "Manager 工作台退出，代码 $LASTEXITCODE。" }
}
finally {
    Pop-Location
}
