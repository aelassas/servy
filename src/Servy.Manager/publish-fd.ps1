<#
.SYNOPSIS
    Builds and publishes Servy.Manager as a framework-dependent application.

.DESCRIPTION
    Standardized Pattern A script. Cleans and publishes Servy.Manager.csproj 
    without debug symbols to ensure a clean production build.
#>

[CmdletBinding()]
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release"
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
# Step 0: Setup variables
# ---------------------------------------------------------------------------------
$scriptDir = $PSScriptRoot
$projectPath = Join-Path $scriptDir "Servy.Manager.csproj"

# ---------------------------------------------------------------------------------
# Step 1: Run resource publishing step
# ---------------------------------------------------------------------------------
$resSuffix = if ($BuildConfiguration -eq "Debug") { "debug" } else { "release" }
$publishResScriptName = "publish-res-$resSuffix.ps1"
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ===" -ForegroundColor Cyan
& $publishResScript -Tfm $Tfm
Check-LastExitCode "$publishResScriptName failed"

# ---------------------------------------------------------------------------------
# Step 2: Clean and Publish (Pattern A: Default output location)
# ---------------------------------------------------------------------------------
Write-Host "=== Publishing Servy.Manager.csproj ===" -ForegroundColor Cyan
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : $BuildConfiguration"

& dotnet restore $projectPath -r win-x64
Check-LastExitCode "dotnet restore failed"

# Use dotnet toolchain for cleaning instead of manual Remove-Item
& dotnet clean $projectPath -c $BuildConfiguration
Check-LastExitCode "Project clean failed"

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
Check-LastExitCode "dotnet publish failed"

Write-Host "=== Servy.Manager.csproj published successfully ===" -ForegroundColor Green