# publish-sc.ps1
# Self-contained setup build script

param(
    [string]$version = "1.0.0",
    [string]$tfm     = "net8.0-windows",
    [switch]$pause
)

$innoCompiler      = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$issFile           = ".\servy.iss"
$buildConfiguration = "Release"
$runtime           = "win-x64"
$ScriptDir         = Split-Path -Parent $MyInvocation.MyCommand.Path

# Build Servy WPF App
& "..\src\Servy\publish.ps1" -tfm $tfm

# Build Servy CLI App
& "..\src\Servy.CLI\publish.ps1" -tfm $tfm

# Run the compiler
Write-Host "Building installer from $issFile..."
& "$innoCompiler" "$issFile" /DMyAppVersion=$version

# Build self-contained ZIP
Write-Host "Building self-contained ZIP..."

# Paths to executables
$servyExe = Join-Path $ScriptDir "..\src\Servy\bin\$buildConfiguration\$tfm\$runtime\publish\Servy.exe"
$cliExe   = Join-Path $ScriptDir "..\src\Servy.CLI\bin\$buildConfiguration\$tfm\$runtime\publish\Servy.CLI.exe"

# Create package folder
$packageFolder = Join-Path $ScriptDir "servy-$version-net8.0-x64-selfcontained"
$outputZip     = "$packageFolder.zip"

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

# Remove old ZIP or package folder if exist
Remove-FileOrFolder -path $outputZip
Remove-FileOrFolder -path $packageFolder
New-Item -ItemType Directory -Path $packageFolder | Out-Null

# Copy executables into package folder with new names
Copy-Item $servyExe (Join-Path $packageFolder "servy-$version-net8.0-x64.exe") -Force
Copy-Item $cliExe   (Join-Path $packageFolder "servy-cli-$version-net8.0-x64.exe") -Force

# Compress with 7-Zip
Write-Host "Creating ZIP: $outputZip"
$parentDir  = Split-Path $packageFolder -Parent
$folderName = Split-Path $packageFolder -Leaf

Push-Location $parentDir
& 7z a -tzip "$outputZip" "$folderName" | Out-Null
Pop-Location

# Cleanup
Remove-FileOrFolder -path $packageFolder

Write-Host "Self-contained ZIP build complete."
Write-Host "Installer build finished."

# Pause before closing
if ($pause -and $Host.Name -eq "ConsoleHost") {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
