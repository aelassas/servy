#requires -Version 5.0
<#
.SYNOPSIS
Publishes Servy.Service and copies its build artifacts into the Servy Resources folder.

.DESCRIPTION
This script:
1. Runs the publish.ps1 script inside the Servy.Service project.
2. Locates the produced build/publish output folders.
3. Copies the generated single-file executable and PDB files into the main
   Servy Resources directory so they can be embedded in Servy builds.

Primarily used as part of the full release pipeline.

.PARAMETER Tfm
Target framework moniker for the publish step. Default: net10.0-windows.

.EXAMPLE
./publish-res-release.ps1
Runs with default target framework.

.EXAMPLE
./publish-res-release.ps1 -Tfm net10.0-windows
Publishes the service using .NET target framework.

.NOTES
Author : Akram El Assas
Project: Servy
This script requires dotnet SDK, PowerShell 5+ or 7+, and correct folder structure.
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm = "net10.0-windows"
)

$ErrorActionPreference = "Stop"

function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work no matter where script is run)
# ---------------------------------------------------------------------------------
$scriptDir = $PSScriptRoot

# ---------------------------------------------------------------------------------
# Paths and build settings
# ---------------------------------------------------------------------------------
$serviceDir         = Join-Path $scriptDir  "..\Servy.Service"
$resourcesFolder    = Join-Path $scriptDir  "..\Servy\Resources"
$buildConfiguration = "Release"
$runtime            = "win-x64"

# Prevent Resolve-Path errors on clean environments
if (-not (Test-Path $serviceDir)) {
    Write-Error "CRITICAL: Service project directory not found at $serviceDir"
    exit 1
}

# Guarded Creation
if (-not (Test-Path $resourcesFolder)) {
    Write-Host "Creating missing resources folder: $resourcesFolder" -ForegroundColor Gray
    New-Item -ItemType Directory -Path $resourcesFolder -Force | Out-Null
}

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Service project
# ---------------------------------------------------------------------------------
$publishServiceScript = Join-Path $serviceDir "publish.ps1"

if (-not (Test-Path $publishServiceScript)) {
    Write-Error "Required script not found: $publishServiceScript"
    exit 1
}

Write-Host "=== [service] Running publish.ps1 ==="
& $publishServiceScript -Tfm $Tfm -BuildConfiguration $buildConfiguration
Check-LastExitCode "$publishServiceScript failed"

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

Write-Host "$buildConfiguration build published successfully to Resources."
#>

# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $buildConfiguration build ($Tfm) published successfully to Resources ==="
