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
$ServicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$ResourcesFolder      = Join-Path $ScriptDir "..\Servy.CLI\Resources" | Resolve-Path
$BuildConfiguration   = "Debug"
$Platform             = "x64"
$BuildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$BuildConfiguration"
$ResourcesBuildOutput = Join-Path $ScriptDir "..\Servy.CLI\bin\$Platform\$BuildConfiguration"

# ------------------------------------------------------------------------
# Step 0: Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $CliProject /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:Platform=$Platform

# -------------------------------------------------------------------------------------------------
# Step 1: Build the project in Debug mode
# -------------------------------------------------------------------------------------------------
& $ServicePublishScript -BuildConfiguration $BuildConfiguration

# ------------------------------------------------------------------------
# 2. Define files to copy
# ------------------------------------------------------------------------
$FilesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.CLI.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.CLI.pdb" },
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
# Step 4 - Copy Servy.Infrastructure.pdb
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
