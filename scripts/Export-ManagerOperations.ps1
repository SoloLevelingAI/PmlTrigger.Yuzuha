[CmdletBinding()]
param([string] $OutputPath = 'artifacts/manager/operation-inventory.json')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepoRoot = Split-Path -Parent $PSScriptRoot
$taskNativeCli = Join-Path $taskRepoRoot 'src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.dll'
$taskManagerCli = Join-Path $taskRepoRoot 'src/YuzuhaToolkit.Manager.Cli/bin/Release/net10.0/Yuzuha.Manager.Cli.dll'
$taskPreviousState = $env:YUZUHA_STATE_DIR
try {
    $env:YUZUHA_STATE_DIR = Join-Path $taskRepoRoot 'artifacts/manager/inventory-state'
    $taskNativeOutput = & $env:DOTNET_HOST_PATH $taskNativeCli op list
    if ($LASTEXITCODE -ne 0) { throw 'Communication CLI inventory failed.' }
    $taskNative = ($taskNativeOutput -join "`n") | ConvertFrom-Json
    $taskManagerOutput = & $env:DOTNET_HOST_PATH $taskManagerCli op list
    if ($LASTEXITCODE -ne 0) { throw 'Manager CLI inventory failed.' }
    $taskManager = ($taskManagerOutput -join "`n") | ConvertFrom-Json
    $taskManagerIds = @($taskManager.data | ForEach-Object { $_.id })
    $taskAssignments = @($taskNative.data | ForEach-Object {
        $taskOwner = if ($_.id -match '^(launch\.|knowledge\.|workspace\.)') { 'manager' } else { 'communication' }
        [ordered]@{ id=$_.id; owner=$taskOwner; targetId=$_.id; compatibility='existing JSON and authorization retained';
            managerAvailable=($taskManagerIds -contains $_.id); descriptor=$_ }
    })
    if (@($taskAssignments.id | Select-Object -Unique).Count -ne $taskNative.data.Count) { throw 'Duplicate or unassigned operation.' }
    $taskResult = [ordered]@{ schemaVersion=1; generatedUtc=[DateTime]::UtcNow.ToString('o');
        nativeCliSha256=(Get-FileHash -LiteralPath $taskNativeCli -Algorithm SHA256).Hash.ToLowerInvariant();
        managerCliSha256=(Get-FileHash -LiteralPath $taskManagerCli -Algorithm SHA256).Hash.ToLowerInvariant();
        nativeOperations=$taskAssignments; managerOperations=$taskManager.data;
        exitCodes=@{success=0;invalidInput=2;unknownOperation=3;denied=4;target=5;host=6;transport=7;cancelled=8;internal=9;configurationConflict=10} }
    $taskOutputFile = [IO.Path]::GetFullPath((Join-Path $taskRepoRoot $OutputPath))
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskOutputFile) -Force | Out-Null
    $taskResult | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $taskOutputFile -Encoding utf8
    [pscustomobject]@{file=$taskOutputFile;nativeOperations=$taskAssignments.Count;managerOperations=$taskManagerIds.Count} | ConvertTo-Json -Compress
}
finally { $env:YUZUHA_STATE_DIR = $taskPreviousState }
