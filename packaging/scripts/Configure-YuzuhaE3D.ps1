[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Defaults to the Git clone that contains this script.
    [string] $ProjectRoot = (Split-Path -Parent $PSScriptRoot),

    # Optional overrides. Normally both are discovered from the AVEVA registry key.
    [string] $E3DInstallDir,
    [string] $EvarsInitPath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Get-VersionSortKey {
    param([string] $Version)

    $parsed = $null
    if ([Version]::TryParse($Version, [ref] $parsed)) {
        return $parsed
    }

    return [Version]'0.0'
}

function Find-E3DInstall {
    $results = @()
    $everything3DRoots = @(
        'HKLM:\SOFTWARE\AVEVA Solutions Ltd\Everything3D',
        'HKLM:\SOFTWARE\WOW6432Node\AVEVA Solutions Ltd\Everything3D'
    )

    foreach ($root in $everything3DRoots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }

        foreach ($versionKey in Get-ChildItem -LiteralPath $root) {
            $properties = Get-ItemProperty -LiteralPath $versionKey.PSPath
            if ($properties.Path) {
                $results += [pscustomobject]@{
                    Version = $versionKey.PSChildName
                    Path = [string]$properties.Path
                    RegistryKey = $versionKey.Name
                }
            }
        }
    }

    # Some installations expose the executable directory under E3D\<version>\Directories.
    $e3DRoots = @(
        'HKLM:\SOFTWARE\AVEVA Solutions Ltd\E3D',
        'HKLM:\SOFTWARE\WOW6432Node\AVEVA Solutions Ltd\E3D'
    )

    foreach ($root in $e3DRoots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }

        foreach ($versionKey in Get-ChildItem -LiteralPath $root) {
            $directoriesKey = Join-Path $versionKey.PSPath 'Directories'
            if (-not (Test-Path -LiteralPath $directoriesKey)) { continue }

            $properties = Get-ItemProperty -LiteralPath $directoriesKey
            if ($properties.AVEVA_DESIGN_EXE) {
                $results += [pscustomobject]@{
                    Version = $versionKey.PSChildName
                    Path = [string]$properties.AVEVA_DESIGN_EXE
                    RegistryKey = $directoriesKey
                }
            }
        }
    }

    $validResults = $results |
        Where-Object { Test-Path -LiteralPath $_.Path } |
        Sort-Object -Property @{ Expression = { Get-VersionSortKey $_.Version }; Descending = $true }, Path -Unique

    return $validResults | Select-Object -First 1
}

function Find-EvarsInit {
    param([Parameter(Mandatory = $true)][string] $InstallDir)

    # The standard E3D filename is evars.init. The other names accommodate site variants.
    foreach ($name in @('evars.init', 'Evars.Init', 'Evar.Init')) {
        $candidate = Join-Path $InstallDir $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Get-Item -LiteralPath $candidate).FullName
        }
    }

    throw "No evars.init file was found directly under '$InstallDir'. Pass -EvarsInitPath explicitly."
}

function Add-PathToSetLine {
    param(
        [Parameter(Mandatory = $true)][string] $Text,
        [Parameter(Mandatory = $true)][string] $Variable,
        [Parameter(Mandatory = $true)][string] $PathToAdd
    )

    $pattern = '(?im)^(?<prefix>\s*set\s+' + [Regex]::Escape($Variable) + '\s*=\s*)(?<value>[^\r\n]*)'
    $match = [Regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw "Could not find a 'set $Variable=...' line in evars.init."
    }

    $value = $match.Groups['value'].Value.TrimEnd()
    $wanted = $PathToAdd.TrimEnd('\', '/')
    $alreadyPresent = $false
    foreach ($part in $value.Split(';')) {
        if ($part.Trim().TrimEnd('\', '/') -ieq $wanted) {
            $alreadyPresent = $true
            break
        }
    }

    if ($alreadyPresent) { return $Text }

    $separator = if ([string]::IsNullOrWhiteSpace($value) -or $value.EndsWith(';')) { '' } else { ';' }
    $replacement = $match.Groups['prefix'].Value + $value + $separator + $PathToAdd
    return $Text.Substring(0, $match.Index) + $replacement + $Text.Substring($match.Index + $match.Length)
}

$ProjectRoot = Get-FullPath $ProjectRoot
$pmlUiPath = Join-Path $ProjectRoot 'PMLUI'
$pmlLibPath = Join-Path $ProjectRoot 'PMLLIB'

foreach ($requiredPath in @($pmlUiPath, $pmlLibPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) {
        throw "Required project directory does not exist: $requiredPath"
    }
}

$registryMatch = $null
if ([string]::IsNullOrWhiteSpace($EvarsInitPath)) {
    if ([string]::IsNullOrWhiteSpace($E3DInstallDir)) {
        $registryMatch = Find-E3DInstall
        if ($null -eq $registryMatch) {
            throw 'AVEVA Everything3D was not found in the registry. Pass -E3DInstallDir or -EvarsInitPath.'
        }
        $E3DInstallDir = $registryMatch.Path
    }

    $E3DInstallDir = Get-FullPath $E3DInstallDir
    $EvarsInitPath = Find-EvarsInit $E3DInstallDir
}

$EvarsInitPath = Get-FullPath $EvarsInitPath
if (-not (Test-Path -LiteralPath $EvarsInitPath -PathType Leaf)) {
    throw "evars.init does not exist: $EvarsInitPath"
}

$reader = New-Object System.IO.StreamReader($EvarsInitPath, $true)
try {
    $originalText = $reader.ReadToEnd()
    $encoding = $reader.CurrentEncoding
}
finally {
    $reader.Dispose()
}

$updatedText = Add-PathToSetLine -Text $originalText -Variable 'PMLUI' -PathToAdd $pmlUiPath
$updatedText = Add-PathToSetLine -Text $updatedText -Variable 'PMLLIB' -PathToAdd $pmlLibPath

if ($updatedText -ceq $originalText) {
    Write-Host "Already configured: $EvarsInitPath"
}
elseif ($PSCmdlet.ShouldProcess($EvarsInitPath, "append YuzuhaToolkit PMLUI and PMLLIB paths")) {
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupPath = "$EvarsInitPath.yuzuha-$timestamp.bak"
    Copy-Item -LiteralPath $EvarsInitPath -Destination $backupPath
    try {
        [System.IO.File]::WriteAllText($EvarsInitPath, $updatedText, $encoding)
    }
    catch [System.UnauthorizedAccessException] {
        throw "Cannot write '$EvarsInitPath'. Re-run PowerShell as Administrator. Backup: $backupPath"
    }

    Write-Host "Configured: $EvarsInitPath"
    Write-Host "Backup:     $backupPath"
}

if ($registryMatch) {
    Write-Host "E3D registry key: $($registryMatch.RegistryKey)"
}
Write-Host "Project root: $ProjectRoot"
Write-Host "PMLUI:       $pmlUiPath"
Write-Host "PMLLIB:      $pmlLibPath"

$net48Runtime = Join-Path $ProjectRoot 'runtime\net48\YuzuhaToolkit.PmlHost.Net48.dll'
$aotRuntime = Join-Path $ProjectRoot 'runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe'
$aotBuildOutput = Join-Path $ProjectRoot 'artifacts\publish\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe'

if (-not (Test-Path -LiteralPath $net48Runtime -PathType Leaf)) {
    Write-Warning "Net48 PML host is not staged at '$net48Runtime'. Copy the distributable Net48 files to runtime\net48 before starting E3D."
}

if (Test-Path -LiteralPath $aotRuntime -PathType Leaf) {
    Write-Host "Native AOT MCP: $aotRuntime"
}
elseif (Test-Path -LiteralPath $aotBuildOutput -PathType Leaf) {
    Write-Host "Native AOT MCP (development output): $aotBuildOutput"
}
else {
    Write-Warning 'Native AOT MCP executable was not found. Publish it before registering the MCP server.'
}

