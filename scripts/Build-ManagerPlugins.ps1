[CmdletBinding()]
param([string] $OutputDirectory = 'artifacts/manager/plugins', [string[]] $Plugin = @('license'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory, $taskRepo)
$taskRun = Join-Path $taskOutput ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($taskRun) | Out-Null
$taskPackages = [Collections.Generic.List[object]]::new()
foreach ($taskPlugin in $Plugin) {
    $taskProjectName = switch ($taskPlugin) { 'license' {'License'} 'vm' {'Vm'} default {throw "Unknown plugin: $taskPlugin"} }
    $taskProject = Join-Path $taskRepo "src/YuzuhaToolkit.Plugin.$taskProjectName/YuzuhaToolkit.Plugin.$taskProjectName.csproj"
    $taskPackage = Join-Path $taskRun $taskPlugin
    if (Test-Path -LiteralPath $taskPackage) { throw "Output already exists: $taskPackage" }
    & $env:DOTNET_HOST_PATH publish $taskProject -c Release -o $taskPackage -m:1 -nr:false -p:UseSharedCompilation=false -p:PublishAot=false --self-contained false -p:NuGetAudit=false | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Plugin publish failed: $taskPlugin" }
    $taskAssembly = "Yuzuha.Manager.Plugin.$taskProjectName"
    $taskCapabilitiesOutput = & $env:DOTNET_HOST_PATH (Join-Path $taskPackage ($taskAssembly+'.dll')) --describe
    if ($LASTEXITCODE -ne 0) { throw "Plugin metadata failed: $taskPlugin" }
    $taskCapabilities = ($taskCapabilitiesOutput -join "`n") | ConvertFrom-Json
    $taskFiles = [ordered]@{}
    Get-ChildItem -LiteralPath $taskPackage -Recurse -File | Sort-Object FullName | ForEach-Object {
        $taskRelative = [IO.Path]::GetRelativePath($taskPackage, $_.FullName).Replace('\','/')
        $taskFiles[$taskRelative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $taskManifest = [ordered]@{schemaVersion=1;id=$taskCapabilities.pluginId;version=$taskCapabilities.version;contractVersion=$taskCapabilities.contractVersion;
        entryPoint=($taskAssembly+'.exe');files=$taskFiles;operations=$taskCapabilities.operations}
    $taskManifestFile = Join-Path $taskPackage 'manager-plugin.json'
    $taskManifest | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $taskManifestFile -Encoding utf8
    $taskPackages.Add([ordered]@{id=$taskCapabilities.pluginId;directory=$taskPackage;manifestSha256=(Get-FileHash -LiteralPath $taskManifestFile -Algorithm SHA256).Hash.ToLowerInvariant();operations=$taskCapabilities.operations.Count;runtime='net10.0 framework-dependent'})
}
$taskPackages | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRun 'packages.json') -Encoding utf8
$taskPackages | ConvertTo-Json -Depth 8
