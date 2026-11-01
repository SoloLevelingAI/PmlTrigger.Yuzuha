[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$contractRoot = Split-Path $PSScriptRoot -Parent
Push-Location $contractRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'DocFX工具恢复失败；请检查.NET工具源与网络。' }
    # Explicit developer action: capture compiler inputs and documentation, without launching the product.
    & (Join-Path $PSScriptRoot 'Trace-Desktop.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Roslyn方法快照捕获失败；上一发布修订保持可读。' }
    node scripts/capture-contract-atlas-docfx.mjs
    if ($LASTEXITCODE -ne 0) { throw 'DocFX方法文档快照捕获失败；不会把旧站点当作新文档。' }
    Write-Host '方法与DocFX快照已捕获；运行中的Contract Atlas watch会自动生成共同修订。'
}
finally { Pop-Location }
