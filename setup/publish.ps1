# publish.ps1
# Main setup bundle script for .NET Framework build of Servy
# Requirements:
#  1. Add msbuild and nuget.exe to PATH
#  2. Inno Setup installed (ISCC.exe path updated if different)
#  3. 7-Zip installed and 7z in PATH

$ErrorActionPreference = "Stop"

$ScriptHadError = $false

try {
    # Record start time
    $StartTime = Get-Date

    # === CONFIGURATION ===
    $Version      = "5.4"
    $AppName      = "servy"
    $BuildConfig  = "Release"
    $Platform     = "x64"
    $Framework    = "net48"

    # Tools
    $innoCompiler       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    $issFile            = "servy.iss"   # Inno Setup script filename
    $SevenZipExe        = "C:\Program Files\7-Zip\7z.exe"

    # === PATH RESOLUTION ===

    # Directories
    $ScriptDir              = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RootDir                = (Resolve-Path (Join-Path $ScriptDir "..")).Path
    $ServyDir               = Join-Path $RootDir "src\Servy"
    $CliDir                 = Join-Path $RootDir "src\Servy.CLI"
    $ManagerDir             = Join-Path $RootDir "src\Servy.Manager"
    $BuildOutputDir         = Join-Path $ServyDir "bin\$Platform\$BuildConfig"
    $CliBuildOutputDir      = Join-Path $CliDir "bin\$Platform\$BuildConfig"
    $ManagerBuildOutputDir  = Join-Path $ManagerDir "bin\$Platform\$BuildConfig"
    $SignPath               = Join-Path $RootDir "setup\signpath.ps1" | Resolve-Path
    Set-Location $ScriptDir

    # Package folder structure
    $PackageFolder      = "$AppName-$Version-$Framework-$Platform-portable"
    $AppPackageFolder   = ""
    $CliPackageFolder   = ""
    $OutputZip          = "$PackageFolder.7z"

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

    # === BUILD PROJECTS ===
    Write-Host "Restoring NuGet packages..."
    nuget restore "..\Servy.sln"

    Write-Host "Building Servy WPF..."
    & (Join-Path $ScriptDir "..\src\Servy\publish.ps1") -Version $Version

    Write-Host "Building Servy CLI..."
    & (Join-Path $ScriptDir "..\src\Servy.CLI\publish.ps1") -Version $Version

    Write-Host "Building Servy Manager..."
    & (Join-Path $ScriptDir "..\src\Servy.Manager\publish.ps1") -Version $Version

    # === BUILD INSTALLER ===
    Write-Host "Building installer from $issFile..."
    & "$innoCompiler" (Join-Path $ScriptDir $issFile) /DMyAppVersion=$Version /DMyAppPlatform=$Framework

    # === SIGN INSTALLER ===
    $InstallerPath = Join-Path $RootDir "setup\servy-$Version-net48-x64-installer.exe" | Resolve-Path
    & $SignPath $InstallerPath

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

    # Copy Servy CLI files
    Copy-Item -Path (Join-Path $CliBuildOutputDir "Servy.CLI.exe") -Destination (Join-Path $CliPackagePath "servy-cli.exe") -Force
    Copy-Item -Path (Join-Path $CliBuildOutputDir "*.dll") -Destination $CliPackagePath -Force
    # Copy-Item -Path (Join-Path $CliBuildOutputDir "Servy.CLI.exe.config") -Destination (Join-Path $CliPackagePath "servy-cli.exe.config") -Force

    # Remove debug symbols (.pdb)
    Get-ChildItem -Path $AppPackagePath -Filter "*.pdb" | Remove-Item -Force
    Get-ChildItem -Path $CliPackagePath -Filter "*.pdb" | Remove-Item -Force

    # === CREATE ZIP PACKAGE ===
    # Clean old artifacts
    Remove-FileOrFolder -path $OutputZip

    # create zib bundle
    Write-Host "Creating zip package $OutputZip..."

    Copy-Item -Path "taskschd" -Destination "$PackageFolder" -Recurse -Force

    Copy-Item -Path (Join-Path $CliDir "Servy.psm1") -Destination "$PackageFolder" -Force
    Copy-Item -Path (Join-Path $CliDir "servy-module-examples.ps1") -Destination "$PackageFolder" -Force

    $ZipArgs = @(
        "a",
        "-t7z",
        "-m0=lzma2",
        "-mx=9",
        "-mfb=273",
        "-md=64m",
        "-ms=on",
        $OutputZip,
        "$PackageFolder"
    )

    $Process = Start-Process -FilePath $SevenZipExe -ArgumentList $ZipArgs -Wait -NoNewWindow -PassThru

    if ($Process.ExitCode -ne 0) {
        Write-Error "ERROR: 7z compression failed."
        exit 1
    }

    # === CLEANUP TEMP PACKAGE FOLDER ===
    Write-Host "Cleaning up temporary files..."
    Remove-Item -Path $PackageFolder -Recurse -Force

    # === DISPLAY ELAPSED TIME ===
    $elapsed = (Get-Date) - $StartTime
    Write-Host "`n=== Build complete in $($elapsed.ToString("hh\:mm\:ss")) ==="
}
catch {
    $ScriptHadError = $true
    Write-Host "`nERROR OCCURRED:" -ForegroundColor Red
    Write-Host $_
}
finally {
    # Pause by default (for double-click usage)
    if ($ScriptHadError) {
        Write-Host "`nBuild failed. Press any key to exit..."
    }
    else {
        Write-Host "`nPress any key to exit..."
    }

    try {
        if ($Host.Name -eq 'ConsoleHost' -or $Host.Name -like '*Console*') {
            [void][System.Console]::ReadKey($true)
        }
        else {
            try {
                $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
            }
            catch {
                Read-Host | Out-Null
            }
        }
    }
    catch { }
}