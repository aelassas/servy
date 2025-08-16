# publish-res-debug.ps1 - Build Servy.Service in Debug mode and copy resources
# ---------------------------------------------------------------------------
# This script builds the Servy.Service project in Debug mode (net48 target)
# and copies the resulting executable and PDB files to the Resources folder.
#
# Requirements:
# - msbuild must be available in PATH
#
# Steps:
# 1. Build Servy.Service in Debug configuration
# 2. Copy build artifacts to the Resources folder with proper naming
# ---------------------------------------------------------------------------

$ErrorActionPreference = "Stop"

# --- Paths ---
$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$serviceProject     = Join-Path $ScriptDir "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder    = Join-Path $ScriptDir "..\Servy\Resources"
$buildConfiguration = "Debug"
$buildOutput        = Join-Path $ScriptDir "..\Servy.Service\bin\$buildConfiguration"

# --- Step 1: Build the project ---
Write-Host "Building Servy.Service in $buildConfiguration mode..."
msbuild $serviceProject /t:Clean,Build /p:Configuration=$buildConfiguration

# --- Step 2: Define files to copy ---
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.pdb" }
    @{ Source = "Servy.Core.dll";    Destination = "Servy.Core.dll" }
)

# --- Step 3: Copy files to Resources folder ---
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
