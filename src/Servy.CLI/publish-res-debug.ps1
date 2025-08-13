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

# -------------------------------------------------------------------------------------------------
# Step 1: Build the project in Debug mode
# -------------------------------------------------------------------------------------------------
Write-Host "Building Servy.Service in $BuildConfiguration mode..."
msbuild $ServiceProject /t:Clean,Build /p:Configuration=$BuildConfiguration

# -------------------------------------------------------------------------------------------------
# Step 2: Files to copy (source → destination)
# -------------------------------------------------------------------------------------------------
$FilesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.CLI.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.CLI.pdb" },
    @{ Source = "Servy.Core.pdb";    Destination = "Servy.Core.pdb" }
)

# -------------------------------------------------------------------------------------------------
# Step 3: Copy files to Resources folder
# -------------------------------------------------------------------------------------------------
foreach ($file in $FilesToCopy) {
    $SourcePath = Join-Path $BuildOutput $file.Source
    $DestPath   = Join-Path $ResourcesFolder $file.Destination
    Copy-Item -Path $SourcePath -Destination $DestPath -Force
    Write-Host "Copied $($file.Source) → $($file.Destination)"
}

Write-Host "$BuildConfiguration build published successfully to Resources."
