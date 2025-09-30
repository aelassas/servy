# publish-sc.ps1 - Self-contained build script for Servy.Service
# Requirements:
#   - .NET SDK installed
#   - msbuild in PATH (if needed for other steps)

param(
    [string]$tfm     = "net8.0-windows",
    [switch]$pause
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Step 0: Setup variables
# ---------------------------------------------------------------------------------
$AppName    = "Servy.Service"
$Runtime    = "win-x64"
$SelfContained = $true
$SingleFile    = $true
$Configuration = "Release"

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Project path (relative to script location)
$ProjectPath = Join-Path $ScriptDir "$AppName.csproj"

# Output folder
$PublishDir = Join-Path $ScriptDir "bin\$Configuration\$tfm\$Runtime\publish"

# ---------------------------------------------------------------------------------
# Step 1: Clean previous build artifacts
# ---------------------------------------------------------------------------------
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# ---------------------------------------------------------------------------------
# Step 2: Build and publish
# ---------------------------------------------------------------------------------
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing $AppName ==="
Write-Host "Target Framework : $tfm"
Write-Host "Configuration    : $Configuration"
Write-Host "Runtime          : $Runtime"
Write-Host "Self-contained   : $SelfContained"
Write-Host "Single File      : $SingleFile"

& dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
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

Write-Host "=== $AppName published successfully to $PublishDir ==="

# ---------------------------------------------------------------------------------
# Step 3: Pause (optional)
# ---------------------------------------------------------------------------------
if ($pause) {
    Pause
}
