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
    [string]$Version = "1.0",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

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

# Output Artifacts
$issFile       = Join-Path $scriptDir "servy-fd.iss"
$packageFolder = Join-Path $scriptDir "servy-$Version-x64-frameworkdependent"
$outputZip     = "$packageFolder.7z"

# ========================
# Functions
# ========================
function Check-LastExitCode {
    param([string]$ErrorMessage)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

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
$projects = @($servyDir, $cliDir, $managerDir)

foreach ($project in $projects) {
    $projectName = Split-Path $project -Leaf
    Write-Host "--- Publishing $projectName (Framework-Dependent) ---" -ForegroundColor Cyan
    
    # We call the standardized project-level publish script.
    $publishScript = Join-Path $project "publish-fd.ps1"
    if (Test-Path $publishScript) {
        & $publishScript -BuildConfiguration $BuildConfiguration -Tfm $Tfm
        Check-LastExitCode "$publishScript failed"
    }
    else {
        # Fallback if specific FD script is missing
        Write-Warning "Specific FD script missing for $projectName. Using dotnet publish."
        & dotnet restore $project
        Check-LastExitCode "dotnet restore failed"

        & dotnet clean $project -c $BuildConfiguration
        Check-LastExitCode "Project clean failed"
        
        # Explicitly disable PDB copying during publish to prevent MSB3030
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
Write-Host "--- Building Installer ---" -ForegroundColor Cyan
if (-not (Test-Path $innoCompiler)) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found at: $innoCompiler"
    exit 1
}
& $innoCompiler $issFile /DMyAppVersion=$Version
Check-LastExitCode "Inno Setup compilation failed"

# ========================
# Step 3: Prepare ZIP package
# ========================
Write-Host "--- Packaging FD ZIP ---" -ForegroundColor Cyan

Remove-ItemSafely -Path $outputZip
Remove-ItemSafely -Path $packageFolder
[void](New-Item -ItemType Directory -Path $packageFolder)

# Define internal structure for FD package
$subFolders = @{
    "servy-app"     = Join-Path $servyDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
    "servy-cli"     = Join-Path $cliDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
    "servy-manager" = Join-Path $managerDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
}

foreach ($entry in $subFolders.GetEnumerator()) {
    $dest = Join-Path $packageFolder $entry.Key
    if (Test-Path $entry.Value) {
        Write-Host "Copying $($entry.Key)..."
        [void](New-Item -ItemType Directory -Path $dest -Force)
        Copy-Item -Path "$($entry.Value)\*" -Destination $dest -Recurse -Force
    }
}

# Standardize CLI executable name in the package
$cliExe = Join-Path $packageFolder "servy-cli\Servy.CLI.exe"
if (Test-Path $cliExe) {
    Rename-Item -Path $cliExe -NewName "servy-cli.exe" -Force
}

# Include Task Scheduler Hooks and PowerShell Module
$taskSchdDest = Join-Path $packageFolder "taskschd"
[void](New-Item -Path $taskSchdDest -ItemType Directory -Force)
Copy-Item -Path (Join-Path $scriptDir "taskschd\*") -Destination $taskSchdDest -Recurse -Force -Exclude "smtp-cred.xml"

$cliArtifacts = @("Servy.psm1", "Servy.psd1", "servy-module-examples.ps1")
foreach ($art in $cliArtifacts) {
    Copy-Item -Path (Join-Path $cliDir $art) -Destination $packageFolder -Force
}

# ========================
# Step 4: Create ZIP
# ========================
if (-not (Test-Path $sevenZipExe)) {
    Write-Error "7-Zip executable not found at: $sevenZipExe. Compression failed."
    exit 1
}

$zipArgs = @("a", "-t7z", "-m0=lzma2", "-mx=9", "-ms=on", $outputZip, $packageFolder)
$process = Start-Process -FilePath $sevenZipExe -ArgumentList $zipArgs -Wait -NoNewWindow -PassThru
    
if ($process.ExitCode -ne 0) {
    Write-Error "7-Zip failed with exit code $($process.ExitCode)"
    exit $process.ExitCode
}

# Only remove the folder if the ZIP was successful
Remove-ItemSafely -Path $packageFolder
Write-Host "Success: $outputZip" -ForegroundColor Green

if ($Pause) {
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}