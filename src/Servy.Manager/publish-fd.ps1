#requires -Version 5.0
<#
.SYNOPSIS
    Builds and publishes Servy.Manager as a framework-dependent application.

.DESCRIPTION
    Standardized Pattern A script. Cleans and publishes Servy.Manager.csproj 
    without debug symbols to ensure a clean production build.
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
    -ProjectName "Servy.Manager" `
    -Tfm $Tfm `
    -Runtime "win-x64" `
    -BuildConfiguration $BuildConfiguration `
    -FrameworkDependent