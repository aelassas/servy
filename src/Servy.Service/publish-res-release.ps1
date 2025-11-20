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
$restarterDir     = Join-Path $ScriptDir "..\Servy.Restarter" | Resolve-Path
$resourcesFolder    = Join-Path $ScriptDir "..\Servy.Service\Resources" | Resolve-Path
$buildConfiguration = "Release"
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
& $PublishRestarterScript -tfm $tfm
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
# Step 5: Copy artifacts (renaming as needed)
# ---------------------------------------------------------------------------------

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.Restarter.exe failed."
    exit $LASTEXITCODE
}

Copy-Item -Path (Join-Path $publishFolder "Servy.Restarter.exe") `
          -Destination (Join-Path $resourcesFolder "Servy.Restarter.exe") -Force

Copy-Item -Path (Join-Path $buildFolder "Servy.Restarter.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Restarter.pdb") -Force
<#
Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Core.pdb") -Force
#>
# ----------------------------------------------------------------------
# Step 6 - CopyServy.Infrastructure.pdb
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
