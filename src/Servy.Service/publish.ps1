<#
.SYNOPSIS
    Builds the Servy.Service project and optionally signs the output.

.DESCRIPTION
    This script:
      1. Locates and builds the Servy.Service csproj using MSBuild.
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
    Builds Servy.Service in Release mode and signs it.

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
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$serviceProject   = Join-Path $scriptDir "..\Servy.Service\Servy.Service.csproj" | Resolve-Path
$platform         = "x64"
$buildOutput      = Join-Path $scriptDir "..\Servy.Service\bin\$platform\$BuildConfiguration"
$signPath         = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ---------------------------------------------------------------------------------
# Step 1: Publish resources first
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $publishResScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ----------------------------------------------------------------------
# Step 2: Build Servy.Service
# ----------------------------------------------------------------------
Write-Host "Building Servy.Service in $BuildConfiguration mode..."
& msbuild $serviceProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:AllowUnsafeBlocks=true /p:Platform=$platform

# ----------------------------------------------------------------------
# Step 3: Sign the executable only in Release mode
# ----------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $exePath = Join-Path $buildOutput "Servy.Service.exe" | Resolve-Path
    & $signPath $exePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.CLI.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "Build completed for Servy.Service in $BuildConfiguration mode."

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
