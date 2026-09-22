#Requires -Version 5.0

<#
.SYNOPSIS
    Publishes the Servy WPF application as a self-contained executable and optionally signs it.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing script (publish-res-debug.ps1 or publish-res-release.ps1, selected by -BuildConfiguration).
      2. Builds and publishes `Servy.csproj` as a self-contained executable for the specified runtime.
      3. Signs the published executable with SignPath when -BuildConfiguration is Release
         and setup/signpath.ps1 is present; otherwise signing is skipped with a warning.

.PARAMETER Tfm
    Target Framework Moniker. Defaults to the value in build-config.ps1.

.PARAMETER BuildConfiguration
    Build configuration to use (default: "Release").

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing (default: "win-x64").

.NOTES
    Requirements:
      - .NET SDK must be installed
    Optional:
      - setup/signpath.ps1: used to sign the output when -BuildConfiguration is Release;
        when absent, signing is skipped with a warning.

.EXAMPLE
    ./publish.ps1
    Publishes using default parameters.

.EXAMPLE
    ./publish.ps1 -Tfm "net10.0-windows" -BuildConfiguration "Debug" -Runtime "win-x64"
#>
param(
    [string]$Tfm                = "",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
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
    -ProjectName "Servy" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration
