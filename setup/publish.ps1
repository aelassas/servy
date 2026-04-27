#requires -Version 5.0
# Main setup bundle script for .NET Framework build of Servy

$ErrorActionPreference = "Stop"
$scriptHadError = $false

# PS 2.0/3.0 compatible path resolution
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

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
    $rootDir                = (Resolve-Path (Join-Path $scriptDir "..")).Path
    $servyDir               = Join-Path $rootDir "src\Servy"
    $cliDir                 = Join-Path $rootDir "src\Servy.CLI"
    $managerDir             = Join-Path $rootDir "src\Servy.Manager"
    
    $buildOutputDir         = Join-Path $servyDir "bin\$platform\$buildConfig"
    $cliBuildOutputDir      = Join-Path $cliDir "bin\$platform\$buildConfig"
    $managerBuildOutputDir  = Join-Path $managerDir "bin\$platform\$buildConfig"
    
    # Do NOT use Resolve-Path here. It returns null/errors if the file is missing.
    $signPath               = Join-Path $scriptDir "signpath.ps1"

    $packageFolder          = "$appName-$version-$framework-$platform-portable"
    $outputZip              = "$packageFolder.7z"

    # === Tool Discovery ===
    try {
        # Import the resolution helper
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
        exit 1
    }

    # === Functions ===
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
    Write-Host "--- Restoring & Building ---" -ForegroundColor Cyan
    nuget restore "..\Servy.sln"
    Check-LastExitCode "NuGet restore failed"

    $projects = @(
        @{ Name="WPF"; Path="..\src\Servy\publish.ps1" },
        @{ Name="CLI"; Path="..\src\Servy.CLI\publish.ps1" },
        @{ Name="Manager"; Path="..\src\Servy.Manager\publish.ps1" }
    )

    foreach ($p in $projects) {
        $pPath = Join-Path $scriptDir $p.Path
        if (Test-Path $pPath) {
            Write-Host "Building $($p.Name)..."
            & $pPath -Version $version
            Check-LastExitCode "$($p.Name) build failed"
        }
    }

    # === BUILD & SIGN INSTALLER ===
    Write-Host "--- Installer Generation ---" -ForegroundColor Cyan

    $installerPath = Join-Path $scriptDir "servy-$version-net48-x64-installer.exe"

    # CRITICAL: Clean up the old installer first to break any existing file locks
    Remove-ItemSafely -Path $installerPath

    & "$innoCompiler" (Join-Path $scriptDir $issFile) /DMyAppVersion=$version /DMyAppPlatform=$framework
    Check-LastExitCode "Inno Setup failed"

    if (Test-Path $installerPath) {
        if (Test-Path $signPath) {
            Write-Host ">>> Signing Installer..." -ForegroundColor Yellow
            & $signPath $installerPath
            Check-LastExitCode "Signing failed"
        }
    } else {
        throw "Installer binary not found at $installerPath"
    }

    # === PREPARE PACKAGE FILES ===
    Write-Host "--- Packaging Portable ZIP ---" -ForegroundColor Cyan

    Remove-ItemSafely -Path $packageFolder
    Remove-ItemSafely -Path $outputZip

    # Atomic Packaging Block
    try {
        New-Item -ItemType Directory -Force -Path $packageFolder | Out-Null

        # Copy Core Binaries
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

        # Copy DLLs
        $outDirs = @($buildOutputDir, $managerBuildOutputDir, $cliBuildOutputDir)
        foreach ($dir in $outDirs) {
            if (Test-Path $dir) {
                Copy-Item (Join-Path $dir "*.dll") -Destination $packageFolder -Force
            }
        }

        # Task Scheduler hooks
        $taskSource = Join-Path $scriptDir "taskschd"
        if (Test-Path $taskSource) {
            $taskDest = Join-Path $packageFolder "taskschd"
            New-Item -Path $taskDest -ItemType Directory -Force | Out-Null
            Copy-Item "$taskSource\*" -Destination $taskDest -Recurse -Force -Exclude "smtp-cred.xml", "*.dat", "*.log"
        }

        # CLI Module Artifacts
        $cliArtifacts = @("Servy.psm1", "Servy.psd1", "servy-module-examples.ps1")
        foreach ($art in $cliArtifacts) {
            $artPath = Join-Path $cliDir $art
            if (Test-Path $artPath) {
                Copy-Item $artPath -Destination $packageFolder -Force
            } else {
                throw "Missing module artifact: $artPath"
            }
        }

        # Cleanup PDBs
        Get-ChildItem $packageFolder -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

        # === CREATE ZIP ===
        
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
        # Always clean up the temporary staging folder
        Remove-ItemSafely -Path $packageFolder
    }

    $elapsed = (Get-Date) - $startTime
    Write-Host "`n=== Build complete in $($elapsed.ToString("hh\:mm\:ss")) ==="

} catch {
    $scriptHadError = $true
    Write-Host "`nFATAL ERROR: $_" -ForegroundColor Red
    Write-Host "Error details: $($_.ScriptStackTrace)" -ForegroundColor Gray
} finally {
    if ($scriptHadError) { exit 1 }
    
    Write-Host "`nPress any key to exit..."
    if ($Host.Name -eq 'ConsoleHost') {
        [void][System.Console]::ReadKey($true)
    } else {
        Read-Host
    }
}