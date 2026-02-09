<#
.SYNOPSIS
    Builds Servy.Restarter (.NET Framework 4.8, Release) and copies its output
    into the Servy.Service Resources folder for use at runtime.

.DESCRIPTION
    This script performs the Debug build of Servy.Restarter using MSBuild
    and prepares the binaries needed by Servy.Service. The generated
    artifacts (EXE, PDB, optional dependencies) are copied into the
    Resources folder with the correct naming conventions.

.REQUIREMENTS
    - MSBuild must be installed and available in PATH.
    - Script must be run under PowerShell (x64 recommended).
    - Servy.Restarter targets .NET Framework 4.8, so the .NET 4.8 Developer Pack
      must be installed.

.NOTES
    Author : Akram El Assas
    Project: Servy
    Script : publish-res-debug.ps1
#>

$ErrorActionPreference = "Stop"

# ----------------------------------------------------------------------
# Resolve script directory (absolute path to this script's location)
# ----------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$restarterPublishScript = Join-Path $scriptDir "..\Servy.Restarter\publish.ps1" | Resolve-Path
$resourcesFolder        = Join-Path $scriptDir "..\Servy.Service\Resources" | Resolve-Path
$buildConfiguration     = "Debug"
$platform               = "x64"
$buildOutput            = Join-Path $scriptDir "..\Servy.Restarter\bin\$platform\$buildConfiguration"

# ----------------------------------------------------------------------
# Step 1: Build Servy.Restarter in Debug mode
# ----------------------------------------------------------------------
& $restarterPublishScript -BuildConfiguration $buildConfiguration

# ----------------------------------------------------------------------
# Step 2: Files to copy (with renamed outputs where applicable)
# ----------------------------------------------------------------------
$FilesToCopy = @(
    @{ Source = "Servy.Restarter.exe"; Destination = "Servy.Restarter.exe" },
    @{ Source = "Servy.Restarter.pdb"; Destination = "Servy.Restarter.pdb" }
    #@{ Source = "Dapper.dll";          Destination = "Dapper.dll" }
    #@{ Source = "Newtonsoft.Json.dll"; Destination = "Newtonsoft.Json.dll" }
    #@{ Source = "Servy.Core.dll";      Destination = "Servy.Core.dll" }
)

# ----------------------------------------------------------------------
# Step 3: Copy the files into the Resources folder
# ----------------------------------------------------------------------
foreach ($File in $FilesToCopy) {
    $SourcePath = Join-Path $buildOutput $File.Source
    $DestPath   = Join-Path $resourcesFolder $File.Destination

    Copy-Item -Path $SourcePath -Destination $DestPath -Force
    Write-Host "Copied $($File.Source) -> $($File.Destination)"
}

# ----------------------------------------------------------------------
# Step 4: Copy Servy.Infrastructure.pdb
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
