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
$resourcesFolder    = Join-Path $ScriptDir "..\Servy.Manager\Resources"
$buildConfiguration = "Debug"
$buildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy.Manager\bin\$platform\$buildConfiguration"

# --- Step 1: Build the project ---
Write-Host "Building Servy.Service in $buildConfiguration mode..."
$serviceProjectPublishRes     = Join-Path $ScriptDir "..\Servy.Service\publish-res-release.ps1"
& $serviceProjectPublishRes
msbuild $serviceProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration /p:AllowUnsafeBlocks=true

# --- Step 2: Define files to copy ---
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.pdb" }
    @{ Source = "*.dll";    Destination = "*.dll" }
)

# ------------------------------------------------------------------------
# 3. Copy files to Resources folder
# ------------------------------------------------------------------------
foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $buildOutput $file.Source

    if ($file.Source -like "*.dll") {
        Copy-Item -Path $sourcePath -Destination $resourcesFolder -Force
        Write-Host "Copied $($file.Source) -> $resourcesFolder"
    } else {
        $destPath = Join-Path $resourcesFolder $file.Destination
        Copy-Item -Path $sourcePath -Destination $destPath -Force
        Write-Host "Copied $($file.Source) -> $($file.Destination)"
    }
}

# Ensure destination folders exist
New-Item -ItemType Directory -Force -Path "$resourcesFolder\x86" | Out-Null
New-Item -ItemType Directory -Force -Path "$resourcesFolder\x64" | Out-Null

# Copy x86/ x64/ folders
Copy-Item -Path "$resourcesBuildOutput\x86\*" -Destination "$resourcesFolder\x86" -Force -Recurse
Copy-Item -Path "$resourcesBuildOutput\x64\*" -Destination "$resourcesFolder\x64" -Force -Recurse

# ----------------------------------------------------------------------
# Step 4 - CopyServy.Infrastructure.pdb
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
