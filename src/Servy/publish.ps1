<#
.SYNOPSIS
    Publishes the Servy WPF application as a self-contained executable and signs it.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing script (`publish-res-release.ps1`)
      2. Builds and publishes `Servy.csproj` as a self-contained win-x64 executable
      3. Signs the published executable using SignPath (if enabled)

.PARAMETER Tfm
    Target Framework Moniker (default: "net10.0-windows").

.PARAMETER BuildConfiguration
    Build configuration to use (default: "Release").

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing (default: "win-x64").

.NOTES
    Requirements:
      - .NET SDK must be installed
      - The SignPath script (signpath.ps1) must exist in ../../setup/

.EXAMPLE
    ./publish.ps1
    Publishes using default parameters.

.EXAMPLE
    ./publish.ps1 -Tfm "net10.0-windows" -BuildConfiguration "Debug" -Runtime "win-x64"
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$signPath = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ---------------------------------------------------------------------------------
# Step 0: Publish resources
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $publishResScript -Tfm $Tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$projectPath = Join-Path $scriptDir "Servy.csproj" | Resolve-Path

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework: $Tfm"
Write-Host "Configuration: $BuildConfiguration"
Write-Host "Runtime: $Runtime"
Write-Host "Self-contained: true"

& dotnet restore $projectPath -r $Runtime

& dotnet clean $projectPath -c $BuildConfiguration

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --force `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 2: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $basePath      = Join-Path $scriptDir "..\Servy\bin\$BuildConfiguration\$Tfm\$Runtime"
    $publishFolder = Join-Path $basePath "publish"
    $exePath       = Join-Path $publishFolder "Servy.exe" | Resolve-Path
    & $signPath $exePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "=== Servy.csproj published successfully ==="
