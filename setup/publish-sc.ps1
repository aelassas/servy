# publish-sc.ps1
# Build script for Servy self-contained installer and ZIP package

param(
    [string]$fm     = "net8.0",
    [string]$version = "1.0.0",
    [switch]$pause
)

if (-not $tfm) {
    $tfm = "$fm-windows"
}

# ========================
# Configuration
# ========================
$buildConfiguration = "Release"
$runtime            = "win-x64"
$innoCompiler       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# Directories
$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir            = (Resolve-Path (Join-Path $ScriptDir "..")).Path
$ServyDir           = Join-Path $RootDir "src\Servy"
$CliDir             = Join-Path $RootDir "src\Servy.CLI"
$ManagerDir         = Join-Path $RootDir "src\Servy.Manager"

# Inno Setup file
$issFile            = Join-Path $ScriptDir "servy.iss"

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

# ========================
# Step 1: Build Applications
# ========================
Write-Host "Building Servy WPF app..."
& (Join-Path $ServyDir "publish.ps1") -tfm $tfm

Write-Host "Building Servy CLI app..."
& (Join-Path $CliDir "publish.ps1") -tfm $tfm

Write-Host "Building Servy.Manager app..."
& (Join-Path $ManagerDir "publish.ps1") -tfm $tfm

# ========================
# Step 2: Build Installer
# ========================
Write-Host "Building installer from $issFile..."
& $innoCompiler $issFile /DMyAppVersion=$version /DMyAppPlatform=$fm

# ========================
# Step 3: Build Self-Contained ZIP
# ========================
Write-Host "Building self-contained ZIP..."

# Paths to executables
$servyExe    = Join-Path $ServyDir   "bin\$buildConfiguration\$tfm\$runtime\publish\Servy.exe"
$cliExe      = Join-Path $CliDir     "bin\$buildConfiguration\$tfm\$runtime\publish\Servy.CLI.exe"
$managerExe  = Join-Path $ManagerDir "bin\$buildConfiguration\$tfm\$runtime\publish\Servy.Manager.exe"

# Package folder
$packageFolder = Join-Path $ScriptDir "servy-$version-$fm-x64-portable"
$outputZip     = "$packageFolder.zip"

# Clean old artifacts
Remove-FileOrFolder -path $outputZip
Remove-FileOrFolder -path $packageFolder
New-Item -ItemType Directory -Path $packageFolder | Out-Null

# Copy executables with versioned names
# Copy-Item $servyExe (Join-Path $packageFolder "servy-$version-$tfm-x64.exe") -Force
# Copy-Item $cliExe   (Join-Path $packageFolder "servy-cli-$version-$tfm-x64.exe") -Force
Copy-Item $servyExe (Join-Path $packageFolder "Servy.exe") -Force
Copy-Item $cliExe (Join-Path $packageFolder "servy-cli.exe") -Force
Copy-Item $managerExe (Join-Path $packageFolder "Servy.Manager.exe") -Force

# Compress with 7-Zip
Write-Host "Creating ZIP: $outputZip"
$parentDir  = Split-Path $packageFolder -Parent
$folderName = Split-Path $packageFolder -Leaf

Push-Location $parentDir
& 7z a -tzip "$outputZip" "$folderName" | Out-Null
Pop-Location

# Remove temp folder
Remove-FileOrFolder -path $packageFolder

Write-Host "Self-contained ZIP build complete."
Write-Host "Installer build finished."

# ========================
# Step 4: Pause if requested
# ========================
if ($pause) {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
