#requires -Version 5.0
<#
.SYNOPSIS
    Self-contained build and publish script for Servy.Service.

.DESCRIPTION
    This script builds the Servy.Service project following the standard repository 
    build pattern. It publishes to the default bin directory and optionally 
    signs the resulting executable using SignPath.

.PARAMETER Tfm
    Target framework for the build (default: net10.0-windows).

.PARAMETER Runtime
    Runtime identifier for the build (default: win-x64).

.PARAMETER BuildConfiguration
    Build configuration: Debug or Release (default: Release).

.PARAMETER Pause
    Switch to pause execution at the end of the script.
#>
[CmdletBinding()]
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$Runtime            = "win-x64",
    [string]$BuildConfiguration = "Release",
    [switch]$Pause
)

$P_PublishDir = $PSScriptRoot
. (Join-Path $P_PublishDir "..\..\setup\build-common.ps1")

Invoke-StandardPublish `
    -ProjectDir $P_PublishDir `
    -ProjectName "Servy.Service" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}