[CmdletBinding()]
param([string] $OutputRoot, [string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $projectRoot 'runtime\net10' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$entries = @()
foreach ($name in @('YuzuhaToolkit.Mcp', 'YuzuhaToolkit.Knowledge')) {
    $project = Join-Path $projectRoot "src\$name\$name.csproj"
    & dotnet publish $project -c $Configuration -o $OutputRoot -p:PublishAot=true -p:SelfContained=true -p:PublishSingleFile=false -p:OptimizationPreference=Size -p:IlcGenerateStackTraceData=false -p:IlcMaxParallelism=2
    if ($LASTEXITCODE -ne 0) { throw "Native AOT publish failed: $name" }
    $exe = Join-Path $OutputRoot "$name.exe"
    $entries += [ordered]@{ name = "$name.exe"; publishMode = 'NativeAOT'; sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() }
}
if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot 'e_sqlite3.dll'))) { throw 'Missing native SQLite dependency.' }
[ordered]@{ schema = 1; target = 'win-x64'; components = $entries } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $OutputRoot 'native-aot-manifest.json') -Encoding UTF8
Write-Host "Native AOT servers built and hashed: $OutputRoot"
