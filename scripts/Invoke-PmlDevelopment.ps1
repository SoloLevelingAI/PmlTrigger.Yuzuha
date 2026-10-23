[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CliExecutable,
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$RegisteredPmlLib,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][string]$ExpectedModel,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$WorkspaceAssembly = (Join-Path $PSScriptRoot '../src/YuzuhaToolkit.Workspace/bin/Release/net10.0/YuzuhaToolkit.Workspace.dll'),
    [switch]$AllowExecution
)
$ErrorActionPreference = 'Stop'
if (!$AllowExecution) { throw 'Add -AllowExecution only for an authorized deployment/load/test task.' }
$CliExecutable = (Get-Item -LiteralPath $CliExecutable).FullName
$WorkspaceAssembly = (Get-Item -LiteralPath $WorkspaceAssembly).FullName
Add-Type -Path $WorkspaceAssembly
Import-Module (Join-Path $PSScriptRoot 'PmlDevelopment.psm1') -Force
$calls = [Collections.Generic.List[object]]::new()
$transport = {
    param([string]$Operation,[hashtable]$Payload)
    $json = ConvertTo-Json -InputObject $Payload -Depth 15 -Compress
    $arguments = @('op','invoke',$Operation,'--input',$json)
    if ($Operation -ne 'aveva.sessions.list') { $arguments += @('--pid', $ProcessId.ToString(), '--expected-model', $ExpectedModel) }
    if ($Operation -in @('pml.command.execute','pml.array.read')) { $arguments += '--allow-execution' }
    $raw = & $CliExecutable @arguments
    $code = $LASTEXITCODE
    $response = $raw | ConvertFrom-Json
    $calls.Add(@{operation=$Operation;input=$Payload;exitCode=$code;response=$response})
    if ($code -ne 0 -and $response.success) { throw 'CLI exited unsuccessfully despite a success response.' }
    return $response
}.GetNewClosure()
try {
    $record = Invoke-PmlDevelopmentWorkflow -SourceDirectory $SourceDirectory -RegisteredPmlLib $RegisteredPmlLib -Manifest $Manifest -ProcessId $ProcessId -EvidenceDirectory $EvidenceDirectory -Transport $transport -AllowExecution
    Write-Output (Join-Path ([IO.Path]::GetFullPath($EvidenceDirectory)) 'run.json')
} finally {
    if (Test-Path -LiteralPath $EvidenceDirectory) {
        [IO.File]::WriteAllText((Join-Path ([IO.Path]::GetFullPath($EvidenceDirectory)) 'rpc.json'), (ConvertTo-Json -InputObject $calls.ToArray() -Depth 40), [Text.UTF8Encoding]::new($false))
    }
}
