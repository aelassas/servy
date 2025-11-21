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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ----------------------------------------------------------------------
# Absolute paths and configuration
# ----------------------------------------------------------------------
$restarterPublishScript = Join-Path $ScriptDir "..\Servy.Restarter\publish.ps1" | Resolve-Path
$resourcesFolder        = Join-Path $ScriptDir "..\Servy.Service\Resources" | Resolve-Path
$buildConfiguration     = "Debug"
$platform               = "x64"
$buildOutput            = Join-Path $ScriptDir "..\Servy.Restarter\bin\$platform\$buildConfiguration"

# ----------------------------------------------------------------------
# Step 1: Build Servy.Restarter in Debug mode
# ----------------------------------------------------------------------
& $restarterPublishScript -BuildConfiguration $buildConfiguration

# ----------------------------------------------------------------------
# Step 2: Files to copy (with renamed outputs where applicable)
# ----------------------------------------------------------------------
$filesToCopy = @(
    @{ Source = "Servy.Restarter.exe"; Destination = "Servy.Restarter.exe" },
    @{ Source = "Servy.Restarter.pdb"; Destination = "Servy.Restarter.pdb" }
    #@{ Source = "Dapper.dll";          Destination = "Dapper.dll" }
    #@{ Source = "Newtonsoft.Json.dll"; Destination = "Newtonsoft.Json.dll" }
    #@{ Source = "Servy.Core.dll";      Destination = "Servy.Core.dll" }
)

# ----------------------------------------------------------------------
# Step 3: Copy the files into the Resources folder
# ----------------------------------------------------------------------
foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $buildOutput $file.Source
    $destPath   = Join-Path $resourcesFolder $file.Destination

    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "Copied $($file.Source) -> $($file.Destination)"
}

# ----------------------------------------------------------------------
# Step 4: Copy Servy.Infrastructure.pdb
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
