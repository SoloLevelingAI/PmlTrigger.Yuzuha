# Reusable verification for the LaunchDesk-derived documented test runner.
function Assert-DocumentedTrx([string]$Path, [string]$Method) {
    [xml]$document = Get-Content -LiteralPath $Path -Raw
    $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $counters = $document.SelectSingleNode('//t:ResultSummary/t:Counters', $ns)
    $results = @($document.SelectNodes('//t:Results/t:UnitTestResult', $ns))
    if (-not $counters -or [int]$counters.executed -lt 1 -or $results.Count -lt 1) { throw 'Zero tests executed; this is not a passing result.' }
    if ([int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or
        [int]$counters.passed -ne [int]$counters.executed -or [int]$counters.total -ne [int]$counters.executed -or
        $results.Count -ne [int]$counters.executed) { throw 'Every selected data row must execute and pass; skipped rows are not a pass.' }
    foreach ($result in $results) {
        if ($result.outcome -cne 'Passed') { throw 'Selected test result did not pass.' }
        $definitions = @($document.SelectNodes('//t:TestDefinitions/t:UnitTest', $ns) | Where-Object { $_.id -ceq $result.testId })
        if ($definitions.Count -ne 1) { throw 'Result has no unique test definition.' }
        $testMethod = $definitions[0].SelectSingleNode('t:TestMethod', $ns)
        if (($testMethod.className + '.' + $testMethod.name) -cne $Method) { throw 'Executed test identity differs from the requested method.' }
    }
    return [int]$counters.executed
}
