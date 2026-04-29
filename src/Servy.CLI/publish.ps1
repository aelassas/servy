#requires -Version 5.0
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

function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# ---------------------------------------------------------------------------------
# Script directory (so it works regardless of the current working directory)
# ---------------------------------------------------------------------------------
$scriptDir = $PSScriptRoot

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$signPath = Join-Path $scriptDir "..\..\setup\signpath.ps1"
if (-not (Test-Path $signPath)) {
    Write-Warning "SignPath script not found at: $signPath. Signing will be skipped."
}

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
Check-LastExitCode "$publishResScriptName failed"
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.CLI.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$projectPath = Join-Path $scriptDir "Servy.CLI.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "=== Publishing Servy.CLI.csproj ==="
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $BuildConfiguration"
Write-Host "Runtime          : $Runtime"

& dotnet restore $projectPath -r $Runtime
Check-LastExitCode "dotnet restore failed"

& dotnet clean $projectPath -c $BuildConfiguration
Check-LastExitCode "Project clean failed"

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --force `
    /p:DeleteExistingFiles=true
Check-LastExitCode "dotnet publish failed"    


# ---------------------------------------------------------------------------------
# Step 2: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $publishFolder = Join-Path $scriptDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
    $exePath = Join-Path $publishFolder "Servy.CLI.exe"

    if (Test-Path $exePath) {
        Write-Host "=== Signing Servy.CLI.exe ===" -ForegroundColor Cyan
        & $signPath $exePath
        Check-LastExitCode "Code signing failed"
    }
    else {
        # Critical failure: If the file isn't there, the build is invalid.
        Write-Error "Published executable not found at: $exePath. Ensure the project output name matches 'Servy.CLI.exe'."
        exit 1
    }
}

Write-Host "=== Servy.CLI.csproj published successfully ==="
