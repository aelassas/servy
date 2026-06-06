#requires -Version 5.0
<#
.SYNOPSIS
    Main build script for generating the Servy self-contained installer.

.DESCRIPTION
    This script orchestrates the build process for Servy by invoking the internal
    publish script:
        - publish-sc.ps1   (self-contained bundle)

.REQUIREMENTS
    1. MSBuild must be available in PATH.
    2. Inno Setup (ISCC.exe) installed and accessible.
    3. 7-Zip installed with `7z` available in PATH.

.PARAMETER Tfm
    The target framework moniker (TFM).

.PARAMETER Version
    The Servy version being packaged.

.EXAMPLE
    PS> .\publish.ps1 -Tfm "net10.0-windows" -Version "8.5"

.NOTES
    This script can be run from any working directory. It calculates elapsed time
    and pauses at the end to allow double-click usage from Explorer.
#>

# publish.ps1
# Main setup bundle script for building both self-contained and framework-dependent installers

param(
    [string]$Tfm     = "", 
    [string]$Version = ""
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

        # Reset before invocation so a stale exit code from earlier work cannot fool us.
        $global:LASTEXITCODE = 0

        try {
            & $fullPath @Params
        }
        catch {
            # $ErrorActionPreference='Stop' in the child will land here.
            throw "Script failed ($ScriptPath): $($_.Exception.Message)"
        }

        # Only meaningful if the child ended with a native command or an explicit 'exit N'.
        if ($LASTEXITCODE -ne 0) {
            throw "Script failed ($ScriptPath): native exit code $LASTEXITCODE"
        }
    }

    # Build self-contained installer
    Invoke-Script -ScriptPath "publish-sc.ps1" -Params @{
        Version = $Version
        Tfm      = $Tfm
    }

    # Calculate and display elapsed time
    $elapsed = (Get-Date) - $startTime
    Write-Host "`n=== Build complete in $($elapsed.ToString("hh\:mm\:ss")) ==="
}
catch {
    $scriptHadError = $true
    Write-Host "`nERROR OCCURRED:" -ForegroundColor Red
    Write-Host $_
    # ROBUSTNESS: Explicitly override the native global tracking variable immediately 
    # to guarantee failure persistence across subsequent inner catch/finally blocks.
    $global:LASTEXITCODE = 1
}
finally {
    # ROBUSTNESS: Detect if running in a non-interactive environment (CI pipeline, automated task).
    # If [Environment]::UserInteractive evaluates to false or no physical window is attached, 
    # bypass the ReadKey sequence entirely to prevent the process from hanging indefinitely.
    $isInteractive = [Environment]::UserInteractive -and 
                     ($Host.Name -eq 'ConsoleHost' -or $Host.Name -like '*Console*')

    if ($isInteractive) {
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
    else {
        # Log failure layout directly to the tracking stream for automated log monitoring parses
        if ($scriptHadError) {
            Write-Warning "Build execution terminated with errors. Non-zero exit code enforced for automation handler."
        }
    }

    # ROBUSTNESS: Ensure the orchestrator script exits cleanly with a non-zero code under a failure state.
    # This guarantees that automated CI tools (GitHub Actions, Azure DevOps) successfully detect the failure.
    if ($scriptHadError) {
        exit 1
    }
}