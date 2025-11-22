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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths and build settings
# ---------------------------------------------------------------------------------
$ServiceDir         = Join-Path $ScriptDir  "..\Servy.Service" | Resolve-Path
$ServiceProject     = Join-Path $ServiceDir "Servy.Service.csproj" | Resolve-Path
$ResourcesFolder    = Join-Path $ScriptDir "..\Servy\Resources" | Resolve-Path
$BuildConfiguration = "Debug"
$Runtime            = "win-x64"
$SelfContained      = $true

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Service project
# ---------------------------------------------------------------------------------
$PublishServiceScript = Join-Path $ServiceDir "publish.ps1"

if (-not (Test-Path $PublishServiceScript)) {
    Write-Error "Required script not found: $PublishServiceScript"
    exit 1
}

Write-Host "=== [service] Running publish.ps1 ==="
& $PublishServiceScript -Tfm $Tfm -Configuration $BuildConfiguration
if ($LASTEXITCODE -ne 0) {
    Write-Error "[service] publish.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== [service] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Prepare publish and build folder paths
# ---------------------------------------------------------------------------------
$BasePath      = Join-Path $ScriptDir "..\Servy.Service\bin\$BuildConfiguration\$Tfm\$Runtime"
$PublishFolder = Join-Path $BasePath "publish"
$BuildFolder   = $BasePath

# ---------------------------------------------------------------------------------
# Step 3: Copy artifacts to Resources folder
# ---------------------------------------------------------------------------------
if (-not (Test-Path $ResourcesFolder)) {
    New-Item -ItemType Directory -Path $ResourcesFolder | Out-Null
}

# Copy single-file executable
Copy-Item -Path (Join-Path $PublishFolder "Servy.Service.exe") `
          -Destination (Join-Path $ResourcesFolder "Servy.Service.exe") -Force

# Copy PDB files
Copy-Item -Path (Join-Path $BuildFolder "Servy.Service.pdb") `
          -Destination (Join-Path $ResourcesFolder "Servy.Service.pdb") -Force
<#
Copy-Item -Path (Join-Path $BuildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $ResourcesFolder "Servy.Core.pdb") -Force
#>

# ----------------------------------------------------------------------
# Step 4 - CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$BuildConfiguration\$Tfm\$Runtime\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $ResourcesFolder "Servy.Infrastructure.pdb"

dotnet publish $InfraServiceProject `
    -c $BuildConfiguration `
    -r $Runtime `
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
Write-Host "=== $BuildConfiguration build ($Tfm) published successfully to Resources ==="
