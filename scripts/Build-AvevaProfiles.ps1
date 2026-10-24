[CmdletBinding()]
param(
    [string] $ProfileRoot = $(
        if ($env:AVEVA_PROFILE_ROOT) { $env:AVEVA_PROFILE_ROOT }
        else { 'D:\AVEVA\AvevaProfile' }
    ),
    [string] $Configuration = 'Release',
    [string] $OutputRoot = '',
    [Alias('SkipMcp')][switch] $SkipCli
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# $PSScriptRoot is empty inside parameter defaults on Windows PowerShell 5.1.
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

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'src'
$net35Project = Join-Path $sourceRoot 'YuzuhaToolkit.PmlHost.Net35\YuzuhaToolkit.PmlHost.Net35.csproj'
$net48Project = Join-Path $sourceRoot 'YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj'

if (-not (Test-Path -LiteralPath $ProfileRoot -PathType Container)) {
    throw "AVEVA profile root does not exist: $ProfileRoot"
}

$profiles = @(
    [pscustomobject]@{ Name = 'AM'; Framework = 'net35'; Project = $net35Project },
    [pscustomobject]@{ Name = 'PDMS'; Framework = 'net35'; Project = $net35Project },
    [pscustomobject]@{ Name = 'E3D2.1'; Framework = 'net48'; Project = $net48Project },
    [pscustomobject]@{ Name = 'E3D3.1.6'; Framework = 'net48'; Project = $net48Project }
)

Invoke-DotNet @('restore', $net35Project)

foreach ($profile in $profiles) {
    $profileDir = Join-Path $ProfileRoot $profile.Name
    if (-not (Test-Path -LiteralPath (Join-Path $profileDir 'PMLNet.dll'))) {
        throw "Profile '$($profile.Name)' does not contain PMLNet.dll: $profileDir"
    }

    $outputDir = Join-Path $OutputRoot "$($profile.Name)\$($profile.Framework)"
    $objDir = Join-Path $projectRoot "artifacts\obj\profiles\$($profile.Name)\$($profile.Framework)"
    New-Item -ItemType Directory -Path $outputDir, $objDir -Force | Out-Null

    Write-Host "Building $($profile.Name) -> $outputDir"
    Invoke-DotNet @(
        'msbuild', $profile.Project,
        '/t:Build',
        "/p:Configuration=$Configuration",
        "/p:AvevaProfileRoot=$ProfileRoot",
        "/p:AvevaProfile=$($profile.Name)",
        "/p:OutputPath=$outputDir\",
        "/p:IntermediateOutputPath=$objDir\",
        '/p:AppendTargetFrameworkToOutputPath=false'
    )
}

if (-not $SkipCli) {
    $cliOutput = Join-Path $projectRoot 'runtime\cli'
    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\')
    $resolvedCliOutput = [System.IO.Path]::GetFullPath($cliOutput)
    if (-not $resolvedCliOutput.StartsWith(
            $resolvedProjectRoot + '\',
            [System.StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($resolvedCliOutput) -ne 'cli') {
        throw "Refusing to clean unexpected CLI output path: $resolvedCliOutput"
    }

    New-Item -ItemType Directory -Path $resolvedCliOutput -Force | Out-Null

    & (Join-Path $PSScriptRoot 'Build-NativeCli.ps1') -OutputRoot $cliOutput -Configuration $Configuration

}

Write-Host "All AVEVA profiles built under: $OutputRoot"
if (-not $SkipCli) {
    Write-Host "Native AOT CLI: $cliOutput\PmlTwin.Phayton.Cli.exe"
}
