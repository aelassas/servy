#requires -Version 5.0
<#
.SYNOPSIS
    Self-contained build and publish script for Servy.Service.

.DESCRIPTION
    This script builds the Servy.Service project following the standard repository 
    build pattern. It publishes to the default bin directory and optionally 
    signs the resulting executable using SignPath.

.PARAMETER Tfm
    Target framework for the build (default: net10.0-windows).

.PARAMETER Runtime
    Runtime identifier for the build (default: win-x64).

.PARAMETER BuildConfiguration
    Build configuration: Debug or Release (default: Release).

.PARAMETER Pause
    Switch to pause execution at the end of the script.
#>

[CmdletBinding()]
param(
    [string]$Tfm                = "net10.0-windows",
    [string]$Runtime            = "win-x64",
    [string]$BuildConfiguration = "Release",
    [switch]$Pause
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
$scriptDir   = $PSScriptRoot
$appName     = "Servy.Service"
$signPath    = Join-Path $scriptDir "..\..\setup\signpath.ps1"
$projectPath = Join-Path $scriptDir "$appName.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "CRITICAL: Project file not found: $projectPath"
    exit 1
}

# ---------------------------------------------------------------------------------
# Step 1: Publish resources first
# ---------------------------------------------------------------------------------
$resSuffix = if ($BuildConfiguration -eq "Debug") { "debug" } else { "release" }
$publishResScriptName = "publish-res-$resSuffix.ps1"
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required resource script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ===" -ForegroundColor Cyan
& $publishResScript -Tfm $Tfm
Check-LastExitCode "$publishResScriptName failed"
# ---------------------------------------------------------------------------------
# Step 2: Clean and Restore
# ---------------------------------------------------------------------------------
Write-Host "=== Preparing $appName ===" -ForegroundColor Cyan

& dotnet restore $projectPath -r $Runtime
Check-LastExitCode "dotnet restore failed"

# Pattern A: Use dotnet toolchain for cleaning instead of manual Remove-Item
& dotnet clean $projectPath -c $BuildConfiguration
Check-LastExitCode "Project clean failed"

# ---------------------------------------------------------------------------------
# Step 3: Build and publish (Pattern A: Default output location)
# ---------------------------------------------------------------------------------
Write-Host "=== Publishing $appName ===" -ForegroundColor Cyan

& dotnet publish $projectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --force `
    /p:DeleteExistingFiles=true
Check-LastExitCode "dotnet publish failed"

# ---------------------------------------------------------------------------------
# Step 4: Sign the published executable (Pattern A: Standard Path)
# ---------------------------------------------------------------------------------
if ($BuildConfiguration -eq "Release" -and (Test-Path $signPath)) {
    Write-Host "=== Signing Artifacts ===" -ForegroundColor Cyan
    
    # Target the default .NET publish directory
    $exePath = Join-Path $scriptDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\$appName.exe"
    
    if (Test-Path $exePath) {
        Write-Host "=== Signing Servy.Service.exe ===" -ForegroundColor Cyan
        & $signPath $exePath
        Check-LastExitCode "Code signing failed"
    }
    else {
        # Critical failure: If the file isn't there, the build is invalid.
        Write-Error "Published executable not found at: $exePath. Ensure the project output name matches 'Servy.Service.exe'."
        exit 1
    }
}

# ---------------------------------------------------------------------------------
# Step 5: Finalize
# ---------------------------------------------------------------------------------
Write-Host "=== $appName published successfully ===" -ForegroundColor Green

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}