#requires -Version 5.0
<#
.SYNOPSIS
    Builds and publishes the Servy.CLI application (Release, self-contained) and 
    optionally signs the output executable using SignPath.

.DESCRIPTION
    This script performs the following steps:
      1. Runs publish-res-release.ps1 to ensure shared resources are generated.
      2. Builds and publishes Servy.CLI as a self-contained, single-file executable 
         for win-x64.
      3. Signs the generated executable via the SignPath signing pipeline.
      4. Emits standard build/progress messages used across Servy build tooling.

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
    Runs the script using the default TFM (net10.0-windows).

.EXAMPLE
    ./publish.ps1 -Tfm net10.0-windows
    Publishes the CLI for .NET target framework.

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