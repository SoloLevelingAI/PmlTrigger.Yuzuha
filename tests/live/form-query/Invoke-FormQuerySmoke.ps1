[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$McpExecutable,
    [Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$ProcessId,
    [Parameter(Mandatory)][string]$PmlDirectory,
    [Parameter(Mandatory)][string]$HostDll,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$McpExecutable = (Get-Item -LiteralPath $McpExecutable).FullName
$PmlDirectory = (Get-Item -LiteralPath $PmlDirectory).FullName
$HostDll = (Get-Item -LiteralPath $HostDll).FullName
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository ('artifacts/manual/form-query/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Evidence directory already exists; choose a new run directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$sources = foreach ($name in @('yuzuhav04localadd.pmlfnc','yuzuhav04localprobe.pmlfrm')) {
    $source = Join-Path $PSScriptRoot $name
    $deployed = Join-Path $PmlDirectory $name
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $deployed -Algorithm SHA256).Hash -ne $hash) { throw "Deployed fixture differs from Git source: $name" }
    @{file=$name;sha256=$hash}
}
$script:batchNumber = 0
$script:expectedStart = $null
function Request([string]$Name, [hashtable]$Arguments) { @{name=$Name;arguments=$Arguments} }
function Check([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Batch([object[]]$Requests, [switch]$RejectLast) {
    $script:batchNumber++
    $prefix = Join-Path $OutputDirectory ('batch-' + $script:batchNumber)
    $inputFile = $prefix + '.input.json'
    $requestsWithIdentity = @(Request 'select_aveva_session' @{processId=$ProcessId}) + $Requests
    ConvertTo-Json -InputObject $requestsWithIdentity -Depth 15 | Set-Content -LiteralPath $inputFile -Encoding utf8
    & $McpExecutable --cli batch --file $inputFile 1> ($prefix + '.result.json') 2> ($prefix + '.stderr.log')
    $exitCode = $LASTEXITCODE
    $response = Get-Content -LiteralPath ($prefix + '.result.json') -Raw | ConvertFrom-Json
    if ($RejectLast) { Check ($exitCode -ne 0 -and !$response.success -and $response.stoppedOnError) 'Expected rejection was reported as success.' }
    else { Check ($exitCode -eq 0 -and $response.success -and !$response.stoppedOnError) "CLI batch failed; see $prefix.result.json" }
    Check ($response.completed -eq $requestsWithIdentity.Count) 'CLI returned an incomplete batch.'
    $decoded = @($response.results | ForEach-Object { ($_.result.content | Where-Object type -eq text | Select-Object -First 1).text | ConvertFrom-Json })
    Check ($decoded[0].TargetVerified -eq $true) 'Target identity was not verified.'
    $start = $decoded[0].Host.ProcessStartTimeUtcTicks
    if ($script:expectedStart) { Check ($start -eq $script:expectedStart) 'Target process changed during the test.' }
    else { $script:expectedStart = $start }
    $positive = @($decoded | Select-Object -Skip 1)
    if ($RejectLast) { Check ($positive[-1].Success -eq $false) 'Expected a Host business rejection.'; $positive = @($positive | Select-Object -SkipLast 1) }
    foreach ($item in $positive) {
        if ($item.PSObject.Properties.Name -contains 'Success') { Check ($item.Success -eq $true) 'Host returned a business failure; inspect local batch evidence.' }
    }
    return ,$decoded
}
function ExecuteMacro([string]$Path) {
    Check (!$Path.Contains("'") -and !$Path.Contains("`n")) 'Macro path cannot contain a quote or newline.'
    $run = Batch @(
        (Request 'run_pml_command' @{pmlCommand="!!YuzuhaExecuter.InitArgs('$Path')"}),
        (Request 'run_pml_command' @{pmlCommand="!!YuzuhaExecuter.execute('ExecuteFile')"}),
        (Request 'run_pml_command_list' @{pmlCommand='!!YuzuhaExecuter.Query()';globalVar='YUZUHAV04SMOKERESULT';deleteGlobalVar=$true;includeEmpty=$true})
    )
    Check (($run[-1].Unparsed -join "`n") -match "<STRING> 'Success," -and ($run[-1].Unparsed -join "`n") -notmatch 'Failed') 'Built-in executor did not confirm macro success.'
}
$identity = (Batch @())[0]
Check ($identity.Host.FormQueryVersion -eq 1 -and $identity.Host.BoundedArrayReadVersion -eq 1) 'This live test requires the new NET35/NET48 Host capabilities.'
$metadata = @{scope='AVEVA synthetic fixture; no engineering saves';status='Running';commit=(& git -C $repository rev-parse HEAD).Trim();dirty=[bool](& git -C $repository status --porcelain);identity=$identity;hostDllSha256=(Get-FileHash -LiteralPath $HostDll -Algorithm SHA256).Hash;sources=$sources;startedUtc=[DateTime]::UtcNow.ToString('o')}
$metadataFile = Join-Path $OutputDirectory 'run.json'
try {
    $setup = Join-Path $OutputDirectory 'setup.pmlmac'
    @'
PML REHASH ALL
!!YUZUHAV04NATIVESUM = !!YuzuhaV04LocalAdd(2,3)
PML RELOAD FORM !!YuzuhaV04LocalProbe
!!YUZUHAV04ARRAY = ARRAY()
!!YUZUHAV04ARRAY[1] = 'alpha'
!!YUZUHAV04ARRAY[2] = 'beta'
!!YUZUHAV04ARRAY[3] = 'gamma'
!!YUZUHAV04BEFORE = ARRAY()
!!YUZUHAV04BEFORE[1] = !!YuzuhaV04LocalProbe.counter.string()
!!YUZUHAV04BEFORE[2] = !!YuzuhaV04LocalProbe.message.val
!!YUZUHAV04BEFORE[3] = !!YuzuhaV04LocalProbe.enabled.val.string()
!!YUZUHAV04BEFORE[4] = !!YuzuhaV04LocalProbe.accept.callback
!!YUZUHAV04BEFORE[5] = !!YuzuhaV04LocalProbe.shown().string()
'@ | Set-Content -LiteralPath $setup -Encoding ascii
    ExecuteMacro $setup
    $windows = Batch @(
        (Request 'read_pml_array' @{globalVar='YUZUHAV04ARRAY';startIndex=1;maxItems=2;maxCharacters=1024}),
        (Request 'read_pml_array' @{globalVar='YUZUHAV04ARRAY';startIndex=3;maxItems=2;maxCharacters=1024})
    )
    $array = $windows[1].ArrayWindow
    Check ($array.IndexUpperBound -eq 3 -and $array.Rows.Count -eq 2 -and $array.NextIndex -eq 3) 'Bounded array did not return two matching rows with continuation.'
    Check ($array.Rows[0] -eq "<STRING> 'alpha'" -and $array.Rows[1] -eq "<STRING> 'beta'") 'Array rows differ from the owned fixture.'
    Check ($windows[2].ArrayWindow.Rows.Count -eq 1 -and $windows[2].ArrayWindow.Rows[0] -eq "<STRING> 'gamma'" -and $windows[2].ArrayWindow.NextIndex -eq 0) 'Array continuation did not reach the final matching row.'
    $queries = Batch @(
        (Request 'list_pml_forms' @{shownOnly=$false;maxItems=1000;maxCharacters=262144}),
        (Request 'list_pml_gadgets' @{formName='YuzuhaV04LocalProbe';maxItems=1000}),
        (Request 'get_pml_gadget' @{formName='YuzuhaV04LocalProbe';gadgetName='accept'}),
        (Request 'get_pml_gadget' @{formName='YuzuhaV04LocalProbe';gadgetName='message'}),
        (Request 'get_pml_gadget' @{formName='YuzuhaV04LocalProbe';gadgetName='enabled'}),
        (Request 'get_pml_gadget' @{formName='YuzuhaV04LocalProbe';gadgetName='items'}),
        (Request 'list_pml_forms' @{shownOnly=$true;maxItems=1000;maxCharacters=262144}),
        (Request 'list_pml_gadgets' @{formName='YuzuhaV04LocalProbe';maxItems=2}),
        (Request 'list_pml_gadgets' @{formName='YuzuhaV04LocalProbe';startIndex=3;maxItems=2})
    )
    Check ($queries[1].FormQueryResult.Items.Name -contains 'YUZUHAV04LOCALPROBE') 'Loaded fixture is absent from the native form list.'
    $gadgets = $queries[2].FormQueryResult.Items
    foreach ($name in @('ACCEPT','MESSAGE','ENABLED','ITEMS')) { Check ($gadgets.Name -contains $name) "Missing native gadget: $name" }
    Check ($gadgets.Name -notcontains 'ORDINARY' -and $gadgets.Name -notcontains 'COUNTER' -and $gadgets.Name -notcontains 'POPUP') 'Non-gadget member leaked into the list.'
    Check ($queries[7].FormQueryResult.Items.Name -notcontains 'YUZUHAV04LOCALPROBE') 'Hidden fixture appeared in shown forms.'
    Check ($queries[8].FormQueryResult.Scanned -eq 2 -and $queries[8].FormQueryResult.NextIndex -eq 3 -and $queries[9].FormQueryResult.Items.Name -contains 'ACCEPT') 'Gadget paging lost native scan positions.'
    $rejected = Batch @((Request 'get_pml_gadget' @{formName='YuzuhaV04LocalProbe';gadgetName='ordinary'})) -RejectLast
    Check ($rejected[-1].ErrorMessage -match 'PML_UI_NOT_GADGET') 'Non-gadget rejection lost its cause.'
    $rejected = Batch @((Request 'get_pml_gadget' @{formName='YuzuhaV04LocalProbe';gadgetName='accept';maxCharacters=1})) -RejectLast
    Check ($rejected[-1].ErrorMessage -match 'PML_UI_ITEM_TOO_LARGE') 'Character budget rejection lost its cause.'
    $after = Join-Path $OutputDirectory 'after.pmlmac'
    (Get-Content -LiteralPath $setup | Where-Object { $_ -like '!!YUZUHAV04BEFORE*' }) -replace 'YUZUHAV04BEFORE','YUZUHAV04AFTER' | Set-Content -LiteralPath $after -Encoding ascii
    ExecuteMacro $after
    $values = Batch @(
        (Request 'run_pml_command_list' @{pmlCommand="!!YuzuhaReadGlobal(20,1,'','YUZUHAV04BEFORE')";globalVar='YUZUHAV04SMOKERESULT';deleteGlobalVar=$true;includeEmpty=$true}),
        (Request 'run_pml_command_list' @{pmlCommand="!!YuzuhaReadGlobal(20,1,'','YUZUHAV04AFTER')";globalVar='YUZUHAV04SMOKERESULT';deleteGlobalVar=$true;includeEmpty=$true}),
        (Request 'run_pml_command_list' @{pmlCommand="!!YuzuhaReadGlobal(10,1,'','YUZUHAV04NATIVESUM')";globalVar='YUZUHAV04SMOKERESULT';deleteGlobalVar=$true;includeEmpty=$true})
    )
    $beforeRows = @($values[1].Items | Where-Object Depth -eq 1 | Sort-Object { [int]$_.Path })
    $afterRows = @($values[2].Items | Where-Object Depth -eq 1 | Sort-Object { [int]$_.Path })
    Check (($beforeRows.Value | ConvertTo-Json -Compress) -eq ($afterRows.Value | ConvertTo-Json -Compress)) 'Query changed fixture counter, value, callback or display state.'
    Check ($beforeRows.Count -eq 5 -and $afterRows.Count -eq 5 -and $beforeRows[0].Value -eq '0') 'Missing before/after evidence or callback was executed.'
    Check ($values[3].Count -eq 1 -and $values[3].Items[0].Value -eq '5') 'PMLFNC did not return 2+3=5.'
    $metadata.status = 'Passed'
} catch { $metadata.status='Failed';$metadata.error=$_.Exception.Message;throw }
finally { $metadata.finishedUtc=[DateTime]::UtcNow.ToString('o');$metadata | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metadataFile -Encoding utf8 }
Write-Output "Live synthetic smoke passed. Evidence: $OutputDirectory"
