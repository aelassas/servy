#requires -Version 5.0
<#
.SYNOPSIS
    Builds the Servy self-contained installer and portable ZIP package.

.DESCRIPTION
    This script compiles all Servy applications (WPF, CLI, Manager) as self-contained,
    builds the Inno Setup installer, signs the generated installer,
    and generates a portable 7z package containing the published executables.

.PARAMETER Tfm
    Target framework moniker prefix (e.g., "net10.0").

.PARAMETER Version
    Application version used for installer and ZIP output file names.

.PARAMETER Pause
    Optional switch that pauses the script before exiting.

.NOTES
    Requirements:
      - .NET SDK
      - Inno Setup (ISCC.exe)
      - 7-Zip (7z.exe)
      - setup/signpath.ps1
#>
[CmdletBinding()]
param(
    [string]$Tfm      = "net10.0-windows",
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version = "1.0",
    [switch]$Pause
)
$ErrorActionPreference = "Stop"
$BuildConfiguration = "Release"
$Runtime = "win-x64"

# ========================
# Configuration
# ========================
$scriptDir = $PSScriptRoot
# Resolve the root once at the start (this is safe as the script is running inside it)
$rootDir   = (Resolve-Path (Join-Path $scriptDir "..")).Path
$servyDir   = Join-Path $rootDir "src\Servy"
$cliDir     = Join-Path $rootDir "src\Servy.CLI"
$managerDir = Join-Path $rootDir "src\Servy.Manager"

# Define paths as strings. Do not resolve them yet.
$signPath      = Join-Path $rootDir "setup\signpath.ps1"
$issFile       = Join-Path $scriptDir "servy.iss"
$packageFolder = Join-Path $scriptDir "servy-$Version-x64-portable"
$outputZip     = "$packageFolder.7z"
$installerPath = Join-Path $rootDir "setup\servy-$Version-x64-installer.exe"

# ---------------------------------------------------------
# Tool Discovery & Initialization
# ---------------------------------------------------------
try {
    # Import the resolution helper
    . (Join-Path $scriptDir "tools-config.ps1")
    # Import the newly extracted common functions
    . (Join-Path $scriptDir "publish-common.ps1")

    Write-Host "Resolving build tools..." -ForegroundColor Cyan

    $innoCompiler = Resolve-Tool -Name "ISCC" -Fallbacks @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    )

    $sevenZipExe  = Resolve-Tool -Name "7z" -Fallbacks @(
        "C:\Program Files\7-Zip\7z.exe",
        "C:\Program Files (x86)\7-Zip\7z.exe"
    )
    
    Write-Host "Tools resolved successfully." -ForegroundColor Green
}
catch {
    Write-Error "Configuration Failed: $($_.Exception.Message)"
    exit 1
}

# ========================
# Step 1: Build Applications
# ========================
# Use the call operator and explicit TFM to ensure consistency
$projects = @($servyDir, $cliDir, $managerDir)
foreach ($project in $projects) {
    $projectName = Split-Path $project -Leaf
    Write-Host "--- Publishing $projectName ---" -ForegroundColor Cyan
    
    $publishScript = Join-Path $project "publish.ps1"
    if (Test-Path $publishScript) {
        & $publishScript -BuildConfiguration $BuildConfiguration -Tfm $Tfm
        Check-LastExitCode "$publishScript failed"
    }
    else {
        Write-Warning "Publish script not found for $projectName. Using generic dotnet publish."
        & dotnet restore $project
        Check-LastExitCode "dotnet restore failed"
        & dotnet clean $project -c $BuildConfiguration
        Check-LastExitCode "Project clean failed"
        & dotnet publish $project -c $BuildConfiguration -f $Tfm -r $Runtime --self-contained true
        Check-LastExitCode "dotnet publish failed"
    }
}

# ========================
# Step 2: Build & Sign Installer
# ========================
Remove-ItemSafely -Path $installerPath
Build-Installer -InnoCompiler $innoCompiler -IssFile $issFile -Version $Version

# Validate the installer exists before attempting to sign
if (Test-Path $installerPath) {
    # Resolve the absolute path for the signer
    $resolvedInstaller = (Resolve-Path $installerPath).Path
    
    # Validate the signing script exists before trying to execute it
    if (Test-Path $signPath) {
        $resolvedSigner = (Resolve-Path $signPath).Path
        Write-Host "--- Signing Artifacts ---" -ForegroundColor Cyan
        & $resolvedSigner -Path $resolvedInstaller
        Check-LastExitCode "Signing artifacts failed"
    } else {
        Write-Warning "Signing script not found at $signPath. Installer will remain unsigned."
    }
} else {
    Write-Error "Installer executable not found at $installerPath after Inno Setup build."
    exit 1
}

# ========================
# Step 3: Build Portable Package
# ========================
Write-Host "--- Packaging Portable ZIP ---" -ForegroundColor Cyan

Remove-ItemSafely -Path $outputZip
Remove-ItemSafely -Path $packageFolder

try {
    [void](New-Item -ItemType Directory -Path $packageFolder)

    # 1. Consolidate binaries
    $binaries = @{
        "Servy.exe"         = Join-Path $servyDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.exe"
        "servy-cli.exe"     = Join-Path $cliDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.CLI.exe"
        "Servy.Manager.exe" = Join-Path $managerDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.Manager.exe"
    }

    foreach ($item in $binaries.GetEnumerator()) {
        if (Test-Path $item.Value) {
            Copy-Item -Path $item.Value -Destination (Join-Path $packageFolder $item.Name) -Force
        } else {
            throw "Critical binary missing: $($item.Value)"
        }
    }

    Copy-CommonArtifacts -ScriptDir $scriptDir -CliDir $cliDir -DestFolder $packageFolder
    New-PortablePackage -SevenZipExe $sevenZipExe -OutputZip $outputZip -PackageFolder $packageFolder
}
catch {
    Write-Error "Packaging failed: $_"
    exit 1
}
finally {
    # ALWAYS clean up the temporary workspace folder, even on failure
    Remove-ItemSafely -Path $packageFolder
}

if ($Pause) {
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}