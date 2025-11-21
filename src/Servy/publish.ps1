<#
.SYNOPSIS
    Publishes the Servy WPF application as a self-contained executable and signs it.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing script (`publish-res-release.ps1`)
      2. Builds and publishes `Servy.csproj` as a self-contained win-x64 executable
      3. Signs the published executable using SignPath (if enabled)

.PARAMETER tfm
    Target Framework Moniker (default: "net10.0-windows").

.PARAMETER buildConfiguration
    Build configuration to use (default: "Release").

.PARAMETER runtime
    Target runtime identifier (RID) for publishing (default: "win-x64").

.NOTES
    Requirements:
      - .NET SDK must be installed
      - The SignPath script (signpath.ps1) must exist in ../../setup/

.EXAMPLE
    ./publish.ps1
    Publishes using default parameters.

.EXAMPLE
    ./publish.ps1 -tfm "net8.0-windows" -buildConfiguration "Debug" -runtime "win-x64"
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$tfm                = "net10.0-windows",
    [string]$buildConfiguration = "Release",
    [string]$runtime            = "win-x64"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$SignPath = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ---------------------------------------------------------------------------------
# Step 0: Publish resources
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($buildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$PublishResScript = Join-Path $ScriptDir $publishResScriptName

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $PublishResScript -tfm $tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.csproj" | Resolve-Path

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework: $tfm"
Write-Host "Configuration: $buildConfiguration"
Write-Host "Runtime: $runtime"
Write-Host "Self-contained: true"

& dotnet clean $ProjectPath -c $buildConfiguration

& dotnet publish $ProjectPath `
    -c $buildConfiguration `
    -r $runtime `
    --self-contained true `
    --force `
    /p:DeleteExistingFiles=true `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 2: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
$basePath      = Join-Path $ScriptDir "..\Servy\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$exePath       = Join-Path $publishFolder "Servy.exe"
& $SignPath $exePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.exe failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.csproj published successfully ==="
