[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $InstallRoot = (Join-Path $env:LOCALAPPDATA 'YuzuhaToolkit\PmlTrigger.Yuzuha'),
    [string] $E3DInstallDir,
    [string] $EvarsInitPath,
    [switch] $SkipE3DConfiguration,
    [switch] $RegisterCodex,
    [switch] $InstallCodexSkill,
    [switch] $Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string] $Path)
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

$releaseRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$payloadRoot = Join-Path $releaseRoot 'payload\PmlTrigger.Yuzuha'
$configureScript = Join-Path $releaseRoot 'scripts\Configure-YuzuhaE3D.ps1'
$InstallRoot = Get-FullPath $InstallRoot
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$installBackup = $null

$requiredPayload = @(
    (Join-Path $payloadRoot 'PMLUI'),
    (Join-Path $payloadRoot 'PMLLIB'),
    (Join-Path $payloadRoot 'runtime\net48\YuzuhaToolkit.PmlHost.Net48.dll'),
    (Join-Path $payloadRoot 'runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe')
)
foreach ($path in $requiredPayload) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Release payload is incomplete: $path"
    }
}

if (Test-Path -LiteralPath $InstallRoot) {
    if (-not $Force) {
        throw "Install directory already exists: $InstallRoot. Use -Force only after preserving local PML customizations."
    }

    $installParent = Split-Path -Parent $InstallRoot
    $backupRoot = Join-Path $installParent 'backup'
    $installBackup = Join-Path $backupRoot ((Split-Path -Leaf $InstallRoot) + '-' + $timestamp)
    if ($PSCmdlet.ShouldProcess($InstallRoot, "back up the current installation to $installBackup and replace it")) {
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        Move-Item -LiteralPath $InstallRoot -Destination $installBackup
        Copy-Item -LiteralPath $payloadRoot -Destination $InstallRoot -Recurse
    }
}
elseif ($PSCmdlet.ShouldProcess($InstallRoot, 'copy the YuzuhaToolkit release payload')) {
    $parent = Split-Path -Parent $InstallRoot
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $payloadRoot -Destination $InstallRoot -Recurse
}

$mcpExe = Join-Path $InstallRoot 'runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe'

if (-not $SkipE3DConfiguration) {
    $configureArgs = @{ ProjectRoot = $InstallRoot }
    if (-not [string]::IsNullOrWhiteSpace($E3DInstallDir)) {
        $configureArgs.E3DInstallDir = $E3DInstallDir
    }
    if (-not [string]::IsNullOrWhiteSpace($EvarsInitPath)) {
        $configureArgs.EvarsInitPath = $EvarsInitPath
    }
    & $configureScript @configureArgs -WhatIf:$WhatIfPreference
}

if ($InstallCodexSkill) {
    $codexHome = if ([string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        Join-Path $env:USERPROFILE '.codex'
    } else {
        $env:CODEX_HOME
    }
    $skillSource = Join-Path $InstallRoot 'skill'
    $skillTarget = Join-Path $codexHome 'skills\yuzuha-toolkit'

    if ((Test-Path -LiteralPath $skillTarget) -and -not $Force) {
        Write-Warning "Codex skill already exists and was not replaced: $skillTarget"
    }
    elseif ($PSCmdlet.ShouldProcess($skillTarget, 'install the yuzuha-toolkit skill')) {
        if (Test-Path -LiteralPath $skillTarget) {
            $skillBackupRoot = Join-Path $codexHome 'backups\skills'
            $skillBackup = Join-Path $skillBackupRoot ('yuzuha-toolkit-' + $timestamp)
            New-Item -ItemType Directory -Path $skillBackupRoot -Force | Out-Null
            Move-Item -LiteralPath $skillTarget -Destination $skillBackup
            Write-Host "Previous Codex Skill backup: $skillBackup"
        }
        New-Item -ItemType Directory -Path $skillTarget -Force | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $skillSource -Force) {
            Copy-Item -LiteralPath $item.FullName -Destination $skillTarget -Recurse -Force
        }
    }
}

if ($RegisterCodex) {
    $codex = Get-Command codex -ErrorAction SilentlyContinue
    if ($null -eq $codex) {
        Write-Warning 'Codex CLI was not found. MCP registration was skipped.'
    }
    else {
        & $codex.Source mcp get YuzuhaToolkit *> $null
        $alreadyRegistered = $LASTEXITCODE -eq 0
        if ($alreadyRegistered) {
            Write-Warning 'Codex MCP entry YuzuhaToolkit already exists and was not replaced.'
        }
        elseif ($PSCmdlet.ShouldProcess('Codex MCP configuration', "register YuzuhaToolkit -> $mcpExe")) {
            & $codex.Source mcp add YuzuhaToolkit -- $mcpExe
            if ($LASTEXITCODE -ne 0) {
                throw "Codex MCP registration failed with exit code $LASTEXITCODE."
            }
        }
    }
}

$info = [ordered]@{
    version = '0.1.0-preview.6'
    installedAtUtc = [DateTime]::UtcNow.ToString('o')
    installRoot = $InstallRoot
    mcpExecutable = $mcpExe
    pipeName = 'yuzuha.pml.command.v1'
}
if (-not $WhatIfPreference) {
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $InstallRoot 'install-info.json') -Encoding UTF8
}

Write-Host ''
Write-Host 'YuzuhaToolkit files are ready.'
Write-Host "Install root: $InstallRoot"
Write-Host "Native AOT MCP: $mcpExe"
if ($null -ne $installBackup) {
    Write-Host "Previous installation backup: $installBackup"
}
Write-Host 'Next: restart E3D, verify !!YuzuhaRpcHost.GetRpcServerStatus() returns RUNNING, then call get_connection_status.'
if (-not $RegisterCodex) {
    Write-Host "Optional Codex registration: codex mcp add YuzuhaToolkit -- `"$mcpExe`""
}
