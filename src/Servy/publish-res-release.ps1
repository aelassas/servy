<#
.SYNOPSIS
Builds the Servy.Service project in Release mode and copies the binaries to the Resources folder.

.DESCRIPTION
This script performs the following steps:
1. Builds the main Servy project to ensure all dependencies exist.
2. Builds Servy.Service in Release configuration.
3. Copies the required binaries, PDBs, and DLLs into the Servy\Resources folder.
4. Ensures x86 and x64 subfolders exist and copies platform-specific outputs.
5. Optionally builds and copies Servy.Infrastructure.pdb (commented by default).

.PARAMETER BuildConfiguration
Specifies the build configuration. Default is "Release".

.REQUIREMENTS
- MSBuild must be installed and available in PATH.
- The project structure must match the folder layout assumed in the script.

.NOTES
- The script is intended to prepare release-ready resources for packaging or distribution.
- Author: Akram El Assas
- Adjust paths if project structure changes.

.EXAMPLE
.\publish-res-release.ps1
Builds Servy.Service in Release mode and copies outputs to Resources folder.

#>

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------------
# Paths
# ------------------------------------------------------------------------
# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Absolute paths to relevant folders and project
$ServyProject         = Join-Path $ScriptDir "..\Servy\Servy.csproj" | Resolve-Path
$servicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder      = Join-Path $ScriptDir "..\Servy\Resources" | Resolve-Path
$buildConfiguration   = "Release"
$platform             = "x64"
$buildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy\bin\$platform\$buildConfiguration"
$signPath             = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ------------------------------------------------------------------------
# 0. Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $ServyProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$platform

# ------------------------------------------------------------------------
# 1. Build Servy.Service
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
# 5. CopyServy.Infrastructure.pdb
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
