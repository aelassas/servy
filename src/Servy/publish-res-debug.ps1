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

function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# --- Paths ---
$scriptDir            = $PSScriptRoot
$servyProject         = Join-Path $scriptDir "..\Servy\Servy.csproj" | Resolve-Path
$servicePublishScript = Join-Path $scriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
$resourcesFolder      = Join-Path $scriptDir "..\Servy\Resources" | Resolve-Path
$buildConfiguration   = "Debug"
$platform             = "x64"
$buildOutput          = Join-Path $scriptDir "..\Servy.Service\bin\$buildConfiguration"
$resourcesBuildOutput = Join-Path $scriptDir "..\Servy\bin\$platform\$buildConfiguration"

# Safety check: Resolve-Path will fail if the directory structure is missing
try {
    $servicePublishScript = Join-Path $scriptDir "..\Servy.Service\publish.ps1" | Resolve-Path
    $resourcesFolder      = Join-Path $scriptDir "..\Servy\Resources"
} catch {
    Write-Error "CRITICAL: Could not resolve project paths. Ensure the folder structure is correct."
    exit 1
}

# ------------------------------------------------------------------------
# 1. Build the project
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
# Step 4 - CopyServy.Infrastructure.pdb
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
