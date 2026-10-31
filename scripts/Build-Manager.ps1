[CmdletBinding()]
param([string] $OutputDirectory = 'artifacts/manager/packages')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory, $taskRepo)
$taskRun = Join-Path $taskOutput ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$taskPackage = Join-Path $taskRun 'manager'
[IO.Directory]::CreateDirectory($taskPackage) | Out-Null
& $env:DOTNET_HOST_PATH publish (Join-Path $taskRepo 'src/YuzuhaToolkit.Manager.Cli') -c Release -r win-x64 --self-contained false -o $taskPackage -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Manager CLI publish failed.' }
$taskDependencies = Get-Content -LiteralPath (Join-Path $taskPackage 'Yuzuha.Manager.Cli.deps.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($taskName in $taskDependencies.libraries.Keys) {
    if ($taskName -match '^(PlantHost\.Rpc|YuzuhaToolkit\.(Mcp|Cli\.Compatibility|Plugin\.(License|Vm))|Yuzuha\.Manager\.Plugin\.)') { throw "Base manager contains a forbidden implementation: $taskName" }
}
$taskPreviousState = $env:YUZUHA_STATE_DIR
try {
    $env:YUZUHA_STATE_DIR = Join-Path $taskRun 'metadata-state'
    $taskList = & $env:DOTNET_HOST_PATH (Join-Path $taskPackage 'Yuzuha.Manager.Cli.dll') op list
    if ($LASTEXITCODE -ne 0) { throw 'Published manager metadata failed.' }
    $taskOperations = ($taskList -join "`n") | ConvertFrom-Json
    $taskPlugins = & $env:DOTNET_HOST_PATH (Join-Path $taskPackage 'Yuzuha.Manager.Cli.dll') op invoke plugin.list
    if ($LASTEXITCODE -ne 0) { throw 'Published manager cannot read the empty optional registry.' }
}
finally { $env:YUZUHA_STATE_DIR = $taskPreviousState }
$taskFiles = [Collections.Generic.List[object]]::new()
foreach ($taskFile in Get-ChildItem -LiteralPath $taskPackage -Recurse -File | Sort-Object FullName) {
    if ($taskFile.Extension -in @('.db','.sqlite','.sqlite3','.vmx','.vmdk') -or $taskFile.Name -in @('registry.json','workspace.json','credentials.json')) { throw "User data or VM payload in base package: $($taskFile.Name)" }
    $taskFiles.Add([ordered]@{path=[IO.Path]::GetRelativePath($taskPackage,$taskFile.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()})
}
$taskManifest = [ordered]@{schemaVersion=1;component='Yuzuha.Manager.Cli';entryPoint='Yuzuha.Manager.Cli.exe';runtime='net10.0 win-x64 framework-dependent';
    operations=$taskOperations.data.Count;scope='manager CLI only; communication CLI and optional workers are separate';files=$taskFiles}
$taskManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskPackage 'manager-package.json') -Encoding utf8
[ordered]@{directory=$taskPackage;files=$taskFiles.Count;operations=$taskOperations.data.Count;runtime=$taskManifest.runtime;installed=$false} | ConvertTo-Json
