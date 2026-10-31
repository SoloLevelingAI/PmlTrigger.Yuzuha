param([string]$DataDirectory, [ValidateRange(1024,65535)][int]$Port=4318, [switch]$ServeWeb)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'source'
if (-not $DataDirectory) { $DataDirectory=Join-Path $env:LOCALAPPDATA 'PMLTWIN/Ribbon/workspace' }
$env:UIC_DATA_DIR=[IO.Path]::GetFullPath($DataDirectory)
$env:UIC_PORT=[string]$Port
$env:UIC_WEB_ROOT=Join-Path $source 'apps/web/dist'
$exe=Join-Path $source 'apps/backend/bin/RibbonStudio.Backend.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run node integrations/ribbon/source/scripts/build-backend.mjs first.' }
Push-Location $source
try { if ($ServeWeb) { & $exe --serve-web } else { & $exe }; exit $LASTEXITCODE } finally { Pop-Location }
