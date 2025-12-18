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
$ServicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$ResourcesFolder      = Join-Path $ScriptDir "..\Servy\Resources" | Resolve-Path
$BuildConfiguration   = "Release"
$Platform             = "x64"
$BuildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$Platform\$BuildConfiguration"
$ResourcesBuildOutput = Join-Path $ScriptDir "..\Servy\bin\$Platform\$BuildConfiguration"
$SignPath             = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path

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
# 5. CopyServy.Infrastructure.pdb
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
