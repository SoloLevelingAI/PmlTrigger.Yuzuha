$ErrorActionPreference = 'SilentlyContinue'
$roots = @('Registry::HKEY_LOCAL_MACHINE\SOFTWARE\AVEVA', 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\AVEVA', 'Registry::HKEY_CURRENT_USER\SOFTWARE\AVEVA')
$paths = foreach ($root in $roots) {
    if (Test-Path -LiteralPath $root) {
        Get-ChildItem -LiteralPath $root -Recurse | ForEach-Object {
            (Get-ItemProperty -LiteralPath $_.PSPath).PSObject.Properties |
                Where-Object { $_.Name -match 'Install|Root|Path|Location' -and $_.Value -is [string] } |
                ForEach-Object { $_.Value }
        }
    }
}
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
ConvertTo-Json -InputObject @($paths | Select-Object -Unique) -Compress
