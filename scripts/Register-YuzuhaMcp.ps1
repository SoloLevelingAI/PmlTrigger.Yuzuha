[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $McpExecutable,

    [string] $Name = 'YuzuhaToolkit',

    [ValidateSet('AM', 'PDMS')]
    [string] $AvevaProfile,

    [string] $EvarBat,

    [string] $ToolkitRoot = $(Split-Path -Parent $PSScriptRoot),

    [switch] $SkipMcpRegistration
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

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

function Get-UpdatedEvarText {
    param(
        [Parameter(Mandatory = $true)][string] $Text,
        [Parameter(Mandatory = $true)][string] $Profile,
        [Parameter(Mandatory = $true)][string] $Root
    )

    $newLine = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $managedStart = 'rem >>> Yuzuha managed settings'
    $managedEnd = 'rem <<< Yuzuha managed settings'
    $pmlLib = Join-Path $Root 'PMLLIB'
    $pmlUi = Join-Path $Root 'PMLUI'
    $managedBlock = @(
        $managedStart,
        'rem Custom variable name must remain Yuzuha (no underscore).',
        "set `"Yuzuha=$Profile`"",
        "set `"pmllib=$pmlLib;%pmllib%`"",
        "set `"pdmsui=$pmlUi;%pdmsui%`"",
        $managedEnd
    ) -join $newLine
    $managedBlock += $newLine

    $blockPattern = '(?ms)^' +
        [System.Text.RegularExpressions.Regex]::Escape($managedStart) +
        '\r?\n.*?^' +
        [System.Text.RegularExpressions.Regex]::Escape($managedEnd) +
        '(?:\r?\n)?'
    if ([System.Text.RegularExpressions.Regex]::IsMatch($Text, $blockPattern)) {
        $blockRegex = [System.Text.RegularExpressions.Regex]::new($blockPattern)
        return $blockRegex.Replace(
            $Text,
            [System.Text.RegularExpressions.MatchEvaluator] {
                param($match)
                return $managedBlock
            },
            1)
    }

    $echoPattern = '(?im)^[ \t]*@echo[ \t]+off[ \t]*(?:\r?\n|$)'
    $echoMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Text,
        $echoPattern)
    if ($echoMatch.Success) {
        return $Text.Insert(
            $echoMatch.Index + $echoMatch.Length,
            $newLine + $managedBlock)
    }

    return $managedBlock + $newLine + $Text
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

$mcpPath = $null
$mcpAlreadyConfigured = $false
if (-not $SkipMcpRegistration) {
    if ([string]::IsNullOrWhiteSpace($McpExecutable)) {
        throw 'McpExecutable is required unless SkipMcpRegistration is specified.'
    }
    $mcpPath = [System.IO.Path]::GetFullPath($McpExecutable)
    if (-not (Test-Path -LiteralPath $mcpPath -PathType Leaf)) {
        throw "Yuzuha MCP executable does not exist: $mcpPath"
    }

    $mcpListJson = & codex mcp list --json
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot inspect existing Codex MCP configurations (exit code $LASTEXITCODE)."
    }
    try {
        $mcpConfigurations = @($mcpListJson | ConvertFrom-Json)
    }
    catch {
        throw "Cannot parse existing Codex MCP configurations: $($_.Exception.Message)"
    }

    $existing = @($mcpConfigurations | Where-Object { $_.name -eq $Name }) |
        Select-Object -First 1
    if ($null -ne $existing) {
        $transport = $existing.transport
        $existingCommand = [string] $transport.command
        $existingArguments = @($transport.args)
        $existingCommandPath = Get-NormalizedCommandPath $existingCommand
        $sameCommand = [string]::Equals(
            $existingCommandPath,
            $mcpPath,
            [System.StringComparison]::OrdinalIgnoreCase)
        $sameRegistration = (
            $transport.type -eq 'stdio' -and
            $sameCommand -and
            $existingArguments.Count -eq 0 -and
            $existing.enabled -eq $true)

        if ($sameRegistration) {
            $mcpAlreadyConfigured = $true
            Write-Host "MCP already registered correctly; unchanged: $Name"
        }
        else {
            $argumentText = if ($existingArguments.Count -eq 0) {
                '<none>'
            }
            else {
                $existingArguments -join ' '
            }
            throw @"
Codex MCP configuration '$Name' already exists but does not match this package.
Existing: enabled=$($existing.enabled); type=$($transport.type); command=$existingCommand; args=$argumentText
Expected: enabled=True; type=stdio; command=$mcpPath; args=<none>
Nothing was changed. Inspect it with: codex mcp get $Name --json
Remove it explicitly only if replacement is intended: codex mcp remove $Name
"@
        }
    }
    else {
        $desiredFileName = [System.IO.Path]::GetFileName($mcpPath)
        $possibleDuplicates = @($mcpConfigurations | Where-Object {
                $candidateCommand = [string] $_.transport.command
                $candidatePath = Get-NormalizedCommandPath $candidateCommand
                [System.IO.Path]::GetFileName($candidatePath) -eq $desiredFileName
            })
        if ($possibleDuplicates.Count -gt 0) {
            $duplicateSummary = ($possibleDuplicates | ForEach-Object {
                    "name=$($_.name); command=$($_.transport.command)"
                }) -join [Environment]::NewLine
            throw @"
Yuzuha MCP may already be registered under a different name:
$duplicateSummary
Nothing was changed. Resolve the existing entry explicitly before adding '$Name'.
"@
        }
    }
}

$hasProfile = -not [string]::IsNullOrWhiteSpace($AvevaProfile)
$hasEvar = -not [string]::IsNullOrWhiteSpace($EvarBat)
if ($hasProfile -ne $hasEvar) {
    throw 'AvevaProfile and EvarBat must be supplied together for AM/PDMS.'
}

$evarChanged = $false
$evarPreview = $false
if ($hasProfile) {
    $profile = $AvevaProfile.ToUpperInvariant()
    $rootPath = [System.IO.Path]::GetFullPath($ToolkitRoot).TrimEnd('\')
    $evarPath = [System.IO.Path]::GetFullPath($EvarBat)
    if (-not (Test-Path -LiteralPath $evarPath -PathType Leaf)) {
        throw "AVEVA EVAR batch file does not exist: $evarPath"
    }
    foreach ($requiredDirectory in @('PMLLIB', 'PMLUI')) {
        $requiredPath = Join-Path $rootPath $requiredDirectory
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) {
            throw "ToolkitRoot does not contain ${requiredDirectory}: $requiredPath"
        }
    }
    $legacyHost = Join-Path $rootPath (
        "runtime\profiles\$profile\net35\YuzuhaToolkit.PmlHost.Net35.dll")
    if (-not (Test-Path -LiteralPath $legacyHost -PathType Leaf)) {
        throw "ToolkitRoot does not contain the $profile NET35 Host: $legacyHost"
    }
    if ($rootPath.IndexOfAny([char[]]@('"', '%', "`r", "`n")) -ge 0) {
        throw 'ToolkitRoot cannot contain a quote, percent sign, or newline.'
    }

    [byte[]] $evarBytes = [System.IO.File]::ReadAllBytes($evarPath)
    $evarEncoding = Get-EvarEncoding -Bytes $evarBytes
    $encodedRootPath = $evarEncoding.GetString($evarEncoding.GetBytes($rootPath))
    if ($encodedRootPath -ne $rootPath) {
        throw "ToolkitRoot cannot be represented by the EVAR file encoding ($($evarEncoding.EncodingName)): $rootPath"
    }
    $evarText = $evarEncoding.GetString($evarBytes)
    if ($evarText.Length -gt 0 -and $evarText[0] -eq [char]0xFEFF) {
        $evarText = $evarText.Substring(1)
    }
    $updatedEvarText = Get-UpdatedEvarText `
        -Text $evarText `
        -Profile $profile `
        -Root $rootPath

    if ($updatedEvarText -ne $evarText) {
        if ($PSCmdlet.ShouldProcess(
                $evarPath,
                "back up and configure $profile Yuzuha EVAR settings")) {
            $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
            $backupPath = "$evarPath.yuzuha-$timestamp.bak"
            Copy-Item -LiteralPath $evarPath -Destination $backupPath
            [System.IO.File]::WriteAllText(
                $evarPath,
                $updatedEvarText,
                $evarEncoding)
            Write-Host "EVAR backup: $backupPath"
            $evarChanged = $true
        }
        else {
            $evarPreview = $true
        }
    }
    else {
        Write-Host "EVAR already configured: $evarPath"
    }
}

$registered = $false
if (-not $SkipMcpRegistration -and
    -not $mcpAlreadyConfigured -and
    $PSCmdlet.ShouldProcess(
        $Name,
        'register discovery-first Yuzuha MCP')) {
    & codex mcp add $Name -- $mcpPath
    if ($LASTEXITCODE -ne 0) {
        throw "codex mcp add failed with exit code $LASTEXITCODE."
    }
    $registered = $true
}

if ($SkipMcpRegistration) {
    Write-Host 'MCP registration skipped.'
}
elseif ($mcpAlreadyConfigured) {
    Write-Host "Registration reused: $Name"
}
elseif ($registered) {
    Write-Host "Registered: $Name"
}
else {
    Write-Host "Preview only: $Name"
}
if (-not $SkipMcpRegistration) {
    Write-Host "Executable: $mcpPath"
    Write-Host 'No PID environment variables are stored. Use list_aveva_sessions and select_aveva_session at runtime.'
}
if ($hasProfile) {
    Write-Host "AVEVA profile: $profile"
    Write-Host "EVAR file: $evarPath"
    Write-Host 'EVAR custom variable: Yuzuha (no underscore)'
    if ($evarPreview) {
        Write-Host 'EVAR preview only; no file was changed.'
    }
    elseif ($evarChanged) {
        Write-Host 'EVAR configured. Fully restart AM/PDMS before testing.'
    }
}
