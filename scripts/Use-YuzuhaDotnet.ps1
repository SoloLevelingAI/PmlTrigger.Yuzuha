# Dot-source this file to select the repository SDK without changing machine settings.
$taskSdkVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../global.json') -Raw | ConvertFrom-Json).sdk.version
$taskDotnetCandidates = @(
    $(if ($env:DOTNET_ROOT) { Join-Path $env:DOTNET_ROOT 'dotnet.exe' }),
    (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet/dotnet.exe'),
    $((Get-Command dotnet -ErrorAction SilentlyContinue).Source)
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique
$taskSelectedDotnet = $null
foreach ($taskDotnetCandidate in $taskDotnetCandidates) {
    $taskSdkOutput = & $taskDotnetCandidate --list-sdks
    if ($taskSdkOutput -match ('^' + [regex]::Escape($taskSdkVersion) + '\s')) { $taskSelectedDotnet = $taskDotnetCandidate; break }
}
if (-not $taskSelectedDotnet) { throw "Install .NET SDK $taskSdkVersion (global.json); no matching dotnet host was found." }
$env:DOTNET_ROOT = Split-Path -Parent $taskSelectedDotnet
$env:DOTNET_HOST_PATH = $taskSelectedDotnet
$env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
