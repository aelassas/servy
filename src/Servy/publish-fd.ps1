<#
.SYNOPSIS
Builds Servy for release as a framework-dependent Windows x64 executable.

.DESCRIPTION
This script:
1. Runs publish-res-release.ps1 to generate embedded resources.
2. Cleans old publish output.
3. Publishes Servy.csproj as a framework-dependent app for win-x64.
4. Produces a non-self-contained build suitable for distribution.

.PARAMETER Tfm
Specifies the target framework. Default is "net10.0-windows".

.EXAMPLE
./publish-release.ps1
Publishes using the default target framework.

.EXAMPLE
./publish-release.ps1 -Tfm net10.0-windows
Publishes using .NET target framework.

.NOTES
Author: Akram El Assas
Project: Servy
This script must be run from PowerShell 5+ or PowerShell 7+.
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so the script can be run from anywhere)
# ---------------------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Step 0: Run publish-res-release.ps1 (resource publishing step)
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
# Step 1: Clean and publish Servy.csproj (Framework-dependent, win-x64)
# ---------------------------------------------------------------------------------
$projectPath   = Join-Path $scriptDir "Servy.csproj" | Resolve-Path
$publishFolder = Join-Path $scriptDir "bin\Release\$Tfm\win-x64\publish"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

# Remove old publish output if it exists
if (Test-Path $publishFolder) {
    Write-Host "Removing old publish folder: $publishFolder"
    Remove-Item -Recurse -Force $publishFolder
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : Release"
Write-Host "Runtime          : win-x64"
Write-Host "Self-contained   : false"
Write-Host "Single File      : false"

& dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained false `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    --no-restore `
    --nologo `
    --verbosity minimal `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:ErrorOnDuplicatePublishOutputFiles=true `
    -p:GeneratePackageOnBuild=false `
    -p:UseAppHost=true `
    -p:Clean=true `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.csproj published successfully ==="
