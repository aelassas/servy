#requires -Version 5.0
<#
.SYNOPSIS
Main setup bundle script for the .NET Framework 4.8 build of Servy.

.DESCRIPTION
This script automates the full build lifecycle for the net48 platform, including:
1. Tool resolution (Inno Setup, 7-Zip).
2. Project compilation and publishing.
3. Installer generation and code signing.
4. Secure packaging of portable artifacts with credential leak protection.
#>

$ErrorActionPreference = "Stop"
$scriptHadError = $false

$scriptDir = $PSScriptRoot

try {
    $startTime = Get-Date

    # === CONFIGURATION ===
    $version      = "8.4"
    $appName      = "servy"
    $buildConfig  = "Release"
    $platform     = "x64"
    $framework    = "net48"
    $issFile      = "servy.iss"

    # === PATH RESOLUTION ===
    Set-Location $scriptDir
    $rootDir               = (Resolve-Path (Join-Path $scriptDir "..")).Path
    $servyDir              = Join-Path $rootDir "src\Servy"
    $cliDir                = Join-Path $rootDir "src\Servy.CLI"
    $managerDir            = Join-Path $rootDir "src\Servy.Manager"
    
    $buildOutputDir        = Join-Path $servyDir "bin\$platform\$buildConfig"
    $cliBuildOutputDir     = Join-Path $cliDir "bin\$platform\$buildConfig"
    $managerBuildOutputDir = Join-Path $managerDir "bin\$platform\$buildConfig"
    
    # Resolver for the code signing sub-script
    $signPath              = Join-Path $scriptDir "signpath.ps1"

    $packageFolder         = "$appName-$version-$framework-$platform-portable"
    $outputZip              = "$packageFolder.7z"

    # === Tool Discovery ===
    <# 
    Attempts to resolve required external binaries (Inno Setup and 7-Zip) 
    using the tools-config helper. 
    #>
    try {
        . (Join-Path $scriptDir "tools-config.ps1")

        Write-Host "Resolving build tools..." -ForegroundColor Cyan

        $innoCompiler = Resolve-Tool -Name "ISCC" -Fallbacks @(
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
        )

        $sevenZipExe  = Resolve-Tool -Name "7z" -Fallbacks @(
            "C:\Program Files\7-Zip\7z.exe",
            "C:\Program Files (x86)\7-Zip\7z.exe"
        )
    
        Write-Host "Tools resolved successfully." -ForegroundColor Green
    }
    catch {
        Write-Error "Configuration Failed: $($_.Exception.Message)"
        return
    }

    # === Internal Functions ===

    function Check-LastExitCode {
        param([string]$ErrorMessage)
        if ($LASTEXITCODE -ne 0) { throw "ERROR: $ErrorMessage (Exit Code: $LASTEXITCODE)" }
    }

    function Remove-ItemSafely {
        param ([string]$Path)
        if (Test-Path $Path) {
            Write-Host "Cleaning: $Path" -ForegroundColor Gray
            Remove-Item -Recurse -Force $Path
        }
    }

    # === BUILD PROJECTS ===
    Write-Host "--- Restoring & Building Projects ---" -ForegroundColor Cyan
    
    # Restore NuGet packages for the entire solution
    nuget restore (Join-Path $rootDir "Servy.sln")
    Check-LastExitCode "NuGet restore failed"

    # Define the project list and their respective publish script paths
    $projects = @(
        @{ Name="WPF"; Path="..\src\Servy\publish.ps1" },
        @{ Name="CLI"; Path="..\src\Servy.CLI\publish.ps1" },
        @{ Name="Manager"; Path="..\src\Servy.Manager\publish.ps1" }
    )

    foreach ($p in $projects) {
        $pPath = Join-Path $scriptDir $p.Path
        if (Test-Path $pPath) {
            Write-Host "Invoking publish for $($p.Name)..."
            & $pPath -Version $version
            Check-LastExitCode "$($p.Name) build failed"
        }
    }

    # === BUILD & SIGN INSTALLER ===
    Write-Host "--- Installer Generation ---" -ForegroundColor Cyan

    $installerPath = Join-Path $scriptDir "servy-$version-net48-x64-installer.exe"

    # Clean existing installer to prevent file access errors during ISCC execution
    Remove-ItemSafely -Path $installerPath

    # Execute Inno Setup Compiler
    & "$innoCompiler" (Join-Path $scriptDir $issFile) /DMyAppVersion=$version /DMyAppPlatform=$framework
    Check-LastExitCode "Inno Setup failed"

    # Code Signing (if the signing script is available)
    if (Test-Path $installerPath) {
        if (Test-Path $signPath) {
            Write-Host ">>> Signing Installer binary..." -ForegroundColor Yellow
            & $signPath $installerPath
            Check-LastExitCode "Signing failed"
        }
    } else {
        throw "Installer binary not found at $installerPath"
    }

    # === PREPARE PACKAGE FILES ===
    Write-Host "--- Packaging Portable ZIP ---" -ForegroundColor Cyan

    # Ensure a clean staging environment
    Remove-ItemSafely -Path $packageFolder
    Remove-ItemSafely -Path $outputZip

    try {
        New-Item -ItemType Directory -Force -Path $packageFolder | Out-Null

        # 1. Copy Core Binaries
        $binMap = @{
            "Servy.exe"         = Join-Path $buildOutputDir "Servy.exe"
            "Servy.Manager.exe" = Join-Path $managerBuildOutputDir "Servy.Manager.exe"
            "servy-cli.exe"     = Join-Path $cliBuildOutputDir "Servy.CLI.exe"
        }

        foreach ($item in $binMap.GetEnumerator()) {
            if (Test-Path $item.Value) {
                Copy-Item $item.Value -Destination (Join-Path $packageFolder $item.Name) -Force
            } else {
                throw "Missing binary required for package: $($item.Value)"
            }
        }

        # 2. Copy Shared DLL Dependencies
        $outDirs = @($buildOutputDir, $managerBuildOutputDir, $cliBuildOutputDir)
        foreach ($dir in $outDirs) {
            if (Test-Path $dir) {
                Copy-Item (Join-Path $dir "*.dll") -Destination $packageFolder -Force
            }
        }

        # 3. Securely include Task Scheduler hooks
        # Using Get-ChildItem to ensure -Exclude correctly recurses through subdirectories.
        $taskSource = Join-Path $scriptDir "taskschd"
        if (Test-Path $taskSource) {
            $taskDest = Join-Path $packageFolder "taskschd"
            [void](New-Item -Path $taskDest -ItemType Directory -Force)
            
            Get-ChildItem -Path $taskSource -Recurse -Exclude 'smtp-cred.xml','*.dat','*.log' |
                Copy-Item -Destination {
                    Join-Path $taskDest $_.FullName.Substring($taskSource.Length).TrimStart('\')
                } -Force

            # SECURITY AUDIT: Ensure no sensitive files leaked into the portable package
            $leaks = Get-ChildItem -Path $taskDest -Recurse -Include 'smtp-cred.xml','*.dat','*.log'
            if ($leaks) { 
                throw "SECURITY ERROR: Excluded files leaked into package: $($leaks.FullName -join ', ')" 
            }
        }

        # 4. Include PowerShell Module Artifacts
        $cliArtifacts = @("Servy.psm1", "Servy.psd1", "servy-module-examples.ps1")
        foreach ($art in $cliArtifacts) {
            $artPath = Join-Path $cliDir $art
            if (Test-Path $artPath) {
                Copy-Item $artPath -Destination $packageFolder -Force
            } else {
                throw "Missing module artifact: $artPath"
            }
        }

        # Remove PDB files to minimize package size
        Get-ChildItem $packageFolder -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

        # === CREATE ZIP ARCHIVE ===
        # Maximum compression settings for 7-Zip (LZMA2/Ultra)
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
        if ($process.ExitCode -ne 0) { throw "7z compression failed" }

        Write-Host "Success: $outputZip" -ForegroundColor Green
    }
    finally {
        # Always clean up the temporary staging folder regardless of success or failure
        Remove-ItemSafely -Path $packageFolder
    }

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
