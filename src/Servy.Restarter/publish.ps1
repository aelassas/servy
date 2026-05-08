#requires -Version 5.0
<#
.SYNOPSIS
    Publishes the Servy.Restarter project as a self-contained, single-file executable.

.DESCRIPTION
    This script builds the Servy.Restarter project following the standard repository 
    build pattern (Pattern A). It publishes to the default bin directory and 
    optionally signs the artifact using SignPath.

.PARAMETER Version
    Version to assign to the published assembly. Default is "1.0.0".

.PARAMETER Tfm
    Target framework to build against. Default is "net10.0-windows".

.PARAMETER Runtime
    Runtime identifier (RID) for the published executable. Default is "win-x64".

.PARAMETER BuildConfiguration
    Build configuration (e.g., Release or Debug). Default is "Release".

.PARAMETER Pause
    If specified, pauses the script at the end for review.
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
    -ProjectName "Servy.Restarter" `
    -Tfm $Tfm `
    -Runtime $Runtime `
    -BuildConfiguration $BuildConfiguration

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}