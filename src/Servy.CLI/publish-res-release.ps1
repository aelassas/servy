param(
    # Target framework for build (default: net8.0-windows)
    [string]$tfm = "net8.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths & build configuration
# ---------------------------------------------------------------------------------
$serviceProject     = Join-Path $ScriptDir "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder    = Join-Path $ScriptDir "..\Servy.CLI\Resources"
$buildConfiguration = "Release"
$runtime            = "win-x64"
$selfContained      = $true

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

Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Core.pdb") -Force

# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $buildConfiguration build ($tfm) published successfully to Resources ==="
