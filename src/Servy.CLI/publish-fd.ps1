#requires -Version 5.0
<#
.SYNOPSIS
    Publishes the Servy.CLI project (framework-dependent) for Windows.

.DESCRIPTION
    Standardized Pattern A script. This script:
    1. Runs the resource publishing script.
    2. Cleans and publishes Servy.CLI.csproj without debug symbols.
    3. Produces a framework-dependent build targeting win-x64.
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
    -ProjectName "Servy.CLI" `
    -Tfm $Tfm `
    -Runtime "win-x64" `
    -BuildConfiguration $BuildConfiguration `
    -FrameworkDependent