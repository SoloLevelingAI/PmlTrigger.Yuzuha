[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $InstallRoot,
    [string] $CodexRoot,
    [string] $McpName = 'YuzuhaToolkit',
    [string] $EvarBat
)

& (Join-Path $PSScriptRoot 'YuzuhaAgentLifecycle.ps1') `
    -Action Uninstall @PSBoundParameters
