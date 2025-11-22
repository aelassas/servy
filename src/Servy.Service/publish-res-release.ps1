<#
.SYNOPSIS
    Publishes the Servy.Restarter project and copies its build artifacts into the Servy.Service Resources folder.

.DESCRIPTION
    This script builds the Servy.Restarter project targeting the specified framework (default: net10.0-windows)
    in Release configuration. It produces a self-contained executable and copies the resulting executable and PDB
    files into the Servy.Service Resources folder. Optional core and infrastructure PDBs can also be copied
    (currently commented out).

.PARAMETER Tfm
    The target framework to build against. Default is "net10.0-windows".

.NOTES
    - Requires the .NET SDK to be installed and 'dotnet' to be available in PATH.
    - Can be run from any working directory; paths are resolved relative to the script location.
    - Produces output in 'Servy.Restarter\bin\Release\<tfm>\win-x64\publish'.

.EXAMPLE
    PS> .\publish-res-release.ps1
    Publishes Servy.Restarter using the default target framework and copies artifacts to Servy.Service Resources folder.

.EXAMPLE
    PS> .\publish-res-release.ps1 -Tfm net10.0-windows
    Publishes Servy.Restarter targeting .NET 10 and copies the artifacts to the Resources folder.
#>

param(
    # Target framework for build (default: net10.0-windows)
    [string]$Tfm = "net10.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths & build configuration
# ---------------------------------------------------------------------------------
$RestarterDir       = Join-Path $ScriptDir "..\Servy.Restarter" | Resolve-Path
$ResourcesFolder    = Join-Path $ScriptDir "..\Servy.Service\Resources" | Resolve-Path
$BuildConfiguration = "Release"
$Runtime            = "win-x64"
$SelfContained      = $true

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Restarter project
# ---------------------------------------------------------------------------------
$PublishRestarterScript = Join-Path $RestarterDir "publish.ps1"

if (-not (Test-Path $PublishRestarterScript)) {
    Write-Error "Project file not found: $PublishRestarterScript"
    exit 1
}

Write-Host "=== [restarter] Running publish.ps1 ==="
& $PublishRestarterScript -Tfm $Tfm -Runtime $Runtime -Configuration $BuildConfiguration
if ($LASTEXITCODE -ne 0) {
    Write-Error "[restarter] publish.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== [restarter] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Locate publish and build output folders
# ---------------------------------------------------------------------------------
$BasePath      = Join-Path $ScriptDir "..\Servy.Restarter\bin\$BuildConfiguration\$Tfm\$Runtime"
$PublishFolder = Join-Path $BasePath "publish"
$BuildFolder   = $BasePath

# ---------------------------------------------------------------------------------
# Step 3: Ensure resources folder exists
# ---------------------------------------------------------------------------------
if (-not (Test-Path $ResourcesFolder)) {
    New-Item -ItemType Directory -Path $ResourcesFolder | Out-Null
}

# ---------------------------------------------------------------------------------
# Step 5: Copy artifacts (renaming as needed)
# ---------------------------------------------------------------------------------

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.Restarter.exe failed."
    exit $LASTEXITCODE
}

Copy-Item -Path (Join-Path $PublishFolder "Servy.Restarter.exe") `
          -Destination (Join-Path $ResourcesFolder "Servy.Restarter.exe") -Force

Copy-Item -Path (Join-Path $BuildFolder "Servy.Restarter.pdb") `
          -Destination (Join-Path $ResourcesFolder "Servy.Restarter.pdb") -Force
<#
Copy-Item -Path (Join-Path $BuildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $ResourcesFolder "Servy.Core.pdb") -Force
#>
# ----------------------------------------------------------------------
# Step 6 - CopyServy.Infrastructure.pdb
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
