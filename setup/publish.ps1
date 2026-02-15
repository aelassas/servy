# publish.ps1
# Main setup bundle script for .NET Framework build of Servy
# Requirements:
#  1. Add msbuild and nuget.exe to PATH
#  2. Inno Setup installed (ISCC.exe path updated if different)
#  3. 7-Zip installed and 7z in PATH

$ErrorActionPreference = "Stop"

$scriptHadError = $false

try {
    # Record start time
    $startTime = Get-Date

    # === CONFIGURATION ===
    $version      = "6.8"
    $appName      = "servy"
    $buildConfig  = "Release"
    $platform     = "x64"
    $framework    = "net48"

    # Tools
    $innoCompiler       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    $issFile            = "servy.iss"   # Inno Setup script filename
    $sevenZipExe        = "C:\Program Files\7-Zip\7z.exe"

    # === PATH RESOLUTION ===

    # Directories
    $scriptDir              = Split-Path -Parent $MyInvocation.MyCommand.Path
    $rootDir                = (Resolve-Path (Join-Path $scriptDir "..")).Path
    $servyDir               = Join-Path $rootDir "src\Servy"
    $cliDir                 = Join-Path $rootDir "src\Servy.CLI"
    $managerDir             = Join-Path $rootDir "src\Servy.Manager"
    $buildOutputDir         = Join-Path $servyDir "bin\$platform\$buildConfig"
    $cliBuildOutputDir      = Join-Path $cliDir "bin\$platform\$buildConfig"
    $managerBuildOutputDir  = Join-Path $managerDir "bin\$platform\$buildConfig"
    $signPath               = Join-Path $rootDir "setup\signpath.ps1" | Resolve-Path
    Set-Location $scriptDir

    # Package folder structure
    $packageFolder      = "$appName-$version-$framework-$platform-portable"
    $appPackageFolder   = ""
    $cliPackageFolder   = ""
    $outputZip          = "$packageFolder.7z"

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
    & (Join-Path $scriptDir "..\src\Servy\publish.ps1") -Version $version

    Write-Host "Building Servy CLI..."
    & (Join-Path $scriptDir "..\src\Servy.CLI\publish.ps1") -Version $version

    Write-Host "Building Servy Manager..."
    & (Join-Path $scriptDir "..\src\Servy.Manager\publish.ps1") -Version $version

    # === BUILD INSTALLER ===
    Write-Host "Building installer from $issFile..."
    & "$innoCompiler" (Join-Path $scriptDir $issFile) /DMyAppVersion=$version /DMyAppPlatform=$framework

    # === SIGN INSTALLER ===
    $installerPath = Join-Path $rootDir "setup\servy-$version-net48-x64-installer.exe" | Resolve-Path
    & $signPath $installerPath

    # === PREPARE PACKAGE FILES ===
    Write-Host "Preparing package files..."

    # Clean old artifacts
    Remove-FileOrFolder -path $packageFolder

    # Create directories
    $appPackagePath = Join-Path $packageFolder $appPackageFolder
    $cliPackagePath = Join-Path $packageFolder $cliPackageFolder
    New-Item -ItemType Directory -Force -Path $appPackagePath | Out-Null
    New-Item -ItemType Directory -Force -Path $cliPackagePath | Out-Null

    # Copy Servy WPF files
    Copy-Item -Path (Join-Path $buildOutputDir "Servy.exe") -Destination $appPackagePath -Force
    Copy-Item -Path (Join-Path $buildOutputDir "*.dll") -Destination $appPackagePath -Force
    # Copy-Item -Path (Join-Path $buildOutputDir "Servy.exe.config") -Destination $appPackagePath -Force

    Copy-Item -Path (Join-Path $managerBuildOutputDir "Servy.Manager.exe") -Destination $appPackagePath -Force
    Copy-Item -Path (Join-Path $managerBuildOutputDir "*.dll") -Destination $appPackagePath -Force

    # Copy Servy CLI files
    Copy-Item -Path (Join-Path $cliBuildOutputDir "Servy.CLI.exe") -Destination (Join-Path $cliPackagePath "servy-cli.exe") -Force
    Copy-Item -Path (Join-Path $cliBuildOutputDir "*.dll") -Destination $cliPackagePath -Force
    # Copy-Item -Path (Join-Path $cliBuildOutputDir "Servy.CLI.exe.config") -Destination (Join-Path $cliPackagePath "servy-cli.exe.config") -Force

    # Remove debug symbols (.pdb)
    Get-ChildItem -Path $appPackagePath -Filter "*.pdb" | Remove-Item -Force
    Get-ChildItem -Path $cliPackagePath -Filter "*.pdb" | Remove-Item -Force

    # === CREATE ZIP PACKAGE ===
    # Clean old artifacts
    Remove-FileOrFolder -path $outputZip

    # create zib bundle
    Write-Host "Creating zip package $outputZip..."

    Copy-Item -Path "taskschd" -Destination "$packageFolder" -Recurse -Force

    Copy-Item -Path (Join-Path $cliDir "Servy.psm1") -Destination "$packageFolder" -Force
    Copy-Item -Path (Join-Path $cliDir "servy-module-examples.ps1") -Destination "$packageFolder" -Force

    $zipArgs = @(
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

    $process = Start-Process -FilePath $sevenZipExe -ArgumentList $zipArgs -Wait -NoNewWindow -PassThru

    if ($process.ExitCode -ne 0) {
        Write-Error "ERROR: 7z compression failed."
        exit 1
    }

    # === CLEANUP TEMP PACKAGE FOLDER ===
    Write-Host "Cleaning up temporary files..."
    Remove-Item -Path $packageFolder -Recurse -Force

    # === DISPLAY ELAPSED TIME ===
    $elapsed = (Get-Date) - $startTime
    Write-Host "`n=== Build complete in $($elapsed.ToString("hh\:mm\:ss")) ==="
}
catch {
    $scriptHadError = $true
    Write-Host "`nERROR OCCURRED:" -ForegroundColor Red
    Write-Host $_
}
finally {
    # Pause by default (for double-click usage)
    if ($scriptHadError) {
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