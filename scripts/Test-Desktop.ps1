[CmdletBinding()]
param([string]$OutputDirectory, [switch]$NoBuild, [string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/tests/desktop' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $NoBuild) {
    & dotnet build (Join-Path $root 'src/YuzuhaToolkit.Desktop') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'WPF build failed.' }
}
$exe = if ($ExecutablePath) { [IO.Path]::GetFullPath($ExecutablePath) } else { Join-Path $root 'src/YuzuhaToolkit.Desktop/bin/Release/net10.0-windows/YuzuhaToolkit.Desktop.exe' }
$report = Join-Path $OutputDirectory 'desktop-smoke.json'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$process = Start-Process -FilePath $exe -ArgumentList '--smoke-output',('"' + $OutputDirectory + '"') -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw 'Isolated WPF smoke timed out; no AVEVA process was stopped.'
}
if ($process.ExitCode -ne 0) { throw "WPF smoke failed. See $OutputDirectory/smoke-error.txt" }
if (-not (Test-Path -LiteralPath $report)) { throw 'WPF smoke produced no current receipt.' }
$receipt = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($receipt.passed -ne $true -or $receipt.viewports.Count -ne 3 -or $receipt.errorStates -ne 4 -or $receipt.themes.Count -ne 2 -or $receipt.pages -ne 14 -or $receipt.themePreservesWork -ne $true -or $receipt.savedCredentials -ne $true -or $receipt.guiKnowledge -ne $true -or $receipt.objectQueries -ne $true -or $receipt.profileLibrary -ne $true) { throw 'Invalid WPF smoke receipt.' }
foreach ($theme in 'Light','Dark') {
    foreach ($size in 1440,1280,853) {
        foreach ($screen in 'overview','launch','deployment','runtime','logs','sessions','functions','queries','review','tests','web','knowledge','settings','profiles') {
            $image = Join-Path $OutputDirectory "$theme/$screen-$size.png"
            if (-not (Test-Path -LiteralPath $image) -or (Get-Item -LiteralPath $image).Length -eq 0) { throw "Missing rendered screen: $image" }
        }
    }
    foreach ($screen in 'overview','launch','deployment','runtime','logs','sessions','functions','queries','review','tests','web','knowledge','settings','profiles') {
        if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory "$theme/design-$screen.png"))) { throw "Missing design canvas: $screen / $theme" }
    }
    foreach ($state in 'rejected','unknown') {
        $image = Join-Path $OutputDirectory "$theme/state-$state.png"
        if (-not (Test-Path -LiteralPath $image) -or (Get-Item -LiteralPath $image).Length -eq 0) { throw "Missing error-state screen: $image" }
    }
}
@{ commit=(& git -C $root rev-parse HEAD).Trim(); dirty=[bool](& git -C $root status --porcelain);
   environment=[Runtime.InteropServices.RuntimeInformation]::OSDescription; finishedUtc=[DateTime]::UtcNow.ToString('o');
   scope='WPF synthetic binding and layout; not AVEVA live or human usability acceptance'; receipt=$report } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'run-metadata.json') -Encoding utf8
Write-Host "WPF synthetic flows passed: $OutputDirectory"
