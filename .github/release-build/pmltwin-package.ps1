param(
    [string] $Repository = 'SoloLevelingAI/PmlTrigger.Yuzuha',
    [string] $Checkout = $env:GITHUB_WORKSPACE,
    [string] $Scratch = $env:RUNNER_TEMP,
    [string] $Compiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [string] $SetupVersion = '0.3.5.6'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$checkoutDirectory = (Resolve-Path -LiteralPath $Checkout).Path
$compilerPath = (Resolve-Path -LiteralPath $Compiler).Path
$buildDirectory = Join-Path (Resolve-Path -LiteralPath $Scratch).Path ('phayton-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $buildDirectory
$sourceDirectory = Join-Path $buildDirectory 'source'
$baselineDirectory = Join-Path $buildDirectory 'baseline'
$releaseDirectory = Join-Path $buildDirectory 'release'
$desktopDirectory = Join-Path $buildDirectory 'desktop'

$baselineTag = 'v0.3.5.5-login-fix.1'
$baselineFile = 'Phayton-0.3.5.5-login-fix.1-win-x64-signed.zip'
$baselineHash = '9cd636250739c74b0e824f78a7cd903a3aa8317a022c849c56344b82f24ea9fc'
& gh release download $baselineTag --repo $Repository --pattern $baselineFile --dir $buildDirectory
$baselineArchive = Join-Path $buildDirectory $baselineFile
if ((Get-FileHash -LiteralPath $baselineArchive).Hash -ine $baselineHash) {
    throw 'The official runtime ZIP did not match its published SHA-256.'
}
Expand-Archive -LiteralPath $baselineArchive -DestinationPath $baselineDirectory
$program = @(
    Get-ChildItem -LiteralPath $baselineDirectory -Filter '.setup-payload.json' -File -Recurse -Force |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.DirectoryName 'runtime/cli/PmlTwin.Phayton.Cli.exe') }
)
if ($program.Count -ne 1) { throw 'The official runtime ZIP must contain one program manifest.' }
$manifest = Get-Content -LiteralPath $program[0].FullName -Raw | ConvertFrom-Json
$baselineRoot = $program[0].DirectoryName
if ($manifest.scope -ne 'program' -or $manifest.entryPoint -ne 'runtime/cli/PmlTwin.Phayton.Cli.exe') {
    throw 'The previous release manifest is not the expected Phayton program layout.'
}

# A checkout and an archive of its exact commit avoid build-state or local user files.
$commit = (& git -C $checkoutDirectory rev-parse HEAD).Trim()
if ($commit -ne '8fc61f2cd6b86286baf86fb4aa78bce5bfa0cb1e') { throw 'Wrong release source.' }
$sourceArchive = Join-Path $buildDirectory 'source.zip'
& git -C $checkoutDirectory archive --format=zip "--output=$sourceArchive" $commit
Expand-Archive -LiteralPath $sourceArchive -DestinationPath $sourceDirectory
$workbench = @(Get-ChildItem -LiteralPath (Join-Path $sourceDirectory 'workbench') -Recurse -File -Filter 'PmlTwin.Workbench.csproj')
if ($workbench.Count -ne 1) { throw 'Could not uniquely locate the standalone WPF project in workbench/.' }
Push-Location $sourceDirectory
try {
    & dotnet publish $workbench[0].FullName -c Release -r win-x64 --self-contained true `
        -p:BundlePhayton=false -p:BundleProjectReader=false "-p:SourceRevisionId=$commit" -o $desktopDirectory
    $desktop = Get-Item -LiteralPath (Join-Path $desktopDirectory 'PmlTwin.Workbench.exe')
    if ($desktop.VersionInfo.ProductVersion -ne "0.2.7+$commit") { throw 'Incorrect Workbench product version.' }

    $stageRoots = @('profiles', 'manager', 'webapps')
    $passRoots = @('cli', 'documentation', 'documentation-host')
    foreach ($file in $manifest.files) {
        $relative = $file.path
        if ($relative -notmatch '^runtime/([^/]+)/') { continue }
        $component = $Matches[1]
        if ($component -notin $stageRoots -and $component -notin $passRoots) { continue }
        if ($relative -match '[\\:]' -or ($relative -split '/') -contains '..') {
            throw "Invalid path in the program manifest: $relative"
        }
        $from = Join-Path $baselineRoot $relative
        if ((Get-FileHash -LiteralPath $from).Hash -ine $file.sha256) {
            throw "A runtime component did not match its signed package inventory: $relative"
        }
        if ($component -in $stageRoots) {
            $to = Join-Path $sourceDirectory $relative
            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force
            Copy-Item -LiteralPath $from -Destination $to
        }
    }
    foreach ($component in $passRoots) {
        $folder = Join-Path $baselineRoot "runtime/$component"
        foreach ($file in Get-ChildItem -LiteralPath $folder -Recurse -File -Force) {
            $relative = [IO.Path]::GetRelativePath($baselineRoot, $file.FullName).Replace('\', '/')
            if ($relative -notin $manifest.files.path) { throw "Unlisted runtime file: $relative" }
        }
    }

    & python 'installer/build.py' --iscc $compilerPath --output $releaseDirectory --version $SetupVersion `
        --cli (Join-Path $baselineRoot 'runtime/cli') --desktop $desktopDirectory `
        --documentation (Join-Path $baselineRoot 'runtime/documentation') `
        --documentation-host (Join-Path $baselineRoot 'runtime/documentation-host')

    $setup = Join-Path $releaseDirectory "Phayton-$SetupVersion-Windows-Setup.exe"
    $agent = Join-Path $releaseDirectory "Phayton-$SetupVersion-Windows-Agent.zip"
    & python 'installer/tests/verify_delivery.py' $releaseDirectory
    & python 'installer/tests/verify_agent_bundle.py' $agent $setup

    $outputManifest = Get-Content -LiteralPath (Join-Path $releaseDirectory 'payload/.setup-payload.json') -Raw | ConvertFrom-Json
    $found = $outputManifest.files.path | Where-Object {
        $_ -match '(?i)/(Core30\.AvevaProjectReader\.exe(?:\.config)?|sag\.exe|YuzuhaToolkit\.Desktop\.exe)$'
    }
    if ($found) { throw "Unexpected installer component: $found" }

    [ordered]@{
        sourceCommit = $commit
        baselineTag = $baselineTag
        baselineSha256 = $baselineHash
        setupSha256 = (Get-FileHash -LiteralPath $setup).Hash
        agentSha256 = (Get-FileHash -LiteralPath $agent).Hash
        executedSetup = $false
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseDirectory 'build-verification.json') -Encoding utf8
    if ($env:GITHUB_OUTPUT) { "release_directory=$releaseDirectory" >> $env:GITHUB_OUTPUT }
    $releaseDirectory
}
finally { Pop-Location }
