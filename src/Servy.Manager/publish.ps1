#requires -Version 5.0
<#
.SYNOPSIS
    Self-contained build and publish script for Servy.Manager.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing step via publish-res-release.ps1.
      2. Builds and publishes the Servy.Manager project as a
         self-contained, single-file executable for the specified
         target framework and runtime.
      3. Optionally signs the resulting executable using SignPath.

.PARAMETER Tfm
    Target framework for the build.
    Default: net10.0-windows.

.PARAMETER BuildConfiguration
    Build configuration to use.
    Default: Release.

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing.
    Default: win-x64.

.EXAMPLE
    .\publish.ps1
    Builds Servy.Manager in Release mode with default settings.

.NOTES
    Author: Akram El Assas

    Requirements:
      - .NET SDK installed and accessible in PATH.
      - SignPath.ps1 script available in ..\..\setup\ for signing.
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
    -ProjectName "Servy.Manager" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration