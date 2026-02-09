<#
.SYNOPSIS
    Builds and publishes the Servy.CLI application (Release, self-contained) and 
    optionally signs the output executable using SignPath.

.DESCRIPTION
    This script performs the following steps:
      1. Runs publish-res-release.ps1 to ensure shared resources are generated.
      2. Builds and publishes Servy.CLI as a self-contained, single-file executable 
         for win-x64.
      3. Signs the generated executable via the SignPath signing pipeline.
      4. Emits standard build/progress messages used across Servy build tooling.

    This is used as part of the Release build pipeline to produce final CLI artifacts.

.PARAMETER Tfm
    Target Framework Moniker to publish for.
    Default: net10.0-windows.

.PARAMETER BuildConfiguration
    Build configuration to use.
    Default: Release.

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing.
    Default: win-x64.

.EXAMPLE
    ./publish.ps1
    Runs the script using the default TFM (net10.0-windows).

.EXAMPLE
    ./publish.ps1 -Tfm net10.0-windows
    Publishes the CLI for .NET target framework.

.NOTES
    Author : Akram El Assas
    Project: Servy
    Requirements:
        - .NET SDK installed
        - SignPath setup
        - Valid folder structure
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so it works regardless of the current working directory)
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
# Step 1: Build and publish Servy.CLI.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$projectPath = Join-Path $scriptDir "Servy.CLI.csproj" | Resolve-Path

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "=== Publishing Servy.CLI.csproj ==="
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $BuildConfiguration"
Write-Host "Runtime          : $Runtime"

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
    $basePath      = Join-Path $scriptDir "..\Servy.CLI\bin\$BuildConfiguration\$Tfm\$Runtime"
    $publishFolder = Join-Path $basePath "publish"
    $exePath       = Join-Path $publishFolder "Servy.CLI.exe" | Resolve-Path
    & $signPath $exePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.CLI.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "=== Servy.CLI.csproj published successfully ==="
