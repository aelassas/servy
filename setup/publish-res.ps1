#requires -Version 5.0
<#
.SYNOPSIS
    Builds a Servy project (.NET Framework 4.8) and copies the binaries to the Resources folder.

.DESCRIPTION
    Consolidated DRY script for .NET Framework 4.8 resources. This script performs the following steps:
    1. Runs the publish.ps1 script inside the specified source project.
    2. Copies the resulting executable and PDBs into the specified target Resources directory.
    3. Renames the output artifacts to include a '.Net48' suffix to prevent collisions.
    4. Optionally appends an additional suffix (e.g., '.CLI') if provided.
    5. Ensures platform-specific outputs (x64) are handled.
    6. Optionally copies *.dll dependencies if requested.
    7. Optionally builds and copies Servy.Infrastructure.pdb (commented by default).

.PARAMETER ProjectName
    The name of the source project (e.g., "Servy.Service", "Servy.Restarter").

.PARAMETER TargetResourcesFolder
    The absolute or relative path to the destination resources folder.

.PARAMETER Configuration
    The build configuration ("Debug" or "Release").

.PARAMETER Platform
    Target platform. Default is "x64".

.PARAMETER OutputSuffix
    An optional suffix to append to the destination filename after '.Net48' (e.g., 'CLI').

.PARAMETER IncludeDlls
    If specified, copies all *.dll files from the build output to the resources folder.

.REQUIREMENTS
    - MSBuild must be installed and available in PATH.
    - Script must be run under PowerShell (x64 recommended).
    - The .NET 4.8 Developer Pack must be installed.
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectName,

    [Parameter(Mandatory=$true)]
    [string]$TargetResourcesFolder,

    [Parameter(Mandatory=$true)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,

    [string]$Platform = "x64",

    [string]$OutputSuffix = "",

    [switch]$IncludeDlls
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
# Paths and build settings
# ---------------------------------------------------------------------------------
$scriptDir = $PSScriptRoot

# Assumes this script is in setup/, making source projects sibling directories
$sourceDir = Join-Path $scriptDir "..\src\$ProjectName"

# Prevent Resolve-Path errors on clean environments
if (-not (Test-Path $sourceDir)) {
    Write-Error "CRITICAL: Project directory not found at $sourceDir"
    exit 1
}

# Ensure the target resources folder exists, but resolve its full path first
if (-not (Test-Path $TargetResourcesFolder)) {
    Write-Host "Creating missing resources folder: $TargetResourcesFolder" -ForegroundColor Gray
    New-Item -ItemType Directory -Path $TargetResourcesFolder -Force | Out-Null
}

# ---------------------------------------------------------------------------------
# Step 1: Build source project
# ---------------------------------------------------------------------------------
$publishScript = Join-Path $sourceDir "publish.ps1"
if (-not (Test-Path $publishScript)) {
    Write-Error "Required script not found: $publishScript"
    exit 1
}

Write-Host "=== [$ProjectName] Running publish.ps1 ==="
& $publishScript -BuildConfiguration $Configuration
Check-LastExitCode "$publishScript failed"
Write-Host "=== [$ProjectName] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Prepare publish and build folder paths
# ---------------------------------------------------------------------------------
$buildOutput = Join-Path $sourceDir "bin\$Platform\$Configuration"

# ---------------------------------------------------------------------------------
# Step 3: Copy artifacts to Resources folder (with renaming logic)
# ---------------------------------------------------------------------------------
$sourceExe = Join-Path $buildOutput "$ProjectName.exe"
$sourcePdb = Join-Path $buildOutput "$ProjectName.pdb"

# Build the dynamic suffix
$suffix = ".Net48"
if (-not [string]::IsNullOrWhiteSpace($OutputSuffix)) {
    $suffix += ".$OutputSuffix"
}

$destExe = Join-Path $TargetResourcesFolder "$ProjectName$suffix.exe"
$destPdb = Join-Path $TargetResourcesFolder "$ProjectName$suffix.pdb"

Copy-Item -Path $sourceExe -Destination $destExe -Force
Write-Host "Copied $ProjectName.exe -> $destExe"

Copy-Item -Path $sourcePdb -Destination $destPdb -Force
Write-Host "Copied $ProjectName.pdb -> $destPdb"

if ($IncludeDlls) {
    Copy-Item -Path (Join-Path $buildOutput "*.dll") -Destination $TargetResourcesFolder -Force
    Write-Host "Copied *.dll -> $TargetResourcesFolder"
}

# ----------------------------------------------------------------------
# Step 4 - Copy Servy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $scriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $scriptDir "..\Servy.Infrastructure\bin\$Configuration\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $TargetResourcesFolder "Servy.Infrastructure.pdb"

& msbuild $InfraServiceProject /t:Clean,Rebuild /p:Configuration=$Configuration

Copy-Item -Path $InfraSourcePath -Destination $InfraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>

# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $Configuration build (.NET 4.8) published successfully to Resources ==="