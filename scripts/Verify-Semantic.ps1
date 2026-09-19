[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet('TimeThenHash','Hash')][string]$Mode = 'TimeThenHash'
)
$ErrorActionPreference = 'Stop'
$failed = $false
$resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
Get-ChildItem -LiteralPath (Join-Path $resolvedRoot 'docs\semantic') -Filter '*.Semantic.md' -Recurse | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    $fileMatch = [regex]::Match($text, '(?m)^File: (.+)\r?$')
    $timeMatch = [regex]::Match($text, '(?m)^LastWriteTimeUtc: (.+)\r?$')
    $hashMatch = [regex]::Match($text, '(?m)^SHA256: ([A-Fa-f0-9]{64})\r?$')
    if (!$fileMatch.Success -or !$timeMatch.Success -or !$hashMatch.Success) { throw "Invalid semantic file: $($_.FullName)" }
    $name = $fileMatch.Groups[1].Value.Trim()
    if ([IO.Path]::IsPathRooted($name)) { throw 'Semantic File must be project-relative.' }
    $target = [IO.Path]::GetFullPath((Join-Path $resolvedRoot $name))
    if (!$target.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Semantic path escapes project.' }
    $status = 'Missing'
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        $item = Get-Item -LiteralPath $target
        $expected = [DateTime]::Parse($timeMatch.Groups[1].Value.Trim(), [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        if ($Mode -eq 'TimeThenHash' -and $item.LastWriteTimeUtc.Ticks -eq $expected.ToUniversalTime().Ticks) {
            $status = 'TimestampMatch (content not hashed)'
        } elseif ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hashMatch.Groups[1].Value) {
            $status = 'HashMatch'
        } else { $status = 'Changed' }
    }
    if ($status -in @('Missing','Changed')) { $failed = $true }
    [pscustomobject]@{File=$target; Status=$status}
}
if ($failed) { throw 'Semantic verification found changed or missing files.' }
