#Requires -Version 2.0
<#
.SYNOPSIS
    Unit tests for $script:EnvVarValidationPattern in Servy.psm1.

.DESCRIPTION
    Tests valid and invalid environment variable string formats against
    $script:EnvVarValidationPattern, including edge cases like spaces in keys,
    leading/trailing whitespace, multi-space separators, and escaped characters.

.NOTES
    Compatible with PowerShell 2.0 and later.
#>

# ----------------------------------------------------------------
# Load Validation Pattern
# ----------------------------------------------------------------
$scriptRoot = if ($PSVersionTable.PSVersion.Major -ge 3) {
    $PSScriptRoot
} else {
    Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$scriptPath = Join-Path $scriptRoot "Servy.psm1"

if (Test-Path $scriptPath) {
    # Extract $script:EnvVarValidationPattern directly from Servy.psm1
    $patternLine = Get-Content $scriptPath | Where-Object { $_ -match '\$script:EnvVarValidationPattern\s*=' }
    if ($patternLine) {
        Invoke-Expression ($patternLine -join "`n")
    }
}

# Require the pattern to be loaded directly from Servy.psm1
if ([string]::IsNullOrEmpty($script:EnvVarValidationPattern)) {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host " FATAL ERROR: Failed to load `$script:EnvVarValidationPattern!" -ForegroundColor Red
    Write-Host " Could not locate or parse the pattern definition inside:" -ForegroundColor Red
    Write-Host " '$scriptPath'" -ForegroundColor Yellow
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host ""
    exit 1
}

# ----------------------------------------------------------------
# Test Harness Helper
# ----------------------------------------------------------------
$script:TotalTests = 0
$script:PassedTests = 0
$script:FailedTests = 0

function Test-EnvVarPattern {
    param (
        [string]$InputString,
        [bool]$ExpectedMatch,
        [string]$Description
    )

    $script:TotalTests++
    $isMatch = $InputString -match $script:EnvVarValidationPattern

    if ($isMatch -eq $ExpectedMatch) {
        $script:PassedTests++
        Write-Host "  [PASS] $Description" -ForegroundColor Green
    }
    else {
        $script:FailedTests++
        Write-Host "  [FAIL] $Description" -ForegroundColor Red
        Write-Host "         Input    : '$InputString'" -ForegroundColor Yellow
        Write-Host "         Expected : $ExpectedMatch" -ForegroundColor Yellow
        Write-Host "         Actual   : $isMatch" -ForegroundColor Yellow
    }
}

# ----------------------------------------------------------------
# Test Suites
# ----------------------------------------------------------------

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Servy EnvVarValidationPattern Tests    " -ForegroundColor Cyan
Write-Host " Pattern: $script:EnvVarValidationPattern" -ForegroundColor DarkGray
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Basic Valid Strings ---
Write-Host "[1] Basic Valid Formats" -ForegroundColor Yellow
Test-EnvVarPattern -InputString "KEY=VALUE" -ExpectedMatch $true -Description "Single KEY=VALUE record"
Test-EnvVarPattern -InputString "A=1;B=2" -ExpectedMatch $true -Description "Multiple records with semicolon"
Test-EnvVarPattern -InputString "A=1;B=2;" -ExpectedMatch $true -Description "Trailing semicolon"
Test-EnvVarPattern -InputString "KEY=" -ExpectedMatch $true -Description "Key with empty value"
Write-Host ""

# --- 2. Whitespace & Spaces (Issue #5012 Fixes) ---
Write-Host "[2] Whitespace & Spaces Handling (Issue #5012)" -ForegroundColor Yellow
Test-EnvVarPattern -InputString "MY VAR=x" -ExpectedMatch $true -Description "Keys containing spaces (e.g. 'MY VAR=x')"
Test-EnvVarPattern -InputString " A=1" -ExpectedMatch $true -Description "Leading whitespace in string"
Test-EnvVarPattern -InputString "A=1 " -ExpectedMatch $true -Description "Trailing whitespace in string"
Test-EnvVarPattern -InputString "  KEY = VALUE ; OTHER = 123  " -ExpectedMatch $true -Description "Padded keys, values, and semicolons"
Test-EnvVarPattern -InputString 'A=1;   B=2' -ExpectedMatch $true -Description "Multi-space separator (e.g. ';   ')"
Test-EnvVarPattern -InputString "A=1; B=2; C=3" -ExpectedMatch $true -Description "Standard space after semicolon"
Write-Host ""

# --- 3. Escaped Characters ---
Write-Host "[3] Escaped Character Handling" -ForegroundColor Yellow
Test-EnvVarPattern -InputString 'KEY=VAL\;UE' -ExpectedMatch $true -Description 'Escaped semicolon in value (\;)'
# Note: \= and \" illustrate escaping grammar compatibility alongside [^;]
Test-EnvVarPattern -InputString 'KEY=VAL\=UE' -ExpectedMatch $true -Description 'Escaped equals sign in value (\=)'
Test-EnvVarPattern -InputString 'KEY=VAL\"UE' -ExpectedMatch $true -Description 'Escaped quote in value (\")'
Test-EnvVarPattern -InputString 'KEY=VAL\\\\UE' -ExpectedMatch $true -Description 'Escaped backslash in value (\\\\)'
Test-EnvVarPattern -InputString 'K1=V1\;;K2=V2' -ExpectedMatch $true -Description "Escaped semicolon preceding record separator"
# Discriminate load-bearing interaction between \\\\ and record separators (\\ followed by ;)
Test-EnvVarPattern -InputString 'K1=a\\;b' -ExpectedMatch $false -Description 'Escaped backslash then separator: key-less second record must be rejected'
Test-EnvVarPattern -InputString 'K1=a\\;K2=b' -ExpectedMatch $true -Description 'Escaped backslash immediately before a record separator'
Write-Host ""

# --- 4. Invalid Formats (Should Fail) ---
Write-Host "[4] Invalid Formats" -ForegroundColor Yellow
Test-EnvVarPattern -InputString "NO_EQUALS_SIGN" -ExpectedMatch $false -Description "String without equals sign"
Test-EnvVarPattern -InputString "=VALUE_WITHOUT_KEY" -ExpectedMatch $false -Description "Missing key before equals sign"
Test-EnvVarPattern -InputString "A=1;;B=2" -ExpectedMatch $false -Description "Double semicolon separator (;;)"
Test-EnvVarPattern -InputString ";A=1" -ExpectedMatch $false -Description "Leading unescaped semicolon"
Test-EnvVarPattern -InputString "A=1; B" -ExpectedMatch $false -Description "Second record missing equals sign"
Write-Host ""

# --- 5. Backtracking Guard (regression test for #1091) ---
Write-Host "[5] Catastrophic Backtracking Guard (#1091)" -ForegroundColor Yellow
$script:TotalTests++
if ($script:EnvVarValidationPattern -match '\(\?>') {
    $script:PassedTests++
    Write-Host "  [PASS] Atomic groups (?>...) present in regex pattern" -ForegroundColor Green
} else {
    $script:FailedTests++
    Write-Host "  [FAIL] Atomic groups (?>...) missing from regex pattern - potential ReDoS regression (#1091)" -ForegroundColor Red
}

$evil = "KEY=" + ("\" * 40) + [char]1
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$null = $evil -match $script:EnvVarValidationPattern
$sw.Stop()
$script:TotalTests++
if ($sw.ElapsedMilliseconds -lt 1000) {
    $script:PassedTests++
    Write-Host "  [PASS] Pathological escape run evaluated in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
} else {
    $script:FailedTests++
    Write-Host "  [FAIL] Pathological escape run took $($sw.ElapsedMilliseconds)ms - atomic groups may have been removed" -ForegroundColor Red
}
Write-Host ""

# ----------------------------------------------------------------
# Summary Output
# ----------------------------------------------------------------
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Test Summary" -ForegroundColor Cyan
Write-Host " Total   : $script:TotalTests" -ForegroundColor Gray
Write-Host " Passed  : $script:PassedTests" -ForegroundColor Green

if ($script:FailedTests -gt 0) {
    Write-Host " Failed  : $script:FailedTests" -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    exit 1
} else {
    Write-Host " Failed  : 0" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
    exit 0
}
