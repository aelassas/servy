#requires -Version 5.0
<#
.SYNOPSIS
    Sanity tests for the shared retry policy behind Invoke-BuildInstaller.

.DESCRIPTION
    Invoke-BuildInstaller in publish-common.ps1 drives ISCC.exe through Invoke-WithRetry,
    the single retry policy that lives in common-helpers.ps1 beside Assert-LastExitCode.
    These tests pin the three things a second, hand-rolled copy of that loop would be free
    to drift on: where the surviving policy lives, how many attempts it makes, and the
    failure message the caller finally sees.
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running publish-common.ps1 retry policy Tests      " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

$publishCommonPath = Join-Path $scriptDir "publish-common.ps1"
$commonHelpersPath = Join-Path $scriptDir "common-helpers.ps1"

foreach ($required in @($publishCommonPath, $commonHelpersPath)) {
    if (-not (Test-Path $required)) {
        Write-Host "FAIL: required script was not found at path: $required" -ForegroundColor Red
        exit 1
    }
}

# Every attempt the fake compiler makes appends one line here, so the attempt count is a
# file length rather than a timing observation.
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("servy-publish-common-tests-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

function Remove-TestArtifacts {
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

# Writes a stand-in for ISCC.exe that logs each invocation and fails the first
# $FailuresBeforeSuccess times. A negative count never succeeds.
function New-FakeInnoCompiler {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AttemptLog,
        [Parameter(Mandatory = $true)][int]$FailuresBeforeSuccess
    )

    $lines = @(
        'param()',
        "Add-Content -LiteralPath '$AttemptLog' -Value (`$args -join ' ')",
        "`$attempts = @(Get-Content -LiteralPath '$AttemptLog').Count",
        "if ($FailuresBeforeSuccess -lt 0 -or `$attempts -le $FailuresBeforeSuccess) { exit 1 }",
        'exit 0'
    )
    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
}

function Get-AttemptCount {
    param([Parameter(Mandatory = $true)][string]$AttemptLog)
    if (-not (Test-Path $AttemptLog)) { return 0 }
    return @(Get-Content -LiteralPath $AttemptLog).Count
}

try {
    . $publishCommonPath | Out-Null

    # --- Invoke-WithRetry is centralized in common-helpers.ps1 -----------------------
    # This is the duplication itself: publish-common.ps1 must reach the shared policy
    # rather than carry a private copy of the loop.

    # Arrange
    $retryCommand = Get-Command Invoke-WithRetry -ErrorAction SilentlyContinue

    # Act
    $retryHome = if ($null -ne $retryCommand) { $retryCommand.ScriptBlock.File } else { $null }

    # Assert
    if ($null -eq $retryCommand) {
        Write-Host "FAIL: Invoke-WithRetry is not available after dot-sourcing publish-common.ps1." -ForegroundColor Red
        exit 1
    }
    if ($retryHome -ne $commonHelpersPath) {
        Write-Host "FAIL: Invoke-WithRetry should be defined in '$commonHelpersPath', but it came from '$retryHome'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Invoke-WithRetry is defined once, in common-helpers.ps1." -ForegroundColor Gray

    if (Get-Content -LiteralPath $publishCommonPath -Raw | Select-String -SimpleMatch -Quiet 'MaxRetry') {
        Write-Host "FAIL: publish-common.ps1 still carries a private MaxRetry knob; the retry loop is duplicated again." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] publish-common.ps1 carries no private retry knob." -ForegroundColor Gray

    # --- A transient ISCC failure is retried and then succeeds -----------------------

    # Arrange
    $transientLog = Join-Path $tempDir "transient-attempts.txt"
    $transientCompiler = Join-Path $tempDir "iscc-transient.ps1"
    New-FakeInnoCompiler -Path $transientCompiler -AttemptLog $transientLog -FailuresBeforeSuccess 1

    # Act
    Invoke-BuildInstaller -InnoCompiler $transientCompiler -IssFile "servy.iss" -Version "1.2.3" | Out-Null

    # Assert
    $transientAttempts = Get-AttemptCount -AttemptLog $transientLog
    if ($transientAttempts -ne 2) {
        Write-Host "FAIL: a single transient failure should cost exactly 2 attempts, got $transientAttempts." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] A transient ISCC failure is retried once and then succeeds." -ForegroundColor Gray

    # --- A permanent ISCC failure gives up after the shared attempt count ------------
    # Invoke-WithRetry defaults to 3 attempts; the hand-rolled loop this replaced used
    # its own MaxRetry knob, and that is exactly the drift this asserts against.

    # Arrange
    $permanentLog = Join-Path $tempDir "permanent-attempts.txt"
    $permanentCompiler = Join-Path $tempDir "iscc-permanent.ps1"
    New-FakeInnoCompiler -Path $permanentCompiler -AttemptLog $permanentLog -FailuresBeforeSuccess -1
    $caught = $null

    # Act
    try {
        Invoke-BuildInstaller -InnoCompiler $permanentCompiler -IssFile "servy.iss" -Version "1.2.3" | Out-Null
    }
    catch {
        $caught = $_
    }

    # Assert
    if ($null -eq $caught) {
        Write-Host "FAIL: a permanently failing ISCC.exe should have thrown." -ForegroundColor Red
        exit 1
    }
    $permanentAttempts = Get-AttemptCount -AttemptLog $permanentLog
    if ($permanentAttempts -ne 3) {
        Write-Host "FAIL: the shared retry policy should make exactly 3 attempts, got $permanentAttempts." -ForegroundColor Red
        exit 1
    }
    $message = $caught.Exception.Message
    if ($message -notlike "*Inno Setup (ISCC.exe) failed*" -or $message -notlike "*Failed after 3 attempts*") {
        Write-Host "FAIL: the failure should carry the shared policy's message, got '$message'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] A permanent ISCC failure gives up after 3 attempts with the shared message." -ForegroundColor Gray

    Write-Host ""
    Write-Host "SUCCESS: publish-common.ps1 retry policy tests passed." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "FAIL: Unexpected error during execution: $_" -ForegroundColor Red
    exit 1
}
finally {
    Remove-TestArtifacts
}
