#requires -Version 5.0
<#
.SYNOPSIS
    Simple sanity test for build-config.ps1.
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$configPath = Join-Path $scriptDir "build-config.ps1"

Write-Host "Testing build-config.ps1..." -ForegroundColor Cyan

if (-not (Test-Path $configPath)) {
    Write-Host "FAIL: build-config.ps1 was not found at path: $configPath" -ForegroundColor Red
    exit 1
}

try {
    # Execute build-config.ps1
    $cfg = & $configPath

    if ($null -eq $cfg -or -not ($cfg -is [hashtable])) {
        Write-Host "FAIL: Output is null or not a hashtable." -ForegroundColor Red
        exit 1
    }

    # Verify required keys exist
    $requiredKeys = @('Version', 'Tfm', 'BuildConfiguration', 'Runtime')
    foreach ($key in $requiredKeys) {
        if (-not $cfg.ContainsKey($key)) {
            Write-Host "FAIL: Missing required key '$key' in build configuration." -ForegroundColor Red
            exit 1
        }

        $val = $cfg[$key]
        if ([string]::IsNullOrWhiteSpace($val)) {
            Write-Host "FAIL: Value for key '$key' is empty or null." -ForegroundColor Red
            exit 1
        }

        Write-Host "  [OK] $key = $val" -ForegroundColor Gray
    }

    # Verify values are in the form every consumer requires
    if ($cfg.Version -notmatch '^\d+\.\d+$') {
        Write-Host "FAIL: Version '$($cfg.Version)' is not Major.Minor; Assert-ServyVersion and publish.yml both reject it." -ForegroundColor Red
        exit 1
    }

    if ($cfg.Runtime -notin @('win-x64', 'win-arm64')) {
        Write-Host "FAIL: Runtime '$($cfg.Runtime)' is not one of win-x64 / win-arm64; publish-sc.ps1 would silently build x64." -ForegroundColor Red
        exit 1
    }

    if ($cfg.BuildConfiguration -notin @('Debug', 'Release')) {
        Write-Host "FAIL: BuildConfiguration '$($cfg.BuildConfiguration)' is not Debug / Release." -ForegroundColor Red
        exit 1
    }

    if ($cfg.Tfm -notmatch '^net\d+\.\d+-windows$') {
        Write-Host "FAIL: Tfm '$($cfg.Tfm)' does not look like a Windows TFM." -ForegroundColor Red
        exit 1
    }

    Write-Host "SUCCESS: build-config.ps1 loaded and validated successfully!" -ForegroundColor Green
}
catch {
    Write-Host "FAIL: Unexpected error during execution: $_" -ForegroundColor Red
    exit 1
}
