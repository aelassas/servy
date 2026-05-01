#requires -Version 5.0
<#
.SYNOPSIS
    Builds Servy for release as a framework-dependent Windows x64 executable.

.DESCRIPTION
    Standardized Pattern A script. This script:
    1. Runs the resource publishing step.
    2. Cleans and publishes Servy.csproj without debug symbols.
    3. Produces a framework-dependent build suitable for distribution.
#>
[CmdletBinding()]
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release"
)

$scriptDir = $PSScriptRoot
. (Join-Path $scriptDir "..\..\setup\build-common.ps1")

Invoke-StandardPublish `
    -ProjectDir $scriptDir `
    -ProjectName "Servy" `
    -Tfm $Tfm `
    -Runtime "win-x64" `
    -BuildConfiguration $BuildConfiguration `
    -FrameworkDependent