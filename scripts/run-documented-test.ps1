# Adapted from SoloLevelingAI/LaunchDesk scripts/run-documented-test.ps1 (main).
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TestId, [switch]$Describe)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/DocumentedTestResults.ps1"
$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$idHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($TestId))).Substring(0,16).ToLowerInvariant()
$reportRoot = Join-Path $repository ('artifacts/dt/' + $idHash + '/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$summary = [ordered]@{schemaVersion=1;testId=$TestId;status='NotRun';startedUtc=[DateTime]::UtcNow.ToString('o');executed=0;evidence=@('console.log')}
Push-Location $repository
Start-Transcript -LiteralPath (Join-Path $reportRoot 'console.log') | Out-Null
try {
    $summary.commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
    $summary.dirty = [bool](& git status --porcelain --untracked-files=normal)
    $summary.environment = [Runtime.InteropServices.RuntimeInformation]::OSDescription + ' / ' + [Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $summary.dotnet = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw '.NET SDK is required.' }
    $summary.powershell = $PSVersionTable.PSVersion.ToString()
    $summary.ciRun = if ($env:GITHUB_ACTIONS -eq 'true') { $env:GITHUB_SERVER_URL + '/' + $env:GITHUB_REPOSITORY + '/actions/runs/' + $env:GITHUB_RUN_ID } else { $null }
    $entry = $null
    if ($TestId.StartsWith('xunit:', [StringComparison]::Ordinal)) {
        $inventory = Join-Path $reportRoot 'inventory.json'
        & dotnet run --project tools/Documentation -c Release -- --tests $repository $inventory
        if ($LASTEXITCODE -ne 0) { throw 'Cannot validate test identity against C# sources.' }
        $method = $TestId.Substring(6)
        $matching = @((Get-Content -LiteralPath $inventory -Raw | ConvertFrom-Json) | Where-Object { $_.kind -ceq 'xunit' -and $_.method -ceq $method })
        if ($matching.Count -ne 1) { throw 'Unknown or ambiguous xUnit test ID.' }
        $entry = $matching[0]
    } else {
        $map = Get-Content -LiteralPath docs/reference/test-map.json -Raw | ConvertFrom-Json
        $matching = @($map.cases | Where-Object { $_.id -ceq $TestId })
        if ($matching.Count -ne 1) { throw 'Unknown or ambiguous documented case ID.' }
        $entry = $matching[0]
    }
    $source = [IO.Path]::GetFullPath((Join-Path $repository $entry.file))
    if (-not $source.StartsWith($repository + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Test source escapes repository.' }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Test source is missing.' }
    if ($entry.kind -cne 'xunit') {
        $matches = @(Select-String -LiteralPath $source -SimpleMatch -Pattern $entry.entryPoint)
        if ($entry.kind -ceq 'manual') { $matches = @($matches | Where-Object { $_.Line.Trim() -ceq $entry.entryPoint }) }
        if ($matches.Count -ne 1) { throw 'Case entry point no longer uniquely matches source.' }
        $summary.line = $matches[0].LineNumber
    } else { $summary.line = $entry.line }
    $summary.source = $entry.file
    $summary.sourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    $summary.kind = $entry.kind
    Write-Host ('Confirmed test: ' + $TestId + ' at ' + $entry.file + ':' + $summary.line)
    if ($Describe -or $entry.kind -ceq 'manual') {
        Write-Host ('NotRun: source verified. Read ' + $entry.file + ' and the documented environment/expected results.')
        if ($entry.kind -ceq 'manual') { Get-Content -LiteralPath $source | Write-Host }
    } elseif ($entry.kind -ceq 'xunit') {
        & dotnet test $entry.projectFile -c Release --nologo -m:1 -nr:false --filter ('FullyQualifiedName=' + $method) --logger 'trx;LogFileName=result.trx' --results-directory $reportRoot
        $testExit = $LASTEXITCODE
        $summary.evidence += 'result.trx'
        if ($testExit -ne 0) { throw 'Selected xUnit test failed.' }
        $summary.executed = Assert-DocumentedTrx (Join-Path $reportRoot 'result.trx') $method
        if ($summary.sourceSha256 -cne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Test source changed during execution.' }
        $summary.status = 'Passed'
    } elseif ($entry.kind -ceq 'smoke') {
        & "$PSScriptRoot/Run-DocumentedSmoke.ps1" -Runner $entry.runner -ReportDirectory $reportRoot
        if ($LASTEXITCODE -ne 0) { throw 'Selected smoke suite failed.' }
        $receipt = Get-Content -LiteralPath (Join-Path $reportRoot 'smoke-result.json') -Raw | ConvertFrom-Json
        if ($receipt.runner -cne $entry.runner -or $receipt.passed -ne $true -or $receipt.executed -lt 1) { throw 'Smoke receipt does not match selected runner.' }
        $summary.executed = $receipt.executed
        $summary.evidence += 'smoke-result.json'
        if ($summary.sourceSha256 -cne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Test source changed during execution.' }
        $summary.status = 'Passed'
    } else { throw 'Unsupported case kind.' }
} catch {
    $summary.status = 'Failed'; $summary.error = $_.Exception.Message
    throw
} finally {
    $summary.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reportRoot 'documented-run.json') -Encoding utf8
    Write-Host ('Test evidence: ' + $reportRoot)
    Stop-Transcript | Out-Null
    Pop-Location
}
if (-not $Describe -and $summary.status -eq 'NotRun') { exit 2 }
