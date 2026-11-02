[CmdletBinding()]
param([string]$OutputRoot = 'artifacts/docfx-user/site')
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
$taskArtifacts = [IO.Path]::GetFullPath((Join-Path $taskRepository 'artifacts'))
$taskUserArtifacts = [IO.Path]::GetFullPath((Join-Path $taskArtifacts 'docfx-user'))
$taskSite = [IO.Path]::GetFullPath($OutputRoot, $taskRepository)
if (-not $taskSite.StartsWith($taskUserArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'User documentation output must stay beneath the dedicated artifacts/docfx-user directory.'
}
for ($taskAncestor = $taskSite; $taskAncestor.Length -ge $taskArtifacts.Length; $taskAncestor = [IO.Path]::GetDirectoryName($taskAncestor)) {
    if ((Test-Path -LiteralPath $taskAncestor) -and ((Get-Item -LiteralPath $taskAncestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "User documentation output cannot use a reparse point: $taskAncestor"
    }
    if ($taskAncestor -eq $taskArtifacts) { break }
}
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
Push-Location $taskRepository
try {
    if (Test-Path -LiteralPath $taskSite) { Remove-Item -LiteralPath $taskSite -Recurse -Force }
    # --output is a DocFX base directory; combining it with build.dest nests the
    # configured output below that base. Freeze an explicit absolute destination
    # instead. DocFX requires an out-of-base source mapping to remain relative,
    # so resolve it against the repository, then rebase it to the temp config.
    $taskConfiguration = Get-Content -LiteralPath (Join-Path $taskRepository 'docfx.user.json') -Raw | ConvertFrom-Json -AsHashtable
    $taskConfiguration.build.dest = $taskSite
    foreach ($taskGroup in @('content', 'resource')) {
        foreach ($taskMapping in $taskConfiguration.build[$taskGroup]) {
            $taskMapping.src = [IO.Path]::GetRelativePath($taskUserArtifacts, [IO.Path]::GetFullPath($taskMapping.src, $taskRepository))
        }
    }
    New-Item -ItemType Directory -Path $taskUserArtifacts -Force | Out-Null
    $taskConfigurationPath = Join-Path $taskUserArtifacts ('build-config-' + [Guid]::NewGuid().ToString('N') + '.json')
    $taskConfiguration | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $taskConfigurationPath -Encoding utf8
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'DocFX tool restore failed.' }
    dotnet docfx build $taskConfigurationPath --warningsAsErrors
    if ($LASTEXITCODE -ne 0) { throw 'Independent user-guide DocFX build failed.' }
    # Modern template source maps are developer assets, not user pages. This does
    # not copy or prune the complete developer site; only this new site is touched.
    foreach ($taskMap in Get-ChildItem -LiteralPath $taskSite -Recurse -File -Filter '*.map') {
        $taskMapPath = [IO.Path]::GetFullPath($taskMap.FullName)
        if (-not $taskMapPath.StartsWith($taskSite + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source map escaped user output.' }
        Remove-Item -LiteralPath $taskMapPath -Force
    }
    python scripts/build-documentation-bundles.py --mode check-user --user-site $taskSite
    if ($LASTEXITCODE -ne 0) { throw 'User-guide budget, navigation, search or stable anchors failed.' }
    Write-Host "Independent user guide: $taskSite"
} finally {
    Pop-Location
}
