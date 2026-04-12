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

function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# ------------------------------------------------------------------------
# Paths
# ------------------------------------------------------------------------
# Get the directory of the current script
$scriptDir = $PSScriptRoot

# Absolute paths to relevant folders and project
$managerProject        = Join-Path $scriptDir "..\Servy.Manager\Servy.Manager.csproj" | Resolve-Path
$servicePublishScript  = Join-Path $scriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder       = Join-Path $scriptDir "..\Servy.Manager\Resources"
$buildConfiguration    = "Release"
$platform              = "x64"
$buildOutput           = Join-Path $scriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput  = Join-Path $scriptDir "..\Servy.Manager\bin\$platform\$buildConfiguration"

if (-not (Test-Path $managerProject)) {
    Write-Error "CRITICAL: Manager Project not found at $managerProject"
    exit 1
}

# Guarded Creation
if (-not (Test-Path $resourcesFolder)) {
    Write-Host "Creating missing resources folder: $resourcesFolder" -ForegroundColor Gray
    New-Item -ItemType Directory -Path $resourcesFolder -Force | Out-Null
}

# ------------------------------------------------------------------------
# 0. Build Servy to ensure x86 and x64 resources exist
# ------------------------------------------------------------------------
& msbuild $managerProject /t:Clean,Rebuild /p:Configuration=$buildConfiguration /p:Platform=$platform
Check-LastExitCode "MSBuild for Manager failed"

# ------------------------------------------------------------------------
# 1. Build Servy.Service
# ------------------------------------------------------------------------
& $servicePublishScript -BuildConfiguration $buildConfiguration
Check-LastExitCode "Service publish script failed"

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
