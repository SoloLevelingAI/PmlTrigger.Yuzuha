[CmdletBinding()]
param(
    [string] $CliPath = 'src/YuzuhaToolkit.Manager.Cli/bin/Debug/net10.0/Yuzuha.Manager.Cli.dll',
    [string] $OutputPath = 'openspec/changes/integrate-pmltwin-product-platform/schemas/launch-workflow-v1.json'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepoRoot = Split-Path -Parent $PSScriptRoot
$taskCli = [IO.Path]::GetFullPath((Join-Path $taskRepoRoot $CliPath))
if (!(Test-Path -LiteralPath $taskCli)) { throw 'Build the Manager CLI before exporting its live descriptors.' }
$taskOutput = & $env:DOTNET_HOST_PATH $taskCli op list
if ($LASTEXITCODE -ne 0) { throw 'Manager CLI operation discovery failed.' }
$taskCatalog = ($taskOutput -join "`n") | ConvertFrom-Json
$taskIds = @('environment.discover', 'environment.resolve', 'launch.plan', 'launch.execute', 'launch.observe')
$taskDescriptors = @($taskCatalog.data | Where-Object { $_.id -in $taskIds } | Sort-Object id)
if ($taskDescriptors.Count -ne $taskIds.Count -or
    @(Compare-Object ($taskIds | Sort-Object) ($taskDescriptors.id | Sort-Object)).Count -ne 0) {
    throw 'Expected exactly the five registered environment/launch workflow input descriptors.'
}
$taskContract = [ordered]@{
    schemaVersion = 1
    kind = 'operation-input-contract-snapshot'
    source = 'LaunchDiscoveryOperations.Registrations and LaunchWorkflowOperations.Registrations via Manager CLI op list'
    scope = 'Input schemas and operation authorization only; this is not a responseSchema. Result DTOs use LaunchDiscoveryJsonContext, LaunchWorkflowJsonContext and OperationJsonContext.'
    operations = $taskDescriptors
}
$taskTarget = [IO.Path]::GetFullPath((Join-Path $taskRepoRoot $OutputPath))
New-Item -ItemType Directory -Path (Split-Path -Parent $taskTarget) -Force | Out-Null
$taskContract | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $taskTarget -Encoding utf8
[pscustomobject]@{ file = $taskTarget; operations = $taskDescriptors.Count } | ConvertTo-Json -Compress
