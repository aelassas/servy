<#
.SYNOPSIS
    Publishes the Servy.CLI project (framework-dependent) for Windows.

.DESCRIPTION
    Standardized Pattern A script. This script:
    1. Runs the resource publishing script.
    2. Cleans and publishes Servy.CLI.csproj without debug symbols.
    3. Produces a framework-dependent build targeting win-x64.
#>

[CmdletBinding()]
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Step 0: Setup variables
# ---------------------------------------------------------------------------------
$scriptDir = $PSScriptRoot
$projectPath = Join-Path $scriptDir "Servy.CLI.csproj"

# ---------------------------------------------------------------------------------
# Step 1: Run resource publishing script
# ---------------------------------------------------------------------------------
$resSuffix = if ($BuildConfiguration -eq "Debug") { "debug" } else { "release" }
$publishResScriptName = "publish-res-$resSuffix.ps1"
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
}

Write-Host "=== Running $publishResScriptName ===" -ForegroundColor Cyan
& $publishResScript -Tfm $Tfm

# ---------------------------------------------------------------------------------
# Step 2: Clean and Publish (Pattern A: Default output location)
# ---------------------------------------------------------------------------------
Write-Host "=== Publishing Servy.CLI.csproj ===" -ForegroundColor Cyan
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $BuildConfiguration"

# Pattern A: Perform restore and clean via dotnet toolchain
& dotnet restore $projectPath -r win-x64

& dotnet clean $projectPath -c $BuildConfiguration

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r win-x64 `
    --self-contained false `
    --no-restore `
    --nologo `
    --verbosity minimal `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    /p:CopyOutputSymbolsToPublishDirectory=false `
    /p:CopyCommandLineArguments=false `
    /p:ErrorOnDuplicatePublishOutputFiles=true `
    /p:UseAppHost=true `
    /p:Clean=true `
    /p:DeleteExistingFiles=true

Write-Host "=== Servy.CLI.csproj published successfully ===" -ForegroundColor Green