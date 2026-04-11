<#
.SYNOPSIS
    Builds the Servy self-contained installer and portable ZIP package.

.DESCRIPTION
    This script compiles all Servy applications (WPF, CLI, Manager) as self-contained,
    builds the Inno Setup installer, signs the generated installer,
    and generates a portable 7z package containing the published executables.

.PARAMETER Tfm
    Target framework moniker prefix (e.g., "net8.0", "net10.0").

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
    [string]$Tfm      = "net10.0",
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version = "1.0",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# Standardize TFM and Configuration names used across the codebase
$Tfm = "$Tfm-windows"
$BuildConfiguration = "Release"
$Runtime = "win-x64"

# ========================
# Configuration
# ========================
$innoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$sevenZipExe  = "C:\Program Files\7-Zip\7z.exe"

# Directories
$scriptDir = $PSScriptRoot
$rootDir   = (Resolve-Path (Join-Path $scriptDir "..")).Path
$servyDir  = Join-Path $rootDir "src\Servy"
$cliDir    = Join-Path $rootDir "src\Servy.CLI"
$managerDir = Join-Path $rootDir "src\Servy.Manager"
$signPath  = Join-Path $rootDir "setup\signpath.ps1"

# Output Artifacts
$issFile       = Join-Path $scriptDir "servy.iss"
$packageFolder = Join-Path $scriptDir "servy-$Version-x64-portable"
$outputZip     = "$packageFolder.7z"
$installerPath = Join-Path $rootDir "setup\servy-$Version-x64-installer.exe"

# ========================
# Functions
# ========================
function Remove-ItemSafely {
    param ([string]$Path)
    if (Test-Path $Path) {
        Write-Host "Cleaning: $Path" -ForegroundColor Gray
        Remove-Item -Recurse -Force $Path
    }
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
    }
    else {
        Write-Warning "Publish script not found for $projectName. Using generic dotnet publish."
        & dotnet restore $project
        & dotnet clean $project -c $BuildConfiguration
        & dotnet publish $project -c $BuildConfiguration -f $Tfm -r $Runtime --self-contained true
    }
}

# ========================
# Step 2: Build & Sign Installer
# ========================
Write-Host "--- Building Installer ---" -ForegroundColor Cyan
if (Test-Path $innoCompiler) {
    & $innoCompiler $issFile /DMyAppVersion=$Version
}
else {
    Write-Error "ISCC.exe not found. Skipping installer build."
}

if (Test-Path $signPath) {
    Write-Host "--- Signing Artifacts ---" -ForegroundColor Cyan
    & $signPath -Path $installerPath
}

# ========================
# Step 3: Build Portable Package
# ========================
Write-Host "--- Packaging Portable ZIP ---" -ForegroundColor Cyan

Remove-ItemSafely -Path $outputZip
Remove-ItemSafely -Path $packageFolder
[void](New-Item -ItemType Directory -Path $packageFolder)

# Consolidate executables
$binaries = @{
    "Servy.exe"         = Join-Path $servyDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.exe"
    "servy-cli.exe"     = Join-Path $cliDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.CLI.exe"
    "Servy.Manager.exe" = Join-Path $managerDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish\Servy.Manager.exe"
}

foreach ($item in $binaries.GetEnumerator()) {
    if (Test-Path $item.Value) {
        Copy-Item -Path $item.Value -Destination (Join-Path $packageFolder $item.Name) -Force
    }
}

# Include PowerShell Module and Task Scheduler hooks
$taskSchdDest = Join-Path $packageFolder "taskschd"
[void](New-Item -Path $taskSchdDest -ItemType Directory -Force)
Copy-Item -Path (Join-Path $scriptDir "taskschd\*") -Destination $taskSchdDest -Recurse -Force -Exclude "smtp-cred.xml"

$cliArtifacts = @("Servy.psm1", "Servy.psd1", "servy-module-examples.ps1")
foreach ($art in $cliArtifacts) {
    Copy-Item -Path (Join-Path $cliDir $art) -Destination $packageFolder -Force
}

# Compress
if (Test-Path $sevenZipExe) {
    $zipArgs = @("a", "-t7z", "-m0=lzma2", "-mx=9", "-ms=on", $outputZip, $packageFolder)
    $process = Start-Process -FilePath $sevenZipExe -ArgumentList $zipArgs -Wait -NoNewWindow -PassThru
    
    if ($process.ExitCode -eq 0) {
        Remove-ItemSafely -Path $packageFolder
        Write-Host "Success: $outputZip" -ForegroundColor Green
    }
}

if ($Pause) {
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}