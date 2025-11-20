# ========================================================================
# publish-res-release.ps1
# ------------------------------------------------------------------------
# Purpose:
#   Builds the Servy.Service project in Release mode and copies the
#   resulting binaries into the Servy\Resources folder for packaging
#   or distribution.
#
# Requirements:
#   - msbuild must be available in the PATH.
#
# Steps:
#   1. Build Servy.Service in Release configuration.
#   2. Copy the required binaries and symbols to the Resources folder.
# ========================================================================

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------------
# Paths
# ------------------------------------------------------------------------
# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Absolute paths to relevant folders and project
$serviceProject       = Join-Path $ScriptDir "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder      = Join-Path $ScriptDir "..\Servy\Resources"
$buildConfiguration   = "Release"
$platform             = "x64"
$buildOutput          = Join-Path $ScriptDir "..\Servy.Service\bin\$platform\$buildConfiguration"
$resourcesBuildOutput = Join-Path $ScriptDir "..\Servy\bin\$platform\$buildConfiguration"
$signPath             = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ------------------------------------------------------------------------
# 1. Build the project
# ------------------------------------------------------------------------
Write-Host "Building Servy.Service in $buildConfiguration mode..."
$serviceProjectPublishRes     = Join-Path $ScriptDir "..\Servy.Service\publish-res-release.ps1"
& $serviceProjectPublishRes
msbuild $serviceProject /t:Clean,Build /p:Configuration=$buildConfiguration /p:Platform=$platform /p:AllowUnsafeBlocks=true

# ----------------------------------------------------------------------
# 2. Sign the published executable if signing is enabled
# ----------------------------------------------------------------------
$exePath = Join-Path $buildOutput "Servy.Service.exe"
& $signPath $exePath

# ------------------------------------------------------------------------
# 3. Define files to copy
# ------------------------------------------------------------------------
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.pdb" },
    @{ Source = "*.dll"; Destination = "*.dll" }
)

# ------------------------------------------------------------------------
# 4. Copy files to Resources folder
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
# 5. CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$infraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$infraSourcePath = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\Servy.Infrastructure.pdb"
$infraDestPath   = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

msbuild $infraServiceProject /t:Clean,Build /p:Configuration=$buildConfiguration

Copy-Item -Path $infraSourcePath -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>
Write-Host "$buildConfiguration build published successfully to Resources."
