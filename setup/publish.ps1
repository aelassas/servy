#requires -Version 5.0
<#
.SYNOPSIS
    Main build script for generating Servy installers (self-contained and optional framework-dependent).

.DESCRIPTION
    This script orchestrates the build process for Servy by invoking the internal
    publish scripts:
        - publish-sc.ps1   (self-contained bundle)
        - publish-fd.ps1   (framework-dependent bundle, optional via switch)

    It ensures the build environment is prepared and passes versioning and
    framework parameters to child scripts.

.REQUIREMENTS
    1. MSBuild must be available in PATH.
    2. Inno Setup (ISCC.exe) installed and accessible.
    3. 7-Zip installed with `7z` available in PATH.

.PARAMETER Tfm
    The target framework moniker (TFM).

.PARAMETER Version
    The Servy version being packaged.

.PARAMETER IncludeFrameworkDependent
    Opt-in switch to also build the framework-dependent (FD) installer. By default, 
    only the self-contained (SC) installer is built.

.EXAMPLE
    PS> .\publish.ps1 -Tfm "net10.0-windows" -Version "8.4" -IncludeFrameworkDependent

.NOTES
    This script can be run from any working directory. It calculates elapsed time
    and pauses at the end to allow double-click usage from Explorer.
#>

# publish.ps1
# Main setup bundle script for building both self-contained and framework-dependent installers

param(
    [string]$Tfm     = "", 
    [string]$Version = "",
    [switch]$IncludeFrameworkDependent
)

$ErrorActionPreference = "Stop"

# Load central defaults
$configPath = Join-Path $PSScriptRoot "build-config.ps1"
if (Test-Path $configPath) {
    $buildConfig = & $configPath
    if (-not $Tfm) { $Tfm = $buildConfig.Tfm }
    if (-not $Version) { $Version = $buildConfig.Version }
} else {
    throw "Central build configuration not found at $configPath"
}

# Validate version format after defaults are applied
if ($Version -notmatch "^\d+\.\d+$") {
    throw "Version must match pattern '^\d+\.\d+$'. Provided: '$Version'"
}

$scriptHadError = $false

try {
    # Record start time
    $startTime = Get-Date

    # Script directory (so we can run from anywhere)
    $scriptDir = $PSScriptRoot

    function Invoke-Script {
        param(
            [string]$ScriptPath,
            [hashtable]$Params
        )

        $fullPath = Join-Path $scriptDir $ScriptPath

        if (-not (Test-Path $fullPath)) {
            Write-Error "Script not found: $fullPath"
            return
        }

        Write-Host "`n=== Running: $fullPath ==="
        & $fullPath @Params
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Script failed: $fullPath"
        }
    }

    # Build self-contained installer
    Invoke-Script -ScriptPath "publish-sc.ps1" -Params @{
        Version = $Version
        Tfm      = $Tfm
    }

    # Build framework-dependent installer (Opt-in)
    if ($IncludeFrameworkDependent) {
        Invoke-Script -ScriptPath "publish-fd.ps1" -Params @{
            Version = $Version
            Tfm      = $Tfm
        }
    }

    # Calculate and display elapsed time
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