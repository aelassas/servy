# publish-cli.ps1 - Build Servy CLI (Release)
# ------------------------------------------------------------
# This script builds the Servy CLI project in Release mode.
# It first runs publish-res-release.ps1 to publish resources,
# then cleans and builds the Servy.CLI.csproj using MSBuild.
#
# Requirements:
#   - MSBuild must be in the PATH (e.g., from Visual Studio 2022)
#   - publish-res-release.ps1 must exist in the same directory
# ------------------------------------------------------------

$ErrorActionPreference = "Stop"
$buildConfiguration    = "Release"
$platform              = "x64"

# Determine script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Project path
$ProjectPath = Join-Path $ScriptDir "Servy.CLI.csproj"

# Step 0: Run publish-res-release.ps1
$PublishResScript = Join-Path $ScriptDir "publish-res-release.ps1"
Write-Host "Running publish-res-release.ps1..."
& $PublishResScript
Write-Host "Finished publish-res-release.ps1. Continuing with main build..."

# Step 1: Clean and build the CLI project
Write-Host "Building Servy.CLI project in $buildConfiguration mode..."
msbuild $ProjectPath /t:Clean,Build /p:Configuration=$buildConfiguration /p:Platform=$platform
Write-Host "Build completed."
