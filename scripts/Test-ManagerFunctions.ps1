param([string]$Configuration = 'Debug', [string]$ReportPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manager = Join-Path $repository "src/YuzuhaToolkit.Manager.Cli/bin/$Configuration/net10.0/Yuzuha.Manager.Cli.dll"
$communication = Join-Path $repository "src/PmlTwin.Phayton.Cli/bin/$Configuration/net10.0/PmlTwin.Phayton.Cli.dll"
foreach ($entry in @($manager, $communication)) { if (-not (Test-Path -LiteralPath $entry)) { throw "Build both CLI projects first: $entry" } }
# 2147483647 不是本机真实会话；调用前确认，随后仅验证严格目标拒绝，绝不选择其他 PID。
if (Get-Process -Id 2147483647 -ErrorAction SilentlyContinue) { throw 'Reserved absent PID unexpectedly exists; refuse this smoke test.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('pmltwin-functions-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch) | Out-Null
$savedEnvironment = @{}
foreach ($key in @('YUZUHA_COMMUNICATION_CLI', 'YUZUHA_FUNCTION_REGISTRY', 'YUZUHA_STATE_DIR')) { $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key) }
$checks = [Collections.Generic.List[object]]::new()
function Invoke-FunctionCheck([string]$Name, [string]$Id, [object]$InputValue, [string[]]$Options, [string]$ExpectedCode) {
    $request = ConvertTo-Json -InputObject $InputValue -Depth 20 -Compress
    $lines = $request | & $taskSelectedDotnet $manager op invoke $Id --input-stdin @Options
    $exit = $LASTEXITCODE
    $reply = ($lines -join "`n") | ConvertFrom-Json
    if ($reply.operationId -ne $Id -or $reply.exitCode -ne $exit) { throw "$Name : mismatched operation or exit code" }
    if ($ExpectedCode) {
        if ($reply.success -or $reply.error.code -ne $ExpectedCode -or $reply.error.mayHaveExecuted) { throw "$Name : unexpected result: $lines" }
    } elseif (-not $reply.success) { throw "$Name : unexpected failure: $lines" }
    $checks.Add([ordered]@{ name = $Name; operationId = $Id; exitCode = $exit; code = $reply.error.code; message = $reply.error.message; mayHaveExecuted = [bool]$reply.error.mayHaveExecuted; passed = $true })
    return $reply
}
try {
    $env:YUZUHA_COMMUNICATION_CLI = $communication
    $env:YUZUHA_FUNCTION_REGISTRY = Join-Path $scratch 'functions.json'
    $env:YUZUHA_STATE_DIR = Join-Path $scratch 'state'
    $source = Join-Path $scratch 'SmokeNeverDispatch.pmlfnc'
    [IO.File]::WriteAllText($source, "define function !!SmokeNeverDispatch() is STRING`r`nreturn 'synthetic'`r`nendfunction`r`n")
    $write = @('--allow-write')
    $target = @('--allow-execution', '--pid', '2147483647', '--expected-model', 'DESIGN', '--expected-start-time-utc-ticks', '1')
    $call = @{ functionName = 'SmokeNeverDispatch'; parameters = @() }
    $registered = Invoke-FunctionCheck 'register-denied' 'pml.function.register' @{ sourceFile = $source; functionName = 'SmokeNeverDispatch' } $write ''
    if ($registered.data.AllowInvoke) { throw 'Registration unexpectedly granted invocation.' }
    $null = Invoke-FunctionCheck 'list' 'pml.functions.list' @{} @() ''
    $null = Invoke-FunctionCheck 'deny-before-target' 'pml.function.invoke' $call $target 'ACCESS_DENIED'
    $null = Invoke-FunctionCheck 'grant' 'pml.function.permission' @{ functionName = 'SmokeNeverDispatch'; allowInvoke = $true } $write ''
    $null = Invoke-FunctionCheck 'absent-host-refused' 'pml.function.invoke' $call $target 'TARGET_FAILED'
    [IO.File]::AppendAllText($source, "`r`n-- changed source`r`n")
    $null = Invoke-FunctionCheck 'changed-source-refused' 'pml.function.invoke' $call $target 'INVALID_INPUT'
    $null = Invoke-FunctionCheck 'changed-source-grant-refused' 'pml.function.permission' @{ functionName = 'SmokeNeverDispatch'; allowInvoke = $true } $write 'INVALID_INPUT'
    $registered = Invoke-FunctionCheck 'reregister-revokes' 'pml.function.register' @{ sourceFile = $source; functionName = 'SmokeNeverDispatch' } $write ''
    if ($registered.data.AllowInvoke) { throw 'Re-registration unexpectedly retained invocation permission.' }
    $null = Invoke-FunctionCheck 'grant-new-source' 'pml.function.permission' @{ functionName = 'SmokeNeverDispatch'; allowInvoke = $true } $write ''
    $null = Invoke-FunctionCheck 'revoke' 'pml.function.permission' @{ functionName = 'SmokeNeverDispatch'; allowInvoke = $false } $write ''
    $null = Invoke-FunctionCheck 'revoked-call-refused' 'pml.function.invoke' $call $target 'ACCESS_DENIED'
    $report = [ordered]@{ recordedAt = [DateTimeOffset]::UtcNow.ToString('O'); checks = $checks.ToArray(); actualAvevaDispatch = $false; retries = 0;
        managerSha256 = (Get-FileHash -LiteralPath $manager -Algorithm SHA256).Hash; communicationSha256 = (Get-FileHash -LiteralPath $communication -Algorithm SHA256).Hash }
    $json = ConvertTo-Json -InputObject $report -Depth 20
    if ($ReportPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportPath), $json) }
    $json
} finally {
    foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key]) }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedScratch = [IO.Path]::GetFullPath($scratch)
    if (-not $resolvedScratch.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refuse cleanup outside temporary directory.' }
    Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
}
