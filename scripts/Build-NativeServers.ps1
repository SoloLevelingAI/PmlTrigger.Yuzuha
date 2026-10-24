[CmdletBinding()]
param([string] $OutputRoot, [string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
# Compatibility script name. The default build no longer produces MCP executables.
& (Join-Path $PSScriptRoot 'Build-NativeCli.ps1') -OutputRoot $OutputRoot -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Native CLI build failed.' }
