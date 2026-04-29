#requires -Version 5.0
<#
.SYNOPSIS
    Builds the framework-dependent Servy installer and ZIP package.

.DESCRIPTION
    This script compiles the Servy WPF, CLI, and Manager applications in
    framework-dependent mode, builds the Inno Setup installer, and creates a
    compressed 7z package containing all published framework-dependent builds.

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
#>
[CmdletBinding()]
param(
    [string]$Tfm = "net10.0-windows",
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version = "8.4",
    [switch]$Pause
)
$ErrorActionPreference = "Stop"
$BuildConfiguration = "Release"
$Runtime = "win-x64"

# ========================
# Configuration
# ========================
# Directories
$scriptDir = $PSScriptRoot
$rootDir   = (Resolve-Path (Join-Path $scriptDir "..")).Path
$servyDir   = Join-Path $rootDir "src\Servy"
$cliDir     = Join-Path $rootDir "src\Servy.CLI"
$managerDir = Join-Path $rootDir "src\Servy.Manager"

# Keep as strings to avoid Resolve-Path crashes on clean builds
$issFile       = Join-Path $scriptDir "servy-fd.iss"
$packageFolder = Join-Path $scriptDir "servy-$Version-x64-frameworkdependent"
$outputZip     = "$packageFolder.7z"
$installerPath = Join-Path $rootDir "setup\servy-$Version-x64-installer-fd.exe"

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
$projects = @($servyDir, $cliDir, $managerDir)
foreach ($project in $projects) {
    $projectName = Split-Path $project -Leaf
    Write-Host "--- Publishing $projectName (Framework-Dependent) ---" -ForegroundColor Cyan
    
    $publishScript = Join-Path $project "publish-fd.ps1"
    if (Test-Path $publishScript) {
        & $publishScript -BuildConfiguration $BuildConfiguration -Tfm $Tfm
        Check-LastExitCode "$publishScript failed"
    }
    else {
        Write-Warning "Specific FD script missing for $projectName. Using dotnet publish."
        & dotnet restore $project
        Check-LastExitCode "dotnet restore failed"

        & dotnet clean $project -c $BuildConfiguration
        Check-LastExitCode "Project clean failed"
        
        & dotnet publish $project `
            -c $BuildConfiguration `
            -f $Tfm `
            --no-self-contained `
            -p:CopyOutputSymbolsToPublishDirectory=false `
            -p:DebugType=none
        Check-LastExitCode "dotnet publish failed"
    }
}

# ========================
# Step 2: Build Installer
# ========================
Remove-ItemSafely -Path $installerPath
Build-Installer -InnoCompiler $innoCompiler -IssFile $issFile -Version $Version

# Optional: Add signing check here if we use it for FD builds as well
# if (Test-Path $installerPath) { ... }

# ========================
# Step 3: Prepare ZIP package
# ========================
Write-Host "--- Packaging FD ZIP ---" -ForegroundColor Cyan

Remove-ItemSafely -Path $outputZip
Remove-ItemSafely -Path $packageFolder

try {
    [void](New-Item -ItemType Directory -Path $packageFolder)

    # 1. Consolidate binaries into subfolders
    $subFolders = @{
        "servy-app"     = Join-Path $servyDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
        "servy-cli"     = Join-Path $cliDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
        "servy-manager" = Join-Path $managerDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
    }

    foreach ($entry in $subFolders.GetEnumerator()) {
        $source = $entry.Value
        $dest = Join-Path $packageFolder $entry.Key
        
        if (Test-Path $source) {
            Write-Host "Copying $($entry.Key)..."
            [void](New-Item -ItemType Directory -Path $dest -Force)
            Copy-Item -Path "$source\*" -Destination $dest -Recurse -Force
        } else {
            throw "Critical publish directory missing: $source. Ensure Step 1 succeeded."
        }
    }

    $cliExe = Join-Path $packageFolder "servy-cli\Servy.CLI.exe"
    if (Test-Path $cliExe) {
        Rename-Item -Path $cliExe -NewName "servy-cli.exe" -Force
    }

    Copy-CommonArtifacts -ScriptDir $scriptDir -CliDir $cliDir -DestFolder $packageFolder
    
    # ========================
    # Step 4: Create ZIP
    # ========================
    New-PortablePackage -SevenZipExe $sevenZipExe -OutputZip $outputZip -PackageFolder $packageFolder
}
catch {
    Write-Error "Build failed at Step 3/4: $_"
    exit 1
}
finally {
    # Ensure no partial/dirty folders remain on disk
    Remove-ItemSafely -Path $packageFolder
}

if ($Pause) {
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}