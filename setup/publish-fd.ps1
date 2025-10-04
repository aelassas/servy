# publish-fd.ps1
# Framework-dependent setup build script for Servy
# Builds WPF and CLI apps, creates Inno Setup installer, and packages a ZIP.

param(
    [string]$fm     = "net8.0",    
    [string]$version = "1.0.0",
    [switch]$pause
)

$tfm = "$fm-windows"

# -----------------------------
# Configuration
# -----------------------------
$innoCompiler       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
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
& $innoCompiler $issFile /DMyAppVersion=$version  /DMyAppPlatform=$fm

# -----------------------------
# Step 4: Prepare ZIP package
# -----------------------------
$packageFolder = Join-Path $ScriptDir "servy-$version-x64-frameworkdependent"
$outputZip     = "$packageFolder.zip"

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

Push-Location $parentDir
& 7z a -tzip "$outputZip" "$folderName" | Out-Null
Pop-Location

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
