<#
.SYNOPSIS
使用本仓代码启动 Manager 与 Ribbon，不查找相邻项目。
#>
[CmdletBinding()]
param([switch]$NoBrowser, [switch]$NoBuild, [string]$RibbonDataDirectory,
    [ValidateRange(0,65535)][int]$ManagerPort=0, [ValidateRange(0,65535)][int]$RibbonPort=0)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskRibbonRoot=Join-Path $taskRoot 'integrations/ribbon/source'
$taskBackend=Join-Path $taskRibbonRoot 'apps/backend/bin/RibbonStudio.Backend.exe'
if (!$NoBuild) {
    Push-Location $taskRibbonRoot
    try {
        if (!(Test-Path -LiteralPath 'node_modules/.package-lock.json')) {
            & npm ci --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) { throw 'Ribbon dependencies could not be restored.' }
        }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Ribbon build failed.' }
    } finally { Pop-Location }
}
if (!(Test-Path -LiteralPath $taskBackend)) { throw 'Run without -NoBuild to build the repository Ribbon backend.' }
if (!$RibbonDataDirectory) { $RibbonDataDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PMLTWIN/Ribbon/workspace' }
if (!$RibbonPort) {
    $taskListener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $taskListener.Start(); $RibbonPort=$taskListener.LocalEndpoint.Port; $taskListener.Stop()
}
$taskSaved=@{}
foreach ($taskKey in @('UIC_DATA_DIR','UIC_PORT','UIC_WEB_ROOT','YUZUHA_RIBBON_ENDPOINT')) { $taskSaved[$taskKey]=[Environment]::GetEnvironmentVariable($taskKey) }
$taskProcess=$null
try {
    $env:UIC_DATA_DIR=[IO.Path]::GetFullPath($RibbonDataDirectory)
    $env:UIC_PORT=[string]$RibbonPort
    $env:UIC_WEB_ROOT=Join-Path $taskRibbonRoot 'apps/web/dist'
    $env:YUZUHA_RIBBON_ENDPOINT="http://127.0.0.1:$RibbonPort/"
    $taskProcess=Start-Process -FilePath $taskBackend -ArgumentList '--serve-web' -WorkingDirectory $taskRibbonRoot -WindowStyle Hidden -PassThru
    $taskReady=$false
    for($taskTry=0;$taskTry -lt 50;$taskTry++) {
        $taskProcess.Refresh()
        if($taskProcess.HasExited) { throw 'The repository Ribbon backend exited before readiness.' }
        try { $taskHealth=Invoke-RestMethod ($env:YUZUHA_RIBBON_ENDPOINT+'api/health') -TimeoutSec 1; $taskReady=$taskHealth.contractVersion -eq 1 -and $taskHealth.implementation -eq 'csharp' -and $taskHealth.status -eq 'ok' } catch { }
        $taskProcess.Refresh();if($taskProcess.HasExited){throw 'The launched Ribbon process exited during readiness.'}
        if($taskReady){break}; Start-Sleep -Milliseconds 100
    }
    if(!$taskReady){throw 'Ribbon did not become ready; Manager has not started.'}
    Write-Host "Ribbon: $env:YUZUHA_RIBBON_ENDPOINT (data: $env:UIC_DATA_DIR)"
    Write-Host 'Manager and Ribbon share the configured local endpoint. Ctrl+C stops services started by this command.'
    & (Join-Path $PSScriptRoot 'Start-LaunchWorkbench.ps1') -NoBrowser:$NoBrowser -NoBuild:$NoBuild -Port $ManagerPort
} finally {
    if($taskProcess){$taskProcess.Refresh();if(!$taskProcess.HasExited){Stop-Process -InputObject $taskProcess};$taskProcess.Dispose()}
    foreach($taskKey in $taskSaved.Keys){[Environment]::SetEnvironmentVariable($taskKey,$taskSaved[$taskKey])}
}
