[CmdletBinding()]
param([switch] $SkipBuild, [string] $SevenZipTool, [switch] $FullOffline)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $taskRepoRoot
$taskPreviousSevenZipTest = $env:YUZUHA_TEST_7ZR_PATH
try {
    if ($SevenZipTool) { $env:YUZUHA_TEST_7ZR_PATH = (Resolve-Path -LiteralPath $SevenZipTool).Path }
    $taskBuildOptions = if ($SkipBuild) { @('--no-build','--no-restore') } else { @('-m:1','-nr:false','-p:UseSharedCompilation=false','-p:NuGetAudit=false') }
    & $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Manager.Tests/YuzuhaToolkit.Manager.Tests.csproj -c Release @taskBuildOptions --logger 'trx;LogFileName=manager-process.trx' --results-directory artifacts/manager/tests
    if ($LASTEXITCODE -ne 0) { throw 'Manager isolated tests failed.' }
    $taskLegacyFilter = if ($FullOffline) { @() } else { @('--filter','FullyQualifiedName~UserDataManagementTests|FullyQualifiedName~LaunchOperationTests|FullyQualifiedName~LaunchDraftTests|FullyQualifiedName~NativeCliCapabilityTests|FullyQualifiedName~CommunicationProtocolTests|FullyQualifiedName~WorkspaceBehaviorTests|FullyQualifiedName~DesktopFunctionalCorrectionsTests|FullyQualifiedName~DesktopCliBoundaryTests') }
    $taskLegacyLog = if ($FullOffline) { 'trx;LogFileName=manager-all-existing.trx' } else { 'trx;LogFileName=manager-compatibility.trx' }
    & $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Tests/YuzuhaToolkit.Tests.csproj -c Release @taskBuildOptions @taskLegacyFilter --logger $taskLegacyLog --results-directory artifacts/manager/tests
    if ($LASTEXITCODE -ne 0) { throw 'Manager compatibility tests failed.' }
    if (!$SkipBuild) {
        foreach ($taskProject in @('src/YuzuhaToolkit.Manager.Cli','src/PmlTwin.Phayton.Cli')) {
            & $env:DOTNET_HOST_PATH build $taskProject -c Release -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $taskProject" }
        }
    }
    & (Join-Path $PSScriptRoot 'Export-ManagerOperations.ps1')
    & (Join-Path $PSScriptRoot 'Test-ManagerExamples.ps1')
    if ($FullOffline) {
        if (!$SkipBuild) {
            foreach ($taskProject in @('src/YuzuhaToolkit.Desktop','installer/tests/SetupTests.csproj')) {
                & $env:DOTNET_HOST_PATH build $taskProject -c Release -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false
                if ($LASTEXITCODE -ne 0) { throw "Offline build failed: $taskProject" }
            }
        }
        & $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Architecture.Tests/YuzuhaToolkit.Architecture.Tests.csproj -c Release @taskBuildOptions --logger 'trx;LogFileName=manager-architecture.trx' --results-directory artifacts/manager/tests
        if ($LASTEXITCODE -ne 0) { throw 'Manager architecture tests failed.' }
        & (Join-Path $taskRepoRoot 'installer/tests/bin/Release/net48/SetupTests.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Installer isolated tests failed.' }
        & node (Join-Path $PSScriptRoot 'architecture.mjs') check
        if ($LASTEXITCODE -ne 0) { throw 'Architecture projection is stale.' }
        [ordered]@{finishedUtc=[DateTime]::UtcNow.ToString('o');sdk=(& $env:DOTNET_HOST_PATH --version).Trim();
            scope='offline fixtures; no real AVEVA/VM/VIX execution';managerReport='tests/manager-process.trx';legacyReport='tests/manager-all-existing.trx';
            architectureReport='tests/manager-architecture.trx';cliExamples='cli-examples.json';operations='operation-inventory.json';
            nativeSevenZip=[bool]$SevenZipTool;desktop='build only; visual and real product acceptance pending'} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRepoRoot 'artifacts/manager/offline-run.json') -Encoding utf8
    }
}
finally { $env:YUZUHA_TEST_7ZR_PATH = $taskPreviousSevenZipTest; Pop-Location }
