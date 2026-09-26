#Requires -Version 5.0

<#
.SYNOPSIS
    Publishes the Servy.Restarter project as a self-contained, single-file executable.

.DESCRIPTION
    This script builds the Servy.Restarter project using the interactive publish pattern
    shared with Servy.Service: [CmdletBinding()] plus a -Pause switch for manual runs.
    It publishes to the default bin directory and optionally signs the published executable
    with SignPath when -BuildConfiguration is Release and setup/signpath.ps1 is present;
    otherwise signing is skipped with a warning.

.PARAMETER Tfm
    Target framework to build against. Defaults to the value in build-config.ps1.

.PARAMETER Runtime
    Runtime identifier (RID) for the published executable. Default is "win-x64".

.PARAMETER BuildConfiguration
    Build configuration (e.g., Release or Debug). Default is "Release".

.PARAMETER Pause
    If specified, pauses the script at the end for review.
#>
[CmdletBinding()]
param(
    [string]$Tfm                = "",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64",
    [switch]$Pause
)

$P_PublishDir = $PSScriptRoot

# Load central defaults
$configPath = Join-Path $P_PublishDir "..\..\setup\build-config.ps1"
if (Test-Path $configPath) {
    $buildConfig = & $configPath
    if (-not $Tfm) { $Tfm = $buildConfig.Tfm }
} else {
    throw "Central build configuration not found at $configPath"
}

. (Join-Path $P_PublishDir "..\..\setup\build-common.ps1")

Invoke-StandardPublish `
    -ProjectDir $P_PublishDir `
    -ProjectName "Servy.Restarter" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration

if ($Pause) {
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
