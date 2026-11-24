param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repo = 'SoloLevelingAI/PmlTrigger.Yuzuha'
$tag = 'v0.3.5.6'
$manifest = Get-Content -LiteralPath (Join-Path $PackageDirectory 'build-verification.json') -Raw | ConvertFrom-Json
if ($manifest.sourceCommit -ne '8fc61f2cd6b86286baf86fb4aa78bce5bfa0cb1e') { throw 'Wrong release source.' }

function Get-Release {
    (gh release view $tag --repo $repo --json assets,isDraft,isPrerelease,tagName | Out-String) | ConvertFrom-Json
}
function Get-Asset([object]$release,[string]$name) {
    @($release.assets | Where-Object { $_.name -ceq $name }) | Select-Object -First 1
}
$draft = Get-Release
if (!$draft.isDraft -or !$draft.isPrerelease -or $draft.tagName -ne $tag) { throw 'Expected the pending test release.' }
$pdf = Get-Asset $draft 'PmlTwin-Configuration.zh-CN.pdf'
$plain = Get-Asset $draft 'SHA256SUMS.txt'
$expectedPdf = '07c6aa0e5554f5062b36ce57e91b23cc145e7e884b09cc35127b6503b229786d'
if ($pdf.digest -ne "sha256:$expectedPdf" -or $pdf.state -ne 'uploaded') { throw 'The PDF does not match the verified guide.' }

$assets = [ordered]@{
    'Phayton-0.3.5.6-Windows-Setup.exe' = $manifest.setupSha256
    'Phayton-0.3.5.6-Windows-Agent.zip' = $manifest.agentSha256
}
foreach ($name in $assets.Keys) {
    $path = Join-Path $PackageDirectory $name
    $hash = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
    if ($hash -ine $assets[$name]) { throw "Local hash mismatch: $name" }
    $known = Get-Asset $draft $name
    if ($known) {
        if ($known.digest -ne "sha256:$hash" -or $known.state -ne 'uploaded') { throw "A different asset already exists: $name" }
    } else {
        gh release upload $tag --repo $repo $path
    }
}
$uploaded = Get-Release
foreach ($name in $assets.Keys) {
    $hash = ([string]$assets[$name]).ToLowerInvariant()
    $server = Get-Asset $uploaded $name
    if ($server.digest -ne "sha256:$hash" -or $server.state -ne 'uploaded') { throw "GitHub hash mismatch: $name" }
}
$sha = Join-Path $PackageDirectory 'SHA256SUMS.txt'
$digests = @(foreach ($name in $assets.Keys) { "$( ([string]$assets[$name]).ToLowerInvariant() )  $name" })
$digests += "$expectedPdf  PmlTwin-Configuration.zh-CN.pdf"
$digests | Set-Content -LiteralPath $sha -Encoding ascii
$desired = (Get-FileHash -LiteralPath $sha).Hash.ToLowerInvariant()
if ($plain.digest -ne "sha256:$desired") {
    if ($plain.digest -ne 'sha256:ae9d70aea66685b6c3e227aca60c20d4f2ae62f89e58118e7058d5a09d2b3365') {
        throw 'The draft checksum file has been changed since this release was staged.'
    }
    gh release upload $tag --repo $repo $sha --clobber
}
$current = Get-Release
$remoteSums = Get-Asset $current 'SHA256SUMS.txt'
if (!$current.isDraft -or $current.assets.Count -ne 4 -or $remoteSums.digest -ne "sha256:$desired") {
    throw 'The draft package or checksum is incomplete.'
}
$current.assets | Select-Object name,size,digest | ConvertTo-Json -Depth 3
