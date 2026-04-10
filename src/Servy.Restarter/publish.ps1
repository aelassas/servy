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
    [string]$Version            = "1.0.0",
    [string]$Tfm                = "net10.0-windows",
    [string]$Runtime            = "win-x64",
    [string]$BuildConfiguration = "Release",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# Project and directories
$projectName = "Servy.Restarter"
$scriptDir   = $PSScriptRoot
$signPath    = Join-Path $scriptDir "..\..\setup\signpath.ps1"
$projectPath = Join-Path $scriptDir "$projectName.csproj"

# Step 0: Clean and Restore
Write-Host "--- Preparing $projectName ---" -ForegroundColor Cyan

& dotnet restore $projectPath -r $Runtime

& dotnet clean $projectPath -c $BuildConfiguration

# Step 1: Publish project (Pattern A: Uses default output location)
Write-Host "--- Publishing $projectName ---" -ForegroundColor Cyan

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --force `
    /p:DeleteExistingFiles=true

# Step 2: Sign the published executable (Pattern A: Self-referencing bin path)
if ($BuildConfiguration -eq "Release" -and (Test-Path $signPath)) {
    Write-Host "--- Signing Artifacts ---" -ForegroundColor Cyan
    
    # Resolving path within the standard bin structure
    $exePath = Join-Path $scriptDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\$projectName.exe"
    
    if (Test-Path $exePath) {
        & $signPath -Path $exePath
    }
}

Write-Host "Publish completed for $projectName." -ForegroundColor Green

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}