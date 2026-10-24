[CmdletBinding()]
param([string] $OutputRoot, [string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'Windows Native AOT CLI must be built on Windows with the C++ build tools installed.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $projectRoot 'runtime/cli' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$project = Join-Path $projectRoot 'src/PmlTwin.Phayton.Cli/PmlTwin.Phayton.Cli.csproj'
& dotnet publish $project -c $Configuration -r win-x64 -o $OutputRoot -p:OS=Windows_NT -p:PublishAot=true -p:SelfContained=true -p:PublishSingleFile=false -p:OptimizationPreference=Size -p:IlcGenerateStackTraceData=false -p:IlcMaxParallelism=2 -m:1 -nr:false
if ($LASTEXITCODE -ne 0) { throw 'Native AOT CLI publish failed.' }
$entries = @()
foreach ($name in @('PmlTwin.Phayton.Cli.exe', 'e_sqlite3.dll')) {
    $file = Join-Path $OutputRoot $name
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing CLI publish dependency: $name" }
    $entries += [ordered]@{ name = $name; publishMode = if ($name.EndsWith('.exe')) { 'NativeAOT' } else { 'NativeDependency' }; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
}
[ordered]@{ schema = 1; target = 'win-x64'; entryPoint = 'PmlTwin.Phayton.Cli.exe'; transport = 'native-cli'; components = $entries } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $OutputRoot 'native-aot-manifest.json') -Encoding UTF8
Write-Host "Native AOT CLI built and hashed: $OutputRoot"
