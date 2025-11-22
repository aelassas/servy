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
./publish-res-release.ps1 -Tfm net9.0-windows
Publishes the service using .NET 9.

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

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work no matter where script is run)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths and build settings
# ---------------------------------------------------------------------------------
$ServiceDir         = Join-Path $ScriptDir  "..\Servy.Service" | Resolve-Path
$ServiceProject     = Join-Path $ServiceDir "Servy.Service.csproj" | Resolve-Path
$ResourcesFolder    = Join-Path $ScriptDir  "..\Servy\Resources" | Resolve-Path
$BuildConfiguration = "Release"
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

Write-Host "$BuildConfiguration build published successfully to Resources."
#>

# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $BuildConfiguration build ($Tfm) published successfully to Resources ==="
