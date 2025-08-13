# publish.ps1 - Main setup bundle script for .NET Framework build
# Requirements: add msbuild to PATH environment variable

$ErrorActionPreference = "Stop"

$version       = "1.1.0"
$innoCompiler  = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$issFile       = "servy.iss"

$AppName = "servy"
$BuildConfig = "Release"
$Platform = "x64"
$Framework = "net48"

# Get the directory of this script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Build Servy WPF App
Write-Host "Building Servy WPF..."
& "$ScriptDir\..\src\Servy\publish.ps1" -Version $version

# Build Servy CLI App
Write-Host "Building Servy CLI..."
& "$ScriptDir\..\src\Servy.CLI\publish.ps1" -Version $version

# Compile installer
Write-Host "Building installer from $issFile..."
& "$innoCompiler" "$issFile" /DMyAppVersion=$version

# Build portable framework dependant ZIP bundle
$PackageFolder = "$AppName-$version-$Framework-$Platform-frameworkdependent"
$AppPackageFolder = "servy-app"
$CliPackageFolder = "servy-cli"
$OutputZip = "$PackageFolder.zip"

$BuildOutputDir = "..\src\Servy\bin\$BuildConfig"
$CliBuildOutputDir = "..\src\Servy.CLI\bin\$BuildConfig"

# === COPY BUILD FILES TO PACKAGE DIRECTORY ===
Write-Host "Preparing package files..."

# Create directories
$AppPackagePath = Join-Path $PackageFolder $AppPackageFolder
$CliPackagePath = Join-Path $PackageFolder $CliPackageFolder
New-Item -ItemType Directory -Force -Path $AppPackagePath | Out-Null
New-Item -ItemType Directory -Force -Path $CliPackagePath | Out-Null

# Copy app files
Copy-Item -Path (Join-Path $BuildOutputDir "Servy.exe") -Destination $AppPackagePath -Force
Copy-Item -Path (Join-Path $BuildOutputDir "*.dll") -Destination $AppPackagePath -Force

# Copy CLI files
Copy-Item -Path (Join-Path $CliBuildOutputDir "Servy.CLI.exe") -Destination (Join-Path $CliPackagePath "servy-cli.exe") -Force
Copy-Item -Path (Join-Path $CliBuildOutputDir "*.dll") -Destination $CliPackagePath -Force

# Optionally remove .pdb files
Get-ChildItem -Path $AppPackagePath -Filter "*.pdb" | Remove-Item -Force
Get-ChildItem -Path $CliPackagePath -Filter "*.pdb" | Remove-Item -Force

# === CREATE ZIP PACKAGE ===
Write-Host "Creating zip package $OutputZip..."
$SevenZipExe = "7z"  # Assumes 7z is in PATH
$ZipArgs = @("a", "-tzip", $OutputZip, (Join-Path $PackageFolder "*"))
$Process = Start-Process -FilePath $SevenZipExe -ArgumentList $ZipArgs -Wait -NoNewWindow -PassThru
if ($Process.ExitCode -ne 0) {
    Write-Error "ERROR: 7z compression failed."
    exit 1
}

# === CLEANUP PACKAGE DIRECTORY ===
Write-Host "Cleaning up temporary files..."
Remove-Item -Path $PackageFolder -Recurse -Force


# Pause if launched by double-click
if ($Host.Name -eq "ConsoleHost") {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
