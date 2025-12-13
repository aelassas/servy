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
$ProjectName = "Servy.Restarter"
$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$SignPath    = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$ProjectPath = Join-Path $ScriptDir "$ProjectName.csproj" | Resolve-Path

$BasePath      = Join-Path $ScriptDir "..\Servy.Restarter\bin\$Configuration\$Tfm\$Runtime"
$PublishFolder = Join-Path $BasePath "publish"

# Publish output folder
$PublishDir = Join-Path $ScriptDir "bin\$Configuration\$Tfm\$Runtime\publish"

# Step 0: Clean previous publish folder
if (Test-Path $PublishDir) {
    Write-Host "Cleaning previous publish folder: $PublishDir"
    Remove-Item $PublishDir -Recurse -Force
}

# Step 1: Publish project
Write-Host "Publishing $ProjectName..."
dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    -f $Tfm `
    -o $PublishDir `
    --force `
    /p:DeleteExistingFiles=true

# Step 2: Sign the published executable if signing is enabled
if ($Configuration -eq "Release") {
    $ExePath = Join-Path $PublishFolder "Servy.Restarter.exe" | Resolve-Path
    & $SignPath $ExePath
}

Write-Host "Publish completed for $ProjectName."

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
