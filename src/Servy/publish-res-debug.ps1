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
$ServicePublishScript = Join-Path $ScriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$ResourcesFolder      = Join-Path $ScriptDir "..\Servy\Resources" | Resolve-Path
$BuildConfiguration   = "Debug"
$Platform             = "x64"
$BuildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$BuildConfiguration"
$ResourcesBuildOutput = Join-Path $ScriptDir "..\Servy\bin\$Platform\$BuildConfiguration"

# ------------------------------------------------------------------------
# 1. Build the project
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
# Step 4 - CopyServy.Infrastructure.pdb
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
