[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $McpExecutable,

    [string] $Name = 'YuzuhaToolkit',

    [string] $KnowledgeExecutable,

    [string] $KnowledgeName = 'YuzuhaToolkitKnowledge',

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.]{0,31}$')]
    [string] $AvevaProfile,

    [string] $EvarBat,

    [string] $ToolkitRoot = '',

    [switch] $SkipMcpRegistration,

    [switch] $CheckOnly,

    [string] $McpJsonPath
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# $PSScriptRoot is empty inside parameter defaults on Windows PowerShell 5.1.
if ([string]::IsNullOrWhiteSpace($ToolkitRoot)) {
    $ToolkitRoot = Split-Path -Parent $PSScriptRoot
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

function Get-UpdatedEvarText {
    param(
        [Parameter(Mandatory = $true)][string] $Text,
        [Parameter(Mandatory = $true)][string] $Profile,
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Framework
    )

    $newLine = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $managedStart = 'rem >>> Yuzuha managed settings'
    $managedEnd = 'rem <<< Yuzuha managed settings'
    $pmlLib = Join-Path $Root 'PMLLIB'
    $pmlUi = Join-Path $Root 'PMLUI'
    # net48 profiles are E3D (UI variable pmlui); net35 are AM/PDMS (pdmsui).
    $uiVariable = if ($Framework -eq 'net48') { 'pmlui' } else { 'pdmsui' }
    $managedBlock = @(
        $managedStart,
        'rem Custom variable name must remain Yuzuha (no underscore).',
        "set Yuzuha=$Profile",
        "set YuzuhaFramework=$Framework",
        "set pmllib=$pmlLib;%pmllib%",
        "set $uiVariable=$pmlUi;%$uiVariable%",
        $managedEnd
    ) -join $newLine
    $managedBlock += $newLine

    # [PATCH F] The block expands %pmllib% and the UI variable immediately, so it must
    # execute AFTER AVEVA's own variable definitions. Inserting right after
    # @echo off placed the block at the top of the file: later AVEVA lines
    # overwrote the values, which could break product startup. Always strip
    # any previously written block (older installers left it at the top) and
    # re-append at the END of evars.bat. Assumes no 'exit /b' at EOF
    # (PDMS 12.1 evars.bat is a plain sequence of set statements).
    $blockPattern = '(?ms)^' +
        [System.Text.RegularExpressions.Regex]::Escape($managedStart) +
        '\r?\n.*?^' +
        [System.Text.RegularExpressions.Regex]::Escape($managedEnd) +
        '(?:\r?\n)?'
    $strippedText = [System.Text.RegularExpressions.Regex]::Replace(
        $Text, $blockPattern, '').TrimEnd("`r", "`n")
    if ($strippedText.Length -gt 0) {
        $strippedText += $newLine + $newLine
    }
    return $strippedText + $managedBlock
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

function Register-ManagedMcp {
    param(
        [Parameter(Mandatory = $true)][string] $TargetName,
        [Parameter(Mandatory = $true)][string] $ExecutablePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Configurations
    )

    $targetPath = [System.IO.Path]::GetFullPath($ExecutablePath)
    if (-not $CheckOnly -and -not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
        throw "Yuzuha MCP executable does not exist: $targetPath"
    }

    $existing = @($Configurations | Where-Object { $_.name -eq $TargetName }) |
        Select-Object -First 1
    if ($null -ne $existing) {
        $transport = $existing.transport
        $existingCommand = [string] $transport.command
        $existingArguments = @($transport.args)
        $existingCommandPath = Get-NormalizedCommandPath $existingCommand
        $sameCommand = [string]::Equals(
            $existingCommandPath,
            $targetPath,
            [System.StringComparison]::OrdinalIgnoreCase)
        $sameRegistration = (
            $transport.type -eq 'stdio' -and
            $sameCommand -and
            $existingArguments.Count -eq 0 -and
            $existing.enabled -eq $true)

        if ($sameRegistration) {
            Write-Host "MCP already registered correctly; unchanged: $TargetName"
            return $null
        }

        $argumentText = if ($existingArguments.Count -eq 0) {
            '<none>'
        }
        else {
            $existingArguments -join ' '
        }
        throw @"
Codex MCP configuration '$TargetName' already exists but does not match this package.
Existing: enabled=$($existing.enabled); type=$($transport.type); command=$existingCommand; args=$argumentText
Expected: enabled=True; type=stdio; command=$targetPath; args=<none>
Nothing was changed. Inspect it with: codex mcp get $TargetName --json
Remove it explicitly only if replacement is intended: codex mcp remove $TargetName
"@
    }

    $desiredFileName = [System.IO.Path]::GetFileName($targetPath)
    $possibleDuplicates = @($Configurations | Where-Object {
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
Nothing was changed. Resolve the existing entry explicitly before adding '$TargetName'.
"@
    }

    return [pscustomobject]@{ Name = $TargetName; Path = $targetPath }
}

# Build one read-only plan for both stdio services; commit only after all preflight passes.
function New-GenericJsonMcpPlan {
    param([string] $JsonPath, [array] $Entries)
    $fullPath = [IO.Path]::GetFullPath($JsonPath)
    $existed = Test-Path -LiteralPath $fullPath -PathType Leaf
    [byte[]]$bytes = @()
    if ($existed) { $bytes = [IO.File]::ReadAllBytes($fullPath) }
    $raw = if ($existed) { [IO.File]::ReadAllText($fullPath) } else { '' }
    $json = if ([string]::IsNullOrWhiteSpace($raw)) { [pscustomobject]@{} } else { ConvertFrom-Json -InputObject $raw }
    if ($json -isnot [pscustomobject]) { throw 'MCP JSON root must be an object.' }
    if (-not $json.PSObject.Properties['mcpServers']) {
        $json | Add-Member NoteProperty mcpServers ([pscustomobject]@{})
    }
    $servers = $json.mcpServers
    if ($servers -isnot [pscustomobject]) { throw 'mcpServers must be an object.' }
    $changed = $false
    foreach ($entry in $Entries) {
        $target = [IO.Path]::GetFullPath($entry.Path)
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "MCP executable missing: $target" }
        foreach ($other in $servers.PSObject.Properties) {
            if ($other.Name -eq $entry.Name) { continue }
            if ($other.Value -and $other.Value.PSObject.Properties['command'] -and
                (Get-NormalizedCommandPath ([string]$other.Value.command)) -ieq $target) {
                throw "MCP executable already registered under another name: $($other.Name)"
            }
        }
        $existing = $servers.PSObject.Properties[$entry.Name]
        if ($null -ne $existing) {
            $value = $existing.Value
            if ($value -isnot [pscustomobject] -or -not $value.PSObject.Properties['command']) { throw "Invalid MCP entry: $($entry.Name)" }
            $arguments = @()
            if ($value.PSObject.Properties['args']) { $arguments = @($value.args) }
            $disabled = ($value.PSObject.Properties['disabled'] -and $value.disabled -eq $true) -or
                ($value.PSObject.Properties['enabled'] -and $value.enabled -eq $false)
            $wrongType = $value.PSObject.Properties['type'] -and $value.type -ne 'stdio'
            if ((Get-NormalizedCommandPath ([string]$value.command)) -ine $target -or $arguments.Count -gt 0 -or $disabled -or $wrongType) {
                throw "Conflicting or disabled MCP entry: $($entry.Name). Nothing was changed."
            }
        } else {
            $servers | Add-Member NoteProperty $entry.Name ([pscustomobject]@{ command=$target; args=@() })
            $changed = $true
        }
    }
    return [pscustomobject]@{ Path=$fullPath; Existed=$existed; Bytes=[byte[]]$bytes;
        Changed=$changed; Output=((ConvertTo-Json -InputObject $json -Depth 100)+"`r`n") }
}

$hasProfile = -not [string]::IsNullOrWhiteSpace($AvevaProfile)
$hasEvar = -not [string]::IsNullOrWhiteSpace($EvarBat)
if ($hasProfile -ne $hasEvar) {
    throw 'AvevaProfile and EvarBat must be supplied together for the selected AVEVA profile.'
}

$mcpConfigurations = @()
$useGenericJson = -not [string]::IsNullOrWhiteSpace($McpJsonPath)
if (-not $SkipMcpRegistration -and -not $useGenericJson) {
    if ([string]::IsNullOrWhiteSpace($McpExecutable)) {
        throw 'McpExecutable is required unless SkipMcpRegistration is specified.'
    }
    $mcpListJson = & codex mcp list --json
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot inspect existing Codex MCP configurations (exit code $LASTEXITCODE)."
    }
    try {
        $parsedConfigurations = ConvertFrom-Json -InputObject ($mcpListJson -join "`n")
        $mcpConfigurations = @($parsedConfigurations)
    }
    catch {
        throw "Cannot parse existing Codex MCP configurations: $($_.Exception.Message)"
    }
}

$plans = @()
if (-not $SkipMcpRegistration -and -not $useGenericJson) {
    if ($Name -eq $KnowledgeName -and $KnowledgeExecutable) { throw 'The two MCP names must be different.' }
    $plans += @(Register-ManagedMcp -TargetName $Name -ExecutablePath $McpExecutable -Configurations $mcpConfigurations)
    if ($KnowledgeExecutable) {
        $plans += @(Register-ManagedMcp -TargetName $KnowledgeName -ExecutablePath $KnowledgeExecutable -Configurations $mcpConfigurations)
    }
    $plans = @($plans | Where-Object { $null -ne $_ })
}

if ($useGenericJson -and -not $SkipMcpRegistration) {
    if ([string]::IsNullOrWhiteSpace($McpExecutable)) {
        throw 'McpExecutable is required when McpJsonPath is specified.'
    }
    if ($Name -eq $KnowledgeName -and $KnowledgeExecutable) { throw 'The two MCP names must be different.' }
    $genericEntries = @([pscustomobject]@{ Name=$Name; Path=$McpExecutable })
    if ($KnowledgeExecutable) { $genericEntries += [pscustomobject]@{ Name=$KnowledgeName; Path=$KnowledgeExecutable } }
    $genericPlan = New-GenericJsonMcpPlan -JsonPath $McpJsonPath -Entries $genericEntries
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
    $frameworks = @('net35', 'net48' | Where-Object {
        $suffix = $_.Substring(3)
        Test-Path -LiteralPath (Join-Path $rootPath "runtime\profiles\$profile\$_\YuzuhaToolkit.PmlHost.Net$suffix.dll") -PathType Leaf
    })
    if ($frameworks.Count -ne 1) { throw "Profile $profile must contain exactly one NET35/NET48 Host; found $($frameworks.Count)." }
    $framework = $frameworks[0]
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
        -Root $rootPath -Framework $framework

}
if ($CheckOnly) { return }

$attempted = @()
$evarWriteStarted = $false
$genericWriteStarted = $false
$genericTemporary = $null
try {
    foreach ($plan in $plans) {
        if ($PSCmdlet.ShouldProcess($plan.Name, 'register Yuzuha MCP')) {
            $attempted += $plan
            & codex mcp add $plan.Name -- $plan.Path
            if ($LASTEXITCODE -ne 0) { throw "codex mcp add failed for $($plan.Name): $LASTEXITCODE" }
        }
    }
    if ($useGenericJson -and -not $SkipMcpRegistration -and $genericPlan.Changed -and
        $PSCmdlet.ShouldProcess($genericPlan.Path, 'register both MCP stdio services')) {
        $parent = Split-Path -Parent $genericPlan.Path
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        $existsNow = Test-Path -LiteralPath $genericPlan.Path
        if ($existsNow -ne $genericPlan.Existed -or ($existsNow -and
            [Convert]::ToBase64String([IO.File]::ReadAllBytes($genericPlan.Path)) -ne [Convert]::ToBase64String($genericPlan.Bytes))) {
            throw 'MCP configuration changed during preflight. Nothing was overwritten.'
        }
        $genericTemporary = $genericPlan.Path + '.yuzuha-' + [guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText($genericTemporary, $genericPlan.Output, [Text.UTF8Encoding]::new($false))
        if ($genericPlan.Existed) {
            [IO.File]::Replace($genericTemporary, $genericPlan.Path, $genericPlan.Path + '.yuzuha-' + [guid]::NewGuid().ToString('N') + '.bak')
        } else { [IO.File]::Move($genericTemporary, $genericPlan.Path) }
        $genericWriteStarted = $true
        Write-Host "Registered MCP stdio services in $($genericPlan.Path). Fully exit and restart that AI client; accept its own configuration approval if requested."
    }
    if ($hasProfile) {
    if ($updatedEvarText -ne $evarText) {
        if ($PSCmdlet.ShouldProcess(
                $evarPath,
                "back up and configure $profile Yuzuha EVAR settings")) {
            $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
            $backupPath = "$evarPath.yuzuha-$timestamp.bak"
            Copy-Item -LiteralPath $evarPath -Destination $backupPath
            $evarWriteStarted = $true
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
}
catch {
    $originalError = $_
    $rollbackErrors = @()
    if ($genericWriteStarted) {
        try {
            if ([IO.File]::ReadAllText($genericPlan.Path) -cne $genericPlan.Output) { throw 'MCP JSON changed concurrently; retained for recovery.' }
            if ($genericPlan.Existed) { [IO.File]::WriteAllBytes($genericPlan.Path, $genericPlan.Bytes) }
            else { [IO.File]::Delete($genericPlan.Path) }
        } catch { $rollbackErrors += "MCP JSON: $($_.Exception.Message)" }
    }
    if ($evarWriteStarted) {
        try { [IO.File]::WriteAllBytes($evarPath, $evarBytes) }
        catch { $rollbackErrors += "EVAR: $($_.Exception.Message)" }
    }
    [array]::Reverse($attempted)
    foreach ($plan in $attempted) {
        try {
            $json = & codex mcp list --json
            if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect registration during rollback.' }
            $parsedConfigurations = ConvertFrom-Json -InputObject ($json -join "`n")
            $current = @($parsedConfigurations | Where-Object { $_.name -eq $plan.Name })
            if ($current.Count -eq 0) { continue }
            if ($current.Count -ne 1 -or $current[0].transport.type -ne 'stdio' -or
                @( $current[0].transport.args ).Count -ne 0 -or
                (Get-NormalizedCommandPath $current[0].transport.command) -ne $plan.Path) {
                throw "Configuration changed concurrently; retained $($plan.Name)."
            }
            & codex mcp remove $plan.Name
            if ($LASTEXITCODE -ne 0) { throw "Cannot remove $($plan.Name)." }
        }
        catch { $rollbackErrors += $_.Exception.Message }
    }
    if ($rollbackErrors.Count) {
        throw "Rollback incomplete; keep deployed files for recovery. Original: $originalError. Recovery: $($rollbackErrors -join '; ')"
    }
    throw $originalError
}
finally {
    if ($genericTemporary -and [IO.File]::Exists($genericTemporary)) { [IO.File]::Delete($genericTemporary) }
}

if (-not $SkipMcpRegistration) {
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
        Write-Host 'EVAR configured. Fully restart AVEVA before testing.'
    }
}
