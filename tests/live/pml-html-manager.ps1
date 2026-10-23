[CmdletBinding()]
param(
    [string]$BaseUri='http://127.0.0.1:5288',
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][ValidatePattern('^=[0-9]+/[0-9]+$')][string]$ElementRef,
    [Parameter(Mandatory)][string]$OutputFile,
    [int]$ExpectedItemCount=0
)
$ErrorActionPreference='Stop'
$bootstrap=Invoke-RestMethod "$BaseUri/api/bootstrap"
$headers=@{'X-Yuzuha-Token'=$bootstrap.token}
$evidence=[Collections.Generic.List[object]]::new()
function Assert($Condition,[string]$Reason) { if(!$Condition){throw $Reason} }
function Post([string]$Path,$Body) { Invoke-RestMethod "$BaseUri/api/$Path" -Method Post -Headers $headers -ContentType application/json -Body (ConvertTo-Json -InputObject $Body -Depth 30 -Compress) }
function Call([string]$App,[string]$Action,[hashtable]$Parameters) {
    $request=@{requestId=[guid]::NewGuid().ToString('N');catalogRevision=$bootstrap.catalog.revision;appId=$App;actionId=$Action;parameters=$Parameters}
    $record=Post 'invoke' $request
    $evidence.Add($record)
    Assert ($record.status -eq 'Succeeded') "Call failed: $($record.status) $($record.error)"
    Assert ($record.result.data.UnparsedCount -eq 0) 'Unparsed result rows.'
    return $record
}
function Meta($Record,[string]$Path) { ($Record.result.data.Items | Where-Object Path -eq $Path).Value }
$session=Post 'session' @{processId=$ProcessId}
$full=Call 'attribute-browser' 'attribute.read' @{ref=$ElementRef;attribute='ELDATA';index=0;maxRows=20000}
Assert ((Meta $full 'Meta.Source') -eq 'VAR SPLIT') 'Expected VAR SPLIT fallback.'
$total=[int](Meta $full 'Meta.Total')
Assert ($total -gt 1000) 'Fixture is not a large array.'
if($ExpectedItemCount -gt 0) { Assert ($total -eq $ExpectedItemCount) 'Unexpected array size.' }
Assert ([int](Meta $full 'Meta.Returned') -eq $total) 'Incomplete extraction.'
Assert ((Meta $full 'Meta.HasMore') -eq 'FALSE') 'Full result marked truncated.'
$children=@($full.result.data.Items | Where-Object Path -Match '^ELDATA\.\|[0-9]+\|$')
Assert ($children.Count -eq $total) 'Missing formatted children.'
for($i=1;$i -le $total;$i++) { Assert ($children[$i-1].Path -eq "ELDATA.|$i|") 'Incorrect index sequence.' }
foreach($index in @(1,[int][Math]::Ceiling($total/2),$total)) {
    $single=Call 'attribute-browser' 'attribute.read' @{ref=$ElementRef;attribute='ELDATA';index=$index;maxRows=5}
    Assert ((Meta $single 'Meta.Returned') -eq '1') 'Single-item count mismatch.'
    $item=$single.result.data.Items | Where-Object Path -eq "ELDATA.$index"
    Assert ($item.Value -eq $children[$index-1].Value) 'Directed index differs from full extraction.'
}
$bounded=Call 'attribute-browser' 'attribute.read' @{ref=$ElementRef;attribute='ELDATA';index=0;maxRows=5}
Assert ((Meta $bounded 'Meta.HasMore') -eq 'TRUE') 'Bounded result must expose truncation.'
Assert ((Meta $bounded 'Meta.Total') -eq $total.ToString()) 'Row budget changed extraction size.'
$sum=Call 'function-probe' 'function.add' @{left=12.5;right=7.5}
Assert ((Meta $sum 'Result') -eq '20') 'Typed PMLFNC parameters did not round-trip.'
$form=Call 'native-probe' 'form.open' @{}
Assert ((Meta $form 'Result') -eq 'Dispatched') 'Form show was not dispatched.'
$duplicate=Post 'invoke' $sum.request
Assert ($duplicate.steps[0].response.data.RequestId -eq $sum.steps[0].response.data.RequestId) 'Duplicate request was re-executed.'
foreach($case in @(
    @{name='no-token';path='catalog';method='Put';body=$bootstrap.catalog;headers=@{};expected=403},
    @{name='cross-origin';path='bootstrap';method='Get';body=$null;headers=@{Origin='http://example.invalid'};expected=403},
    @{name='stale-binding';path='invoke';method='Post';body=@{requestId=[guid]::NewGuid().ToString('N');catalogRevision=0;appId='function-probe';actionId='function.add';parameters=@{}};headers=$headers;expected=409},
    @{name='invalid-dbref';path='invoke';method='Post';body=@{requestId=[guid]::NewGuid().ToString('N');catalogRevision=$bootstrap.catalog.revision;appId='attribute-browser';actionId='attribute.read';parameters=@{ref="invalid`nPML"}};headers=$headers;expected=400}
)) {
    $arguments=@{Uri="$BaseUri/api/$($case.path)";Method=$case.method;Headers=$case.headers;SkipHttpErrorCheck=$true}
    if($case.body) { $arguments.ContentType='application/json';$arguments.Body=ConvertTo-Json -InputObject $case.body -Depth 30 }
    $response=Invoke-WebRequest @arguments
    Assert ([int]$response.StatusCode -eq $case.expected) "Unexpected HTTP status: $($case.name)"
}
$report=@{status='Passed';session=$session;total=$total;formattedChildren=$children.Count;runs=$evidence.ToArray();duplicateWasReplayed=$false;httpGuards=4;completedUtc=[DateTime]::UtcNow.ToString('o')}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputFile))) | Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),(ConvertTo-Json -InputObject $report -Depth 60),[Text.UTF8Encoding]::new($false))
Write-Output "Passed: $total children, 7 IPC actions, 3 directed indices, 4 HTTP guards. $OutputFile"
