[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Install', 'Update', 'Uninstall')]
    [string] $Action,

    [string] $InstallRoot,

    [string] $CodexRoot,

    [string] $McpName = 'YuzuhaToolkit',

    [string] $KnowledgeMcpName = 'YuzuhaToolkitKnowledge',

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.]{0,31}$')]
    [string] $AvevaProfile,

    [string] $EvarBat,

    [string] $BootstrapFolderToken,

    [switch] $SkipMcpRegistration,

    [switch] $AdoptLegacyInstallation
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$packageId = 'YuzuhaToolkit.Agent'
$packageVersion = '0.3.0'
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
        [Parameter(Mandatory = $true)][string] $State,
        [string] $BootstrapFolderToken = 'PMLTRI'
    )

    $marker = [ordered]@{
        schema = 1
        packageId = $packageId
        version = $packageVersion
        installId = $InstallId
        state = $State
        installRoot = $InstallRoot
        bootstrapFolderToken = $BootstrapFolderToken
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $marker | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Resolve-BootstrapFolderToken {
    param([Parameter(Mandatory = $true)][string] $LeafName)

    $upper = $LeafName.ToUpperInvariant()
    if ($upper.Contains('PMLTRI')) {
        # The PML bootstrap default already matches both the long name
        # (PmlTrigger.Yuzuha) and its Windows 8.3 short form (PMLTRI~1).
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($BootstrapFolderToken)) {
        $explicit = $BootstrapFolderToken.Trim().ToUpperInvariant()
        if ($explicit -notmatch '^[A-Z0-9]{1,12}$') {
            throw "BootstrapFolderToken may contain only 1-12 letters and digits: '$explicit'"
        }
        return $explicit
    }

    # Windows 8.3 short names keep at most the first six characters before
    # '~1', so a longer token stops matching when the EVAR carries the
    # short form of a long path.
    $compact = $upper -replace '[^A-Z0-9]', ''
    if ([string]::IsNullOrEmpty($compact)) {
        throw @"
Install folder '$LeafName' does not contain 'PmlTrigger' and no token can be derived from it. The PML bootstrap matches the installation folder by name and would never resolve the runtime. Either install into a folder whose name contains 'PmlTrigger' (preferred), or pass -BootstrapFolderToken with 1-12 letters or digits from the folder name.
"@
    }
    return $compact.Substring(0, [Math]::Min(6, $compact.Length))
}

function Update-BootstrapFolderToken {
    param(
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $Token
    )

    $bootstrapPath = Join-Path $StageRoot 'PMLLIB\Bootstrap\YuzuhaResolveRuntimePath.pmlfnc'
    if (-not (Test-Path -LiteralPath $bootstrapPath -PathType Leaf)) {
        throw "PML bootstrap is missing from the staged payload: $bootstrapPath"
    }
    $text = [System.IO.File]::ReadAllText($bootstrapPath)
    $pattern = '(?m)^(?<indent>[ \t]*)!folderName[ \t]*=[ \t]*''[^'']*'''
    $regex = [System.Text.RegularExpressions.Regex]::new($pattern)
    $updated = $regex.Replace(
        $text,
        { param($match) $match.Groups['indent'].Value + "!folderName = '$Token'" },
        1)
    if ($updated -eq $text) {
        throw "Could not rewrite !folderName in $bootstrapPath; the payload layout changed."
    }
    [System.IO.File]::WriteAllText($bootstrapPath, $updated)
    Write-Warning @"
Install folder '$installLeafName' does not contain 'PmlTrigger'. The installer rewrote !folderName = '$Token' in PMLLIB\Bootstrap\YuzuhaResolveRuntimePath.pmlfnc. Risks: (1) '$Token' matches ANY PMLUI path containing that substring, so a lookalike folder can hijack the runtime resolution; (2) a token longer than six characters stops matching when Windows shortens the path to its 8.3 form; (3) future installs/updates into this folder must keep using the same token. Keeping 'PmlTrigger' in the folder name avoids all of these.
"@
}

$installLeafName = Split-Path -Leaf $InstallRoot
$resolvedBootstrapToken = Resolve-BootstrapFolderToken -LeafName $installLeafName
$markerBootstrapToken = if ($resolvedBootstrapToken) { $resolvedBootstrapToken } else { 'PMLTRI' }

function Get-McpConfigurations {
    $json = & codex mcp list --json
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot inspect Codex MCP configurations (exit code $LASTEXITCODE)."
    }
    try {
        $parsedConfigurations = ConvertFrom-Json -InputObject ($json -join "`n")
        return $parsedConfigurations
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

    $configurations = @(Get-McpConfigurations)
    $managedNames = @($McpName)
    $knowledgeExecutable = Join-Path $InstallRoot 'runtime\net10\YuzuhaToolkit.Knowledge.exe'
    $managedNames += $KnowledgeMcpName

    if ($ForUninstall) {
        $states = @()
        foreach ($managedName in $managedNames) {
            $named = @($configurations | Where-Object { $_.name -eq $managedName }) |
                Select-Object -First 1
            if ($null -eq $named) {
                $states += [pscustomobject]@{
                    Name = $managedName
                    Status = 'Missing'
                    Configuration = $null
                }
                continue
            }
            $namedPath = Get-NormalizedCommandPath ([string] $named.transport.command)
            if (-not [string]::Equals(
                    $namedPath,
                    $installedMcpPath,
                    [System.StringComparison]::OrdinalIgnoreCase) -and
                -not $namedPath.StartsWith(
                    $InstallRoot + '\',
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                $states += [pscustomobject]@{
                    Name = $managedName
                    Status = 'Conflict'
                    Configuration = $named
                }
                continue
            }
            $states += [pscustomobject]@{
                Name = $managedName
                Status = 'Managed'
                Configuration = $named
            }
        }

        $foreignPathUsers = @($configurations | Where-Object {
                $candidatePath = Get-NormalizedCommandPath ([string] $_.transport.command)
                $candidatePath.StartsWith(
                    $InstallRoot + '\',
                    [System.StringComparison]::OrdinalIgnoreCase) -and
                $_.name -notin $managedNames
            })
        if ($foreignPathUsers.Count -gt 0) {
            $names = ($foreignPathUsers.name -join ', ')
            throw "Cannot uninstall files: MCP entries still use them under other names: $names"
        }
        return [pscustomobject]@{ Status = 'Multiple'; States = $states }
    }

    # Validate both requested registrations before staging or touching files.
    $preflightArguments = @{
        McpExecutable = $installedMcpPath
        Name = $McpName
        KnowledgeExecutable = $knowledgeExecutable
        KnowledgeName = $KnowledgeMcpName
        ToolkitRoot = $(if ($Action -eq 'Update') { $InstallRoot } else { $sourceRoot })
        CheckOnly = $true
    }
    if ($AvevaProfile) { $preflightArguments.AvevaProfile = $AvevaProfile; $preflightArguments.EvarBat = $EvarBat }
    # Legacy adoption is checked below; normal installs use full shared preflight.
    if (-not $AdoptLegacyInstallation) {
        & (Join-Path $sourceRoot 'scripts\Register-YuzuhaMcp.ps1') @preflightArguments
    }
    $named = @($configurations | Where-Object { $_.name -eq $McpName }) | Select-Object -First 1
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
            'runtime\net10\YuzuhaToolkit.Mcp.exe',
            'runtime\net10\YuzuhaToolkit.Knowledge.exe')) {
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
        # 'knowledge' is skipped so a locally built database (AVEVA-derived
        # content) can never be copied into a package or another install.
        if ($item.Name -in @('.git', 'artifacts', 'src', 'tests', 'knowledge', 'trust')) {
            continue
        }
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function Copy-LocalState {
    param([string] $Destination)
    foreach ($name in @('knowledge', 'trust')) {
        $old = Join-Path $InstallRoot $name
        if (Test-Path -LiteralPath $old) {
            Copy-Item -LiteralPath $old -Destination $Destination -Recurse -Force
        }
    }
    $oldProfiles = Join-Path $InstallRoot 'runtime\profiles'
    if (Test-Path -LiteralPath $oldProfiles) {
        foreach ($profile in Get-ChildItem -LiteralPath $oldProfiles -Directory) {
            $target = Join-Path $Destination ('runtime\profiles\' + $profile.Name)
            if (-not (Test-Path -LiteralPath $target)) {
                New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
                Copy-Item -LiteralPath $profile.FullName -Destination $target -Recurse
            }
        }
    }
}

function Update-ProjectKnowledge {
    $executable = Join-Path $InstallRoot 'runtime\net10\YuzuhaToolkit.Knowledge.exe'
    # Use this installation's local state even when the caller has an override.
    $previousDirectory = $env:YUZUHA_KNOWLEDGE_DIR
    try {
        $env:YUZUHA_KNOWLEDGE_DIR = Join-Path $InstallRoot 'knowledge'
        & $executable --refresh-project $InstallRoot
        if ($LASTEXITCODE -ne 0) { throw "Project knowledge refresh failed: $LASTEXITCODE" }
    }
    finally { $env:YUZUHA_KNOWLEDGE_DIR = $previousDirectory }
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
    $knowledgeExecutable = Join-Path $InstallRoot 'runtime\net10\YuzuhaToolkit.Knowledge.exe'
    if (Test-Path -LiteralPath $knowledgeExecutable -PathType Leaf) {
        $arguments.KnowledgeExecutable = $knowledgeExecutable
        $arguments.KnowledgeName = $KnowledgeMcpName
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
foreach ($otherRoot in @($sourceRoot, $CodexRoot)) {
    if ($InstallRoot.StartsWith($otherRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $otherRoot.StartsWith($InstallRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'InstallRoot must not contain or be inside the setup package or Codex root.'
    }
}

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

    $installMoved = $false
    $skillMoved = $false
    try {
        Copy-PackagePayload -Destination $installStage
        if ($resolvedBootstrapToken) {
            Update-BootstrapFolderToken -StageRoot $installStage -Token $resolvedBootstrapToken
        }
        Write-ManagedMarker -Path (Join-Path $installStage $markerName) `
            -InstallId $installId -State 'installing' `
            -BootstrapFolderToken $markerBootstrapToken
        New-SkillStage -Destination $skillStage -InstallId $installId
        if ($PSCmdlet.ShouldProcess($InstallRoot, 'install Yuzuha Agent package and Skill')) {
            Move-Item -LiteralPath $installStage -Destination $InstallRoot
            $installMoved = $true
            Move-Item -LiteralPath $skillStage -Destination $skillRoot
            $skillMoved = $true
            Update-ProjectKnowledge
            Write-ManagedMarker -Path $installMarkerPath -InstallId $installId -State 'installed' `
                -BootstrapFolderToken $markerBootstrapToken
            Invoke-McpRegistration
            Write-Host "Installed Yuzuha Agent package: $InstallRoot"
            Write-Host "Installed Skill: $skillRoot"
        }
    }
    catch {
        if ($_.Exception.Message -like '*Rollback incomplete*') { throw }
        if ($skillMoved) { Remove-Item -LiteralPath $skillRoot -Recurse -Force }
        if ($installMoved) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
        throw
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

    try {
        Copy-PackagePayload -Destination $installStage
        Copy-LocalState -Destination $installStage
        if ($resolvedBootstrapToken) {
            Update-BootstrapFolderToken -StageRoot $installStage -Token $resolvedBootstrapToken
        }
        Write-ManagedMarker -Path (Join-Path $installStage $markerName) `
            -InstallId $installMarker.installId -State 'updating' `
            -BootstrapFolderToken $markerBootstrapToken
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
            Update-ProjectKnowledge
            Write-ManagedMarker -Path $installMarkerPath `
                -InstallId $installMarker.installId -State 'installed' `
                -BootstrapFolderToken $markerBootstrapToken
            Invoke-McpRegistration
            Write-Host "Updated Yuzuha Agent package: $InstallRoot"
        }
    }
    catch {
        if ($_.Exception.Message -like '*Rollback incomplete*') { throw }
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
    if ($mcpState.Status -eq 'Multiple') {
        foreach ($state in $mcpState.States) {
            if ($state.Status -eq 'Managed') {
                if ($PSCmdlet.ShouldProcess(
                        $state.Name,
                        'remove managed Yuzuha MCP registration')) {
                    & codex mcp remove $state.Name
                    if ($LASTEXITCODE -ne 0) {
                        throw "codex mcp remove failed with exit code $LASTEXITCODE. Files were retained."
                    }
                }
            }
            elseif ($state.Status -eq 'Conflict') {
                Write-Warning "MCP '$($state.Name)' points elsewhere and was left unchanged."
            }
            else {
                Write-Host "MCP registration not present: $($state.Name)"
            }
        }
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
