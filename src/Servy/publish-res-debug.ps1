<#
.SYNOPSIS
Builds the Servy.Service project in Debug mode (.NET Framework 4.8) and copies the binaries to the Resources folder.

.DESCRIPTION
This script performs the following steps:
1. Builds the main Servy project to ensure all dependencies exist.
2. Builds Servy.Service in Debug configuration.
3. Copies the resulting executable, PDBs, and DLLs into the Servy\Resources folder.
4. Ensures x86 and x64 subfolders exist and copies platform-specific outputs.
5. Optionally builds and copies Servy.Infrastructure.pdb (commented by default).

.PARAMETER BuildConfiguration
Specifies the build configuration. Default is "Debug".

.REQUIREMENTS
- MSBuild must be installed and available in PATH.
- The project structure must match the folder layout assumed in the script.

.NOTES
- The script is intended to prepare debug-ready resources for development or testing.
- Author: Akram El Assas
- Adjust paths if project structure changes.

.EXAMPLE
.\publish-res-debug.ps1
Builds Servy.Service in Debug mode and copies outputs to the Resources folder.
#>

$ErrorActionPreference = "Stop"

# --- Paths ---
$ScriptDir            = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServyProject         = Join-Path $ScriptDir "..\Servy\Servy.csproj" | Resolve-Path
$servicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder      = Join-Path $ScriptDir "..\Servy\Resources" | Resolve-Path
$buildConfiguration   = "Debug"
$platform             = "x64"
$buildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$buildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy\bin\$platform\$buildConfiguration"

# ------------------------------------------------------------------------
# 0. Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $ServyProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$platform

# ------------------------------------------------------------------------
# 1. Build the project
# ------------------------------------------------------------------------
& $servicePublishScript -BuildConfiguration $buildConfiguration

# ------------------------------------------------------------------------
# 2. Define files to copy
# ------------------------------------------------------------------------
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.pdb" },
    @{ Source = "*.dll"; Destination = "*.dll" }
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

& msbuild $infraServiceProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration

Copy-Item -Path $infraSourcePath -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>
Write-Host "$buildConfiguration build published successfully to Resources."
