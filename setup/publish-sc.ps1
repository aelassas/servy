<#
.SYNOPSIS
    Builds the Servy self-contained installer and portable ZIP package.

.DESCRIPTION
    This script compiles all Servy applications (WPF, CLI, Manager) as self-contained,
    builds the Inno Setup installer, signs the generated installer (if signing is enabled),
    and generates a portable 7z package containing the published executables.

.PARAMETER Fm
    Target framework moniker (TFM), e.g., "net10.0".

.PARAMETER Version
    Application version used for installer and ZIP output file names.

.PARAMETER Pause
    Optional switch that pauses the script before exiting. Useful when double-clicking.

.NOTES
    Requirements:
      - .NET SDK must be installed
      - Inno Setup (ISCC.exe) must be installed
      - 7-Zip (7z.exe) must be installed
      - SignPath script configured in setup/signpath.ps1

.EXAMPLE
    ./publish-sc.ps1 -fm "net10.0" -version "3.8"

.EXAMPLE
    ./publish-sc.ps1 -version "3.8" -Pause
#>

# publish-sc.ps1
# Build script for Servy self-contained installer and ZIP package

param(
    [string]$Fm      = "net10.0",
    [string]$Version = "1.0",
    [switch]$Pause
)

if (-not $Tfm) {
    $Tfm = "$Fm-windows"
}

# ========================
# Configuration
# ========================
$BuildConfiguration = "Release"
$Runtime            = "win-x64"
$innoCompiler       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$SevenZipExe        = "C:\Program Files\7-Zip\7z.exe"

# Directories
$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir            = (Resolve-Path (Join-Path $ScriptDir "..")).Path
$ServyDir           = Join-Path $RootDir "src\Servy"
$CliDir             = Join-Path $RootDir "src\Servy.CLI"
$ManagerDir         = Join-Path $RootDir "src\Servy.Manager"
$SignPath           = Join-Path $RootDir "setup\signpath.ps1" | Resolve-Path

# Inno Setup file
$issFile            = Join-Path $ScriptDir "servy.iss"

# ========================
# Functions
# ========================
function Remove-FileOrFolder {
    param (
        [string]$Path
    )
    if (Test-Path $Path) {
        Write-Host "Removing: $Path"
        Remove-Item -Recurse -Force $Path
        Write-Host "Removed: $Path"
    }
}

# ========================
# Step 1: Build Applications
# ========================
Write-Host "Building Servy WPF app..."
& (Join-Path $ServyDir "publish.ps1") -Tfm $Tfm

Write-Host "Building Servy CLI app..."
& (Join-Path $CliDir "publish.ps1") -Tfm $Tfm

Write-Host "Building Servy.Manager app..."
& (Join-Path $ManagerDir "publish.ps1") -Tfm $Tfm

# ========================
# Step 2: Build Installer
# ========================
Write-Host "Building installer from $issFile..."
& $innoCompiler $issFile /DMyAppVersion=$Version

# ========================
# Step 3: Sign Installer if signing is enabled
# ========================
$InstallerPath = Join-Path $RootDir "setup\servy-$Version-x64-installer.exe"
& $SignPath $InstallerPath

# ========================
# Step 4: Build Self-Contained ZIP
# ========================
Write-Host "Building self-contained ZIP..."

# Paths to executables
$servyExe    = Join-Path $ServyDir   "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.exe"
$cliExe      = Join-Path $CliDir     "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.CLI.exe"
$managerExe  = Join-Path $ManagerDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.Manager.exe"

# Package folder
$packageFolder = Join-Path $ScriptDir "servy-$Version-x64-portable"
$outputZip     = "$packageFolder.7z"

# Clean old artifacts
Remove-FileOrFolder -path $outputZip
Remove-FileOrFolder -path $packageFolder
New-Item -ItemType Directory -Path $packageFolder | Out-Null

# Copy executables with versioned names
# Copy-Item $servyExe (Join-Path $packageFolder "servy-$Version-$Tfm-x64.exe") -Force
# Copy-Item $cliExe   (Join-Path $packageFolder "servy-cli-$Version-$Tfm-x64.exe") -Force
Copy-Item $servyExe (Join-Path $packageFolder "Servy.exe") -Force
Copy-Item $cliExe (Join-Path $packageFolder "servy-cli.exe") -Force
Copy-Item $managerExe (Join-Path $packageFolder "Servy.Manager.exe") -Force

# Compress with 7-Zip
Write-Host "Creating ZIP: $outputZip"
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
    "-md=128m",
    "-ms=on",
    $outputZip,
    "$packageFolder"
)

$Process = Start-Process -FilePath $SevenZipExe -ArgumentList $ZipArgs -Wait -NoNewWindow -PassThru

if ($Process.ExitCode -ne 0) {
    Write-Error "ERROR: 7z compression failed."
    exit 1
}

# Remove temp folder
Remove-FileOrFolder -path $packageFolder

Write-Host "Self-contained ZIP build complete."
Write-Host "Installer build finished."

# ========================
# Step 5: Pause if requested
# ========================
if ($Pause) {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
