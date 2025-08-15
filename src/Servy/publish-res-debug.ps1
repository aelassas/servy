param(
    # Target framework (default: net8.0-windows)
    [string]$tfm = "net8.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work no matter where script is run)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths and build settings
# ---------------------------------------------------------------------------------
$serviceDir         = Join-Path $ScriptDir  "..\Servy.Service"
$serviceProject     = Join-Path $serviceDir "Servy.Service.csproj"
$resourcesFolder    = Join-Path $ScriptDir "..\Servy\Resources"
$buildConfiguration = "Debug"
$runtime            = "win-x64"
$selfContained      = $true

# ---------------------------------------------------------------------------------
# Step 0: Run publish-res-release.ps1 (publish resources first)
# ---------------------------------------------------------------------------------
$PublishResScript = Join-Path $serviceDir "publish-res-debug.ps1"

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running publish-res-release.ps1 ==="
& $PublishResScript -tfm $tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "publish-res-release.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed publish-res-release.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Service project
# ---------------------------------------------------------------------------------
if (-not (Test-Path $serviceProject)) {
    Write-Error "Project file not found: $serviceProject"
    exit 1
}

Write-Host "=== Publishing Servy.Service ==="
Write-Host "Target Framework : $tfm"
Write-Host "Configuration    : $buildConfiguration"
Write-Host "Runtime          : $runtime"
Write-Host "Self-contained   : $selfContained"
Write-Host "Single File      : true"

dotnet publish $serviceProject `
    -c $buildConfiguration `
    -r $runtime `
    --self-contained $selfContained `
    /p:TargetFramework=$tfm `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 2: Prepare publish and build folder paths
# ---------------------------------------------------------------------------------
$basePath      = Join-Path $ScriptDir "..\Servy.Service\bin\$buildConfiguration\$tfm\$runtime"
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
