$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$temp = Join-Path (Split-Path $root) ('outputs\generic-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
$register = Join-Path $root 'scripts\Register-YuzuhaMcp.ps1'
$exe = Join-Path $root 'runtime\net10\YuzuhaToolkit.Mcp.exe'
$knowledge = Join-Path $root 'runtime\net10\YuzuhaToolkit.Knowledge.exe'
$config = Join-Path $temp 'mcp.json'
$argsForRegister = @{ McpJsonPath=$config; McpExecutable=$exe; KnowledgeExecutable=$knowledge; ToolkitRoot=$root }
function Assert($condition, $message) { if (-not $condition) { throw $message } }
function codex { throw 'Generic registration must not invoke Codex.' }
& $register @argsForRegister -CheckOnly
Assert (-not (Test-Path $config)) 'CheckOnly wrote JSON'
& $register @argsForRegister -WhatIf
Assert (-not (Test-Path $config)) 'WhatIf wrote JSON'
$original = '{"settings":{"keep":true},"mcpServers":{"unrelated":{"command":"elsewhere","args":["keep"]}}}'
[IO.File]::WriteAllText($config, $original)
& $register @argsForRegister
$j = [IO.File]::ReadAllText($config) | ConvertFrom-Json
Assert ($j.settings.keep -and $j.mcpServers.unrelated.args[0] -eq 'keep') 'Unrelated config lost'
Assert ($j.mcpServers.YuzuhaToolkit.command -eq $exe) 'Execution server absent'
Assert ($j.mcpServers.YuzuhaToolkitKnowledge.command -eq $knowledge) 'Knowledge server absent'
$hash = (Get-FileHash $config).Hash
& $register @argsForRegister
Assert ((Get-FileHash $config).Hash -eq $hash) 'Reuse rewrote config'
$j.mcpServers.YuzuhaToolkitKnowledge.args = @('conflict')
[IO.File]::WriteAllText($config, ($j | ConvertTo-Json -Depth 20))
$hash = (Get-FileHash $config).Hash
$rejected = $false
try { & $register @argsForRegister } catch { $rejected = $true }
Assert $rejected 'Conflicting second entry was accepted'
Assert ((Get-FileHash $config).Hash -eq $hash) 'Conflict changed config'
[IO.File]::WriteAllText($config, $original)
$rejected = $false
try { & $register @argsForRegister -AvevaProfile PDMS -EvarBat (Join-Path $temp 'missing.INIT') } catch { $rejected=$true }
Assert $rejected 'Missing EVAR accepted'
Assert ([IO.File]::ReadAllText($config) -ceq $original) 'EVAR preflight failure changed JSON'
foreach ($case in @(@('PDMS','pdmsui'),@('E3D2.1','pmlui'))) {
    $init = Join-Path $temp ($case[0] + '.INIT')
    [IO.File]::WriteAllText($init, "@echo off`r`nset pmllib=original`r`nset $($case[1])=original-ui`r`n")
    & $register -SkipMcpRegistration -ToolkitRoot $root -AvevaProfile $case[0] -EvarBat $init
    $text = [IO.File]::ReadAllText($init)
    Assert ($text.IndexOf('rem >>>') -gt $text.IndexOf('set pmllib=original')) 'Managed block not appended'
    Assert ($text.Contains("set $($case[1])=")) 'Wrong framework UI variable'
    $hash = (Get-FileHash $init).Hash
    & $register -SkipMcpRegistration -ToolkitRoot $root -AvevaProfile $case[0] -EvarBat $init
    Assert ((Get-FileHash $init).Hash -eq $hash) 'INIT rewrite is not idempotent'
}
# Failure after JSON commit must restore original bytes. A read-only fixture prevents EVAR write.
$init = Join-Path $temp 'locked.INIT'
[IO.File]::WriteAllText($init, "@echo off`r`n")
[IO.File]::SetAttributes($init, [IO.FileAttributes]::ReadOnly)
try {
    $rejected = $false
    try { & $register @argsForRegister -AvevaProfile PDMS -EvarBat $init } catch { $rejected=$true }
    Assert $rejected 'Read-only EVAR did not fail'
    Assert ([IO.File]::ReadAllText($config) -ceq $original) 'JSON rollback did not restore bytes'
} finally { [IO.File]::SetAttributes($init, [IO.FileAttributes]::Normal) }
Write-Output 'PASS: generic dual-server registration, preview, conflicts, no Codex, unrelated state, INIT tail placement/framework choice/idempotency, failed EVAR rollback.'
