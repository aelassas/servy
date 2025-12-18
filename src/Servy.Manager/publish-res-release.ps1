<#
.SYNOPSIS
Builds the Servy.Service project in Release mode (.NET Framework 4.8) and copies the resulting binaries into the Servy.Manager\Resources folder.

.DESCRIPTION
This script automates the preparation of CLI and manager resources for release:
1. Builds the Servy.Service project in Release configuration.
2. Copies the executable, PDB files, and DLLs into the Servy.Manager\Resources folder.
3. Ensures x86 and x64 subfolders exist and copies platform-specific outputs.
4. Optionally builds and copies Servy.Infrastructure.pdb (currently commented out).

.PARAMETER BuildConfiguration
Optional. Specifies the build configuration. Default is "Release".

.REQUIREMENTS
- MSBuild must be available in the PATH.
- Script should be run in PowerShell (x64).
- Project folder structure must match the expected layout.

.NOTES
- Author: Akram El Assas
- Intended for preparing release-ready resources for Servy.Manager.
- Adjust file paths if the project structure changes.

.EXAMPLE
.\publish-res-release.ps1
Builds Servy.Service in Release mode and copies binaries into Servy.Manager\Resources.
#>

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------------
# Paths
# ------------------------------------------------------------------------
# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Absolute paths to relevant folders and project
$ManagerProject        = Join-Path $ScriptDir "..\Servy.Manager\Servy.Manager.csproj" | Resolve-Path
$ServicePublishScript  = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$ResourcesFolder       = Join-Path $ScriptDir "..\Servy.Manager\Resources" | Resolve-Path
$BuildConfiguration    = "Release"
$Platform              = "x64"
$BuildOutput           = Join-Path $ScriptDir "..\Servy.Service\bin\$Platform\$BuildConfiguration"
$ResourcesBuildOutput  = Join-Path $ScriptDir "..\Servy.Manager\bin\$Platform\$BuildConfiguration"

# ------------------------------------------------------------------------
# 0. Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $ManagerProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$Platform

# ------------------------------------------------------------------------
# 1. Build Servy.Service
# ------------------------------------------------------------------------
& $ServicePublishScript -BuildConfiguration $BuildConfiguration

# ------------------------------------------------------------------------
# 2. Define files to copy
# ------------------------------------------------------------------------
$FilesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.pdb" },
    @{ Source = "*.dll"; Destination = "*.dll" }
)

# ------------------------------------------------------------------------
# 3. Copy files to Resources folder
# ------------------------------------------------------------------------
foreach ($File in $FilesToCopy) {
    $SourcePath = Join-Path $BuildOutput $File.Source

    if ($File.Source -like "*.dll") {
        Copy-Item -Path $SourcePath -Destination $ResourcesFolder -Force
        Write-Host "Copied $($File.Source) -> $ResourcesFolder"
    } else {
        $DestPath = Join-Path $ResourcesFolder $File.Destination
        Copy-Item -Path $SourcePath -Destination $DestPath -Force
        Write-Host "Copied $($File.Source) -> $($File.Destination)"
    }
}

# ----------------------------------------------------------------------
# 5. Copy Servy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$BuildConfiguration\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $ResourcesFolder "Servy.Infrastructure.pdb"

& msbuild $InfraServiceProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration

Copy-Item -Path $InfraSourcePath -Destination $InfraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>

Write-Host "$BuildConfiguration build published successfully to Resources."
