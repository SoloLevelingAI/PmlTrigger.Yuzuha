[CmdletBinding()]
param(
    [string] $Root = $PSScriptRoot
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$failed = $false

function Add-Failure {
    param([Parameter(Mandatory = $true)][string] $Message)
    Write-Error $Message -ErrorAction Continue
    $script:failed = $true
}

$required = @(
    'Install-Yuzuha.ps1',
    'README-FIRST.zh-CN.md',
    'README-FIRST.en.md',
    'INSTALL-SKILL.zh-CN.md',
    'INSTALL-SKILL.en.md',
    'SHA256SUMS.txt',
    'scripts\Configure-YuzuhaE3D.ps1',
    'payload\PmlTrigger.Yuzuha\PMLUI',
    'payload\PmlTrigger.Yuzuha\PMLLIB\pml.index',
    'payload\PmlTrigger.Yuzuha\PMLLIB\Examples\YuzuhaTriggerCommand.pmlcmd',
    'payload\PmlTrigger.Yuzuha\runtime\net48\YuzuhaToolkit.PmlHost.Net48.dll',
    'payload\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe',
    'payload\PmlTrigger.Yuzuha\docs\Agent.PmlTrigger.md',
    'payload\PmlTrigger.Yuzuha\docs\local-development.zh-CN.md',
    'payload\PmlTrigger.Yuzuha\docs\local-development.en.md',
    'payload\PmlTrigger.Yuzuha\skill\SKILL.md'
)

foreach ($relative in $required) {
    $path = Join-Path $Root $relative
    if (Test-Path -LiteralPath $path) {
        Write-Host "[OK] $relative"
    }
    else {
        Add-Failure "[MISSING] $relative"
    }
}

$proprietaryNames = @('Aveva.Core.Utilities.dll', 'PMLNet.dll', 'ForeignLanguage.dll')
foreach ($name in $proprietaryNames) {
    if (Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $name -ErrorAction SilentlyContinue) {
        Add-Failure "AVEVA proprietary assembly must not be distributed: $name"
    }
}

$forbiddenFiles = @('PlantHost.Rpc.Net8.dll', 'PlantHost.Rpc.Net8.xml')
foreach ($name in $forbiddenFiles) {
    if (Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $name -ErrorAction SilentlyContinue) {
        Add-Failure "Legacy Net8 dependency must not be distributed: $name"
    }
}

if (Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.pdb' -ErrorAction SilentlyContinue) {
    Add-Failure 'PDB files must not be distributed in the cross-machine package.'
}

$skillRoot = Join-Path $Root 'payload\PmlTrigger.Yuzuha\skill'
if (Test-Path -LiteralPath $skillRoot) {
    $skillFiles = @(Get-ChildItem -LiteralPath $skillRoot -Recurse -File -Filter 'SKILL.md')
    if ($skillFiles.Count -ne 1 -or $skillFiles[0].DirectoryName -ne $skillRoot) {
        Add-Failure 'The release must contain exactly one skill\SKILL.md with no nested skill directory.'
    }
    if (Get-ChildItem -LiteralPath $skillRoot -Recurse -File |
        Where-Object { $_.Extension -in @('.dll', '.exe', '.pdb') }) {
        Add-Failure 'The Skill directory must not contain runtime binaries.'
    }
}

$obsolete = @('ATestGetByce20260823', 'YuzuhaTriggrtCommand')
$maintainedRoots = @(
    (Join-Path $Root 'payload\PmlTrigger.Yuzuha\docs'),
    (Join-Path $Root 'payload\PmlTrigger.Yuzuha\skill'),
    (Join-Path $Root 'payload\PmlTrigger.Yuzuha\PMLLIB')
)
foreach ($rootPath in $maintainedRoots) {
    if (-not (Test-Path -LiteralPath $rootPath)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $rootPath -Recurse -File -Include '*.md','*.pmlcmd','*.pmlfnc','*.pmlobj','*.txt') {
        $content = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($term in $obsolete) {
            if ($content -match [regex]::Escape($term)) {
                Add-Failure "Obsolete command '$term' found in $($file.FullName)"
            }
        }
    }
}

$oldCommand = Join-Path $Root 'payload\PmlTrigger.Yuzuha\PMLLIB\Examples\YuzuhaTriggrtCommand.pmlcmd'
if (Test-Path -LiteralPath $oldCommand) {
    Add-Failure 'Obsolete YuzuhaTriggrtCommand.pmlcmd must not be distributed.'
}

$aotExe = Join-Path $Root 'payload\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe'
if (Test-Path -LiteralPath $aotExe) {
    $file = Get-Item -LiteralPath $aotExe
    if ($file.Length -lt 10MB) {
        Add-Failure "Native AOT executable is unexpectedly small: $($file.Length) bytes"
    }
    else {
        Write-Host "[OK] Native AOT executable size: $($file.Length) bytes"
    }
    $aotVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($aotExe)
    if ($aotVersion.ProductVersion -notlike '0.1.0-preview.6*') {
        Add-Failure "Native AOT ProductVersion is not preview.6: $($aotVersion.ProductVersion)"
    }
}

$aotRuntime = Join-Path $Root 'payload\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot'
if (Test-Path -LiteralPath $aotRuntime) {
    $aotFiles = @(Get-ChildItem -LiteralPath $aotRuntime -File)
    if ($aotFiles.Count -ne 1 -or $aotFiles[0].Name -ne 'YuzuhaToolkit.Mcp.exe') {
        Add-Failure 'Native AOT runtime must contain exactly one YuzuhaToolkit.Mcp.exe file.'
    }
}

$net48Host = Join-Path $Root 'payload\PmlTrigger.Yuzuha\runtime\net48\YuzuhaToolkit.PmlHost.Net48.dll'
if (Test-Path -LiteralPath $net48Host) {
    $hostVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($net48Host)
    if ($hostVersion.ProductVersion -notlike '0.1.0-preview.6*') {
        Add-Failure "Net48 Host ProductVersion is not preview.6: $($hostVersion.ProductVersion)"
    }
}

$installer = Join-Path $Root 'Install-Yuzuha.ps1'
if ((Test-Path -LiteralPath $installer) -and
    (Get-Content -LiteralPath $installer -Raw) -notmatch "version = '0\.1\.0-preview\.6'") {
    Add-Failure 'Installer version is not 0.1.0-preview.6.'
}

$sumPath = Join-Path $Root 'SHA256SUMS.txt'
if (Test-Path -LiteralPath $sumPath) {
    $hashEntries = 0
    foreach ($line in Get-Content -LiteralPath $sumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
            Add-Failure "Invalid SHA256SUMS line: $line"
            continue
        }
        $hashEntries++
        $expected = $Matches[1]
        $relative = $Matches[2].Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $path = Join-Path $Root $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Add-Failure "SHA256SUMS references a missing file: $relative"
            continue
        }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($actual -ne $expected) {
            Add-Failure "SHA-256 mismatch: $relative"
        }
    }
    $expectedEntries = @(Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { $_.FullName -ne $sumPath }).Count
    if ($hashEntries -ne $expectedEntries) {
        Add-Failure "SHA256SUMS has $hashEntries entries; expected $expectedEntries."
    }
    else {
        Write-Host "[OK] Verified $hashEntries SHA-256 entries"
    }
}

if ($failed) { throw 'Release validation failed.' }
Write-Host 'Release validation passed.'
