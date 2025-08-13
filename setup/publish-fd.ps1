# publish-fd.ps1
# Framework-dependent setup build script

param(
    [string]$version = "1.0.0",
    [string]$tfm     = "net8.0-windows",
    [switch]$pause
)

$innoCompiler      = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$issFile           = ".\servy-fd.iss"
$buildConfiguration = "Release"
$runtime           = "win-x64"
$ScriptDir         = Split-Path -Parent $MyInvocation.MyCommand.Path

function Remove-FileOrFolder {
    param ([string]$path)
    if (Test-Path $path) {
        Write-Host "Removing: $path"
        Remove-Item -Recurse -Force $path
    }
}

# Step 1: Build Servy WPF App
& "..\src\Servy\publish-fd.ps1" -tfm $tfm

# Step 2: Build Servy CLI App
& "..\src\Servy.CLI\publish-fd.ps1" -tfm $tfm

# Step 3: Build installer
& "$innoCompiler" "$issFile" /DMyAppVersion=$version

# Step 4: Create ZIP package
$packageFolder = Join-Path $ScriptDir "servy-$version-net8.0-x64-frameworkdependent"
$outputZip     = "$packageFolder.zip"

Remove-FileOrFolder -path $packageFolder
Remove-FileOrFolder -path $outputZip
New-Item -ItemType Directory -Path $packageFolder | Out-Null

# Copy folders
$servyPublish = Join-Path $ScriptDir "..\src\Servy\bin\$buildConfiguration\$tfm\$runtime\publish"
$cliPublish   = Join-Path $ScriptDir "..\src\Servy.CLI\bin\$buildConfiguration\$tfm\$runtime\publish"
$servyApp     = "servy-app"
$servyCli     = "servy-cli"

$servyAppFolder = Join-Path $packageFolder $servyApp
$servyCliFolder = Join-Path $packageFolder $servyCli

New-Item -ItemType Directory -Path $servyAppFolder -Force | Out-Null
New-Item -ItemType Directory -Path $servyCliFolder -Force | Out-Null

# Copy everything from publish folder **without wildcards**, so renaming works
Copy-Item "$servyPublish\*" $servyAppFolder -Recurse -Force
Copy-Item "$cliPublish\*" $servyCliFolder -Recurse -Force

# Rename CLI EXE
$cliExePath = Join-Path $servyCliFolder "Servy.CLI.exe"
if (Test-Path $cliExePath) {
    Rename-Item -Path $cliExePath -NewName "servy-cli.exe" -Force
}

# Remove all .pdb files
Get-ChildItem -Path $servyAppFolder -Recurse -Filter *.pdb | Remove-Item -Force
Get-ChildItem -Path $servyCliFolder -Recurse -Filter *.pdb | Remove-Item -Force

# ZIP with 7-Zip
$parentDir  = Split-Path $packageFolder -Parent
$folderName = Split-Path $packageFolder -Leaf

Push-Location $parentDir
& 7z a -tzip "$outputZip" "$folderName" | Out-Null
Pop-Location

# Cleanup
Remove-FileOrFolder -path $packageFolder

Write-Host "Framework-dependent ZIP build complete."
Write-Host "Installer build finished."

# Pause when double-clicked
if ($pause -and $Host.Name -eq "ConsoleHost") {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
