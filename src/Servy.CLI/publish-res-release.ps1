<#
.SYNOPSIS
Builds Servy.Service in Release mode (.NET Framework 4.8) and copies required binaries into the Servy.CLI\Resources folder.

.DESCRIPTION
This script performs the following steps:
1. Builds Servy.CLI to ensure all x86 and x64 resources exist.
2. Builds Servy.Service in Release mode.
3. Copies the resulting executables, PDBs, and DLLs into the CLI Resources folder.
4. Ensures x86 and x64 subfolders exist and copies platform-specific outputs.
5. Optionally builds and copies Servy.Infrastructure.pdb (commented by default).

.PARAMETER BuildConfiguration
Specifies the build configuration. Default is "Release".

.REQUIREMENTS
- MSBuild must be installed and available in PATH.
- The project structure must match the folder layout assumed in the script.
- Script should be run in PowerShell x64.

.NOTES
- Intended for preparing release-ready resources for the CLI.
- Adjust file paths if project structure changes.
- Author: Akram El Assas

.EXAMPLE
.\publish-res-release.ps1
Builds Servy.Service in Release mode and copies outputs to Servy.CLI\Resources.
#>

$ErrorActionPreference = "Stop"

# ----------------------------------------------------------------------
# Resolve script directory (absolute path to this script's location)
# ----------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$CliProject            = Join-Path $ScriptDir "..\Servy.CLI\Servy.CLI.csproj" | Resolve-Path
$servicePublishScript  = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder       = Join-Path $ScriptDir "..\Servy.CLI\Resources" | Resolve-Path
$buildConfiguration    = "Release"
$platform              = "x64"
$buildOutput           = Join-Path $ScriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput  = Join-Path $ScriptDir "..\Servy.CLI\bin\$platform\$buildConfiguration"

# ------------------------------------------------------------------------
# Step 0: Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $CliProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$platform

# ----------------------------------------------------------------------
# 1. Build Servy.Service in Release mode
# ----------------------------------------------------------------------
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
# 5. Copy Servy.Infrastructure.pdb
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
