<#
.SYNOPSIS
    Self-contained build and publish script for Servy.Manager.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing step via publish-res-release.ps1.
      2. Builds and publishes the Servy.Manager project as a
         self-contained, single-file executable for the specified
         target framework and runtime.
      3. Optionally signs the resulting executable using SignPath.

.PARAMETER Tfm
    Target framework for the build.
    Default: net10.0-windows.

.PARAMETER BuildConfiguration
    Build configuration to use.
    Default: Release.

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing.
    Default: win-x64.

.REQUIREMENTS
    - .NET SDK installed and accessible in PATH.
    - SignPath.ps1 script available in ..\..\setup\ for signing.

.EXAMPLE
    .\publish.ps1
    Builds Servy.Manager in Release mode with default settings.

.NOTES
    Author: Akram El Assas
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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$SignPath = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ---------------------------------------------------------------------------------
# Step 0: Publish resources
# ---------------------------------------------------------------------------------
$PublishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$PublishResScript = Join-Path $ScriptDir $PublishResScriptName

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running $PublishResScriptName ==="
& $PublishResScript -Tfm $Tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "$PublishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $PublishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.Manager.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.Manager.csproj" | Resolve-Path

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.Manager.csproj ==="
Write-Host "Target Framework: $Tfm"
Write-Host "Configuration: $BuildConfiguration"
Write-Host "Runtime: $Runtime"

& dotnet restore $ProjectPath -r $Runtime

& dotnet clean $ProjectPath -c $BuildConfiguration

& dotnet publish $ProjectPath `
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
    $BasePath      = Join-Path $ScriptDir "..\Servy.Manager\bin\$BuildConfiguration\$Tfm\$Runtime"
    $PublishFolder = Join-Path $BasePath "publish"
    $ExePath       = Join-Path $PublishFolder "Servy.Manager.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.Manager.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "=== Servy.Manager.csproj published successfully ==="
