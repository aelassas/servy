<#
.SYNOPSIS
    Publishes Servy.Service and copies its build artifacts into the Servy.CLI Resources folder.

.DESCRIPTION
    This script publishes the Servy.Service project (self-contained) for Windows
    targeting the specified framework. After publishing, it copies the resulting
    executable and PDB files into the Servy.CLI Resources folder, renaming them
    as needed. Optional infrastructure and core PDBs can also be copied.

.PARAMETER tfm
    The target framework to build against. Default is "net10.0-windows".

.NOTES
    - Requires .NET SDK installed and 'dotnet' available in PATH.
    - Can be run from any working directory; all paths are resolved relative to the script location.
    - Produces output in 'Servy.Service\bin\Release\<tfm>\win-x64\publish'.
    - Self-contained single-file executable is produced for Servy.Service.

.EXAMPLE
    PS> .\publish-res-release.ps1
    Publishes Servy.Service using the default target framework and copies the artifacts to the CLI resources folder.

.EXAMPLE
    PS> .\publish-res-release.ps1 -tfm net10.0-windows
    Publishes Servy.Service targeting .NET 10 and copies the artifacts.
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
$serviceDir         = Join-Path $ScriptDir "..\Servy.Service" | Resolve-Path
$serviceProject     = Join-Path $serviceDir "Servy.Service.csproj" | Resolve-Path
$resourcesFolder    = Join-Path $ScriptDir "..\Servy.CLI\Resources" | Resolve-Path
$buildConfiguration = "Release"
$runtime            = "win-x64"
$selfContained      = $true

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Service project
# ---------------------------------------------------------------------------------
$PublishServiceScript = Join-Path $serviceDir "publish.ps1"

if (-not (Test-Path $PublishServiceScript)) {
    Write-Error "Required script not found: $PublishServiceScript"
    exit 1
}

Write-Host "=== [service] Running publish.ps1 ==="
& $PublishServiceScript -tfm $tfm -configuration $buildConfiguration
if ($LASTEXITCODE -ne 0) {
    Write-Error "[service] publish.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== [service] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Locate publish and build output folders
# ---------------------------------------------------------------------------------
$basePath      = Join-Path $ScriptDir "..\Servy.Service\bin\$buildConfiguration\$tfm\$runtime"
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
Copy-Item -Path (Join-Path $publishFolder "Servy.Service.exe") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.CLI.exe") -Force

Copy-Item -Path (Join-Path $buildFolder "Servy.Service.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.CLI.pdb") -Force
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
