[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $InstallRoot,
    [string] $CodexRoot,
    [string] $McpName = 'YuzuhaToolkit',
    [ValidateSet('AM', 'PDMS')][string] $AvevaProfile,
    [string] $EvarBat,
    [switch] $SkipMcpRegistration
)

& (Join-Path $PSScriptRoot 'YuzuhaAgentLifecycle.ps1') `
    -Action Install @PSBoundParameters
