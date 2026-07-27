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

        # Verify string values are trimmed
        if ($val -ne $val.Trim()) {
            Write-Host "FAIL: Key '$key' has untrimmed whitespace: '$val'" -ForegroundColor Red
            exit 1
        }

        Write-Host "  [OK] $key = $val" -ForegroundColor Gray
    }

    Write-Host "SUCCESS: build-config.ps1 loaded and validated successfully!" -ForegroundColor Green
}
catch {
    Write-Host "FAIL: Unexpected error during execution: $_" -ForegroundColor Red
    exit 1
}
