Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Format-PmlMacroCommand([string]$Path) {
    if ($Path -match '[\$\r\n]') { throw 'Native macro paths cannot contain PML escape characters or newlines.' }
    return '$M ' + "'" + $Path.Replace("'", "''") + "'"
}

function Invoke-PmlDevelopmentWorkflow {
    <#
    .SYNOPSIS
    Reuse PluginDeployment and the existing Phayton IPC operations to copy, rehash, reload and verify PML.
    .DESCRIPTION
    Transport receives an operation ID and input hashtable and returns the unchanged OperationResult.
    Every step is persisted before dispatch. Failed or unknown outcomes stop the workflow without retry.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceDirectory,
        [Parameter(Mandatory)][string]$RegisteredPmlLib,
        [Parameter(Mandatory)][string]$Manifest,
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][string]$EvidenceDirectory,
        [Parameter(Mandatory)][scriptblock]$Transport,
        [switch]$AllowExecution
    )
    if (!$AllowExecution) { throw 'Execution must be explicitly enabled; no copy or PML was sent.' }
    if ($ProcessId -le 0) { throw 'A positive discovered AVEVA PID is required.' }
    $source = [IO.Path]::GetFullPath($SourceDirectory)
    $registered = [IO.Path]::GetFullPath($RegisteredPmlLib).TrimEnd('\','/')
    $evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
    if (Test-Path -LiteralPath $evidence) { throw 'Use a new evidence directory for every run.' }
    if ($evidence.StartsWith($registered + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence and backups must stay outside PMLLIB.' }
    $config = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json -AsHashtable
    $allowed = @('schemaVersion','pluginName','objects','forms','validationMacro','resultGlobal','expected')
    foreach ($field in $config.Keys) { if ($field -notin $allowed) { throw "Unknown manifest field: $field" } }
    foreach ($field in $allowed) { if (!$config.ContainsKey($field)) { throw "Missing manifest field: $field" } }
    if ($config.schemaVersion -ne 1 -or $config.pluginName -notmatch '^[A-Za-z][A-Za-z0-9_-]{0,63}$') { throw 'Invalid manifest version or plugin name.' }
    foreach ($name in @($config.objects) + @($config.forms) + @($config.resultGlobal)) {
        if ($name -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') { throw 'Use plain object, form and result names.' }
    }
    if ((@($config.objects) + @($config.forms)).Count -eq 0) { throw 'Declare at least one object or form to reload.' }
    if ($config.validationMacro -notmatch '^[A-Za-z0-9_.-]+\.pmlmac$' -or $config.validationMacro.Contains('..')) { throw 'Use a validation macro filename in the payload root.' }
    if ($config.expected -isnot [System.Collections.IDictionary] -or $config.expected.Count -eq 0) { throw 'Expected results must be a nonempty path-to-string map.' }
    foreach ($value in $config.expected.Values) { if ($value -isnot [string]) { throw 'Expected values must be exact strings.' } }
    $plan = [YuzuhaToolkit.Workspace.PluginDeployment]::Preview($source, $registered, $config.pluginName)
    foreach ($file in @($config.objects | ForEach-Object { "${_}.pmlobj" }) + @($config.forms | ForEach-Object { "${_}.pmlfrm" }) + @($config.validationMacro)) {
        if (@($plan.Files | Where-Object { $_.RelativePath -ieq $file }).Count -ne 1) { throw "Reload/validation file is missing from the payload root: $file" }
    }
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    $record = [ordered]@{ schemaVersion=1; status='Running'; processId=$ProcessId; startedUtc=[DateTime]::UtcNow.ToString('o'); completedUtc=$null;
        source=$source; target=$plan.Target; manifest=$config; plan=$plan; identity=$null; steps=[Collections.Generic.List[object]]::new(); error=$null; lastFailure=$null }
    $recordFile = Join-Path $evidence 'run.json'
    $writeRecord = { [IO.File]::WriteAllText($recordFile, (ConvertTo-Json -InputObject $record -Depth 40), [Text.UTF8Encoding]::new($false)) }
    $step = {
        param([string]$Name,[scriptblock]$Action)
        $item = [ordered]@{ name=$Name; status='Running'; startedUtc=[DateTime]::UtcNow.ToString('o'); completedUtc=$null; result=$null; error=$null }
        $record.steps.Add($item); & $writeRecord
        try { $item.result = & $Action; $item.status='Passed'; return $item.result }
        catch { $item.status='Failed'; $item.error=$_.Exception.Message; throw }
        finally { $item.completedUtc=[DateTime]::UtcNow.ToString('o'); & $writeRecord }
    }
    $request = {
        param([string]$Operation,[hashtable]$Payload)
        $response = & $Transport $Operation $Payload
        if (!$response.success) { $record.lastFailure=$response; throw "Operation failed or outcome unknown; inspect raw response, do not retry: $Operation" }
        return $response.data
    }
    $checkIdentity = {
        $identity = & $request 'aveva.session.status' @{}
        if (!$identity.TargetVerified -or $identity.Host.ProcessId -ne $ProcessId) { throw 'AVEVA target identity was not verified.' }
        if ($record.identity -and $identity.Host.ProcessStartTimeUtcTicks -ne $record.identity.Host.ProcessStartTimeUtcTicks) { throw 'AVEVA process identity changed during the workflow.' }
        return $identity
    }
    $command = {
        param([string]$Pml)
        $null = & $checkIdentity
        & $request 'pml.command.execute' @{pmlCommand=$Pml}
    }
    try {
        $record.identity = & $step 'verify-session' {
            $sessions = & $request 'aveva.sessions.list' @{}
            if (@($sessions.Sessions | Where-Object ProcessId -eq $ProcessId).Count -ne 1) { throw 'PID was not returned by current AVEVA discovery.' }
            & $checkIdentity
        }
        $pathGlobal = 'YuzuhaDevPath' + [Guid]::NewGuid().ToString('N')
        $pathMacro = Join-Path $evidence 'query-path.pmlmac'
        [IO.File]::WriteAllText($pathMacro, ('!!' + $pathGlobal + " = object array()`nvar !path evar PMLLIB`n!!" + $pathGlobal + ".append('[0] PmlLib<STRING> ' + !path)`n"), [Text.UTF8Encoding]::new($false))
        $null = & $step 'query-pmllib' { & $command (Format-PmlMacroCommand $pathMacro) }
        $null = & $step 'verify-pmllib' {
            $paths = & $request 'pml.array.read' @{pmlCommand=('!!'+$pathGlobal);globalVar=$pathGlobal;deleteGlobalVar=$true;includeEmpty=$true}
            if ($paths.UnparsedCount -ne 0 -or @($paths.Items).Count -ne 1) { throw 'Could not read the native PMLLIB path.' }
            $roots = @($paths.Items[0].Value.Split(';') | Where-Object { $_.Trim() } | ForEach-Object { [IO.Path]::GetFullPath($_.Trim()).TrimEnd('\','/') })
            if ($registered -notin $roots) { throw 'RegisteredPmlLib must exactly match a path returned by this AVEVA session (including any short-path spelling).' }
            return $paths
        }
        $null = & $step 'copy-payload' {
            $null = & $checkIdentity
            $receipt = [YuzuhaToolkit.Workspace.PluginDeployment]::Apply($plan, (Join-Path $evidence 'deployment'))
            foreach ($file in $plan.Files) {
                if ((Get-FileHash -LiteralPath (Join-Path $plan.Target $file.RelativePath) -Algorithm SHA256).Hash -cne $file.SourceHash) { throw 'Deployed source hash differs from the frozen plan.' }
            }
            return $receipt
        }
        $null = & $step 'rehash-all' { & $command 'PML REHASH ALL' }
        foreach ($object in $config.objects) { $null = & $step "reload-object:$object" { & $command ("pml reload object $object") } }
        foreach ($form in $config.forms) { $null = & $step "reload-form:$form" { & $command ("pml reload form !!$form") } }
        $null = & $step 'execute-validation' { & $command (Format-PmlMacroCommand (Join-Path $plan.Target $config.validationMacro)) }
        $result = & $step 'read-results' {
            $null = & $checkIdentity
            & $request 'pml.array.read' @{pmlCommand=('!!'+$config.resultGlobal);globalVar=$config.resultGlobal;deleteGlobalVar=$true;includeEmpty=$true}
        }
        $null = & $step 'verify-results' {
            if ($result.UnparsedCount -ne 0 -or @($result.Items).Count -ne $config.expected.Count) { throw 'Unexpected, missing or unparsed validation rows.' }
            foreach ($key in $config.expected.Keys) {
                $matches = @($result.Items | Where-Object Path -ceq $key)
                if ($matches.Count -ne 1 -or $matches[0].Value -cne $config.expected[$key]) { throw "Loaded behavior does not match expected result: $key" }
            }
            return @{matched=$config.expected.Count;unparsed=0}
        }
        $record.status='Passed'
    } catch { $record.status='FailedOrUnknown'; $record.error=$_.Exception.Message; throw }
    finally { $record.completedUtc=[DateTime]::UtcNow.ToString('o'); & $writeRecord }
    return [pscustomobject]$record
}

Export-ModuleMember -Function Invoke-PmlDevelopmentWorkflow
