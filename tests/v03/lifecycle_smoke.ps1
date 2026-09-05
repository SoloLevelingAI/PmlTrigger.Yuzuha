$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ($env:YUZUHA_TEST_PACKAGE) { $root = [IO.Path]::GetFullPath($env:YUZUHA_TEST_PACKAGE) }
$testRoot = Join-Path (Split-Path $root) ('outputs\v03-lifecycle-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$global:v03Entries = @()
$global:v03FailName = ''
$global:v03AddCount = 0
$global:v03FailRemove = $false
function codex {
    $global:LASTEXITCODE = 0
    if ($args[1] -eq 'list') { ConvertTo-Json -InputObject @($global:v03Entries) -Depth 10 -Compress; return }
    if ($args[1] -eq 'add') {
        $global:v03AddCount++
        $global:v03Entries += [pscustomobject]@{ name=$args[2]; enabled=$true; transport=[pscustomobject]@{type='stdio';command=$args[-1];args=@()} }
        # Simulate a nonzero exit even though the CLI already persisted the entry.
        if ($args[2] -eq $global:v03FailName) { $global:LASTEXITCODE=1 }
        return
    }
    if ($args[1] -eq 'remove') {
        if ($global:v03FailRemove) { $global:LASTEXITCODE=1; return }
        $removeName=$args[2]; $global:v03Entries=@($global:v03Entries | Where-Object name -ne $removeName); return
    }
    throw 'Unexpected mock codex call'
}
function Assert($ok,$message) { if (-not $ok) { throw $message } }
function Invoke-Case($action,$caseName) {
    $install = Join-Path $testRoot "$caseName\PmlTrigger.Test"
    $codexTest = Join-Path $testRoot "$caseName\codex-test"
    foreach ($target in @($install,$codexTest)) {
        Assert ([IO.Path]::GetFullPath($target).StartsWith($testRoot+'\',[StringComparison]::OrdinalIgnoreCase)) 'Unsafe test target'
    }
    & (Join-Path $root 'scripts\YuzuhaAgentLifecycle.ps1') -Action $action -InstallRoot $install -CodexRoot $codexTest
}

$global:v03FailName='YuzuhaToolkitKnowledge'
$failed=$false
try { Invoke-Case Install 'failed' } catch { $failed=$true; Write-Host "Expected: $_" }
Assert $failed 'Second registration should fail'
Assert ($global:v03AddCount -eq 2) 'Expected both registrations to be attempted'
Assert ($global:v03Entries.Count -eq 0) 'New MCP entries survived rollback'
Assert (-not (Test-Path (Join-Path $testRoot 'failed\PmlTrigger.Test'))) 'Half-installed root survived'
Assert (-not (Test-Path (Join-Path $testRoot 'failed\codex-test\skills\yuzuha-toolkit'))) 'Half-installed Skill survived'

$global:v03FailName=''
$global:v03AddCount=0
$global:v03Entries=@([pscustomobject]@{ name='YuzuhaToolkitKnowledge';enabled=$true;transport=[pscustomobject]@{type='stdio';command='C:\unrelated\knowledge.exe';args=@()} })
$failed=$false
try { Invoke-Case Install 'conflict' } catch { $failed=$true }
Assert $failed 'Knowledge conflict must reject preflight'
Assert ($global:v03AddCount -eq 0) 'Preflight conflict added an MCP'
Assert ($global:v03Entries.Count -eq 1) 'Unrelated registration changed'

$global:v03Entries=@()
Invoke-Case Install 'success'
$install=Join-Path $testRoot 'success\PmlTrigger.Test'
$official=Join-Path $install 'knowledge\official-test.sqlite3'
$experience=Join-Path $install 'knowledge\experience.sqlite3'
Copy-Item (Join-Path $install 'knowledge\project.sqlite3') $official
$eh=(Get-FileHash $experience).Hash
$oh=(Get-FileHash $official).Hash
foreach ($rel in @('trust\pml-function-trust.json','runtime\profiles\PDMS12.1\net35\custom.txt')) {
    $path=Join-Path $install $rel
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    Set-Content -LiteralPath $path -Value 'sentinel'
}
Invoke-Case Update 'success'
Assert ((Get-FileHash $experience).Hash -eq $eh) 'Experience changed during update'
Assert ((Get-FileHash $official).Hash -eq $oh) 'Official DB changed during update'
Assert (Test-Path (Join-Path $install 'trust\pml-function-trust.json')) 'Trust lost'
Assert (Test-Path (Join-Path $install 'runtime\profiles\PDMS12.1\net35\custom.txt')) 'Custom Host lost'
Assert ($global:v03Entries.Count -eq 2) 'Successful update altered reusable registration count'

# Model upgrading a one-MCP v0.2 installation: second registration fails.
$global:v03Entries=@($global:v03Entries | Where-Object name -eq 'YuzuhaToolkit')
$global:v03FailName='YuzuhaToolkitKnowledge'
$before=(Get-FileHash (Join-Path $install '.yuzuha-agent-managed.json')).Hash
$failed=$false
try { Invoke-Case Update 'success' } catch { $failed=$true }
Assert $failed 'Upgrade should fail when second registration fails'
Assert ($global:v03Entries.Count -eq 1 -and $global:v03Entries[0].name -eq 'YuzuhaToolkit') 'Pre-existing MCP was not preserved'
Assert ((Get-FileHash (Join-Path $install '.yuzuha-agent-managed.json')).Hash -eq $before) 'Old installation was not restored'
Assert ((Get-FileHash $experience).Hash -eq $eh) 'Experience lost on failed update'
Write-Output 'PASS: partial-add rollback; preflight conflict; successful install/update; official/experience/trust/custom Host retained; failed upgrade restored'

# Exercise EVAR generation for NET35 and NET48 without loading any Host.
foreach ($profile in @('PDMS','AM','E3D3.1.6')) {
    $evar=Join-Path $testRoot "$profile.bat"
    [IO.File]::WriteAllText($evar,"@echo off`r`nrem review`r`n")
    & (Join-Path $root 'scripts\Register-YuzuhaMcp.ps1') -SkipMcpRegistration -ToolkitRoot $root -AvevaProfile $profile -EvarBat $evar
    $expected=if($profile -in @('PDMS','AM')){'net35'}else{'net48'}
    Assert ((Get-Content -Raw $evar).Contains("YuzuhaFramework=$expected")) "Wrong framework for $profile"
}
$customRoot=Join-Path $testRoot 'PmlTrigger.Custom'
New-Item -ItemType Directory -Path "$customRoot\PMLLIB","$customRoot\PMLUI","$customRoot\runtime\profiles\LegacyCustom\net35" -Force | Out-Null
Set-Content -LiteralPath "$customRoot\runtime\profiles\LegacyCustom\net35\YuzuhaToolkit.PmlHost.Net35.dll" -Value 'fake profile, never executed'
$evar=Join-Path $testRoot 'custom.bat'
[IO.File]::WriteAllText($evar,"@echo off`r`n")
& (Join-Path $root 'scripts\Register-YuzuhaMcp.ps1') -SkipMcpRegistration -ToolkitRoot $customRoot -AvevaProfile LegacyCustom -EvarBat $evar
Assert ((Get-Content -Raw $evar).Contains('YuzuhaFramework=net35')) 'Custom Legacy framework not recorded'
Write-Output 'PASS: PDMS/AM/E3D/custom Legacy EVAR framework selection'

$global:v03Entries=@()
$global:v03FailName='YuzuhaToolkitKnowledge'
$global:v03FailRemove=$true
$failed=$false
try { Invoke-Case Install 'recovery' } catch { $failed=$_.Exception.Message -like '*Rollback incomplete*' }
Assert $failed 'Rollback failure must give an explicit recovery error'
Assert (Test-Path (Join-Path $testRoot 'recovery\PmlTrigger.Test\runtime\net10\YuzuhaToolkit.Mcp.exe')) 'Recovery deleted files still referenced by MCP'
Write-Output 'PASS: failed rollback retains deployed files for recovery'
