<#
.SYNOPSIS
    Builds the Servy.Restarter project and optionally signs the output.

.DESCRIPTION
    This script:
      1. Locates and builds the Servy.Restarter csproj using MSBuild.
      2. Signs the produced executable using SignPath, but ONLY when the
         build configuration is Release.
      3. Supports optional pause for manual inspection.

.PARAMETER BuildConfiguration
    The build configuration to use (Debug or Release).
    Default: Release.

.PARAMETER pause
    Pauses the script at the end. Useful when running from Explorer.

.REQUIREMENTS
    - MSBuild installed and available in PATH.
    - signpath.ps1 must exist in ../../setup/.
    - .NET SDK or corresponding build tools installed.

.EXAMPLE
    ./build.ps1
    Builds Servy.Restarter in Release mode and signs it.

.EXAMPLE
    ./build.ps1 -BuildConfiguration Debug
    Builds in Debug mode. Signing is skipped.

.NOTES
    Author: Akram El Assas
    Project: Servy
#>

param(
    [string]$BuildConfiguration = "Release",
    [switch]$Pause
)

# ----------------------------------------------------------------------
# Resolve script directory (absolute path to this script's location)
# ----------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$Platform         = "x64"
$RestarterProject = Join-Path $ScriptDir "..\Servy.Restarter\Servy.Restarter.csproj" | Resolve-Path
$BuildOutput      = Join-Path $ScriptDir "..\Servy.Restarter\bin\$Platform\$BuildConfiguration"
$SignPath         = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ----------------------------------------------------------------------
# Step 1: Build Servy.Restarter
# ----------------------------------------------------------------------
Write-Host "Building Servy.Restarter in $BuildConfiguration mode..."
& msbuild $RestarterProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:AllowUnsafeBlocks=true /p:Platform=$Platform

# ----------------------------------------------------------------------
# Step 2: Sign the executable only in Release mode
# ----------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $ExePath = Join-Path $BuildOutput "Servy.Restarter.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.Restarter.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "Build completed for Servy.Restarter in $BuildConfiguration mode."

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
