$ErrorActionPreference = 'Stop'
$programs32 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
$locator = Join-Path $programs32 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $locator -latest -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installation) { throw 'Visual Studio Build Tools with the x86 C compiler are required.' }
$vcvars = Join-Path $installation 'VC\Auxiliary\Build\vcvarsall.bat'
$intermediate = (New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) ('core30-frame-' + [Guid]::NewGuid().ToString('N')))).FullName
$commandFile = Join-Path $intermediate 'build.cmd'
$source = Join-Path $PSScriptRoot 'Core30Frame.c'
$exports = Join-Path $PSScriptRoot 'Core30Frame.def'
$dll = Join-Path $PSScriptRoot 'Core30Frame.dll'
@"
@echo off
call "$vcvars" x86
if errorlevel 1 exit /b %errorlevel%
cl /nologo /utf-8 /std:c11 /O2 /W4 /MT /LD /DUNICODE /D_UNICODE /Fo"$intermediate\Core30Frame.obj" "$source" /link /DEF:"$exports" /OUT:"$dll" /IMPLIB:"$intermediate\Core30Frame.lib" user32.lib gdi32.lib comctl32.lib
exit /b %errorlevel%
"@ | Set-Content -LiteralPath $commandFile -Encoding ascii
& $env:ComSpec /d /c ('"{0}"' -f $commandFile)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
