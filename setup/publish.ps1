<#
.SYNOPSIS
    Main build script for generating Servy installers (self-contained and optional framework-dependent).

.DESCRIPTION
    This script orchestrates the build process for Servy by invoking the internal
    publish scripts:
        - publish-sc.ps1   (self-contained bundle)
        - publish-fd.ps1   (framework-dependent bundle, currently optional)

    It ensures the build environment is prepared and passes versioning and
    framework parameters to child scripts.

.REQUIREMENTS
    1. MSBuild must be available in PATH.
    2. Inno Setup (ISCC.exe) installed and accessible.
    3. 7-Zip installed with `7z` available in PATH.

.PARAMETER fm
    The target framework moniker (TFM).

.PARAMETER version
    The Servy version being packaged.

.EXAMPLE
    PS> .\publish.ps1 -fm "net10.0" -version "3.8"

.NOTES
    This script can be run from any working directory. It calculates elapsed time
    and pauses at the end to allow double-click usage from Explorer.
#>

# publish.ps1
# Main setup bundle script for building both self-contained and framework-dependent installers

param(
    [string]$fm      = "net10.0",    
    [string]$version = "3.8"
)

$tfm = "$fm-windows"

$ErrorActionPreference = "Stop"

# Record start time
$startTime = Get-Date

# Script directory (so we can run from anywhere)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Invoke-Script {
    param(
        [string]$ScriptPath,
        [hashtable]$Params
    )

    $FullPath = Join-Path $ScriptDir $ScriptPath

    if (-not (Test-Path $FullPath)) {
        Write-Error "Script not found: $FullPath"
        exit 1
    }

    Write-Host "`n=== Running: $FullPath ==="
    & $FullPath @Params
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Script failed: $FullPath"
        exit $LASTEXITCODE
    }
}

# Build self-contained installer
Invoke-Script -ScriptPath "publish-sc.ps1" -Params @{
    version = $version
    fm      = $fm
}

<#
# Build framework-dependent installer
Invoke-Script -ScriptPath "publish-fd.ps1" -Params @{
    version = $version
    fm      = $fm
}
#>

# Calculate and display elapsed time
$elapsed = (Get-Date) - $startTime
Write-Host "`n=== Build complete in $($elapsed.ToString("hh\:mm\:ss")) ==="

# Pause by default (for double-click usage)
Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
