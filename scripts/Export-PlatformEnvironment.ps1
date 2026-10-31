[CmdletBinding()]
param(
    [string] $ManagerAssembly = 'src/YuzuhaToolkit.Manager.Cli/bin/Release/net10.0/Yuzuha.Manager.Cli.dll',
    [string] $OutputFile = 'artifacts/platform/environment.json'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepository = Split-Path -Parent $PSScriptRoot
$taskManager = [IO.Path]::GetFullPath($ManagerAssembly, $taskRepository)
if (!(Test-Path -LiteralPath $taskManager -PathType Leaf)) { throw 'Build the Manager CLI before collecting environment evidence.' }
# Reuse the product discovery operation. Do not execute initialization scripts or inspect credentials.
$taskRaw = & $env:DOTNET_HOST_PATH $taskManager op invoke environment.discover
$taskExit = $LASTEXITCODE
$taskEnvelope = ($taskRaw -join "`n") | ConvertFrom-Json
if ($taskExit -notin @(0, 2) -or !$taskEnvelope.data.candidates) { throw 'Discovery returned no usable candidates; inspect environment.discover diagnostics.' }
$taskCandidates = foreach ($taskCandidate in $taskEnvelope.data.candidates) {
    $taskIdentity = $taskCandidate.installation.identity
    $taskAssemblies = foreach ($taskName in @('Aveva.ApplicationFramework.dll', 'Aveva.ApplicationFramework.Implementation.dll', 'Aveva.ApplicationFramework.Presentation.dll')) {
        $taskPath = Join-Path $taskIdentity.root $taskName
        if (!(Test-Path -LiteralPath $taskPath -PathType Leaf)) {
            [ordered]@{ name = $taskName; status = 'unavailable' }
            continue
        }
        try {
            # GetAssemblyName reads PE metadata; it does not load or execute the vendor assembly.
            $taskAssembly = [Reflection.AssemblyName]::GetAssemblyName($taskPath)
            [ordered]@{ name = $taskName; status = 'metadata-only'; identity = $taskAssembly.FullName; sha256 = (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
        catch { [ordered]@{ name = $taskName; status = 'unreadable'; code = $_.Exception.GetType().Name } }
    }
    [ordered]@{
        installationId = $taskIdentity.id; product = $taskIdentity.product; productVersion = $taskIdentity.productVersion
        versionHint = $taskIdentity.versionHint; entryKind = $taskIdentity.kind; entryName = [IO.Path]::GetFileName($taskIdentity.executable)
        entrySha256 = $taskIdentity.executableSha256; environmentSha256 = $taskIdentity.environmentSha256
        sources = $taskCandidate.sources; apsAvailable = $taskCandidate.apsAvailable
        modules = @($taskCandidate.moduleOptions.options | ForEach-Object { $_.value })
        cafAssemblies = @($taskAssemblies); cafAdapterStatus = 'unverified'; licenseCheckout = 'not-tested'; login = 'not-tested'
    }
}
$taskReport = [ordered]@{
    schemaVersion = 1; observedAt = $taskEnvelope.data.observedAt; expiresAt = $taskEnvelope.data.expiresAt
    evidenceKind = 'static-metadata'; complete = $taskEnvelope.data.coverage.complete
    filesSeen = $taskEnvelope.data.coverage.filesSeen; candidates = @($taskCandidates)
    diagnosticCodes = @($taskEnvelope.data.diagnostics | ForEach-Object { $_.code } | Sort-Object -Unique)
    limitations = @('No login or license checkout.', 'Assembly presence is not CAF API compatibility.', 'No credentials, user paths or initialization contents are exported.')
}
$taskDestination = [IO.Path]::GetFullPath($OutputFile, $taskRepository)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDestination)) | Out-Null
$taskReport | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $taskDestination -Encoding utf8
Write-Output $taskDestination
