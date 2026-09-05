[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AvevaInstallDir,

    [ValidateSet('auto', 'net35', 'net48')]
    [string] $Framework = 'auto',

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.]{0,31}$')]
    [string] $ProfileName,

    [string] $SourceRoot = '',

    [string] $OutputRoot = '',

    [string] $Configuration = 'Release',

    [switch] $SkipSafetyNotice
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# $PSScriptRoot is empty inside parameter defaults on Windows PowerShell 5.1.
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'runtime\profiles'
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]] $ArgumentList)

    & dotnet @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code ${LASTEXITCODE}: $($ArgumentList -join ' ')"
    }
}

if (-not $SkipSafetyNotice) {
    Write-Warning @"
Local host build: this compiles the Yuzuha PMLNet host against AVEVA
proprietary assemblies from '$AvevaInstallDir'. The resulting host is
version-specific and untested against your AVEVA build; a mismatch can fail
at load time or destabilize AVEVA. The AVEVA assemblies stay on this machine
and the built host must never be redistributed or shared. Only this
NET35/NET48 host is built here; the Net10 MCP servers are version-independent
and are not rebuilt.
"@
}

$installDir = [System.IO.Path]::GetFullPath($AvevaInstallDir.Trim())
if (-not (Test-Path -LiteralPath $installDir -PathType Container)) {
    throw "AVEVA install directory does not exist: $installDir"
}

$pmlNet = Join-Path $installDir 'PMLNet.dll'
if (-not (Test-Path -LiteralPath $pmlNet -PathType Leaf)) {
    throw "PMLNet.dll was not found under: $installDir. Point -AvevaInstallDir at the directory that contains it (for example the Everything3D or PDMS binary directory)."
}

$isE3D = Test-Path -LiteralPath (Join-Path $installDir 'Aveva.Core.Utilities.dll') -PathType Leaf
$isLegacy = Test-Path -LiteralPath (Join-Path $installDir 'Aveva.Pdms.Utilities.dll') -PathType Leaf
if ($isE3D) {
    $family = 'E3D'
    $framework = if ($Framework -eq 'auto') { 'net48' } else { $Framework }
}
elseif ($isLegacy) {
    $family = 'Legacy'
    $framework = if ($Framework -eq 'auto') { 'net35' } else { $Framework }
}
else {
    throw "Neither Aveva.Core.Utilities.dll (E3D) nor Aveva.Pdms.Utilities.dll (AM/PDMS) was found under: $installDir"
}

if ($family -eq 'E3D' -and $framework -ne 'net48') {
    throw "An E3D installation requires the net48 host."
}
if ($family -eq 'Legacy' -and $framework -ne 'net35') {
    throw "An AM/PDMS installation requires the net35 host."
}

if ([string]::IsNullOrWhiteSpace($ProfileName)) {
    throw "ProfileName is required, for example E3D3.2.0 or PDMS12.2. Use a name that is not one of the prebuilt profiles (AM, PDMS, E3D2.1, E3D3.1.0, E3D3.1.6)."
}
$ProfileName = $ProfileName.Trim()
foreach ($reserved in @('AM', 'PDMS', 'E3D2.1', 'E3D3.1.0', 'E3D3.1.6')) {
    if ($ProfileName -eq $reserved) {
        throw "Profile '$ProfileName' ships prebuilt with the package; a local build would shadow it. Choose a different name."
    }
}

$projectFile = if ($framework -eq 'net48') {
    'YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj'
}
else {
    'YuzuhaToolkit.PmlHost.Net35\YuzuhaToolkit.PmlHost.Net35.csproj'
}
$project = Join-Path $SourceRoot $projectFile
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw @"
Host source was not found: $project. A local build needs the source tree
(clone https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha.git or extract the
matching source release), because the agent package ships binaries only.
Pass -SourceRoot at that checkout.
"@
}

dotnet --version | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "The .NET SDK was not found on PATH. Install the .NET SDK plus the '$framework' targeting pack (Visual Studio '.NET desktop build tools' or the matching .NET Framework Developer Pack), then retry."
}

$outputDir = Join-Path $OutputRoot "$ProfileName\$framework"
$objDir = Join-Path (Split-Path -Parent $SourceRoot) "artifacts\obj\profiles\$ProfileName\$framework"
New-Item -ItemType Directory -Path $outputDir, $objDir -Force | Out-Null

Write-Host "Building profile '$ProfileName' ($family/$framework) -> $outputDir"
Invoke-DotNet @(
    'msbuild', $project,
    '/t:Build',
    "/p:Configuration=$Configuration",
    "/p:AvevaInstallDir=$installDir",
    "/p:AvevaProfile=$ProfileName",
    "/p:OutputPath=$outputDir\",
    "/p:IntermediateOutputPath=$objDir\",
    '/p:AppendTargetFrameworkToOutputPath=false'
)

Write-Host ""
Write-Host "Local host built: $outputDir"
Write-Host "Next steps:"
Write-Host "  1. Keep AVEVA fully closed."
Write-Host "  2. Set the profile before starting AVEVA:  set Yuzuha=$ProfileName"
Write-Host "     Set its runtime family as well:  set YuzuhaFramework=$framework"
Write-Host "     (the EVAR managed block can be edited to add this value)."
Write-Host "  3. Start AVEVA and verify:  !!YuzuhaRpcHost.GetRpcServerStatus()  -> RUNNING"
Write-Host "Risk reminder: this host was compiled against your local AVEVA binaries"
Write-Host "and is not covered by the prebuilt profile testing. Do not share it."
