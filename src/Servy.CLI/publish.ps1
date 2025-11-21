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

.PARAMETER tfm
    Target Framework Moniker to publish for.
    Default: net10.0-windows.

.PARAMETER buildConfiguration
    Build configuration to use.
    Default: Release.

.PARAMETER runtime
    Target runtime identifier (RID) for publishing.
    Default: win-x64.

.EXAMPLE
    ./publish.ps1
    Runs the script using the default TFM (net10.0-windows).

.EXAMPLE
    ./publish.ps1 -tfm net9.0-windows
    Publishes the CLI for .NET 9.

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
    [string]$tfm                = "net10.0-windows",
    [string]$buildConfiguration = "Release",
    [string]$runtime            = "win-x64"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so it works regardless of the current working directory)
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
# Step 1: Build and publish Servy.CLI.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.CLI.csproj" | Resolve-Path

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.CLI.csproj ==="
Write-Host "Target Framework : $tfm"
Write-Host "Configuration    : $buildConfiguration"
Write-Host "Runtime          : $runtime"
Write-Host "Self-contained   : true"
Write-Host "Single File      : true"

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
$basePath      = Join-Path $ScriptDir "..\Servy.CLI\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$exePath       = Join-Path $publishFolder "Servy.CLI.exe" | Resolve-Path
& $SignPath $exePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.CLI.exe failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.CLI.csproj published successfully ==="
