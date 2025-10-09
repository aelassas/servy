# publish.ps1
# Main setup bundle script for building both self-contained and framework-dependent installers
# Requirements:
#  1. Add msbuild to PATH
#  2. Inno Setup installed (ISCC.exe path updated if different)
#  3. 7-Zip installed and 7z in PATH

param(
    [string]$fm     = "net8.0",    
    [string]$version = "2.3"
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
