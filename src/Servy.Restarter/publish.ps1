<#
.SYNOPSIS
    Publishes the Servy.Restarter project as a self-contained, single-file executable.

.DESCRIPTION
    This script builds the Servy.Restarter project with the specified target framework, runtime,
    configuration, and version. It publishes a self-contained single-file executable and optionally
    signs it using SignPath. Previous publish folders are cleaned automatically.

.PARAMETER Version
    Version to assign to the published assembly. Default is "1.0.0".

.PARAMETER Tfm
    Target framework to build against. Default is "net10.0-windows".

.PARAMETER Runtime
    Runtime identifier (RID) for the published executable. Default is "win-x64".

.PARAMETER Configuration
    Build configuration, e.g., Release or Debug. Default is "Release".

.PARAMETER Pause
    If specified, pauses the script at the end for review.

.EXAMPLE
    .\publish.ps1
    Publishes Servy.Restarter with default parameters.

.EXAMPLE
    .\publish.ps1 -Tfm net10.0-windows -version 2.1.0 -Pause
    Publishes Servy.Restarter targeting .NET 10, version 2.1.0, and pauses at the end.
#>

param(
    [string]$Version       = "1.0.0",
    [string]$Tfm           = "net10.0-windows",
    [string]$Runtime       = "win-x64",
    [string]$Configuration = "Release",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# Project and directories
$projectName = "Servy.Restarter"
$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$signPath    = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$projectPath = Join-Path $scriptDir "$projectName.csproj" | Resolve-Path

$basePath      = Join-Path $scriptDir "..\Servy.Restarter\bin\$Configuration\$Tfm\$Runtime"
$publishFolder = Join-Path $basePath "publish"

# Publish output folder
$publishDir = Join-Path $scriptDir "bin\$Configuration\$Tfm\$Runtime\publish"

# Step 0: Clean previous publish folder
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous publish folder: $publishDir"
    Remove-Item $publishDir -Recurse -Force
}

# Step 1: Publish project
Write-Host "Publishing $projectName..."

& dotnet restore $projectPath -r $Runtime

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -f $Tfm `
    -o $publishDir `
    --force `
    /p:DeleteExistingFiles=true

# Step 2: Sign the published executable if signing is enabled
if ($Configuration -eq "Release") {
    $exePath = Join-Path $publishFolder "Servy.Restarter.exe" | Resolve-Path
    & $signPath $exePath
}

Write-Host "Publish completed for $projectName."

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
