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

.PARAMETER Fm
    The target framework moniker (TFM).

.PARAMETER Version
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
    [string]$Fm      = "net10.0",    
    [string]$Version = "4.5"
)

$Tfm = "$Fm-windows"

$ErrorActionPreference = "Stop"

$ScriptHadError = $false

try {
    # Record start time
    $StartTime = Get-Date

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
        Version = $Version
        Fm      = $Fm
    }

    <#
    # Build framework-dependent installer
    Invoke-Script -ScriptPath "publish-fd.ps1" -Params @{
        Version = $Version
        Fm      = $Fm
    }
    #>

    # Calculate and display elapsed time
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
