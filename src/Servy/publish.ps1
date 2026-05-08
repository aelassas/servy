#requires -Version 5.0
<#
.SYNOPSIS
    Publishes the Servy WPF application as a self-contained executable and signs it.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing script (`publish-res-release.ps1`)
      2. Builds and publishes `Servy.csproj` as a self-contained win-x64 executable
      3. Signs the published executable using SignPath (if enabled)

.PARAMETER Tfm
    Target Framework Moniker (default: "net10.0-windows").

.PARAMETER BuildConfiguration
    Build configuration to use (default: "Release").

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing (default: "win-x64").

.NOTES
    Requirements:
      - .NET SDK must be installed
      - The SignPath script (signpath.ps1) must exist in ../../setup/

.EXAMPLE
    ./publish.ps1
    Publishes using default parameters.

.EXAMPLE
    ./publish.ps1 -Tfm "net10.0-windows" -BuildConfiguration "Debug" -Runtime "win-x64"
#>
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
)

# Instead of a generic $scriptDir, use a scoped variable
$P_PublishDir = $PSScriptRoot 

# Dot-sourcing will no longer overwrite $P_PublishDir even if it sets $scriptDir
. (Join-Path $P_PublishDir "..\..\setup\build-common.ps1")

Write-Host "Publish Directory: $P_PublishDir"

Invoke-StandardPublish `
    -ProjectDir $P_PublishDir `
    -ProjectName "Servy" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration