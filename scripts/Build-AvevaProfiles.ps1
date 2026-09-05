[CmdletBinding()]
param(
    [string] $ProfileRoot = $(
        if ($env:AVEVA_PROFILE_ROOT) { $env:AVEVA_PROFILE_ROOT }
        else { 'D:\AVEVA\AvevaProfile' }
    ),
    [string] $Configuration = 'Release',
    [string] $OutputRoot = '',
    [switch] $SkipMcp
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
$mcpProject = Join-Path $sourceRoot 'YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj'

if (-not (Test-Path -LiteralPath $ProfileRoot -PathType Container)) {
    throw "AVEVA profile root does not exist: $ProfileRoot"
}

$profiles = @(
    [pscustomobject]@{ Name = 'AM'; Framework = 'net35'; Project = $net35Project },
    [pscustomobject]@{ Name = 'PDMS'; Framework = 'net35'; Project = $net35Project },
    [pscustomobject]@{ Name = 'E3D2.1'; Framework = 'net48'; Project = $net48Project },
    [pscustomobject]@{ Name = 'E3D3.1.0'; Framework = 'net48'; Project = $net48Project },
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

if (-not $SkipMcp) {
    $mcpOutput = Join-Path $projectRoot 'runtime\net10'
    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\')
    $resolvedMcpOutput = [System.IO.Path]::GetFullPath($mcpOutput)
    if (-not $resolvedMcpOutput.StartsWith(
            $resolvedProjectRoot + '\',
            [System.StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($resolvedMcpOutput) -ne 'net10') {
        throw "Refusing to clean unexpected MCP output path: $resolvedMcpOutput"
    }

    New-Item -ItemType Directory -Path $resolvedMcpOutput -Force | Out-Null

    Invoke-DotNet @('restore', $mcpProject)
    Invoke-DotNet @(
        'publish', $mcpProject,
        '--configuration', $Configuration,
        '--no-restore',
        '--output', $mcpOutput
    )
    $knowledgeProject = Join-Path $sourceRoot 'YuzuhaToolkit.Knowledge\YuzuhaToolkit.Knowledge.csproj'
    Invoke-DotNet @('restore', $knowledgeProject)
    Invoke-DotNet @('publish', $knowledgeProject, '--configuration', $Configuration,
        '--no-restore', '--output', $mcpOutput)
}

Write-Host "All AVEVA profiles built under: $OutputRoot"
if (-not $SkipMcp) {
    Write-Host "Trimmed self-contained single-file MCP: $mcpOutput\YuzuhaToolkit.Mcp.exe"
}
