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

.REQUIREMENTS
    - .NET SDK installed and accessible in PATH.
    - SignPath.ps1 script available in ..\..\setup\ for signing.

.EXAMPLE
    .\publish.ps1
    Builds Servy.Manager in Release mode with default settings.

.NOTES
    Author: Akram El Assas
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
)

$ErrorActionPreference = "Stop"

function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$scriptDir = $PSScriptRoot

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$signPath = Join-Path $scriptDir "..\..\setup\signpath.ps1"
if (-not (Test-Path $signPath)) {
    Write-Warning "SignPath script not found at: $signPath. Signing will be skipped."
}

# ---------------------------------------------------------------------------------
# Step 0: Publish resources
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $publishResScript -Tfm $Tfm
Check-LastExitCode "$publishResScriptName failed"
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.Manager.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$projectPath = Join-Path $scriptDir "Servy.Manager.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "=== Publishing Servy.Manager.csproj ==="
Write-Host "Target Framework: $Tfm"
Write-Host "Configuration: $BuildConfiguration"
Write-Host "Runtime: $Runtime"

& dotnet restore $projectPath -r $Runtime
Check-LastExitCode "dotnet restore failed"

& dotnet clean $projectPath -c $BuildConfiguration
Check-LastExitCode "Project clean failed"

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --force `
    /p:DeleteExistingFiles=true

Check-LastExitCode "dotnet publish failed"

# ---------------------------------------------------------------------------------
# Step 2: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $publishFolder = Join-Path $scriptDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
    $exePath       = Join-Path $publishFolder "Servy.Manager.exe"

    if (Test-Path $exePath) {
        Write-Host "=== Signing Servy.Manager.exe ===" -ForegroundColor Cyan
        & $signPath $exePath
        Check-LastExitCode "Code signing failed"
    }
    else {
        Write-Error "Published executable not found at: $exePath. Ensure TFM and Runtime variables match the project output."
        exit 1
    }
}

Write-Host "=== Servy.Manager.csproj published successfully ==="
