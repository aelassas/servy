#requires -Version 5.0
<#
.SYNOPSIS
    Publishes a Servy project and copies its build artifacts into a target Resources folder.

.DESCRIPTION
    Consolidated DRY script. This script:
    1. Runs the publish.ps1 script inside the specified source project.
    2. Locates the produced build/publish output folders.
    3. Copies the generated single-file executable and PDB files into the specified
       target Resources directory so they can be embedded in Servy builds.
    4. Optionally appends an additional suffix (e.g., '.CLI') to the output files.

.PARAMETER ProjectName
    The name of the source project (e.g., "Servy.Service", "Servy.Restarter").

.PARAMETER TargetResourcesFolder
    The absolute or relative path to the destination resources folder.

.PARAMETER Configuration
    The build configuration ("Debug" or "Release").

.PARAMETER Tfm
    Target framework moniker. Default: net10.0-windows.

.PARAMETER Runtime
    Target runtime. Default: win-x64.

.PARAMETER OutputSuffix
    An optional suffix to append to the destination filename (e.g., 'CLI').
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectName,

    [Parameter(Mandatory=$true)]
    [string]$TargetResourcesFolder,

    [Parameter(Mandatory=$true)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,

    [string]$Tfm = "net10.0-windows",
    [string]$Runtime = "win-x64",

    [string]$OutputSuffix = ""
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
# Step 1: Publish source project
# ---------------------------------------------------------------------------------
$publishScript = Join-Path $sourceDir "publish.ps1"
if (-not (Test-Path $publishScript)) {
    Write-Error "Required script not found: $publishScript"
    exit 1
}

Write-Host "=== [$ProjectName] Running publish.ps1 ==="
& $publishScript -Tfm $Tfm -Runtime $Runtime -BuildConfiguration $Configuration
Check-LastExitCode "$publishScript failed"
Write-Host "=== [$ProjectName] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Prepare publish and build folder paths
# ---------------------------------------------------------------------------------
$basePath      = Join-Path $sourceDir "bin\$Configuration\$Tfm\$Runtime"
$publishFolder = Join-Path $basePath "publish"
$buildFolder   = $basePath

# ---------------------------------------------------------------------------------
# Step 3: Copy artifacts to Resources folder (with renaming logic)
# ---------------------------------------------------------------------------------
$sourceExe = "$ProjectName.exe"
$sourcePdb = "$ProjectName.pdb"

# Build the dynamic suffix
$suffix = ""
if (-not [string]::IsNullOrWhiteSpace($OutputSuffix)) {
    $suffix = ".$OutputSuffix"
}

$destExe = "$ProjectName$suffix.exe"
$destPdb = "$ProjectName$suffix.pdb"

Copy-Item -Path (Join-Path $publishFolder $sourceExe) -Destination (Join-Path $TargetResourcesFolder $destExe) -Force
Write-Host "Copied $sourceExe -> $destExe"

Copy-Item -Path (Join-Path $buildFolder $sourcePdb) -Destination (Join-Path $TargetResourcesFolder $destPdb) -Force
Write-Host "Copied $sourcePdb -> $destPdb"

<#
Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $TargetResourcesFolder "Servy.Core.pdb") -Force
#>

# ----------------------------------------------------------------------
# Step 4 - Copy Servy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $scriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $scriptDir "..\Servy.Infrastructure\bin\$Configuration\$Tfm\$Runtime\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $TargetResourcesFolder "Servy.Infrastructure.pdb"

dotnet publish $InfraServiceProject `
    -c $Configuration `
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
Write-Host "=== $Configuration build ($Tfm) published successfully to Resources ==="