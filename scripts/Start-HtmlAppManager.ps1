[CmdletBinding()]
param(
    [ValidateRange(1024,65535)][int]$Port = 5288,
    [string]$DataDirectory = (Join-Path $env:LOCALAPPDATA 'YuzuhaToolkit/WebApps'),
    [string]$PmlLib = (Join-Path $env:LOCALAPPDATA 'YuzuhaToolkit/PmlTrigger.Yuzuha/PMLLIB'),
    [string]$Executable = (Join-Path $PSScriptRoot '../src/YuzuhaToolkit.WebApps/bin/Release/net10.0/YuzuhaToolkit.WebApps.exe')
)
$ErrorActionPreference='Stop'
$url="http://127.0.0.1:$Port"
try { $health=Invoke-RestMethod "$url/api/health" -TimeoutSec 2 } catch { $health=$null }
if ($health.service -eq 'YuzuhaToolkit.WebApps') { Write-Output $url; return }
$executablePath=(Get-Item -LiteralPath $Executable).FullName
$dataPath=[IO.Path]::GetFullPath($DataDirectory)
$pmlPath=(Get-Item -LiteralPath $PmlLib).FullName
if (($dataPath+$pmlPath).IndexOfAny([char[]]@('$',"`r","`n",'"')) -ge 0) { throw 'Unsupported characters in local paths.' }
New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
$arguments=@('--port',$Port.ToString(),'--data-dir',('"'+$dataPath+'"'),'--pml-root',('"'+$pmlPath+'"'))
$hostProcess=Start-Process -FilePath $executablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $dataPath 'host.stdout.log') -RedirectStandardError (Join-Path $dataPath 'host.stderr.log')
$deadline=[DateTime]::UtcNow.AddSeconds(15)
while ([DateTime]::UtcNow -lt $deadline) {
    $hostProcess.Refresh()
    if ($hostProcess.HasExited) { throw 'HTML manager exited; see host.stderr.log.' }
    try {
        $health=Invoke-RestMethod "$url/api/health" -TimeoutSec 1
        if ($health.service -eq 'YuzuhaToolkit.WebApps') { Write-Output $url; return }
    } catch {}
    Start-Sleep -Milliseconds 200
}
throw 'HTML manager did not become ready; inspect its local logs.'
