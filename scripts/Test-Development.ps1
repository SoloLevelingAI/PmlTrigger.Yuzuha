[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    node scripts/architecture.mjs check
    if ($LASTEXITCODE -ne 0) { throw 'Architecture projection is stale.' }
    dotnet test YuzuhaToolkit.Development.slnx -c Release --logger 'trx;LogFilePrefix=development' --results-directory artifacts/tests
    if ($LASTEXITCODE -ne 0) { throw 'Development tests failed.' }
}
finally { Pop-Location }
