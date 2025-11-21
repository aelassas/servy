<#
.SYNOPSIS
    Publishes the Servy.Restarter project as a self-contained, single-file executable.

.DESCRIPTION
    This script builds the Servy.Restarter project with the specified target framework, runtime,
    configuration, and version. It publishes a self-contained single-file executable and optionally
    signs it using SignPath. Previous publish folders are cleaned automatically.

.PARAMETER version
    Version to assign to the published assembly. Default is "1.0.0".

.PARAMETER tfm
    Target framework to build against. Default is "net10.0-windows".

.PARAMETER runtime
    Runtime identifier (RID) for the published executable. Default is "win-x64".

.PARAMETER configuration
    Build configuration, e.g., Release or Debug. Default is "Release".

.PARAMETER pause
    If specified, pauses the script at the end for review.

.EXAMPLE
    .\publish.ps1
    Publishes Servy.Restarter with default parameters.

.EXAMPLE
    .\publish.ps1 -tfm net10.0-windows -version 2.1.0 -pause
    Publishes Servy.Restarter targeting .NET 10, version 2.1.0, and pauses at the end.
#>

param(
    [string]$version = "1.0.0",
    [string]$tfm     = "net10.0-windows",
    [string]$runtime = "win-x64",
    [string]$configuration = "Release",
    [switch]$pause
)

$ErrorActionPreference = "Stop"

# Project and directories
$projectName = "Servy.Restarter"
$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$signPath    = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$projectPath = Join-Path $scriptDir "$projectName.csproj" | Resolve-Path

$basePath      = Join-Path $scriptDir "..\Servy.Restarter\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"

# Publish output folder
$publishDir = Join-Path $scriptDir "bin\$configuration\$tfm\$runtime\publish"

# Step 0: Clean previous publish folder
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous publish folder: $publishDir"
    Remove-Item $publishDir -Recurse -Force
}

# Step 1: Publish project
Write-Host "Publishing $projectName..."
dotnet publish $projectPath `
    -c $configuration `
    -r $runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    /p:Version=$version `
    -f $tfm `
    -o $publishDir `
    /p:DeleteExistingFiles=true

# Step 2: Sign the published executable if signing is enabled
$exePath = Join-Path $publishFolder "Servy.Restarter.exe" | Resolve-Path
& $signPath $exePath

Write-Host "Publish completed for $projectName."

if ($pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
