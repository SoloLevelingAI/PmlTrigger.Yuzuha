[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    node scripts/architecture.mjs check
    if ($LASTEXITCODE -ne 0) { throw 'Architecture projection is stale.' }
    dotnet build tools/Documentation -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Documentation test inventory build failed.' }
    node --test tests/documentation-tests.test.mjs tests/documentation-inventory.test.mjs tests/documentation-ui-trace.test.mjs tests/documentation-ui-pages.test.mjs tests/documentation-baselines.test.mjs tests/documentation-pml-xml.test.mjs tests/pml-data-table.test.mjs scripts/catalog-filter.test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Documentation association tests failed.' }
    & pwsh -NoProfile -File tests/documentation-runner.test.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Documented runner verification failed.' }
    dotnet test YuzuhaToolkit.Development.slnx -c Release --logger 'trx;LogFilePrefix=development' --results-directory artifacts/tests
    $testExit = $LASTEXITCODE
    @{ commit=(& git rev-parse HEAD).Trim(); dirty=[bool](& git status --porcelain); exitCode=$testExit;
       environment=[Runtime.InteropServices.RuntimeInformation]::OSDescription; sdk=(& dotnet --version).Trim();
       finishedUtc=[DateTime]::UtcNow.ToString('o'); scope='xUnit and ArchUnitNET; not AVEVA live';
       ciRun=$env:GITHUB_RUN_ID } | ConvertTo-Json | Set-Content artifacts/tests/run-metadata.json -Encoding utf8
    if ($testExit -ne 0) { throw 'Development tests failed.' }
    $scenarioReports = Join-Path $root ('artifacts/tests/launch-scenarios/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scenarioReports -Force | Out-Null
    & "$PSScriptRoot/Run-DocumentedSmoke.ps1" -Runner workbench-lifecycle -ReportDirectory $scenarioReports
    if ($LASTEXITCODE -ne 0) { throw 'Launch lifecycle scenario tests failed.' }
    & pwsh -NoProfile -File tests/pml-development.test.ps1
    if ($LASTEXITCODE -ne 0) { throw 'PML deployment workflow offline tests failed.' }
}
finally { Pop-Location }
