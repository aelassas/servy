#requires -Version 5.0
<#
.SYNOPSIS
    Shared build and publish utilities for Servy projects.

.DESCRIPTION
    Provides standard functions to check exit codes and invoke project publishing,
    ensuring DRY compliance across all project scripts.
#>

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot

# Import helpers
. (Join-Path $scriptDir "common-helpers.ps1")

function Invoke-StandardPublish {
    param(
        [Parameter(Mandatory=$true)][string]$ProjectDir,
        [Parameter(Mandatory=$true)][string]$ProjectName,
        [string]$Tfm = "net10.0-windows",
        [string]$Runtime = "win-x64",
        [string]$BuildConfiguration = "Release",
        [switch]$FrameworkDependent
    )

    # Step 0: Publish resources if script exists
    $resSuffix = if ($BuildConfiguration -eq "Debug") { "debug" } else { "release" }
    $publishResScript = Join-Path $ProjectDir "publish-res-$resSuffix.ps1"
    
    if (Test-Path $publishResScript) {
        Write-Host "=== Running publish-res-$resSuffix.ps1 ===" -ForegroundColor Cyan
        & $publishResScript -Tfm $Tfm
        Assert-LastExitCode "publish-res-$resSuffix.ps1 failed"
        Write-Host "=== Completed publish-res-$resSuffix.ps1 ===`n"
    }

    # Step 1: Build and Publish
    $projectPath = Join-Path $ProjectDir "$ProjectName.csproj"
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found: $projectPath"
        return
    }

    Write-Host "=== Publishing $ProjectName.csproj ===" -ForegroundColor Cyan
    Write-Host "Target Framework : $Tfm"
    Write-Host "Configuration    : $BuildConfiguration"
    Write-Host "Runtime          : $Runtime"

    & dotnet restore $projectPath -r $Runtime
    Assert-LastExitCode "dotnet restore failed"

    & dotnet clean $projectPath -c $BuildConfiguration
    Assert-LastExitCode "Project clean failed"

    if ($FrameworkDependent) {
        & dotnet publish $projectPath `
            -c $BuildConfiguration `
            -r $Runtime `
            --self-contained false `
            --no-restore `
            --nologo `
            --verbosity minimal `
            /p:PublishSingleFile=false `
            /p:IncludeAllContentForSelfExtract=true `
            /p:PublishTrimmed=false `
            /p:DebugType=None `
            /p:DebugSymbols=false `
            /p:CopyOutputSymbolsToPublishDirectory=false `
            /p:CopyCommandLineArguments=false `
            /p:ErrorOnDuplicatePublishOutputFiles=true `
            /p:UseAppHost=true `
            /p:Clean=true `
            /p:DeleteExistingFiles=true
    } else {
        & dotnet publish $projectPath `
            -c $BuildConfiguration `
            -r $Runtime `
            --force `
            /p:DeleteExistingFiles=true
    }
    Assert-LastExitCode "dotnet publish failed"

    # Step 2: Sign the published executable if signing is enabled
    if ($BuildConfiguration -eq "Release") {
        $signPath = Join-Path $PSScriptRoot "signpath.ps1"
        $publishFolder = Join-Path $ProjectDir "bin\$BuildConfiguration\$Tfm\$Runtime\publish"
        $exePath       = Join-Path $publishFolder "$ProjectName.exe"

        if (Test-Path $exePath) {
            if (Test-Path $signPath) {
                Write-Host "=== Signing $ProjectName.exe ===" -ForegroundColor Cyan
                & $signPath $exePath
                Assert-LastExitCode "Code signing failed"
            } else {
                Write-Warning "SignPath script not found at: $signPath. Signing will be skipped."
            }
        } else {
            Write-Error "Published executable not found at: $exePath. Ensure TFM and Runtime variables match the project output."
        }
    }

    Write-Host "=== $ProjectName.csproj published successfully ===" -ForegroundColor Green
}