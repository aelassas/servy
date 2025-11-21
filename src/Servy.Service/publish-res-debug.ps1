<#
.SYNOPSIS
    Publishes the Servy.Restarter project and copies its build artifacts into the Servy.Service Resources folder.

.DESCRIPTION
    This script builds the Servy.Restarter project targeting the specified framework (default: net10.0-windows)
    in Debug configuration. It produces a self-contained, single-file executable and copies the resulting
    executable and PDB files into the Servy.Service Resources folder. Optional core and infrastructure PDBs
    can also be copied (currently commented out).

.PARAMETER tfm
    The target framework to build against. Default is "net10.0-windows".

.NOTES
    - Requires the .NET SDK and 'dotnet' available in PATH.
    - Can be run from any working directory; all paths are resolved relative to the script location.
    - Produces output in 'Servy.Restarter\bin\Debug\<tfm>\win-x64\publish'.

.EXAMPLE
    PS> .\publish-res-release.ps1
    Publishes Servy.Restarter using the default target framework and copies artifacts to Servy.Service Resources folder.

.EXAMPLE
    PS> .\publish-res-release.ps1 -tfm net10.0-windows
    Publishes Servy.Restarter targeting .NET 10 and copies the artifacts to the Resources folder.
#>

param(
    # Target framework for build (default: net10.0-windows)
    [string]$tfm = "net10.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths & build configuration
# ---------------------------------------------------------------------------------
$restarterDir       = Join-Path $ScriptDir "..\Servy.Restarter" | Resolve-Path
$resourcesFolder    = Join-Path $ScriptDir "..\Servy.Service\Resources" | Resolve-Path
$buildConfiguration = "Debug"
$runtime            = "win-x64"
$selfContained      = $true

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Restarter project
# ---------------------------------------------------------------------------------
$PublishRestarterScript = Join-Path $restarterDir "publish.ps1"

if (-not (Test-Path $PublishRestarterScript)) {
    Write-Error "Project file not found: $PublishRestarterScript"
    exit 1
}

Write-Host "=== [restarter] Running publish.ps1 ==="
& $PublishRestarterScript -tfm $tfm -runtime $runtime -configuration $buildConfiguration
if ($LASTEXITCODE -ne 0) {
    Write-Error "[restarter] publish.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== [restarter] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Locate publish and build output folders
# ---------------------------------------------------------------------------------
$basePath      = Join-Path $ScriptDir "..\Servy.Restarter\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$buildFolder   = $basePath

# ---------------------------------------------------------------------------------
# Step 3: Ensure resources folder exists
# ---------------------------------------------------------------------------------
if (-not (Test-Path $resourcesFolder)) {
    New-Item -ItemType Directory -Path $resourcesFolder | Out-Null
}

# ---------------------------------------------------------------------------------
# Step 4: Copy artifacts (renaming as needed)
# ---------------------------------------------------------------------------------
Copy-Item -Path (Join-Path $publishFolder "Servy.Restarter.exe") `
          -Destination (Join-Path $resourcesFolder "Servy.Restarter.exe") -Force

Copy-Item -Path (Join-Path $buildFolder "Servy.Restarter.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Restarter.pdb") -Force
<#
Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Core.pdb") -Force
#>
# ----------------------------------------------------------------------
# Step 5 - CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$infraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$infraSourcePath = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$buildConfiguration\$tfm\$runtime\Servy.Infrastructure.pdb"
$infraDestPath   = Join-Path $resourcesFolder "Servy.Infrastructure.pdb"

dotnet publish $infraServiceProject `
    -c $buildConfiguration `
    -r $runtime `
    --self-contained false `
    /p:TargetFramework=$tfm `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=false `
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Copy-Item -Path $infraSourcePath  -Destination $infraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>
# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $buildConfiguration build ($tfm) published successfully to Resources ==="
