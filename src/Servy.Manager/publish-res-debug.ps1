<#
.SYNOPSIS
Builds the Servy.Service project in Debug mode (.NET Framework 4.8) and copies the resulting binaries into the Servy.Manager\Resources folder.

.DESCRIPTION
This script automates preparing debug resources for Servy.Manager:
1. Builds the Servy.Service project in Debug configuration.
2. Copies the executable, PDB files, and DLLs into the Servy.Manager\Resources folder.
3. Ensures x86 and x64 subfolders exist and copies platform-specific outputs.
4. Optionally builds and copies Servy.Infrastructure.pdb (currently commented out).

.PARAMETER BuildConfiguration
Optional. Specifies the build configuration. Default is "Debug".

.REQUIREMENTS
- MSBuild must be available in the PATH.
- Script should be run in PowerShell (x64).
- Project folder structure must match the expected layout.

.NOTES
- Author: Akram El Assas
- Intended for preparing debug-ready resources for Servy.Manager.
- Adjust file paths if the project structure changes.

.EXAMPLE
.\publish-res-debug.ps1
Builds Servy.Service in Debug mode and copies binaries into Servy.Manager\Resources.
#>

$ErrorActionPreference = "Stop"

# --- Paths ---
$ScriptDir            = Split-Path -Parent $MyInvocation.MyCommand.Path
$ManagerProject       = Join-Path $ScriptDir "..\Servy.Manager\Servy.Manager.csproj" | Resolve-Path
$servicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder      = Join-Path $ScriptDir "..\Servy.Manager\Resources" | Resolve-Path
$buildConfiguration   = "Debug"
$platform             = "x64"
$buildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy.Manager\bin\$platform\$buildConfiguration"

# ------------------------------------------------------------------------
# Step 0: Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $ManagerProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$platform

# --- Step 1: Build the project ---
& $servicePublishScript -BuildConfiguration $buildConfiguration

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
# Step 4 - Copy Servy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$infraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$infraSourcePath = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\Servy.Infrastructure.pdb"
$infraDestPath   = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

& msbuild $infraServiceProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration

Copy-Item -Path $infraSourcePath -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>
Write-Host "$buildConfiguration build published successfully to Resources."
