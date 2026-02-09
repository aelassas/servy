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

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$signPath = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path

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
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.Manager.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$projectPath = Join-Path $scriptDir "Servy.Manager.csproj" | Resolve-Path

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "=== Publishing Servy.Manager.csproj ==="
Write-Host "Target Framework: $Tfm"
Write-Host "Configuration: $BuildConfiguration"
Write-Host "Runtime: $Runtime"

& dotnet restore $projectPath -r $Runtime

& dotnet clean $projectPath -c $BuildConfiguration

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --force `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 2: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $basePath      = Join-Path $scriptDir "..\Servy.Manager\bin\$BuildConfiguration\$Tfm\$Runtime"
    $publishFolder = Join-Path $basePath "publish"
    $exePath       = Join-Path $publishFolder "Servy.Manager.exe" | Resolve-Path
    & $signPath $exePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.Manager.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "=== Servy.Manager.csproj published successfully ==="
