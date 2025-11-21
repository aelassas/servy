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

.PARAMETER tfm
    Target framework for the build.
    Default: net10.0-windows.

.PARAMETER buildConfiguration
    Build configuration to use.
    Default: Release.

.PARAMETER runtime
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
# Step 0: Run publish-res-release.ps1 (Resource publishing step)
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
# Step 1: Build and publish Servy.Manager.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.Manager.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.Manager.csproj ==="
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
$basePath      = Join-Path $ScriptDir "..\Servy.Manager\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$exePath       = Join-Path $publishFolder "Servy.Manager.exe"
& $SignPath $exePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.Manager.exe failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.Manager.csproj published successfully ==="
