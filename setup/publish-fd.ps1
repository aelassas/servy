<#
.SYNOPSIS
    Builds the framework-dependent Servy installer and ZIP package.

.DESCRIPTION
    This script compiles the Servy WPF, CLI, and Manager applications in
    framework-dependent mode, builds the Inno Setup installer, and creates a
    compressed 7z package containing all published framework-dependent builds.

.PARAMETER fm
    Target framework moniker (TFM), e.g., "net10.0".

.PARAMETER version
    Application version used for installer and ZIP output file names.

.PARAMETER pause
    Optional switch that pauses the script before exiting. Useful for double-click usage.

.NOTES
    Requirements:
      - .NET SDK must be installed
      - Inno Setup (ISCC.exe) installed
      - 7-Zip (7z.exe) must be installed

.EXAMPLE
    ./publish-fd.ps1 -fm "net10.0" -version "3.8"

.EXAMPLE
    ./publish-fd.ps1 -version "3.8" -pause
#>

# publish-fd.ps1
# Framework-dependent setup build script for Servy
# Builds WPF and CLI apps, creates Inno Setup installer, and packages a ZIP.

param(
    [string]$fm      = "net10.0",    
    [string]$version = "1.0",
    [switch]$pause
)

$tfm = "$fm-windows"

# -----------------------------
# Configuration
# -----------------------------
$innoCompiler       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$SevenZipExe        = "C:\Program Files\7-Zip\7z.exe"
$issFile            = ".\servy-fd.iss"
$buildConfiguration = "Release"
$runtime            = "win-x64"

# Directories
$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir            = (Resolve-Path (Join-Path $ScriptDir "..")).Path
$ServyDir           = Join-Path $RootDir "src\Servy"
$CliDir             = Join-Path $RootDir "src\Servy.CLI"
$ManagerDir         = Join-Path $RootDir "src\Servy.Manager"

# Helper function: Remove file or folder if it exists
function Remove-FileOrFolder {
    param ([string]$path)
    if (Test-Path $path) {
        Write-Host "Removing: $path"
        Remove-Item -Recurse -Force $path
    }
}

# -----------------------------
# Step 1: Build Servy WPF Apps
# -----------------------------
$wpfBuildScript = Join-Path $ScriptDir "..\src\Servy\publish-fd.ps1"
& $wpfBuildScript -tfm $tfm

$managerBuildScript = Join-Path $ScriptDir "..\src\Servy.Manager\publish-fd.ps1"
& $managerBuildScript  -tfm $tfm

# -----------------------------
# Step 2: Build Servy CLI App
# -----------------------------
$cliBuildScript = Join-Path $ScriptDir "..\src\Servy.CLI\publish-fd.ps1"
& $cliBuildScript -tfm $tfm

# -----------------------------
# Step 3: Build installer (Inno Setup)
# -----------------------------
& $innoCompiler $issFile /DMyAppVersion=$version

# -----------------------------
# Step 4: Prepare ZIP package
# -----------------------------
$packageFolder = Join-Path $ScriptDir "servy-$version-x64-frameworkdependent"
$outputZip     = "$packageFolder.7z"

# Cleanup old package
Remove-FileOrFolder -path $packageFolder
Remove-FileOrFolder -path $outputZip

# Create package folders
New-Item -ItemType Directory -Path $packageFolder | Out-Null

$servyPublish = Join-Path $ServyDir "bin\$buildConfiguration\$tfm\$runtime\publish"
$cliPublish   = Join-Path $CliDir "bin\$buildConfiguration\$tfm\$runtime\publish"
$managerPublish   = Join-Path $ManagerDir "bin\$buildConfiguration\$tfm\$runtime\publish"

$servyAppFolder = Join-Path $packageFolder "servy-app"
$servyCliFolder = Join-Path $packageFolder "servy-cli"
$servyManagerFolder = Join-Path $packageFolder "servy-manager"

New-Item -ItemType Directory -Path $servyAppFolder -Force | Out-Null
New-Item -ItemType Directory -Path $servyCliFolder -Force | Out-Null
New-Item -ItemType Directory -Path $servyManagerFolder -Force | Out-Null

# Copy published files
Copy-Item "$servyPublish\*" $servyAppFolder -Recurse -Force
Copy-Item "$cliPublish\*" $servyCliFolder -Recurse -Force
Copy-Item "$managerPublish\*" $servyManagerFolder -Recurse -Force

# Paths appsettings.json
# $servyAppsettings  = Join-Path $ServyDir "appsettings.json"
# $cliExeAppsettings = Join-Path $CliDir   "appsettings.json"

# Copy appsettings.json
# Copy-Item $servyAppsettings (Join-Path $servyAppFolder "appsettings.json") -Force
# Copy-Item $cliExeAppsettings   (Join-Path $servyCliFolder "appsettings.json") -Force

# Rename CLI EXE
$cliExePath = Join-Path $servyCliFolder "Servy.CLI.exe"
if (Test-Path $cliExePath) {
    Rename-Item -Path $cliExePath -NewName "servy-cli.exe" -Force
}

# Remove all .pdb files for cleaner package
Get-ChildItem -Path $servyAppFolder -Recurse -Filter *.pdb | Remove-Item -Force
Get-ChildItem -Path $servyCliFolder -Recurse -Filter *.pdb | Remove-Item -Force

# -----------------------------
# Step 5: Create ZIP using 7-Zip
# -----------------------------
$parentDir  = Split-Path $packageFolder -Parent
$folderName = Split-Path $packageFolder -Leaf

Copy-Item -Path "taskschd" -Destination "$packageFolder" -Recurse -Force

Copy-Item -Path (Join-Path $CliDir "Servy.psm1") -Destination "$packageFolder" -Force
Copy-Item -Path (Join-Path $CliDir "servy-module-examples.ps1") -Destination "$packageFolder" -Force

$ZipArgs = @(
    "a",
    "-t7z",
    "-m0=lzma2",
    "-mx=9",
    "-mfb=273",
    "-md=64m",
    "-ms=on",
    $outputZip,
    "$packageFolder"
)

$Process = Start-Process -FilePath $SevenZipExe -ArgumentList $ZipArgs -Wait -NoNewWindow -PassThru

if ($Process.ExitCode -ne 0) {
    Write-Error "ERROR: 7z compression failed."
    exit 1
}

# Cleanup temporary folder
Remove-FileOrFolder -path $packageFolder

Write-Host "Framework-dependent ZIP build complete."
Write-Host "Installer build finished."

# -----------------------------
# Optional pause when double-clicked
# -----------------------------
if ($pause) {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
