$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../src/YuzuhaToolkit.Workspace/bin/Release/net10.0/YuzuhaToolkit.Workspace.dll')
Import-Module (Join-Path $PSScriptRoot '../scripts/PmlDevelopment.psm1') -Force
$root = Join-Path ([IO.Path]::GetTempPath()) ('yuzuha-development-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $root
function Check([bool]$Condition,[string]$Message) { if (!$Condition) { throw $Message } }
function Scenario([string]$Mode) {
    $base = Join-Path $root $Mode
    $source = Join-Path $base 'source'
    $registered = Join-Path $base 'registered'
    $null = New-Item -ItemType Directory -Path $source,$registered -Force
    foreach ($file in @('Probe.pmlobj','ProbeForm.pmlfrm','validate.pmlmac')) { [IO.File]::WriteAllText((Join-Path $source $file), 'owned fixture') }
    $manifest = Join-Path $base 'manifest.json'
    @{schemaVersion=1;pluginName='Probe';objects=@('Probe');forms=@('ProbeForm');validationMacro='validate.pmlmac';resultGlobal='OwnedReport';expected=@{Version='v2';Sum='5'}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
    $state = @{calls=[Collections.Generic.List[object]]::new();statusCount=0}
    $transport = {
        param([string]$Operation,[hashtable]$Payload)
        $state.calls.Add(@{operation=$Operation;payload=$Payload})
        switch ($Operation) {
            'aveva.sessions.list' { return @{success=$true;data=@{Sessions=@(@{ProcessId=701})}} }
            'aveva.session.status' {
                $state.statusCount++
                $start = if ($Mode -eq 'identity-change' -and $state.statusCount -gt 3) { 43 } else { 42 }
                return @{success=$true;data=@{TargetVerified=$true;Host=@{ProcessId=701;ProcessStartTimeUtcTicks=$start}}}
            }
            'pml.command.execute' {
                if ($Mode -eq 'rehash-failure' -and $Payload.pmlCommand -eq 'PML REHASH ALL') { return @{success=$false;error=@{MayHaveExecuted=$true}} }
                return @{success=$true;data=@{Success=$true;RequestId='synthetic';PmlCommand=$Payload.pmlCommand}}
            }
            'pml.array.read' {
                if ($Payload.globalVar -like 'YuzuhaDevPath*') {
                    $path = if ($Mode -eq 'wrong-library') { Join-Path $base 'other' } else { $registered }
                    return @{success=$true;data=@{UnparsedCount=0;Items=@(@{Path='PmlLib';Value=$path})}}
                }
                $version = if ($Mode -eq 'stale-version') { 'v1' } else { 'v2' }
                return @{success=$true;data=@{UnparsedCount=0;Items=@(@{Path='Version';Value=$version},@{Path='Sum';Value='5'})}}
            }
            default { throw "Unexpected operation: $Operation" }
        }
    }.GetNewClosure()
    $options = @{SourceDirectory=$source;RegisteredPmlLib=$registered;Manifest=$manifest;ProcessId=701;EvidenceDirectory=(Join-Path $base 'evidence');Transport=$transport}
    if ($Mode -eq 'unauthorized') {
        try { Invoke-PmlDevelopmentWorkflow @options; throw 'Expected an authorization failure' } catch { Check ($_.Exception.Message -match 'explicitly enabled') 'Wrong authorization failure' }
        Check ($state.calls.Count -eq 0) 'Unauthorized flow contacted AVEVA'
        Check (!(Test-Path (Join-Path $registered 'Probe'))) 'Unauthorized flow copied files'
        return
    }
    $failed = $false
    try { $null = Invoke-PmlDevelopmentWorkflow @options -AllowExecution } catch { $failed=$true }
    $record = Get-Content (Join-Path $base 'evidence/run.json') -Raw | ConvertFrom-Json
    if ($Mode -eq 'pass') {
        Check (!$failed -and $record.status -eq 'Passed') ("Valid flow did not pass: " + $record.error)
        Check (@($record.steps).Count -eq 10) 'Incomplete chain'
        Check ((Get-Content (Join-Path $registered 'Probe/Probe.pmlobj') -Raw) -eq 'owned fixture') 'Copy did not use PluginDeployment'
        Check (@($state.calls | Where-Object { $_.payload.ContainsKey('pmlCommand') -and $_.payload.pmlCommand -like "`$M '*'" }).Count -eq 2) 'Macro paths were not quoted'
        Check (@($state.calls | Where-Object { $_.payload.ContainsKey('pmlCommand') -and $_.payload.pmlCommand -eq 'PML REHASH ALL' }).Count -eq 1) 'REHASH was replayed'
    } else {
        Check ($failed -and $record.status -eq 'FailedOrUnknown') 'Failure was reported as success'
        if ($Mode -eq 'wrong-library') { Check (!(Test-Path (Join-Path $registered 'Probe'))) 'Wrong library received copied files' }
        if ($Mode -eq 'rehash-failure') {
            Check ($record.lastFailure.error.MayHaveExecuted -eq $true) 'Unknown-operation evidence was lost'
            Check (@($record.steps | Where-Object name -like 'reload-*').Count -eq 0) 'Reload continued after unknown REHASH result'
            Check (@($state.calls | Where-Object { $_.payload.ContainsKey('pmlCommand') -and $_.payload.pmlCommand -eq 'PML REHASH ALL' }).Count -eq 1) 'Unknown command was retried'
        }
        if ($Mode -eq 'stale-version') { Check ($record.steps[-1].name -eq 'verify-results') 'Stale version was not caught by result verification' }
        if ($Mode -eq 'identity-change') { Check (@($record.steps | Where-Object name -like 'reload-*').Count -eq 0) 'Flow continued after process identity changed' }
    }
}
try {
    foreach ($mode in @('pass','unauthorized','wrong-library','rehash-failure','stale-version','identity-change')) { Scenario $mode; Write-Output "PASS $mode" }
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test cleanup escaped temporary root.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
