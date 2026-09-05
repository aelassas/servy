#Requires -Version 5.0

<#
.SYNOPSIS
    Builds and publishes the Servy.CLI application (Release, self-contained) and
    optionally signs the output executable using SignPath.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing script (publish-res-debug.ps1 or publish-res-release.ps1, selected by -BuildConfiguration).
      2. Builds and publishes Servy.CLI as a self-contained, single-file executable
         for the specified target framework and runtime.
      3. Signs the published executable with SignPath when -BuildConfiguration is Release
         and setup/signpath.ps1 is present; otherwise signing is skipped with a warning.

    This is used as part of the Release build pipeline to produce final CLI artifacts.

.PARAMETER Tfm
    Target Framework Moniker to publish for.
    Default: net10.0-windows.

.PARAMETER BuildConfiguration
    Build configuration to use.
    Default: Release.

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing.
    Default: win-x64.

.EXAMPLE
    ./publish.ps1
    Publishes the CLI using the defaults (Release, net10.0-windows, win-x64).

.EXAMPLE
    ./publish.ps1 -Runtime win-arm64
    Publishes the CLI for the ARM64 runtime.

.NOTES
    Author : Akram El Assas
    Project: Servy
    Requirements:
        - .NET SDK installed
        - SignPath setup
        - Valid folder structure
#>
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
)

$P_PublishDir = $PSScriptRoot
. (Join-Path $P_PublishDir "..\..\setup\build-common.ps1")

Invoke-StandardPublish `
    -ProjectDir $P_PublishDir `
    -ProjectName "Servy.CLI" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration
