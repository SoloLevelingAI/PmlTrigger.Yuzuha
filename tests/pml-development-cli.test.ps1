[CmdletBinding()]
param([string]$ReportDirectory = (Join-Path $PSScriptRoot '../artifacts/tests/pml-development-cli'))
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('yuzuha-development-cli-' + [Guid]::NewGuid().ToString('N'))
$report = [IO.Path]::GetFullPath($ReportDirectory)
$previousFixture = $env:YUZUHA_DEVELOPMENT_CLI_FIXTURE
function Check([bool]$Condition,[string]$Message) { if (!$Condition) { throw $Message } }
try {
    $null = New-Item -ItemType Directory -Path $root,$report -Force
    [IO.File]::WriteAllText((Join-Path $root 'Fixture.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $root 'NuGet.Config'), '<configuration><packageSources><clear /></packageSources></configuration>')
    [IO.File]::WriteAllText((Join-Path $root 'Program.cs'), @'
using System.Text.Json;

// A fresh synthetic CLI process per call. It deliberately preserves legacy unpinned
// calls so the regression proves that the workflow itself supplies the frozen identity.
var root = Environment.GetEnvironmentVariable("YUZUHA_DEVELOPMENT_CLI_FIXTURE")!;
var statePath = Path.Combine(root, "state.json");
var state = JsonSerializer.Deserialize<FixtureState>(File.ReadAllText(statePath))!;
var operation = args[2];
string? Option(string name) { var at = Array.IndexOf(args, name); return at < 0 ? null : args[at + 1]; }
var expectedTicks = Option("--expected-start-time-utc-ticks");
var expectedModel = Option("--expected-model");
var input = JsonDocument.Parse(Option("--input") ?? "{}").RootElement;
var rejected = operation != "aveva.sessions.list" && expectedTicks is not null && expectedTicks != state.CurrentTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
var dispatched = !rejected && operation is "pml.command.execute" or "pml.array.read";
File.AppendAllText(Path.Combine(root, "calls.jsonl"), JsonSerializer.Serialize(new {
    operation, expectedTicks, expectedModel, actualTicks = state.CurrentTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
    fixtureProcessId = Environment.ProcessId, rejected, dispatched, arguments = args
}) + "\n");
if (rejected) {
    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = new { code = "TARGET_FAILED", message = "Synthetic PID was reused; dispatch rejected.", mayHaveExecuted = false } }));
    return 5;
}
object data;
switch (operation) {
    case "aveva.sessions.list":
        data = new { Sessions = new[] { new { ProcessId = 701, ProcessStartTimeUtcTicks = state.CurrentTicks } } };
        break;
    case "aveva.session.status":
        data = new { Connected = true, TargetVerified = true, Host = new { ProcessId = 701, ProcessStartTimeUtcTicks = state.CurrentTicks, Model = "Design" } };
        state.StatusCount++;
        if (state.Mode == "reuse-before-command" && state.StatusCount == 2 || state.Mode == "reuse-before-array" && state.StatusCount == 3)
            state.CurrentTicks++;
        break;
    case "pml.command.execute":
        data = new { Success = true, PmlCommand = input.GetProperty("pmlCommand").GetString() };
        if (state.Mode == "reuse-before-status") state.CurrentTicks++;
        break;
    case "pml.array.read":
        data = input.GetProperty("globalVar").GetString()!.StartsWith("YuzuhaDevPath")
            ? new { UnparsedCount = 0, Items = new[] { new { Path = "PmlLib", Value = state.Registered } } }
            : new { UnparsedCount = 0, Items = new[] { new { Path = "Version", Value = "v2" }, new { Path = "Sum", Value = "5" } } };
        break;
    default: throw new Exception("Unexpected synthetic operation: " + operation);
}
File.WriteAllText(statePath, JsonSerializer.Serialize(state));
Console.WriteLine(JsonSerializer.Serialize(new { success = true, data }));
return 0;
sealed class FixtureState {
    public string Mode { get; set; } = "";
    public string Registered { get; set; } = "";
    public long CurrentTicks { get; set; }
    public int StatusCount { get; set; }
}
'@)
    Push-Location $root
    try {
        $packagePath = Join-Path $root 'packages'
        $build = & dotnet build (Join-Path $root 'Fixture.csproj') -c Release --nologo --configfile (Join-Path $root 'NuGet.Config') -p:NuGetAudit=false "-p:RestorePackagesPath=$packagePath" 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Synthetic CLI fixture build failed: $build" }
    } finally { Pop-Location }
    $fixtureName = if ($IsWindows) { 'Fixture.exe' } else { 'Fixture' }
    $cli = Join-Path $root "bin/Release/net10.0/$fixtureName"
    $ticks = '638968451234567891' # Deliberately beyond JavaScript's exact integer range.
    foreach ($mode in @('reuse-before-command','reuse-before-array','reuse-before-status','pass')) {
        $case = Join-Path $root $mode
        $source = Join-Path $case 'source'; $registered = Join-Path $case 'registered'; $evidence = Join-Path $case 'evidence'
        $null = New-Item -ItemType Directory -Path $source,$registered -Force
        foreach ($file in @('Probe.pmlobj','ProbeForm.pmlfrm','validate.pmlmac')) { [IO.File]::WriteAllText((Join-Path $source $file), 'synthetic owned fixture') }
        $manifest = Join-Path $case 'manifest.json'
        @{schemaVersion=1;pluginName='Probe';objects=@('Probe');forms=@('ProbeForm');validationMacro='validate.pmlmac';resultGlobal='OwnedReport';expected=@{Version='v2';Sum='5'}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
        @{Mode=$mode;Registered=$registered;CurrentTicks=[long]$ticks;StatusCount=0} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $case 'state.json') -Encoding utf8
        $env:YUZUHA_DEVELOPMENT_CLI_FIXTURE = $case
        $output = & pwsh -NoProfile -File (Join-Path $PSScriptRoot '../scripts/Invoke-PmlDevelopment.ps1') -CliExecutable $cli -SourceDirectory $source -RegisteredPmlLib $registered -Manifest $manifest -ProcessId 701 -ExpectedModel Design -EvidenceDirectory $evidence -AllowExecution 2>&1
        $exitCode = $LASTEXITCODE
        $calls = @(Get-Content -LiteralPath (Join-Path $case 'calls.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
        $record = Get-Content -LiteralPath (Join-Path $evidence 'run.json') -Raw | ConvertFrom-Json
        $caseReport = Join-Path $report $mode
        $null = New-Item -ItemType Directory -Path $caseReport -Force
        Copy-Item -LiteralPath (Join-Path $case 'calls.jsonl') -Destination $caseReport
        Copy-Item -LiteralPath (Join-Path $evidence 'run.json'),(Join-Path $evidence 'rpc.json') -Destination $caseReport
        if ($mode -eq 'reuse-before-command') {
            Check (@($calls | Where-Object dispatched).Count -eq 0) 'PID reuse after status dispatched PML to the replacement process.'
            Check (!(Test-Path (Join-Path $registered 'Probe'))) 'PID reuse before the first command copied files.'
        }
        if ($mode -eq 'reuse-before-array') { Check (@($calls | Where-Object { $_.operation -eq 'pml.array.read' -and $_.dispatched }).Count -eq 0) 'PID reuse after status dispatched an array producer.' }
        if ($mode -eq 'reuse-before-status') { Check (@($calls | Where-Object { $_.operation -eq 'aveva.session.status' -and $_.rejected }).Count -eq 1) 'Later status did not retain the frozen startup identity.' }
        foreach ($call in @($calls | Where-Object operation -ne 'aveva.sessions.list')) {
            Check ($call.expectedTicks -ceq $ticks) "Frozen 64-bit ticks were missing or changed for $($call.operation)."
            Check ($call.expectedModel -ceq 'Design') 'The expected module was not carried into the CLI process.'
        }
        Check (@($calls.fixtureProcessId | Sort-Object -Unique).Count -gt 1) 'The fixture did not exercise separate CLI processes.'
        if ($mode -eq 'pass') {
            Check ($exitCode -eq 0 -and $record.status -eq 'Passed') "The unchanged target did not complete: $output"
            Check (@($calls | Where-Object { $_.operation -eq 'pml.array.read' -and $_.dispatched }).Count -eq 2) 'The complete loop did not read PMLLIB and validation results.'
        } else {
            Check ($exitCode -ne 0 -and $record.status -eq 'FailedOrUnknown') 'A rejected target was reported as successful.'
            Check ($record.lastFailure.error.mayHaveExecuted -eq $false) 'The pre-dispatch rejection receipt was lost.'
            Check (@($calls | Where-Object rejected).Count -eq 1) 'A rejected operation was retried.'
            Check (@($record.steps | Where-Object name -like 'reload-*').Count -eq 0) 'Reload continued after target rejection.'
        }
        Write-Output "PASS synthetic CLI $mode"
    }
    Write-Output "Synthetic CLI receipts: $report"
} finally {
    $env:YUZUHA_DEVELOPMENT_CLI_FIXTURE = $previousFixture
    $resolved = [IO.Path]::GetFullPath($root)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test cleanup escaped temporary root.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
