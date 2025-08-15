# ========================================================================
# publish-res-release.ps1
# ------------------------------------------------------------------------
# Purpose:
#   Builds the Servy.Service project in Release mode and copies the
#   resulting binaries into the Servy\Resources folder for packaging
#   or distribution.
#
# Requirements:
#   - msbuild must be available in the PATH.
#
# Steps:
#   1. Build Servy.Service in Release configuration.
#   2. Copy the required binaries and symbols to the Resources folder.
# ========================================================================

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------------
# Paths
# ------------------------------------------------------------------------
# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Absolute paths to relevant folders and project
$serviceProject     = Join-Path $ScriptDir "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder    = Join-Path $ScriptDir "..\Servy\Resources"
$buildConfiguration = "Release"
$buildOutput        = Join-Path $ScriptDir "..\Servy.Service\bin\$buildConfiguration"

# ------------------------------------------------------------------------
# 1. Build the project
# ------------------------------------------------------------------------
Write-Host "Building Servy.Service in $buildConfiguration mode..."
msbuild $serviceProject /t:Clean,Build /p:Configuration=$buildConfiguration

# ------------------------------------------------------------------------
# 2. Define files to copy
# ------------------------------------------------------------------------
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.pdb" }
    # @{ Source = "Servy.Core.pdb";    Destination = "Servy.Core.pdb" }
)

# ------------------------------------------------------------------------
# 3. Copy files to Resources folder
# ------------------------------------------------------------------------
foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $buildOutput $file.Source
    $destPath   = Join-Path $resourcesFolder $file.Destination
    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "Copied $($file.Source) -> $($file.Destination)"
}

# ----------------------------------------------------------------------
# Step 4 - CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$infraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$infraSourcePath = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\Servy.Infrastructure.pdb"
$infraDestPath   = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

msbuild $infraServiceProject /t:Clean,Build /p:Configuration=$buildConfiguration

Copy-Item -Path $infraSourcePath -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>
Write-Host "$buildConfiguration build published successfully to Resources."
