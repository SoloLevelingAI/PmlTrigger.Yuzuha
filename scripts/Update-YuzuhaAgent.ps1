[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $InstallRoot,
    [string] $CodexRoot,
    [string] $McpName = 'YuzuhaToolkit',
    [string] $KnowledgeMcpName = 'YuzuhaToolkitKnowledge',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.]{0,31}$')][string] $AvevaProfile,
    [string] $EvarBat,
    [string] $BootstrapFolderToken,
    [switch] $SkipMcpRegistration,
    [switch] $AdoptLegacyInstallation
)

& (Join-Path $PSScriptRoot 'YuzuhaAgentLifecycle.ps1') `
    -Action Update @PSBoundParameters
