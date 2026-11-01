[CmdletBinding()]
param([string]$OutputDirectory, [switch]$NoBuild, [string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/tests/preparation-desktop' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $NoBuild) {
    & dotnet build (Join-Path $root 'src/YuzuhaToolkit.Desktop') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'WPF build failed.' }
}
$exe = if ($ExecutablePath) { [IO.Path]::GetFullPath($ExecutablePath) } else { Join-Path $root 'src/YuzuhaToolkit.Desktop/bin/Release/net10.0-windows/YuzuhaToolkit.Desktop.exe' }
$report = Join-Path $OutputDirectory 'preparation-smoke.json'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$process = Start-Process -FilePath $exe -ArgumentList '--preparation-smoke-output',('"' + $OutputDirectory + '"') -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue; throw 'Isolated preparation smoke timed out.' }
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw "Preparation smoke failed. See $OutputDirectory/smoke-error.txt" }
$receipt = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($receipt.passed -ne $true -or $receipt.palettes.Count -ne 6 -or $receipt.stages -ne 8 -or $receipt.readers -ne 4 -or $receipt.screenshots -ne 31 -or $receipt.viewportEvidence.Count -ne 31 -or $receipt.themePreservesDraft -ne $true -or $receipt.navigationTransitions -ne $true -or $receipt.unknownRecovery -ne $true) { throw 'Invalid preparation receipt.' }
foreach ($capture in $receipt.viewportEvidence) {
    $imagePath = [IO.Path]::GetFullPath((Join-Path (Split-Path $OutputDirectory -Parent) $capture.file))
    if (-not $imagePath.StartsWith($OutputDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Capture escaped output directory.' }
    $bytes = [IO.File]::ReadAllBytes($imagePath)
    if ($bytes.Length -lt 24 -or [Convert]::ToHexString($bytes[0..7]) -cne '89504E470D0A1A0A') { throw "Invalid PNG: $imagePath" }
    $width = [uint32]$bytes[16] * 16777216 + [uint32]$bytes[17] * 65536 + [uint32]$bytes[18] * 256 + $bytes[19]
    $height = [uint32]$bytes[20] * 16777216 + [uint32]$bytes[21] * 65536 + [uint32]$bytes[22] * 256 + $bytes[23]
    if ($capture.requestedWidth -ne $capture.actualWidth -or $capture.requestedHeight -ne $capture.actualHeight -or $capture.actualWidth -ne $width -or $capture.actualHeight -ne $height -or $capture.pixelWidth -ne $width -or $capture.pixelHeight -ne $height) { throw "Viewport dimensions disagree: $imagePath" }
}
foreach ($palette in 'lime','cyan','violet','orange','rose','graphite') {
    foreach ($size in 1440,1280,1024) { if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory "ion-$palette-$size.png"))) { throw 'Missing palette viewport.' } }
}
$bindings = Join-Path $OutputDirectory 'binding-errors.log'
if ((Test-Path -LiteralPath $bindings) -and (Get-Item -LiteralPath $bindings).Length -ne 0) { throw 'Preparation binding errors were recorded.' }
Write-Host "Preparation native controls, six palettes and unknown recovery passed: $OutputDirectory"
