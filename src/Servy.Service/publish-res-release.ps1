# ======================================================================
# publish-res-release.ps1
# ----------------------------------------------------------------------
# Purpose:
#   Builds Servy.Restarter in Release mode (.NET Framework 4.8) and copies
#   the required binaries into the Servy.Service\Resources folder with the
#   appropriate naming conventions.
#
# Requirements:
#   - Ensure msbuild is available in PATH.
#   - Script should be run from PowerShell (x64).
#
# Notes:
#   - This script is intended for preparing the CLI resources for release.
#   - Adjust file paths if the project structure changes.
# ======================================================================

$ErrorActionPreference = "Stop"

# ----------------------------------------------------------------------
# Resolve script directory (absolute path to this script's location)
# ----------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$serviceProject      = Join-Path $ScriptDir "..\Servy.Restarter\Servy.Restarter.csproj"
$resourcesFolder     = Join-Path $ScriptDir "..\Servy.Service\Resources"
$buildConfiguration  = "Release"
$buildOutput         = Join-Path $ScriptDir "..\Servy.Restarter\bin\$buildConfiguration"
$signPath            = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path


# ----------------------------------------------------------------------
# Step 1 - Build Servy.Restarter in Release mode
# ----------------------------------------------------------------------
Write-Host "Building Servy.Restarter in $buildConfiguration mode..."
msbuild $serviceProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration

# ----------------------------------------------------------------------
# Step 2 - Sign the published executable if signing is enabled
# ----------------------------------------------------------------------
$exePath = Join-Path $buildOutput "Servy.Restarter.exe"
& $signPath $exePath

# ----------------------------------------------------------------------
# Step 3 - Files to copy (with renamed outputs where applicable)
# ----------------------------------------------------------------------
$filesToCopy = @(
    @{ Source = "Servy.Restarter.exe"; Destination = "Servy.Restarter.exe" },
    @{ Source = "Servy.Restarter.pdb"; Destination = "Servy.Restarter.pdb" }
    #@{ Source = "Dapper.dll";          Destination = "Dapper.dll" }
    #@{ Source = "Newtonsoft.Json.dll"; Destination = "Newtonsoft.Json.dll" }
    #@{ Source = "Servy.Core.dll";      Destination = "Servy.Core.dll" }
)

# ----------------------------------------------------------------------
# Step 4 - Copy the files into the Resources folder
# ----------------------------------------------------------------------
foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $buildOutput $file.Source
    $destPath   = Join-Path $resourcesFolder $file.Destination

    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "Copied $($file.Source) -> $($file.Destination)"
}

# ----------------------------------------------------------------------
# Step 5 - Copy Servy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$infraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$infraSourcePath = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\Servy.Infrastructure.pdb"
$infraDestPath   = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

msbuild $infraServiceProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration

Copy-Item -Path $infraSourcePath -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>


Write-Host "$buildConfiguration build published successfully to Resources."
