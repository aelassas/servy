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
# Step 1: Build and publish Servy.CLI.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.CLI.csproj" | Resolve-Path

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.CLI.csproj ==="
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $BuildConfiguration"
Write-Host "Runtime          : $Runtime"
Write-Host "Self-contained   : true"
Write-Host "Single File      : true"

& dotnet clean $ProjectPath -c $BuildConfiguration

& dotnet publish $ProjectPath `
    -c $BuildConfiguration `
    -r $Runtime `
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
if ($BuildConfiguration -eq "Release") {
    $BasePath      = Join-Path $ScriptDir "..\Servy.CLI\bin\$BuildConfiguration\$Tfm\$Runtime"
    $PublishFolder = Join-Path $BasePath "publish"
    $ExePath       = Join-Path $PublishFolder "Servy.CLI.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.CLI.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "=== Servy.CLI.csproj published successfully ==="
