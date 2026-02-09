<#
.SYNOPSIS
Publishes the Servy.Service project in Debug mode and copies its build artifacts
into the main Servy Resources folder.

.DESCRIPTION
This script:
1. Runs publish-res-debug.ps1 to generate the required resource files.
2. Publishes the Servy.Service project as a single-file executable.
3. Copies the published executable and PDB files into the Servy\Resources folder.
4. (Optional, commented out) Can also publish and copy Servy.Infrastructure artifacts.

Used as part of the Debug build workflow for local development.

.PARAMETER Tfm
Target framework moniker. Default: net10.0-windows.

.EXAMPLE
./publish-res-debug.ps1
Runs using the default TFM and publishes Debug artifacts.

.EXAMPLE
./publish-res-debug.ps1 -Tfm net10.0-windows
Publishes Servy.Service with .NET target framework.

.NOTES
Author : Akram El Assas
Project: Servy
Requires: .NET SDK, correct folder structure
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm = "net10.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work no matter where script is run)
# ---------------------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths and build settings
# ---------------------------------------------------------------------------------
$serviceDir         = Join-Path $scriptDir  "..\Servy.Service" | Resolve-Path
$serviceProject     = Join-Path $serviceDir "Servy.Service.csproj" | Resolve-Path
$resourcesFolder    = Join-Path $scriptDir "..\Servy\Resources" | Resolve-Path
$buildConfiguration = "Debug"
$runtime            = "win-x64"
$selfContained      = $true

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Service project
# ---------------------------------------------------------------------------------
$publishServiceScript = Join-Path $serviceDir "publish.ps1"

if (-not (Test-Path $publishServiceScript)) {
    Write-Error "Required script not found: $publishServiceScript"
    exit 1
}

Write-Host "=== [service] Running publish.ps1 ==="
& $publishServiceScript -Tfm $Tfm -Configuration $buildConfiguration
if ($LASTEXITCODE -ne 0) {
    Write-Error "[service] publish.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== [service] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Prepare publish and build folder paths
# ---------------------------------------------------------------------------------
$basePath      = Join-Path $scriptDir "..\Servy.Service\bin\$buildConfiguration\$Tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$buildFolder   = $basePath

# ---------------------------------------------------------------------------------
# Step 3: Copy artifacts to Resources folder
# ---------------------------------------------------------------------------------
if (-not (Test-Path $resourcesFolder)) {
    New-Item -ItemType Directory -Path $resourcesFolder | Out-Null
}

# Copy single-file executable
Copy-Item -Path (Join-Path $publishFolder "Servy.Service.exe") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.exe") -Force

# Copy PDB files
Copy-Item -Path (Join-Path $buildFolder "Servy.Service.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.pdb") -Force
<#
Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Core.pdb") -Force
#>

# ----------------------------------------------------------------------
# Step 4 - CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $scriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $scriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\$Tfm\$runtime\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

dotnet publish $InfraServiceProject `
    -c $buildConfiguration `
    -r $runtime `
    --self-contained false `
    /p:TargetFramework=$Tfm `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=false `
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Copy-Item -Path $InfraSourcePath  -Destination $InfraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>

# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $buildConfiguration build ($Tfm) published successfully to Resources ==="
