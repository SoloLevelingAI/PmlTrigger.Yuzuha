[CmdletBinding()]
param([string]$OutputRoot, [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts/desktop' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
dotnet publish (Join-Path $root 'src/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'Native Windows desktop publish failed.' }
Write-Host "Yuzuha workspace: $(Join-Path $OutputRoot 'YuzuhaToolkit.Desktop.exe')"
