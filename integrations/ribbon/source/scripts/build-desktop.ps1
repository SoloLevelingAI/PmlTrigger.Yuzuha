$ErrorActionPreference='Stop'
& node (Join-Path $PSScriptRoot 'build-desktop.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
