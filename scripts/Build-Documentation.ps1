[CmdletBinding()]
param([ValidateRange(1024, 65535)][int]$Port = 5277)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$hostProcess = $null
Push-Location $root
try {
    # Rebuild generated content so removed APIs and obsolete hashed assets cannot linger.
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    foreach ($relative in @('architecture', 'docfx/api', 'docfx/site', 'docfx/navigation')) {
        $generatedDirectory = [IO.Path]::GetFullPath((Join-Path $artifactRoot $relative))
        if (-not $generatedDirectory.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Generated output path escapes artifacts.'
        }
        if (Test-Path -LiteralPath $generatedDirectory) {
            if ((Get-Item -LiteralPath $generatedDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Generated output must not be a junction or symlink: $generatedDirectory"
            }
            Remove-Item -LiteralPath $generatedDirectory -Recurse -Force
        }
    }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'DocFX tool restore failed.' }
    dotnet build src/YuzuhaToolkit.DevHost/YuzuhaToolkit.DevHost.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'DevHost build failed.' }
    node scripts/architecture.mjs check
    if ($LASTEXITCODE -ne 0) { throw 'Architecture projection is stale.' }
    npm run architecture:validate
    if ($LASTEXITCODE -ne 0) { throw 'LikeC4 validation failed.' }
    npm run architecture:build
    if ($LASTEXITCODE -ne 0) { throw 'LikeC4 build failed.' }

    $openApiDirectory = Join-Path $root 'artifacts/openapi'
    New-Item -ItemType Directory -Path $openApiDirectory -Force | Out-Null
    $hostDll = Join-Path $root 'src/YuzuhaToolkit.DevHost/bin/Release/net10.0/YuzuhaToolkit.DevHost.dll'
    $hostProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(
        ('"{0}"' -f $hostDll), '--urls', "http://127.0.0.1:$Port"
    ) -WorkingDirectory $root -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $openApiDirectory 'host.stdout.log') `
        -RedirectStandardError (Join-Path $openApiDirectory 'host.stderr.log')
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        $hostProcess.Refresh()
        if ($hostProcess.HasExited) { throw 'Temporary DevHost exited; inspect artifacts/openapi/host.stderr.log.' }
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2
            if ($health.service -eq 'YuzuhaToolkit.DevHost') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $ready) { throw 'Temporary DevHost did not become ready.' }
    Invoke-WebRequest -Uri "http://127.0.0.1:$Port/openapi/v1.json" -OutFile (Join-Path $openApiDirectory 'v1.json')
    $specification = Get-Content (Join-Path $openApiDirectory 'v1.json') -Raw | ConvertFrom-Json
    if (-not $specification.paths.'/api/pml/preview'.post.operationId) { throw 'OpenAPI is missing PML preview.' }
    Stop-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue
    $hostProcess.WaitForExit()
    $hostProcess = $null

    dotnet run --project tools/Documentation -c Release -- . --check-comments
    if ($LASTEXITCODE -ne 0) { throw 'Source declaration or comment coverage failed.' }
    dotnet run --project tools/Documentation -c Release -- --tests . artifacts/docfx/test-declarations.json
    if ($LASTEXITCODE -ne 0) { throw 'Test source inventory failed.' }
    & (Join-Path $root 'scripts/Trace-Desktop.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'UI semantic trace failed.' }
    dotnet docfx metadata docfx.json --warningsAsErrors
    if ($LASTEXITCODE -ne 0) { throw 'DocFX metadata failed.' }
    node scripts/documentation-index.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Documentation index coverage failed.' }
    dotnet docfx build docfx.json --warningsAsErrors
    if ($LASTEXITCODE -ne 0) { throw 'DocFX build failed.' }
    node scripts/check-documentation.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Generated documentation integrity failed.' }
    foreach ($page in @('index.html', 'api/devhost/YuzuhaToolkit.DevHost.DevelopmentEndpoints.html', 'architecture/index.html', 'openapi/v1.json')) {
        if (-not (Test-Path (Join-Path $root "artifacts/docfx/site/$page"))) { throw "Missing generated page: $page" }
    }
    Write-Host 'Documentation: artifacts/docfx/site/index.html'
}
finally {
    if ($null -ne $hostProcess) {
        $hostProcess.Refresh()
        if (-not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue }
        $hostProcess.Dispose()
    }
    Pop-Location
}
