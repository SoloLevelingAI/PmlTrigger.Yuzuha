<# .SYNOPSIS
在隔离临时工作区验证 Manager CLI → 本仓 Ribbon HTTP → 编辑历史/确定导出；不部署 UIC。
#>
[CmdletBinding()]
param([string]$ReportDirectory='artifacts/portable-ribbon', [string]$Configuration='Debug')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskReport=[IO.Path]::GetFullPath($ReportDirectory,$taskRoot)
[IO.Directory]::CreateDirectory($taskReport)|Out-Null
[ordered]@{schemaVersion=1;success=$false;state='started';observedUtc=[DateTimeOffset]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskReport 'result.json') -Encoding utf8
$taskRibbonRoot=Join-Path $taskRoot 'integrations/ribbon/source'
$taskBackend=Join-Path $taskRibbonRoot 'apps/backend/bin/RibbonStudio.Backend.exe'
$taskCli=Join-Path $taskRoot "src/YuzuhaToolkit.Manager.Cli/bin/$Configuration/net10.0/Yuzuha.Manager.Cli.dll"
foreach($taskFile in @($taskBackend,$taskCli)){if(!(Test-Path -LiteralPath $taskFile)){throw "Build first: $taskFile"}}
$taskSocket=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$taskSocket.Start();$taskPort=$taskSocket.LocalEndpoint.Port;$taskSocket.Stop()
$taskOld=@{}
foreach($taskKey in @('UIC_DATA_DIR','UIC_PORT','YUZUHA_RIBBON_ENDPOINT','YUZUHA_STATE_DIR')){$taskOld[$taskKey]=[Environment]::GetEnvironmentVariable($taskKey)}
$taskRun=[guid]::NewGuid().ToString('N');$taskProcess=$null;$taskChecks=[Collections.Generic.List[string]]::new()
function Invoke-RibbonCheck([string]$Id,[object]$InputObject,[switch]$Write){
    $taskInput=Join-Path $taskReport ($Id+'.input.json')
    ConvertTo-Json -InputObject $InputObject -Depth 60 | Set-Content -LiteralPath $taskInput -Encoding utf8
    $taskArgs=@($taskCli,'op','invoke',$Id,'--input-file',$taskInput)
    if($Write){$taskArgs+='--allow-write'}
    $taskOutput=& $env:DOTNET_HOST_PATH @taskArgs
    $taskCode=$LASTEXITCODE
    $taskOutput|Set-Content -LiteralPath (Join-Path $taskReport ($Id+'.json')) -Encoding utf8
    $taskReply=($taskOutput -join "`n")|ConvertFrom-Json
    if($taskCode -ne 0 -or !$taskReply.success -or $taskReply.data.httpStatus -ne 200){throw "$Id failed; see report"}
    $taskChecks.Add($Id)
    return $taskReply.data
}
try {
    $env:UIC_DATA_DIR=Join-Path $taskReport "workspace-$taskRun";$env:UIC_PORT=[string]$taskPort
    $env:YUZUHA_RIBBON_ENDPOINT="http://127.0.0.1:$taskPort/";$env:YUZUHA_STATE_DIR=Join-Path $taskReport "state-$taskRun"
    $taskProcess=Start-Process -FilePath $taskBackend -WorkingDirectory $taskRibbonRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskReport 'backend.stdout.log') -RedirectStandardError (Join-Path $taskReport 'backend.stderr.log')
    $taskReady=$false
    for($taskTry=0;$taskTry -lt 50;$taskTry++){
        $taskProcess.Refresh();if($taskProcess.HasExited){throw 'Ribbon exited before readiness.'}
        try{$taskHealth=Invoke-RestMethod ($env:YUZUHA_RIBBON_ENDPOINT+'api/health') -TimeoutSec 1;$taskReady=$taskHealth.contractVersion -eq 1 -and $taskHealth.implementation -eq 'csharp' -and $taskHealth.status -eq 'ok'}catch{}
        $taskProcess.Refresh();if($taskProcess.HasExited){throw 'Launched Ribbon process exited during readiness.'}
        if($taskReady){break};Start-Sleep -Milliseconds 100
    }
    if(!$taskReady){throw 'Ribbon readiness timeout.'}
    $taskExample=Get-Content (Join-Path $taskRibbonRoot 'apps/backend/Assets/example.json') -Raw|ConvertFrom-Json
    $taskNormalized=(Invoke-RibbonCheck 'ribbon.projects.normalize' $taskExample).response
    $taskFirst=(Invoke-RibbonCheck 'ribbon.uic.export' $taskNormalized).bytesBase64
    $taskSecond=(Invoke-RibbonCheck 'ribbon.uic.export' $taskNormalized).bytesBase64
    if(!$taskFirst -or $taskFirst -cne $taskSecond){throw 'Repeated export changed bytes.'}
    $taskSaved=(Invoke-RibbonCheck 'ribbon.workspace.save' @{project=$taskNormalized;kind='save';note='isolated portable validation'} -Write).response
    $taskDocument=$taskSaved.document.id
    if(!$taskDocument){throw 'Save response lacked document identity.'}
    $taskOpened=(Invoke-RibbonCheck 'ribbon.workspace.open' @{documentId=$taskDocument}).response
    if($taskOpened.documentId -ne $taskDocument){throw 'Open returned another document.'}
    $taskHistory=(Invoke-RibbonCheck 'ribbon.workspace.versions' @{documentId=$taskDocument}).response
    if(@($taskHistory.versions).Count -ne 1){throw 'Unexpected edit history.'}
    [ordered]@{schemaVersion=1;success=$true;observedUtc=[DateTimeOffset]::UtcNow.ToString('o');checks=$taskChecks;deterministicExport=$true;backendSource='integrations/ribbon/source';installedUicWrites=0;macroExecutions=0;neighborRepositoriesUsed=0}|
        ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $taskReport 'result.json') -Encoding utf8
} catch {
    [ordered]@{schemaVersion=1;success=$false;observedUtc=[DateTimeOffset]::UtcNow.ToString('o');checks=$taskChecks;error=$_.Exception.Message}|
        ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $taskReport 'result.json') -Encoding utf8
    throw
} finally {
    if($taskProcess){$taskProcess.Refresh();if(!$taskProcess.HasExited){Stop-Process -InputObject $taskProcess};$taskProcess.Dispose()}
    foreach($taskKey in $taskOld.Keys){[Environment]::SetEnvironmentVariable($taskKey,$taskOld[$taskKey])}
}
