<#
.SYNOPSIS
    Self-contained build and publish script for Servy.Service.

.DESCRIPTION
    This script builds the Servy.Service project as a self-contained single-file executable
    for the specified target framework and runtime. It also runs the appropriate
    publish-res script to include necessary resources and optionally signs the
    resulting executable using SignPath.

.PARAMETER tfm
    Target framework for the build (default: net10.0-windows).

.PARAMETER runtime
    Runtime identifier for the build (default: win-x64).

.PARAMETER configuration
    Build configuration: Debug or Release (default: Release).

.PARAMETER pause
    Switch to pause execution at the end of the script.

.REQUIREMENTS
    - .NET SDK installed and accessible in PATH.
    - msbuild in PATH (if required for other steps).
    - SignPath.ps1 script available in ..\..\setup\ for signing.

.EXAMPLE
    # Build Servy.Service in Release mode for net10.0-windows
    .\publish.ps1

.EXAMPLE
    # Build Servy.Service in Debug mode and pause after completion
    .\publish.ps1 -configuration Debug -pause

.NOTES
    Author: Akram El Assas
#>

param(
    [string]$tfm           = "net10.0-windows",
    [string]$runtime       = "win-x64",
    [string]$configuration = "Release",
    [switch]$pause
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Step 0: Setup variables
# ---------------------------------------------------------------------------------
# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$signPath      = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$AppName       = "Servy.Service"
$SelfContained = $true
$SingleFile    = $true

# Project path (relative to script location)
$ProjectPath = Join-Path $ScriptDir "$AppName.csproj"

# Output folder
$PublishDir = Join-Path $ScriptDir "bin\$configuration\$tfm\$runtime\publish"

# ---------------------------------------------------------------------------------
# Step 1: Run publish-res-release.ps1 (publish resources first)
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($configuration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$PublishResScript = Join-Path $ScriptDir $publishResScriptName

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $PublishResScript -tfm $tfm -runtime $runtime -configuration $configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Clean previous build artifacts
# ---------------------------------------------------------------------------------
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# ---------------------------------------------------------------------------------
# Step 3: Build and publish
# ---------------------------------------------------------------------------------
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing $AppName ==="
Write-Host "Target Framework : $tfm"
Write-Host "Configuration    : $configuration"
Write-Host "Runtime          : $runtime"
Write-Host "Self-contained   : $SelfContained"
Write-Host "Single File      : $SingleFile"

& dotnet publish $ProjectPath `
    -c $configuration `
    -r $runtime `
    --self-contained:$SelfContained `
    /p:PublishSingleFile=$SingleFile `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    -o $PublishDir `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 4: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
$exePath = Join-Path $PublishDir "Servy.Service.exe"
& $signPath $exePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.Service.exe failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 5: Pause (optional)
# ---------------------------------------------------------------------------------
if ($pause) {
    Pause
}

Write-Host "=== $AppName published successfully to $PublishDir ==="