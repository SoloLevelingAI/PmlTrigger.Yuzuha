[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('native-cli','native-builtin','rpc-net35','rpc-net48','formrecord-net35','formrecord-net48','documentation-contracts','desktop-flows','desktop-preparation','workbench-lifecycle')][string]$Runner,
    [Parameter(Mandatory)][string]$ReportDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Check-Exit([string]$Step) { if ($LASTEXITCODE -ne 0) { throw "$Step failed ($LASTEXITCODE)." } }
Push-Location $root
try {
    $pythonVersion = $null
    if ($Runner.StartsWith('native-') -or $Runner.StartsWith('rpc-') -or $Runner -eq 'workbench-lifecycle') {
        $python = if ($env:YUZUHA_TEST_PYTHON) { $env:YUZUHA_TEST_PYTHON } else { 'python' }
        $pythonVersion = & $python --version 2>&1
        Check-Exit 'Python prerequisite (install Python 3 or set YUZUHA_TEST_PYTHON)'
    }
    if ($Runner -in @('native-cli','native-builtin')) {
        $runtime = Join-Path $ReportDirectory 'native'
        & "$PSScriptRoot/Build-NativeCli.ps1" -OutputRoot $runtime
        $script = if ($Runner -eq 'native-cli') { 'tests/cli_smoke.py' } else { 'tests/v03/builtin_smoke.py' }
        & $python -X utf8 $script $runtime
        Check-Exit $script
    } elseif ($Runner -eq 'workbench-lifecycle') {
        & dotnet build src/PmlTwin.Phayton.Cli/PmlTwin.Phayton.Cli.csproj -c Release
        Check-Exit 'Native CLI build'
        $cli = Join-Path $root 'src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.exe'
        & $python -X utf8 tests/workbench-scenario.test.py
        Check-Exit 'Scenario parser tests'
        foreach ($format in @('toml','json')) {
            $scenarioDirectory = Join-Path $ReportDirectory $format
            & $python -X utf8 scripts/run-workbench-scenario.py --scenario "tests/scenarios/launch-lifecycle.$format" --cli $cli --output $scenarioDirectory
            Check-Exit "Launch lifecycle $format scenario"
            $scenarioResult = Get-Content -LiteralPath (Join-Path $scenarioDirectory 'run.json') -Raw | ConvertFrom-Json
            if ($scenarioResult.status -cne 'Passed' -or $scenarioResult.executed -ne 15) { throw 'Incomplete launch scenario receipt.' }
        }
    } elseif ($Runner.StartsWith('rpc-')) {
        $framework = $Runner.Substring(4)
        $project = if ($framework -eq 'net35') { 'tests/AotRpcSmoke/Server35/Server.csproj' } else { 'tests/AotRpcSmoke/Server/Server.csproj' }
        & dotnet build $project -c Release -o (Join-Path $ReportDirectory 'test-server')
        Check-Exit 'Framework RPC fixture build'
        & dotnet publish tests/AotRpcSmoke/Client/Client.csproj -c Release -o (Join-Path $ReportDirectory 'test-client')
        Check-Exit 'Native AOT RPC client publish'
        & $python -X utf8 tests/AotRpcSmoke/run.py $ReportDirectory
        Check-Exit 'Framework RPC fixture execution'
    } elseif ($Runner.StartsWith('formrecord-')) {
        $framework = $Runner.Substring(11)
        $output = Join-Path $ReportDirectory 'host-fixture'
        & dotnet build tests/FormRecordSmoke/FormRecordSmoke.csproj -c Release -f $framework -o $output
        Check-Exit 'Recorder fixture build'
        & (Join-Path $output 'FormRecordSmoke.exe') (Join-Path $ReportDirectory 'synthetic-records')
        Check-Exit 'Recorder fixture execution'
    } elseif ($Runner -eq 'desktop-flows') {
        & "$PSScriptRoot/Test-Desktop.ps1" -OutputDirectory (Join-Path $ReportDirectory 'desktop')
        Check-Exit 'WPF synthetic flows'
    } elseif ($Runner -eq 'desktop-preparation') {
        & "$PSScriptRoot/Test-PreparationDesktop.ps1" -OutputDirectory (Join-Path $ReportDirectory 'desktop')
        Check-Exit 'WPF installation journey and ION palettes'
    } elseif ($Runner -eq 'documentation-contracts') {
        & dotnet build tools/Documentation -c Release
        Check-Exit 'Inventory build'
        & node --test tests/documentation-tests.test.mjs tests/documentation-inventory.test.mjs tests/documentation-ui-trace.test.mjs tests/documentation-ui-pages.test.mjs scripts/catalog-filter.test.mjs
        Check-Exit 'Documentation contract tests'
        & pwsh -NoProfile -File tests/documentation-runner.test.ps1
        Check-Exit 'TRX identity tests'
    }
    @{runner=$Runner;passed=$true;executed=1;scope='OneOfflineSuite';avevaLive=$false;python=$pythonVersion} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReportDirectory 'smoke-result.json') -Encoding utf8
} finally { Pop-Location }
