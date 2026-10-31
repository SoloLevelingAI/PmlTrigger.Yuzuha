<#
.SYNOPSIS
从单个克隆还原、构建和验证三个集成来源与当前管理器。
.DESCRIPTION
不依赖相邻仓库，不登录 AVEVA，不改安装目录。需要 Windows、仓库固定 .NET SDK、Node.js/npm。
报告记录每一步命令标签、工作目录、退出码与日志；失败立即停止。
#>
[CmdletBinding()]
param([switch] $NoRestore, [string] $ReportDirectory = 'artifacts/portable-validation')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-YuzuhaDotnet.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskReport = [IO.Path]::GetFullPath($ReportDirectory, $taskRepo)
[IO.Directory]::CreateDirectory($taskReport) | Out-Null
$taskSteps = [Collections.Generic.List[object]]::new()
$taskBuildOptions = @('-c','Debug','-m:1','-nr:false','-p:UseSharedCompilation=false','-p:WarningsNotAsErrors=NU1900')
if ($NoRestore) { $taskBuildOptions += '--no-restore' }
function Invoke-PortableStep([string]$Name, [string]$Directory, [scriptblock]$Action) {
    $taskStarted = [DateTimeOffset]::UtcNow
    $taskLog = Join-Path $taskReport ($Name + '.log')
    Push-Location -LiteralPath $Directory
    try {
        Write-Host "[$Name] $Directory"
        $global:LASTEXITCODE = 0
        & $Action 2>&1 | Tee-Object -FilePath $taskLog | Out-Host
        $taskCode = $LASTEXITCODE
        $taskSteps.Add([ordered]@{name=$Name;directory=[IO.Path]::GetRelativePath($taskRepo,$Directory);startedUtc=$taskStarted.ToString('o');finishedUtc=[DateTimeOffset]::UtcNow.ToString('o');exitCode=$taskCode;log=[IO.Path]::GetRelativePath($taskReport,$taskLog)})
        if ($taskCode -ne 0) { throw "$Name failed ($taskCode); inspect $taskLog" }
    }
    catch {
        if (!$taskSteps.Count -or $taskSteps[$taskSteps.Count-1].name -ne $Name) {
            $taskSteps.Add([ordered]@{name=$Name;directory=[IO.Path]::GetRelativePath($taskRepo,$Directory);startedUtc=$taskStarted.ToString('o');finishedUtc=[DateTimeOffset]::UtcNow.ToString('o');exitCode=-1;log=[IO.Path]::GetRelativePath($taskReport,$taskLog);error=$_.Exception.Message})
        }
        throw
    }
    finally {
        Pop-Location
        [ordered]@{schemaVersion=1;completed=$false;commit=(& git -C $taskRepo rev-parse HEAD);dirty=[bool](& git -C $taskRepo status --porcelain);scope='single-checkout offline validation; no AVEVA login/SDK, credentials, VM or production deployment';steps=$taskSteps} |
            ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskReport 'result.json') -Encoding utf8
    }
}
Invoke-PortableStep 'integration-manifests' $taskRepo { node scripts/check-integrations.mjs --tracked }
Invoke-PortableStep 'next-assets' (Join-Path $taskRepo 'integrations/next') { node verify.mjs }
Invoke-PortableStep 'portfolio-http-assets' $taskRepo { node scripts/Test-IntegratedPortfolio.mjs }
Invoke-PortableStep 'manager-tests' $taskRepo { & $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Manager.Tests @taskBuildOptions --logger 'trx;LogFileName=manager.trx' --results-directory $taskReport }
Invoke-PortableStep 'database-tests' $taskRepo { & $env:DOTNET_HOST_PATH test tests/PmlTwin.Database.Tests @taskBuildOptions --logger 'trx;LogFileName=database.trx' --results-directory $taskReport }
Invoke-PortableStep 'architecture-tests' $taskRepo { & $env:DOTNET_HOST_PATH test tests/YuzuhaToolkit.Architecture.Tests @taskBuildOptions --logger 'trx;LogFileName=architecture.trx' --results-directory $taskReport }
Invoke-PortableStep 'manager-cli-build' $taskRepo { & $env:DOTNET_HOST_PATH build src/YuzuhaToolkit.Manager.Cli @taskBuildOptions }
Invoke-PortableStep 'communication-cli-build' $taskRepo { & $env:DOTNET_HOST_PATH build src/PmlTwin.Phayton.Cli @taskBuildOptions }
$taskRewrite = Join-Path $taskRepo 'integrations/rewrite/source/rewrite'
Invoke-PortableStep 'rewrite-build' $taskRewrite { & $env:DOTNET_HOST_PATH build LaunchDesk.Rewrite.slnx @taskBuildOptions }
Invoke-PortableStep 'rewrite-tests' $taskRewrite { & $env:DOTNET_HOST_PATH test LaunchDesk.Architecture.Tests -c Debug --no-build --no-restore --filter 'Category!=Optional7Zip' --logger 'trx;LogFileName=rewrite.trx' --results-directory $taskReport }
Invoke-PortableStep 'rewrite-node-tests' $taskRewrite { node --test scripts/test-*.mjs }
$taskNext = Join-Path $taskRepo 'integrations/next/source'
Invoke-PortableStep 'next-build' $taskNext { & $env:DOTNET_HOST_PATH build LaunchDesk.Next.slnx @taskBuildOptions }
foreach ($taskSuite in @('UnitTests','ArchitectureTests','IntegrationTests')) {
    Invoke-PortableStep "next-$taskSuite" $taskNext { & $env:DOTNET_HOST_PATH test "tests/LaunchDesk.Next.$taskSuite" -c Debug --no-build --no-restore --filter 'TestCategory!=LocalInventory' --logger "trx;LogFileName=next-$taskSuite.trx" --results-directory $taskReport }
}
Invoke-PortableStep 'next-input-tests' $taskNext { & $env:DOTNET_HOST_PATH test tests/LaunchDesk.Next.UiTests -c Debug --no-build --no-restore --filter 'FullyQualifiedName~InputBehaviorTests' --logger 'trx;LogFileName=next-input.trx' --results-directory $taskReport }
$taskRibbon = Join-Path $taskRepo 'integrations/ribbon/source'
if (!$NoRestore) { Invoke-PortableStep 'ribbon-restore' $taskRibbon { npm ci --no-audit --no-fund } }
Invoke-PortableStep 'ribbon-build' $taskRibbon { npm run build }
Invoke-PortableStep 'ribbon-desktop-build' $taskRibbon { npm run desktop:build }
Invoke-PortableStep 'ribbon-architecture' $taskRibbon { npm run architecture:check }
Invoke-PortableStep 'ribbon-tests' $taskRibbon { npm test }
Invoke-PortableStep 'ribbon-manager-chain' $taskRepo { & (Join-Path $PSScriptRoot 'Test-IntegratedRibbon.ps1') -ReportDirectory (Join-Path $taskReport 'ribbon-chain') }
$taskResult=Get-Content (Join-Path $taskReport 'result.json') -Raw|ConvertFrom-Json
$taskResult.completed=$true
$taskResult|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $taskReport 'result.json') -Encoding utf8
Write-Host "Validation completed: $taskReport/result.json"
