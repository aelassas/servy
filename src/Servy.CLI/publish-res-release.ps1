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
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$cliProject            = Join-Path $scriptDir "..\Servy.CLI\Servy.CLI.csproj" | Resolve-Path
$servicePublishScript  = Join-Path $scriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder       = Join-Path $scriptDir "..\Servy.CLI\Resources" | Resolve-Path
$buildConfiguration    = "Release"
$platform              = "x64"
$buildOutput           = Join-Path $scriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput  = Join-Path $scriptDir "..\Servy.CLI\bin\$platform\$buildConfiguration"

# ------------------------------------------------------------------------
# Step 0: Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $cliProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration /p:Platform=$platform

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
foreach ($File in $filesToCopy) {
    $sourcePath = Join-Path $buildOutput $File.Source

    if ($File.Source -like "*.dll") {
        Copy-Item -Path $sourcePath -Destination $resourcesFolder -Force
        Write-Host "Copied $($File.Source) -> $resourcesFolder"
    } else {
        $destPath = Join-Path $resourcesFolder $File.Destination
        Copy-Item -Path $sourcePath -Destination $destPath -Force
        Write-Host "Copied $($File.Source) -> $($File.Destination)"
    }
}

# ----------------------------------------------------------------------
# 5. Copy Servy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $scriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $scriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

& msbuild $InfraServiceProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration

Copy-Item -Path $InfraSourcePath -Destination $InfraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>

Write-Host "$buildConfiguration build published successfully to Resources."
