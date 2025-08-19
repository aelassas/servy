# publish.ps1
# build script for Servy WPF app
# ------------------------------------------------------------
# This script:
# 1. Runs the resource publishing script (publish-res-release.ps1)
# 2. Builds the Servy project in Release mode using MSBuild
# ------------------------------------------------------------

$ErrorActionPreference = "Stop"

# Configuration
$buildConfiguration = "Release"

# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Paths
$ProjectPath = Join-Path $ScriptDir "Servy.csproj"
$PublishResScript = Join-Path $ScriptDir "publish-res-release.ps1"

# Step 1: Run publish-res-release.ps1
Write-Host "Running publish-res-release.ps1..."
& $PublishResScript
Write-Host "Finished publish-res-release.ps1."

# Step 2: Build project with MSBuild
Write-Host "Building Servy project in $buildConfiguration mode..."
& msbuild $ProjectPath /t:Clean,Build /p:Configuration=$buildConfiguration
Write-Host "Build completed."
