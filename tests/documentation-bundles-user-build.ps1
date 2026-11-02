[CmdletBinding()]
param([string]$OutputRoot = ('artifacts/docfx-user/build-regression-' + [Guid]::NewGuid().ToString('N')))
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
$taskExpected = [IO.Path]::GetFullPath($OutputRoot, $taskRepository)
& (Join-Path $taskRepository 'scripts/Build-UserDocumentation.ps1') -OutputRoot $taskExpected
if (-not (Test-Path -LiteralPath (Join-Path $taskExpected 'index.html') -PathType Leaf)) { throw 'DocFX did not write directly to the requested output root.' }
if (Test-Path -LiteralPath (Join-Path $taskExpected 'artifacts/docfx-user/site/index.html')) { throw 'DocFX output was incorrectly nested under its configured destination.' }
$taskPages = @(Get-ChildItem -LiteralPath $taskExpected -Recurse -File -Filter '*.html')
$taskBytes = (Get-ChildItem -LiteralPath $taskExpected -Recurse -File | Measure-Object Length -Sum).Sum
if ($taskPages.Count -gt 40 -or $taskBytes -gt 15 * 1024 * 1024 -or @(Get-ChildItem -LiteralPath $taskExpected -Recurse -File -Filter '*.map').Count) { throw 'Real user DocFX output exceeds its declared budget.' }
@{passed=$true;scope='Real independent DocFX build; no full-site deletion, installation or AVEVA';output=$taskExpected;htmlPages=$taskPages.Count;bytes=$taskBytes} | ConvertTo-Json
