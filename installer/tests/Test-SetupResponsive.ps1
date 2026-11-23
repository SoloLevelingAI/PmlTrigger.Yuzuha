# Run on an interactive Windows desktop with Inno Setup and WinAppCLI.
param(
    [Parameter(Mandatory)] [string] $Iscc,
    [Parameter(Mandatory)] [string] $WinApp,
    [Parameter(Mandatory)] [string] $Payload,
    [Parameter(Mandatory)] [string] $EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
$env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'
. (Join-Path $PSScriptRoot 'Physical-Input.ps1')

$output = [IO.Directory]::CreateDirectory($EvidenceDirectory).FullName
$lab = [IO.Directory]::CreateDirectory((Join-Path $output ('inno-input-' + [guid]::NewGuid().ToString('N')))).FullName
$stage = [IO.Directory]::CreateDirectory((Join-Path $lab 'payload/setup')).FullName
$source = Join-Path $lab 'SlowHelper.cs'
@'
using System.Threading;
internal static class SlowHelper
{
    public static int Main() { Thread.Sleep(20000); return 1; }
}
'@ | Set-Content -LiteralPath $source -Encoding utf8
$helper = Join-Path $stage 'Yuzuha.SetupBridge.exe'
& "$env:SystemRoot/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo "/out:$helper" $source
if ($LASTEXITCODE -ne 0) { throw 'Could not compile the slow fixture.' }
Copy-Item -LiteralPath (Join-Path $Payload 'setup/Newtonsoft.Json.dll') -Destination $stage
Copy-Item -LiteralPath (Join-Path $Payload '.setup-payload.json') -Destination (Split-Path -Parent $stage)
$installer = Join-Path $PSScriptRoot '../Yuzuha.iss'
$version = '0.0.0'
& $Iscc "/DPayloadDir=$(Split-Path -Parent $stage)" "/DOutputDir=$lab" "/DAppVersion=$version" '/DProgramUpgradePreview=1' $installer |
    Set-Content -LiteralPath (Join-Path $lab 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Preview compilation failed. See $lab/compile.log." }

function Invoke-Ui {
    $response = & $WinApp ui @args | Out-String
    if ($LASTEXITCODE -ne 0) { throw $response }
    $response.Trim()
}

function Wait-For([scriptblock] $Probe, [string] $Missing) {
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $value = & $Probe
        if ($value) { return $value }
        Start-Sleep -Milliseconds 120
    } while ([DateTime]::UtcNow -lt $deadline)
    throw $Missing
}

function Control([string] $Tree, [string] $Role, [string] $Name) {
    $pattern = '(?m)^\s*(\d+)\s+' + $Role + '\s+"' + [regex]::Escape($Name) + '"'
    $match = [regex]::Match($Tree, $pattern)
    if (!$match.Success) { throw "Missing control: $Name" }
    [IntPtr][long]$match.Groups[1].Value
}

$preview = Join-Path $lab "Phayton-$version-Upgrade-Preview.exe"
$target = Join-Path $lab 'PmlTrigger.InputFixture'
$loader = Start-Process -FilePath $preview -ArgumentList @('/LANG=chinesesimp', ('/DIR="' + $target + '"')) -WindowStyle Normal -PassThru
$worker = $null
$child = $null
$wizard = $null
try {
    $child = Wait-For {
        Get-CimInstance Win32_Process -Filter "Name LIKE 'Phayton-%-Upgrade-Preview%'" |
            Where-Object ParentProcessId -eq $loader.Id | Select-Object -First 1
    } 'The preview process did not start.'
    $wizard = Wait-For {
        Invoke-Ui list-windows --json | ConvertFrom-Json | Where-Object ProcessId -eq $child.ProcessId |
            Where-Object title -Like '*Phayton*' | Select-Object -First 1
    } 'The preview window did not open.'
    $window = [IntPtr][long]$wizard.hwnd
    [SetupPhysicalInput]::KeepFixtureVisible($window)
    $inspect = { Invoke-Ui inspect -w $wizard.hwnd --depth 12 --hide-offscreen --quiet }
    $terms = '我已阅读并同意 Apache 2.0'
    [SetupPhysicalInput]::Click($window, (Control (& $inspect) 'CheckBox' $terms), $true)
    $checked = Wait-For {
        $tree = & $inspect
        if ($tree.Contains('Apache 2.0" [on]')) { $tree }
    } 'The license checkbox did not respond.'
    [SetupPhysicalInput]::Click($window, (Control $checked 'Button' '安装'), $false)
    $worker = Wait-For {
        Get-CimInstance Win32_Process -Filter "Name='Yuzuha.SetupBridge.exe'" |
            Where-Object ParentProcessId -eq $child.ProcessId | Select-Object -First 1
    } 'The slow preflight did not start.'
    $distance = [SetupPhysicalInput]::Drag($window)
    if ($distance -lt 65 -or ![SetupPhysicalInput]::Ping($window)) { throw 'The wizard stopped responding during preflight.' }
    Invoke-Ui screenshot -w $wizard.hwnd -o (Join-Path $output 'installer-preparing.png') --json | Out-Null
    [SetupPhysicalInput]::Click($window, (Control (& $inspect) 'Button' '取消准备'), $false)
    $null = Wait-For {
        (& $inspect).Contains('已取消准备') -and !(Get-Process -Id $worker.ProcessId -ErrorAction SilentlyContinue)
    } 'The cancel button did not stop preflight and restore the wizard.'
    [pscustomobject]@{ WizardResponded = $true; DragPixels = $distance; PreflightCancelled = $true } |
        ConvertTo-Json | Tee-Object -FilePath (Join-Path $output 'installer-input.json')
} finally {
    if ($worker) { Stop-Process -Id $worker.ProcessId -ErrorAction SilentlyContinue }
    if ($child) { Stop-Process -Id $child.ProcessId -ErrorAction SilentlyContinue }
    if (!$loader.HasExited) { Stop-Process -Id $loader.Id -ErrorAction SilentlyContinue }
}
