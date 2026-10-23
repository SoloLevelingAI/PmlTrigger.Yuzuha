[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$story=Get-Content (Join-Path $root 'docs/media/storyboard.json') -Raw -Encoding utf8 | ConvertFrom-Json
$output=[IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
# Use the locally installed Chinese Windows voice; do not modify voice registration.
$token=New-Object -ComObject SAPI.SpObjectToken
$token.SetId('HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens\MSTTS_V110_zhCN_HuihuiM')
$voice=New-Object -ComObject SAPI.SpVoice
$voice.Voice=$token; $voice.Rate=1
foreach($scene in $story.scenes) {
  $stream=New-Object -ComObject SAPI.SpFileStream
  try {
    $stream.Open((Join-Path $output ($scene.id+'.wav')),3,$false)
    $voice.AudioOutputStream=$stream
    $voice.Speak(($scene.lines -join ' '),0) | Out-Null
  } finally { $stream.Close() }
}
Write-Output "Local Chinese narration: $output"
