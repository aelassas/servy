# publish.ps1
# Main setup bundle script for .NET Framework build of Servy
# Requirements:
#  1. Add msbuild and nuget.exe to PATH
#  2. Inno Setup installed (ISCC.exe path updated if different)
#  3. 7-Zip installed and 7z in PATH

$ErrorActionPreference = "Stop"

# Record start time
$startTime = Get-Date

# === CONFIGURATION ===
$version      = "1.6"
$AppName      = "servy"
$BuildConfig  = "Release"
$Platform     = "x64"
$Framework    = "net48"

# Tools
$innoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$issFile      = "servy.iss"   # Inno Setup script filename
$SevenZipExe  = "7z"          # Assumes 7-Zip is in PATH

# === PATH RESOLUTION ===

# Directories
$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir            = (Resolve-Path (Join-Path $ScriptDir "..")).Path
$ServyDir           = Join-Path $RootDir "src\Servy"
$CliDir             = Join-Path $RootDir "src\Servy.CLI"
$ManagerDir         = Join-Path $RootDir "src\Servy.Manager"
$BuildOutputDir     = Join-Path $ServyDir "bin\$Platform\$BuildConfig"
$CliBuildOutputDir  = Join-Path $CliDir "bin\$Platform\$BuildConfig"
$ManagerBuildOutputDir  = Join-Path $ManagerDir "bin\$Platform\$BuildConfig"
Set-Location $ScriptDir

# Package folder structure
$PackageFolder      = "$AppName-$version-$Framework-$Platform-portable"
$AppPackageFolder   = ""
$CliPackageFolder   = ""
$OutputZip          = "$PackageFolder.zip"

# ========================
# Functions
# ========================
function Remove-FileOrFolder {
    param (
        [string]$path
    )
    if (Test-Path $path) {
        Write-Host "Removing: $path"
        Remove-Item -Recurse -Force $path
        Write-Host "Removed: $path"
    }
}

# === BUILD PROJECTS ===
Write-Host "Restoring NuGet packages..."
nuget restore "..\Servy.sln"

Write-Host "Building Servy WPF..."
& (Join-Path $ScriptDir "..\src\Servy\publish.ps1") -Version $version

Write-Host "Building Servy CLI..."
& (Join-Path $ScriptDir "..\src\Servy.CLI\publish.ps1") -Version $version

Write-Host "Building Servy Manager..."
& (Join-Path $ScriptDir "..\src\Servy.Manager\publish.ps1") -Version $version

# === BUILD INSTALLER ===
Write-Host "Building installer from $issFile..."
& "$innoCompiler" (Join-Path $ScriptDir $issFile) /DMyAppVersion=$version /DMyAppPlatform=$Framework

# === PREPARE PACKAGE FILES ===
Write-Host "Preparing package files..."

# Clean old artifacts
Remove-FileOrFolder -path $PackageFolder

# Create directories
$AppPackagePath = Join-Path $PackageFolder $AppPackageFolder
$CliPackagePath = Join-Path $PackageFolder $CliPackageFolder
New-Item -ItemType Directory -Force -Path $AppPackagePath | Out-Null
New-Item -ItemType Directory -Force -Path $CliPackagePath | Out-Null

# Copy Servy WPF files
Copy-Item -Path (Join-Path $BuildOutputDir "Servy.exe") -Destination $AppPackagePath -Force
Copy-Item -Path (Join-Path $BuildOutputDir "*.dll") -Destination $AppPackagePath -Force
# Copy-Item -Path (Join-Path $BuildOutputDir "Servy.exe.config") -Destination $AppPackagePath -Force

Copy-Item -Path (Join-Path $ManagerBuildOutputDir "Servy.Manager.exe") -Destination $AppPackagePath -Force
Copy-Item -Path (Join-Path $ManagerBuildOutputDir "*.dll") -Destination $AppPackagePath -Force


$destSQLiteWPFX64 = Join-Path $AppPackagePath "x64"
$destSQLiteWPFX86 = Join-Path $AppPackagePath "x86"
if (-not (Test-Path $destSQLiteWPFX64)) { New-Item -ItemType Directory -Path $destSQLiteWPFX64 | Out-Null }
if (-not (Test-Path $destSQLiteWPFX86)) { New-Item -ItemType Directory -Path $destSQLiteWPFX86 | Out-Null }
Copy-Item -Path (Join-Path $CliBuildOutputDir "x64\*") -Destination $destSQLiteWPFX64 -Recurse -Force
Copy-Item -Path (Join-Path $CliBuildOutputDir "x86\*") -Destination $destSQLiteWPFX86 -Recurse -Force

# Copy Servy CLI files
Copy-Item -Path (Join-Path $CliBuildOutputDir "Servy.CLI.exe") -Destination (Join-Path $CliPackagePath "servy-cli.exe") -Force
Copy-Item -Path (Join-Path $CliBuildOutputDir "*.dll") -Destination $CliPackagePath -Force
# Copy-Item -Path (Join-Path $CliBuildOutputDir "Servy.CLI.exe.config") -Destination (Join-Path $CliPackagePath "servy-cli.exe.config") -Force

$destSQLiteCLIX64 = Join-Path $CliPackagePath "x64"
$destSQLiteCLIX86 = Join-Path $CliPackagePath "x86"
if (-not (Test-Path $destSQLiteCLIX64)) { New-Item -ItemType Directory -Path $destSQLiteCLIX64 | Out-Null }
if (-not (Test-Path $destSQLiteCLIX86)) { New-Item -ItemType Directory -Path $destSQLiteCLIX86 | Out-Null }
Copy-Item -Path (Join-Path $CliBuildOutputDir "x64\*") -Destination $destSQLiteCLIX64 -Recurse -Force
Copy-Item -Path (Join-Path $CliBuildOutputDir "x86\*") -Destination $destSQLiteCLIX86 -Recurse -Force

# Remove debug symbols (.pdb)
Get-ChildItem -Path $AppPackagePath -Filter "*.pdb" | Remove-Item -Force
Get-ChildItem -Path $CliPackagePath -Filter "*.pdb" | Remove-Item -Force

# === CREATE ZIP PACKAGE ===
# Clean old artifacts
Remove-FileOrFolder -path $OutputZip

# create zib bundle
Write-Host "Creating zip package $OutputZip..."
$ZipArgs = @("a", "-tzip", $OutputZip, "$PackageFolder\*")
$Process = Start-Process -FilePath $SevenZipExe -ArgumentList $ZipArgs -Wait -NoNewWindow -PassThru
if ($Process.ExitCode -ne 0) {
    Write-Error "ERROR: 7z compression failed."
    exit 1
}

# === CLEANUP TEMP PACKAGE FOLDER ===
Write-Host "Cleaning up temporary files..."
Remove-Item -Path $PackageFolder -Recurse -Force

# === DISPLAY ELAPSED TIME ===
$elapsed = (Get-Date) - $startTime
Write-Host "`n=== Build complete in $($elapsed.ToString("hh\:mm\:ss")) ==="

# === PAUSE IF RUN BY DOUBLE-CLICK ===
Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
