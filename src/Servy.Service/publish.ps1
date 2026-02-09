<#
.SYNOPSIS
    Self-contained build and publish script for Servy.Service.

.DESCRIPTION
    This script builds the Servy.Service project as a self-contained single-file executable
    for the specified target framework and runtime. It also runs the appropriate
    publish-res script to include necessary resources and optionally signs the
    resulting executable using SignPath.

.PARAMETER Tfm
    Target framework for the build (default: net10.0-windows).

.PARAMETER Runtime
    Runtime identifier for the build (default: win-x64).

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release).

.PARAMETER Pause
    Switch to pause execution at the end of the script.

.REQUIREMENTS
    - .NET SDK must be installed
    - SignPath.ps1 script available in ..\..\setup\ for signing.

.EXAMPLE
    # Build Servy.Service in Release mode for net10.0-windows
    .\publish.ps1

.EXAMPLE
    # Build Servy.Service in Debug mode and pause after completion
    .\publish.ps1 -Configuration Debug -Pause

.NOTES
    Author: Akram El Assas
#>

param(
    [string]$Tfm           = "net10.0-windows",
    [string]$Runtime       = "win-x64",
    [string]$Configuration = "Release",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Step 0: Setup variables
# ---------------------------------------------------------------------------------
# Script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$signPath      = Join-Path $scriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$appName       = "Servy.Service"

# Project path (relative to script location)
$projectPath = Join-Path $scriptDir "$appName.csproj" | Resolve-Path

# Output folder
$publishDir = Join-Path $scriptDir "bin\$Configuration\$Tfm\$Runtime\publish"

# ---------------------------------------------------------------------------------
# Step 1:Publish resources first
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($Configuration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $publishResScript -Tfm $Tfm -Runtime $Runtime -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Clean previous build artifacts
# ---------------------------------------------------------------------------------
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

# ---------------------------------------------------------------------------------
# Step 3: Build and publish
# ---------------------------------------------------------------------------------
if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "=== Publishing $appName ==="
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $Configuration"
Write-Host "Runtime          : $Runtime"

& dotnet restore $projectPath -r $Runtime

& dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -o $publishDir `
    --force `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 4: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
if ($Configuration -eq "Release") {
    $exePath = Join-Path $publishDir "Servy.Service.exe" | Resolve-Path
    & $signPath $exePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.Service.exe failed."
        exit $LASTEXITCODE
    }
}

# ---------------------------------------------------------------------------------
# Step 5: Pause (optional)
# ---------------------------------------------------------------------------------
if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

Write-Host "=== $appName published successfully to $publishDir ==="