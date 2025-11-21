<#
.SYNOPSIS
Builds Servy.Service in Debug mode (.NET Framework 4.8) and copies the resulting binaries and resources into the Servy.CLI\Resources folder.

.DESCRIPTION
This script performs the following steps:
1. Builds Servy.CLI to ensure all x86 and x64 resources exist.
2. Builds Servy.Service in Debug mode.
3. Copies the executable, PDBs, and DLLs to the CLI Resources folder.
4. Ensures x86 and x64 subfolders exist and copies platform-specific outputs.
5. Optionally builds and copies Servy.Infrastructure.pdb (currently commented out).

.PARAMETER BuildConfiguration
Specifies the build configuration. Default is "Debug".

.REQUIREMENTS
- MSBuild must be installed and available in PATH.
- Script should be run in PowerShell x64.
- The folder structure must match the assumed layout.

.NOTES
- Intended for preparing debug-ready CLI resources.
- Adjust file paths if project structure changes.
- Author: Akram El Assas

.EXAMPLE
.\publish-res-debug.ps1
Builds Servy.Service in Debug mode and copies outputs to Servy.CLI\Resources.
#>

$ErrorActionPreference = "Stop"

# -------------------------------------------------------------------------------------------------
# Paths & Configuration
# -------------------------------------------------------------------------------------------------
$ScriptDir            = Split-Path -Parent $MyInvocation.MyCommand.Path
$CliProject           = Join-Path $ScriptDir "..\Servy.CLI\Servy.CLI.csproj" | Resolve-Path
$servicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$ResourcesFolder      = Join-Path $ScriptDir "..\Servy.CLI\Resources" | Resolve-Path
$BuildConfiguration   = "Debug"
$platform             = "x64"
$BuildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$BuildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy.CLI\bin\$platform\$buildConfiguration"

# ------------------------------------------------------------------------
# Step 0: Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $CliProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$platform

# -------------------------------------------------------------------------------------------------
# Step 1: Build the project in Debug mode
# -------------------------------------------------------------------------------------------------
& $servicePublishScript -BuildConfiguration $buildConfiguration

# ------------------------------------------------------------------------
# 2. Define files to copy
# ------------------------------------------------------------------------
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.CLI.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.CLI.pdb" },
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

Write-Host "$BuildConfiguration build published successfully to Resources."
