# publish-sc.ps1 - Self-contained build script for Servy.Service
# Requirements:
#   - .NET SDK installed
#   - msbuild in PATH (if needed for other steps)

param(
    [string]$tfm     = "net10.0-windows",
    [switch]$pause
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Step 0: Setup variables
# ---------------------------------------------------------------------------------
# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$signPath      = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$AppName       = "Servy.Service"
$Runtime       = "win-x64"
$SelfContained = $true
$SingleFile    = $true
$Configuration = "Release"

# Project path (relative to script location)
$ProjectPath = Join-Path $ScriptDir "$AppName.csproj"

# Output folder
$PublishDir = Join-Path $ScriptDir "bin\$Configuration\$tfm\$Runtime\publish"

# ---------------------------------------------------------------------------------
# Step 1: Run publish-res-release.ps1 (publish resources first)
# ---------------------------------------------------------------------------------
$PublishResScript = Join-Path $ScriptDir "publish-res-release.ps1"

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
# Step 2: Clean previous build artifacts
# ---------------------------------------------------------------------------------
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# ---------------------------------------------------------------------------------
# Step 3: Build and publish
# ---------------------------------------------------------------------------------
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing $AppName ==="
Write-Host "Target Framework : $tfm"
Write-Host "Configuration    : $Configuration"
Write-Host "Runtime          : $Runtime"
Write-Host "Self-contained   : $SelfContained"
Write-Host "Single File      : $SingleFile"

& dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$SelfContained `
    /p:PublishSingleFile=$SingleFile `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    -o $PublishDir `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 4: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
$exePath = Join-Path $PublishDir "Servy.Service.exe"
& $signPath $exePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.Service.exe failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 5: Pause (optional)
# ---------------------------------------------------------------------------------
if ($pause) {
    Pause
}

Write-Host "=== $AppName published successfully to $PublishDir ==="