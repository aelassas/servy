# =================================================================================================
# publish-res-debug.ps1
# -------------------------------------------------------------------------------------------------
# Build Servy.Service in Debug mode (net48) and copy the executable, PDBs, and resources
# to the Servy.CLI Resources folder for CLI integration.
#
# Requirements:
#   - msbuild available in PATH
#
# Steps:
#   1. Build Servy.Service in Debug mode.
#   2. Copy build artifacts to Servy.CLI\Resources folder with renamed executable.
# =================================================================================================

$ErrorActionPreference = "Stop"

# -------------------------------------------------------------------------------------------------
# Paths & Configuration
# -------------------------------------------------------------------------------------------------
$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServiceProject     = Join-Path $ScriptDir "..\Servy.Service\Servy.Service.csproj"
$ResourcesFolder    = Join-Path $ScriptDir "..\Servy.CLI\Resources"
$BuildConfiguration = "Debug"
$BuildOutput        = Join-Path $ScriptDir "..\Servy.Service\bin\$BuildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy.CLI\bin\$platform\$buildConfiguration"

# -------------------------------------------------------------------------------------------------
# Step 1: Build the project in Debug mode
# -------------------------------------------------------------------------------------------------
Write-Host "Building Servy.Service in $BuildConfiguration mode..."
msbuild $ServiceProject /t:Clean,Build /p:Configuration=$BuildConfiguration

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
# Step 4 - CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$infraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$infraSourcePath = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\Servy.Infrastructure.pdb"
$infraDestPath   = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

msbuild $infraServiceProject /t:Clean,Build /p:Configuration=$buildConfiguration

Copy-Item -Path $infraSourcePath -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>

Write-Host "$BuildConfiguration build published successfully to Resources."
