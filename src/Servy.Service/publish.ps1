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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$SignPath      = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$AppName       = "Servy.Service"

# Project path (relative to script location)
$ProjectPath = Join-Path $ScriptDir "$AppName.csproj" | Resolve-Path

# Output folder
$PublishDir = Join-Path $ScriptDir "bin\$Configuration\$Tfm\$Runtime\publish"

# ---------------------------------------------------------------------------------
# Step 1:Publish resources first
# ---------------------------------------------------------------------------------
$PublishResScriptName = if ($Configuration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$PublishResScript = Join-Path $ScriptDir $PublishResScriptName

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running $PublishResScriptName ==="
& $PublishResScript -Tfm $Tfm -Runtime $Runtime -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "$PublishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $PublishResScriptName ===`n"

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
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $Configuration"
Write-Host "Runtime          : $Runtime"

& dotnet restore $ProjectPath -r $Runtime

& dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    -o $PublishDir `
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
    $ExePath = Join-Path $PublishDir "Servy.Service.exe" | Resolve-Path
    & $SignPath $ExePath

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

Write-Host "=== $AppName published successfully to $PublishDir ==="