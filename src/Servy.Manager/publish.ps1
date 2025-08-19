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
# Step 0: Build and publish Servy.Manager.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.Manager.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.Manager.csproj ==="
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
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.Manager.csproj published successfully ==="
