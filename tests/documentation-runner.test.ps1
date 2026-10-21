$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../scripts/DocumentedTestResults.ps1"
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('yuzuha-trx-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $file = Join-Path $temporary 'result.trx'
    function Write-Trx([int]$Executed, [string]$Outcome, [string]$Method, [int]$Skipped = 0) {
        @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><TestDefinitions><UnitTest id="id"><TestMethod className="Example.Tests" name="$Method" /></UnitTest></TestDefinitions><Results><UnitTestResult testId="id" outcome="$Outcome" /></Results><ResultSummary outcome="Completed"><Counters total="1" executed="$Executed" passed="$Executed" failed="0" notExecuted="$Skipped" /></ResultSummary></TestRun>
"@ | Set-Content -LiteralPath $file
    }
    Write-Trx 1 Passed Check
    if ((Assert-DocumentedTrx $file 'Example.Tests.Check') -ne 1) { throw 'Valid exact method rejected' }
    foreach ($case in @(@(0,'Passed','Check',0),@(1,'Passed','Other',0),@(1,'NotExecuted','Check',0),@(1,'Passed','Check',1))) {
        Write-Trx @case
        $rejected = $false
        try { Assert-DocumentedTrx $file 'Example.Tests.Check' | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw "Invalid result accepted: $case" }
    }
    Write-Host 'PASS: exact TRX identity, positive execution, all passed, no skipped rows.'
} finally {
    # The unique directory was created by this test and contains only its fixture.
    Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $temporary -Force
}
