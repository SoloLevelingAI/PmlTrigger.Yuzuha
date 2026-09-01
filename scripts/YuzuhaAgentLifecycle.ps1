[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Install', 'Update', 'Uninstall')]
    [string] $Action,

    [string] $InstallRoot,

    [string] $CodexRoot,

    [string] $McpName = 'YuzuhaToolkit',

    [ValidateSet('AM', 'PDMS')]
    [string] $AvevaProfile,

    [string] $EvarBat,

    [switch] $SkipMcpRegistration,

    [switch] $AdoptLegacyInstallation
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$packageId = 'YuzuhaToolkit.Agent'
$packageVersion = '0.2.0'
$markerName = '.yuzuha-agent-managed.json'
$sourceRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')

if ([string]::IsNullOrWhiteSpace($CodexRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        $CodexRoot = $env:CODEX_HOME
    }
    else {
        $CodexRoot = Join-Path $env:USERPROFILE '.codex'
    }
}
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $env:LOCALAPPDATA 'YuzuhaToolkit\PmlTrigger.Yuzuha'
}

$CodexRoot = [System.IO.Path]::GetFullPath($CodexRoot).TrimEnd('\')
$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$skillRoot = Join-Path $CodexRoot 'skills\yuzuha-toolkit'
$installMarkerPath = Join-Path $InstallRoot $markerName
$skillMarkerPath = Join-Path $skillRoot $markerName
$installedMcpPath = Join-Path $InstallRoot 'runtime\net10\YuzuhaToolkit.Mcp.exe'

function Assert-SafeManagedRoot {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $driveRoot = [System.IO.Path]::GetPathRoot($fullPath).TrimEnd('\')
    if ([string]::Equals(
            $fullPath,
            $driveRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label cannot be a drive root: $fullPath"
    }
    if ([string]::Equals(
            $fullPath,
            $CodexRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label cannot be the Codex root: $fullPath"
    }
    if ([string]::Equals(
            $fullPath,
            $sourceRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label cannot be the extracted setup package: $fullPath"
    }
}

function Read-ManagedMarker {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label is not managed by the Yuzuha Agent installer; marker missing: $Path"
    }
    try {
        $marker = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        throw "$Label marker is invalid: $Path"
    }
    if ($marker.packageId -ne $packageId) {
        throw "$Label marker belongs to another package: $Path"
    }
    return $marker
}

function Write-ManagedMarker {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $InstallId,
        [Parameter(Mandatory = $true)][string] $State
    )

    $marker = [ordered]@{
        schema = 1
        packageId = $packageId
        version = $packageVersion
        installId = $InstallId
        state = $State
        installRoot = $InstallRoot
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $marker | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-McpConfigurations {
    $json = & codex mcp list --json
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot inspect Codex MCP configurations (exit code $LASTEXITCODE)."
    }
    try {
        return @($json | ConvertFrom-Json)
    }
    catch {
        throw "Cannot parse Codex MCP configurations: $($_.Exception.Message)"
    }
}

function Get-NormalizedCommandPath {
    param([string] $Command)

    try {
        return [System.IO.Path]::GetFullPath($Command)
    }
    catch {
        return $Command
    }
}

function Get-EvarEncoding {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)

    if ($Bytes.Length -ge 3 -and
        $Bytes[0] -eq 0xEF -and
        $Bytes[1] -eq 0xBB -and
        $Bytes[2] -eq 0xBF) {
        return [System.Text.UTF8Encoding]::new($true)
    }
    if ($Bytes.Length -ge 2 -and
        $Bytes[0] -eq 0xFF -and
        $Bytes[1] -eq 0xFE) {
        return [System.Text.UnicodeEncoding]::new($false, $true)
    }
    if ($Bytes.Length -ge 2 -and
        $Bytes[0] -eq 0xFE -and
        $Bytes[1] -eq 0xFF) {
        return [System.Text.UnicodeEncoding]::new($true, $true)
    }
    try {
        $providerType = [System.Type]::GetType(
            'System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages')
        if ($null -ne $providerType) {
            $instance = $providerType.GetProperty('Instance').GetValue($null)
            [System.Text.Encoding]::RegisterProvider($instance)
        }
        $codePage = [System.Globalization.CultureInfo]::CurrentCulture.TextInfo.ANSICodePage
        return [System.Text.Encoding]::GetEncoding($codePage)
    }
    catch {
        return [System.Text.Encoding]::Default
    }
}

function Test-McpPreflight {
    param([switch] $ForUninstall)

    if ($SkipMcpRegistration) {
        return [pscustomobject]@{ Status = 'Skipped'; Configuration = $null }
    }

    $configurations = Get-McpConfigurations
    $named = @($configurations | Where-Object { $_.name -eq $McpName }) |
        Select-Object -First 1
    $pathUsers = @($configurations | Where-Object {
            $candidatePath = Get-NormalizedCommandPath ([string] $_.transport.command)
            [string]::Equals(
                $candidatePath,
                $installedMcpPath,
                [System.StringComparison]::OrdinalIgnoreCase)
        })

    if ($ForUninstall) {
        $foreignPathUsers = @($pathUsers | Where-Object { $_.name -ne $McpName })
        if ($foreignPathUsers.Count -gt 0) {
            $names = ($foreignPathUsers.name -join ', ')
            throw "Cannot uninstall files: MCP entries still use them under other names: $names"
        }
        if ($null -eq $named) {
            return [pscustomobject]@{ Status = 'Missing'; Configuration = $null }
        }
        $namedPath = Get-NormalizedCommandPath ([string] $named.transport.command)
        if (-not [string]::Equals(
                $namedPath,
                $installedMcpPath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject]@{ Status = 'Conflict'; Configuration = $named }
        }
        return [pscustomobject]@{ Status = 'Managed'; Configuration = $named }
    }

    if ($null -ne $named) {
        $namedPath = Get-NormalizedCommandPath ([string] $named.transport.command)
        $args = @($named.transport.args)
        $matches = (
            $named.enabled -eq $true -and
            $named.transport.type -eq 'stdio' -and
            $args.Count -eq 0 -and
            [string]::Equals(
                $namedPath,
                $installedMcpPath,
                [System.StringComparison]::OrdinalIgnoreCase))
        if (-not $matches) {
            $isLegacyManagedPath = (
                $AdoptLegacyInstallation -and
                [System.IO.Path]::GetFileName($namedPath) -eq 'YuzuhaToolkit.Mcp.exe' -and
                $namedPath.StartsWith(
                    $InstallRoot + '\',
                    [System.StringComparison]::OrdinalIgnoreCase))
            $legacyDllPath = if ($args.Count -gt 0) {
                Get-NormalizedCommandPath ([string] $args[0])
            }
            else {
                ''
            }
            $isLegacySkillHosted = (
                $AdoptLegacyInstallation -and
                [System.IO.Path]::GetFileName($namedPath) -eq 'dotnet.exe' -and
                [System.IO.Path]::GetFileName($legacyDllPath) -eq 'YuzuhaToolkit.Mcp.dll' -and
                $legacyDllPath.StartsWith(
                    $skillRoot + '\',
                    [System.StringComparison]::OrdinalIgnoreCase))
            if ($isLegacyManagedPath -or $isLegacySkillHosted) {
                return [pscustomobject]@{ Status = 'Legacy'; Configuration = $named }
            }
            throw "MCP '$McpName' exists but does not match the managed executable. Nothing was changed."
        }
        return [pscustomobject]@{ Status = 'Reusable'; Configuration = $named }
    }

    $sameExecutableName = @($configurations | Where-Object {
            $candidate = [string] $_.transport.command
            [System.IO.Path]::GetFileName($candidate) -eq 'YuzuhaToolkit.Mcp.exe'
        })
    if ($sameExecutableName.Count -gt 0) {
        $summary = ($sameExecutableName | ForEach-Object {
                "name=$($_.name); command=$($_.transport.command)"
            }) -join [Environment]::NewLine
        throw "Possible duplicate Yuzuha MCP found:`n$summary`nNothing was changed."
    }
    return [pscustomobject]@{ Status = 'Missing'; Configuration = $null }
}

function Assert-PackagePayload {
    param([Parameter(Mandatory = $true)][string] $Root)

    foreach ($required in @(
            'PMLLIB',
            'PMLUI',
            'skill\SKILL.md',
            'scripts\Register-YuzuhaMcp.ps1',
            'runtime\net10\YuzuhaToolkit.Mcp.exe')) {
        $path = Join-Path $Root $required
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Setup package is incomplete; missing: $path"
        }
    }
}

function Copy-PackagePayload {
    param([Parameter(Mandatory = $true)][string] $Destination)

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($item in Get-ChildItem -Force -LiteralPath $sourceRoot) {
        if ($item.Name -in @('.git', 'artifacts', 'src', 'tests')) {
            continue
        }
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function New-SkillStage {
    param(
        [Parameter(Mandatory = $true)][string] $Destination,
        [Parameter(Mandatory = $true)][string] $InstallId
    )

    Copy-Item -LiteralPath (Join-Path $sourceRoot 'skill') `
        -Destination $Destination -Recurse
    Write-ManagedMarker -Path (Join-Path $Destination $markerName) `
        -InstallId $InstallId -State 'installed'
}

function Invoke-McpRegistration {
    if ($SkipMcpRegistration) {
        Write-Host 'MCP registration skipped by request.'
        return
    }

    $arguments = @{
        McpExecutable = $installedMcpPath
        Name = $McpName
        ToolkitRoot = $InstallRoot
    }
    if (-not [string]::IsNullOrWhiteSpace($AvevaProfile)) {
        $arguments.AvevaProfile = $AvevaProfile
        $arguments.EvarBat = $EvarBat
    }
    & (Join-Path $InstallRoot 'scripts\Register-YuzuhaMcp.ps1') @arguments
}

function Remove-EvarManagedBlock {
    if ([string]::IsNullOrWhiteSpace($EvarBat)) {
        return
    }

    $path = [System.IO.Path]::GetFullPath($EvarBat)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "EVAR file does not exist: $path"
    }
    [byte[]] $bytes = [System.IO.File]::ReadAllBytes($path)
    $encoding = Get-EvarEncoding -Bytes $bytes
    $text = $encoding.GetString($bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
        $text = $text.Substring(1)
    }
    $pattern = '(?ms)^rem >>> Yuzuha managed settings\r?\n.*?^rem <<< Yuzuha managed settings\r?\n?'
    $updated = [regex]::Replace($text, $pattern, '', 1)
    if ($updated -eq $text) {
        Write-Host "No Yuzuha EVAR managed block found: $path"
        return
    }
    if ($PSCmdlet.ShouldProcess($path, 'back up and remove Yuzuha EVAR managed block')) {
        $backup = "$path.yuzuha-uninstall-$(Get-Date -Format 'yyyyMMdd-HHmmss-fff').bak"
        Copy-Item -LiteralPath $path -Destination $backup
        [System.IO.File]::WriteAllText($path, $updated, $encoding)
        Write-Host "EVAR backup: $backup"
    }
}

Assert-SafeManagedRoot -Path $InstallRoot -Label 'InstallRoot'
Assert-SafeManagedRoot -Path $skillRoot -Label 'Skill root'
Assert-PackagePayload -Root $sourceRoot

$hasProfile = -not [string]::IsNullOrWhiteSpace($AvevaProfile)
$hasEvar = -not [string]::IsNullOrWhiteSpace($EvarBat)
if ($Action -ne 'Uninstall' -and $hasProfile -ne $hasEvar) {
    throw 'AvevaProfile and EvarBat must be supplied together.'
}
if ($Action -eq 'Uninstall' -and $SkipMcpRegistration) {
    throw 'SkipMcpRegistration is not allowed during uninstall because it could leave an MCP pointing to deleted files.'
}

if ($Action -eq 'Install') {
    if (Test-Path -LiteralPath $InstallRoot) {
        throw "InstallRoot already exists. Use Update for a managed installation: $InstallRoot"
    }
    if (Test-Path -LiteralPath $skillRoot) {
        throw "Skill destination already exists; it will not be overwritten: $skillRoot"
    }
    $null = Test-McpPreflight

    $installId = [guid]::NewGuid().ToString('D')
    $installParent = Split-Path -Parent $InstallRoot
    $skillParent = Split-Path -Parent $skillRoot
    New-Item -ItemType Directory -Path $installParent -Force | Out-Null
    New-Item -ItemType Directory -Path $skillParent -Force | Out-Null
    $installStage = Join-Path $installParent ('.yuzuha-install-' + [guid]::NewGuid().ToString('N'))
    $skillStage = Join-Path $skillParent ('.yuzuha-skill-' + [guid]::NewGuid().ToString('N'))

    try {
        Copy-PackagePayload -Destination $installStage
        Write-ManagedMarker -Path (Join-Path $installStage $markerName) `
            -InstallId $installId -State 'installing'
        New-SkillStage -Destination $skillStage -InstallId $installId
        if ($PSCmdlet.ShouldProcess($InstallRoot, 'install Yuzuha Agent package and Skill')) {
            Move-Item -LiteralPath $installStage -Destination $InstallRoot
            Move-Item -LiteralPath $skillStage -Destination $skillRoot
            Invoke-McpRegistration
            Write-ManagedMarker -Path $installMarkerPath -InstallId $installId -State 'installed'
            Write-Host "Installed Yuzuha Agent package: $InstallRoot"
            Write-Host "Installed Skill: $skillRoot"
        }
    }
    finally {
        if (Test-Path -LiteralPath $installStage) {
            Remove-Item -LiteralPath $installStage -Recurse -Force
        }
        if (Test-Path -LiteralPath $skillStage) {
            Remove-Item -LiteralPath $skillStage -Recurse -Force
        }
    }
}
elseif ($Action -eq 'Update') {
    if (Test-Path -LiteralPath $installMarkerPath -PathType Leaf) {
        $installMarker = Read-ManagedMarker -Path $installMarkerPath -Label 'InstallRoot'
        $skillMarker = Read-ManagedMarker -Path $skillMarkerPath -Label 'Skill'
        if ($installMarker.installId -ne $skillMarker.installId) {
            throw 'InstallRoot and Skill markers do not belong to the same installation.'
        }
    }
    elseif ($AdoptLegacyInstallation) {
        $legacyInfoPath = Join-Path $InstallRoot 'install-info.json'
        if (-not (Test-Path -LiteralPath $legacyInfoPath -PathType Leaf)) {
            throw "Legacy adoption requires install-info.json: $legacyInfoPath"
        }
        $legacyInfo = Get-Content -Raw -LiteralPath $legacyInfoPath | ConvertFrom-Json
        $legacyRoot = [System.IO.Path]::GetFullPath([string] $legacyInfo.installRoot).TrimEnd('\')
        if (-not [string]::Equals(
                $legacyRoot,
                $InstallRoot,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Legacy install-info.json identifies another root: $legacyRoot"
        }
        $legacySkillFile = Join-Path $skillRoot 'SKILL.md'
        if (-not (Test-Path -LiteralPath $legacySkillFile -PathType Leaf)) {
            throw "Legacy Yuzuha Skill was not found: $legacySkillFile"
        }
        $legacySkillText = Get-Content -Raw -LiteralPath $legacySkillFile
        if ($legacySkillText -notmatch '(?m)^name:\s*yuzuha-toolkit\s*$') {
            throw "Existing Skill is not yuzuha-toolkit and cannot be adopted: $skillRoot"
        }
        $installMarker = [pscustomobject]@{
            installId = [guid]::NewGuid().ToString('D')
        }
        Write-Host "Adopting legacy Yuzuha installation: $InstallRoot"
    }
    else {
        throw "InstallRoot is not managed by the lifecycle installer. Use AdoptLegacyInstallation only for a verified legacy Yuzuha install: $InstallRoot"
    }
    $mcpPreflight = Test-McpPreflight

    $activeManagedProcesses = @(Get-Process | ForEach-Object {
            try {
                if ($_.Path.StartsWith(
                        $InstallRoot + '\',
                        [System.StringComparison]::OrdinalIgnoreCase)) {
                    $_
                }
            }
            catch {
                # Some protected processes do not expose Path; ignore them.
            }
        })
    if ($activeManagedProcesses.Count -gt 0) {
        $processSummary = ($activeManagedProcesses | ForEach-Object {
                "$($_.ProcessName) (PID $($_.Id))"
            }) -join ', '
        throw "Close the running managed Yuzuha processes before update: $processSummary"
    }

    $installParent = Split-Path -Parent $InstallRoot
    $skillParent = Split-Path -Parent $skillRoot
    $suffix = [guid]::NewGuid().ToString('N')
    $installStage = Join-Path $installParent ".yuzuha-update-$suffix"
    $skillStage = Join-Path $skillParent ".yuzuha-skill-update-$suffix"
    $installBackup = "$InstallRoot.backup-$suffix"
    $skillBackup = "$skillRoot.backup-$suffix"
    $installBackedUp = $false
    $installSwapped = $false
    $skillBackedUp = $false
    $skillSwapped = $false
    $legacyMcpRemoved = $false
    $newMcpAdded = $false

    try {
        Copy-PackagePayload -Destination $installStage
        Write-ManagedMarker -Path (Join-Path $installStage $markerName) `
            -InstallId $installMarker.installId -State 'updating'
        New-SkillStage -Destination $skillStage -InstallId $installMarker.installId
        if ($PSCmdlet.ShouldProcess($InstallRoot, "update Yuzuha Agent package to $packageVersion")) {
            Move-Item -LiteralPath $InstallRoot -Destination $installBackup
            $installBackedUp = $true
            Move-Item -LiteralPath $installStage -Destination $InstallRoot
            $installSwapped = $true
            Move-Item -LiteralPath $skillRoot -Destination $skillBackup
            $skillBackedUp = $true
            Move-Item -LiteralPath $skillStage -Destination $skillRoot
            $skillSwapped = $true
            if ($mcpPreflight.Status -eq 'Legacy') {
                & codex mcp remove $McpName
                if ($LASTEXITCODE -ne 0) {
                    throw "Cannot remove the verified legacy MCP '$McpName' during update."
                }
                $legacyMcpRemoved = $true
            }
            Invoke-McpRegistration
            $newMcpAdded = (
                -not $SkipMcpRegistration -and
                $mcpPreflight.Status -in @('Missing', 'Legacy'))
            Write-ManagedMarker -Path $installMarkerPath `
                -InstallId $installMarker.installId -State 'installed'
            Write-Host "Updated Yuzuha Agent package: $InstallRoot"
        }
    }
    catch {
        if ($newMcpAdded) {
            try {
                & codex mcp remove $McpName
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "Could not remove the new MCP while rolling back: $McpName"
                }
            }
            catch {
                Write-Warning "Could not remove the new MCP while rolling back: $($_.Exception.Message)"
            }
        }
        if ($legacyMcpRemoved) {
            $legacyCommand = [string] $mcpPreflight.Configuration.transport.command
            $legacyArguments = @($mcpPreflight.Configuration.transport.args)
            try {
                & codex mcp add $McpName -- $legacyCommand @legacyArguments
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "Could not restore the legacy MCP while rolling back: $McpName"
                }
            }
            catch {
                Write-Warning "Could not restore the legacy MCP while rolling back: $($_.Exception.Message)"
            }
        }
        if (Test-Path -LiteralPath $skillBackup) {
            if ($skillBackedUp) {
                if ($skillSwapped -and (Test-Path -LiteralPath $skillRoot)) {
                    Remove-Item -LiteralPath $skillRoot -Recurse -Force
                }
                Move-Item -LiteralPath $skillBackup -Destination $skillRoot
            }
            else {
                New-Item -ItemType Directory -Path $skillRoot -Force | Out-Null
                Get-ChildItem -LiteralPath $skillBackup -Force | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination $skillRoot -Recurse -Force
                }
            }
        }
        if (Test-Path -LiteralPath $installBackup) {
            if ($installBackedUp) {
                if ($installSwapped -and (Test-Path -LiteralPath $InstallRoot)) {
                    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
                }
                Move-Item -LiteralPath $installBackup -Destination $InstallRoot
            }
            else {
                New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
                Get-ChildItem -LiteralPath $installBackup -Force | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination $InstallRoot -Recurse -Force
                }
            }
        }
        throw
    }
    finally {
        foreach ($temporaryPath in @($installStage, $skillStage)) {
            if (Test-Path -LiteralPath $temporaryPath) {
                Remove-Item -LiteralPath $temporaryPath -Recurse -Force
            }
        }
    }
    foreach ($backupPath in @($installBackup, $skillBackup)) {
        if (Test-Path -LiteralPath $backupPath) {
            try {
                Remove-Item -LiteralPath $backupPath -Recurse -Force
            }
            catch {
                Write-Warning "Old managed backup is still in use and was retained: $backupPath"
            }
        }
    }
}
else {
    $installMarker = Read-ManagedMarker -Path $installMarkerPath -Label 'InstallRoot'
    $mcpState = Test-McpPreflight -ForUninstall
    if ($mcpState.Status -eq 'Managed') {
        if ($PSCmdlet.ShouldProcess($McpName, 'remove managed Yuzuha MCP registration')) {
            & codex mcp remove $McpName
            if ($LASTEXITCODE -ne 0) {
                throw "codex mcp remove failed with exit code $LASTEXITCODE. Files were retained."
            }
        }
    }
    elseif ($mcpState.Status -eq 'Conflict') {
        Write-Warning "MCP '$McpName' points elsewhere and was left unchanged."
    }
    elseif ($mcpState.Status -eq 'Missing') {
        Write-Host "MCP registration not present: $McpName"
    }

    Remove-EvarManagedBlock

    if (Test-Path -LiteralPath $skillRoot) {
        try {
            $skillMarker = Read-ManagedMarker -Path $skillMarkerPath -Label 'Skill'
            if ($skillMarker.installId -ne $installMarker.installId) {
                throw 'Skill marker belongs to a different Yuzuha installation.'
            }
            if ($PSCmdlet.ShouldProcess($skillRoot, 'remove managed Yuzuha Skill')) {
                Remove-Item -LiteralPath $skillRoot -Recurse -Force
            }
        }
        catch {
            Write-Warning "Skill was retained: $($_.Exception.Message)"
        }
    }

    if ($PSCmdlet.ShouldProcess($InstallRoot, 'remove managed Yuzuha Agent package')) {
        Remove-Item -LiteralPath $InstallRoot -Recurse -Force
        Write-Host "Uninstalled managed Yuzuha Agent package: $InstallRoot"
    }
}
