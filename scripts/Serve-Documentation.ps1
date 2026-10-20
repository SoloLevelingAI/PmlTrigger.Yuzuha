[CmdletBinding()]
param([ValidateRange(1024, 65535)][int]$Port = 5276)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$site = Join-Path $root 'artifacts/docfx/site'
$stagingRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/preview-host'))
$staging = [IO.Path]::GetFullPath((Join-Path $stagingRoot ([Guid]::NewGuid().ToString('N'))))
if (-not (Test-Path (Join-Path $site 'index.html'))) { throw 'Build documentation first: npm run docs:build' }
Push-Location $root
try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root 'src/YuzuhaToolkit.DevHost/bin/Release/net10.0') -Force |
        Copy-Item -Destination $staging -Recurse -Force
    Write-Host "DocFX: http://127.0.0.1:$Port/docs/index.html"
    Write-Host "Scalar: http://127.0.0.1:$Port/scalar/v1"
    dotnet (Join-Path $staging 'YuzuhaToolkit.DevHost.dll') --urls "http://127.0.0.1:$Port" --docs-root $site
    if ($LASTEXITCODE -ne 0) { throw 'Documentation preview failed.' }
}
finally {
    if ($staging.StartsWith($stagingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $staging)) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    Pop-Location
}
