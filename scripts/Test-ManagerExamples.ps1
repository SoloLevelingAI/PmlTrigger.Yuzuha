[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskCli = Join-Path $taskRepo 'src/YuzuhaToolkit.Manager.Cli/bin/Release/net10.0/Yuzuha.Manager.Cli.dll'
$taskRoot = Join-Path ([IO.Path]::GetTempPath()) ('YuzuhaManagerExamples-' + [Guid]::NewGuid().ToString('N'))
$taskSavedEnvironment = @{}
foreach ($taskName in @('YUZUHA_STATE_DIR','YUZUHA_USER_DIR','YUZUHA_KNOWLEDGE_DIR','YUZUHA_TOOLKIT_ROOT')) {
    $taskSavedEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName)
}
$taskExamples = [Collections.Generic.List[object]]::new()
function Invoke-ManagerExample([string[]] $CliArguments, [int] $ExpectedExit = 0) {
    $taskOutput = & $env:DOTNET_HOST_PATH $taskCli @CliArguments
    $taskExit = $LASTEXITCODE
    if ($taskExit -ne $ExpectedExit) { throw "Example exit $taskExit expected $ExpectedExit`: $($CliArguments -join ' ') $taskOutput" }
    $taskParsed = ($taskOutput -join "`n") | ConvertFrom-Json
    $taskExamples.Add([ordered]@{operation=$taskParsed.operationId;exitCode=$taskExit;success=$taskParsed.success})
    return $taskParsed
}
try {
    [IO.Directory]::CreateDirectory($taskRoot) | Out-Null
    $env:YUZUHA_STATE_DIR = Join-Path $taskRoot 'state'
    $env:YUZUHA_USER_DIR = Join-Path $taskRoot 'user'
    $env:YUZUHA_KNOWLEDGE_DIR = Join-Path $taskRoot 'knowledge'
    $env:YUZUHA_TOOLKIT_ROOT = Join-Path $taskRoot 'install'
    Invoke-ManagerExample @('op','list') | Out-Null
    Invoke-ManagerExample @('op','describe','launch.profile.save') | Out-Null
    Invoke-ManagerExample @('op','invoke','manager.status') | Out-Null
    Invoke-ManagerExample @('op','invoke','launch.profile.list','--input','{"query":"","scope":"all"}') | Out-Null
    Invoke-ManagerExample @('op','invoke','missing.example') 3 | Out-Null
    Invoke-ManagerExample @('op','invoke','manager.status','--input','{"unknown":true}') 2 | Out-Null
    $taskFixture = Join-Path $taskRoot 'fixture'
    [IO.Directory]::CreateDirectory($taskFixture) | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskFixture 'evars.init'), "@echo off`r`nset pmllib=vendor`r`nset pmlui=vendor`r`n")
    $taskEvarsInput = Join-Path $taskRoot 'evars-input.json'
    @{installationRoot=$taskFixture;product='E3D';runtimeRoot=(Join-Path $taskRoot 'runtime');profile='E3D2.1';framework='net48';codePage=65001} |
        ConvertTo-Json | Set-Content -LiteralPath $taskEvarsInput -Encoding utf8
    $taskPreview = Invoke-ManagerExample @('op','invoke','evars.preview','--input-file',$taskEvarsInput)
    $taskReference = Join-Path $taskRoot 'reference.json'
    @{planId=$taskPreview.data.planId;sha256=$taskPreview.data.sha256} | ConvertTo-Json | Set-Content -LiteralPath $taskReference -Encoding utf8
    Invoke-ManagerExample @('op','invoke','evars.apply','--input-file',$taskReference) 4 | Out-Null
    $taskApplied = Invoke-ManagerExample @('op','invoke','evars.apply','--input-file',$taskReference,'--allow-write')
    @{planId=$taskApplied.data.planId;sha256=$taskApplied.data.sha256} | ConvertTo-Json | Set-Content -LiteralPath $taskReference -Encoding utf8
    Invoke-ManagerExample @('op','invoke','evars.restore','--input-file',$taskReference,'--allow-write') | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskFixture 'sample.pmlfnc'), "define function !!YuzuhaExample() is STRING`nreturn 'Yuzuha sample'`nendfunction`n")
    $taskKnowledgeInput = Join-Path $taskRoot 'knowledge-input.json'
    @{product='E3D';version='2.1';role='official';name='sample';pmlLib=$taskFixture} | ConvertTo-Json | Set-Content -LiteralPath $taskKnowledgeInput -Encoding utf8
    $taskPreview = Invoke-ManagerExample @('op','invoke','knowledge.import.preview','--input-file',$taskKnowledgeInput)
    @{planId=$taskPreview.data.planId;sha256=$taskPreview.data.sha256} | ConvertTo-Json | Set-Content -LiteralPath $taskReference -Encoding utf8
    Invoke-ManagerExample @('op','invoke','knowledge.import.apply','--input-file',$taskReference) 4 | Out-Null
    Invoke-ManagerExample @('op','invoke','knowledge.import.apply','--input-file',$taskReference,'--allow-write') | Out-Null
    Invoke-ManagerExample @('op','invoke','knowledge.libraries.list','--input','{"product":"E3D","version":"2.1"}') | Out-Null
    Invoke-ManagerExample @('op','invoke','knowledge.library.search','--input','{"product":"E3D","version":"2.1","name":"sample","query":"Yuzuha"}') | Out-Null
    Invoke-ManagerExample @('op','invoke','copy.formats') | Out-Null
    $taskCopyTarget = Join-Path $taskRoot 'copy-target'
    [IO.Directory]::CreateDirectory($taskCopyTarget) | Out-Null
    $taskCopyInput = Join-Path $taskRoot 'copy-input.json'
    @{source=$taskFixture;registeredRoot=$taskCopyTarget;name='Example'} | ConvertTo-Json | Set-Content -LiteralPath $taskCopyInput -Encoding utf8
    $taskPreview = Invoke-ManagerExample @('op','invoke','copy.preview','--input-file',$taskCopyInput)
    @{planId=$taskPreview.data.planId;sha256=$taskPreview.data.sha256} | ConvertTo-Json | Set-Content -LiteralPath $taskReference -Encoding utf8
    Invoke-ManagerExample @('op','invoke','copy.apply','--input-file',$taskReference) 4 | Out-Null
    $taskApplied = Invoke-ManagerExample @('op','invoke','copy.apply','--input-file',$taskReference,'--allow-write')
    @{planId=$taskApplied.data.planId;sha256=$taskApplied.data.sha256} | ConvertTo-Json | Set-Content -LiteralPath $taskReference -Encoding utf8
    Invoke-ManagerExample @('op','invoke','copy.restore','--input-file',$taskReference,'--allow-write') | Out-Null
    $taskReportDirectory = Join-Path $taskRepo 'artifacts/manager'
    [IO.Directory]::CreateDirectory($taskReportDirectory) | Out-Null
    $taskExamples | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskReportDirectory 'cli-examples.json') -Encoding utf8
    [pscustomobject]@{examples=$taskExamples.Count;failed=0;report=(Join-Path $taskReportDirectory 'cli-examples.json')} | ConvertTo-Json -Compress
}
finally {
    foreach ($taskName in $taskSavedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($taskName, $taskSavedEnvironment[$taskName]) }
    $taskVerifiedRoot = [IO.Path]::GetFullPath($taskRoot)
    if ($taskVerifiedRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $taskVerifiedRoot).StartsWith('YuzuhaManagerExamples-')) {
        Remove-Item -LiteralPath $taskVerifiedRoot -Recurse -Force
    }
}
