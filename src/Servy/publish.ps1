param(
    # Target framework (default: net8.0-windows)
    [string]$tfm = "net8.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Step 0: Run publish-res-release.ps1 (Resource publishing step)
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
# Step 1: Build and publish Servy.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework: $tfm"
Write-Host "Configuration: Release"
Write-Host "Runtime: win-x64"
Write-Host "Self-contained: true"

& dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.csproj published successfully ==="
