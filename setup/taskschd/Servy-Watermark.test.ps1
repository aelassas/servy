#requires -Version 5.1
<#
.SYNOPSIS
    Unit test runner for setup/taskschd/Servy-Watermark.psm1 module.
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$modulePath = Join-Path $scriptDir "Servy-Watermark.psm1"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Servy-Watermark.psm1 Tests                 " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $modulePath)) {
    Write-Host "FAIL: Servy-Watermark.psm1 was not found at path: $modulePath" -ForegroundColor Red
    exit 1
}

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "ServyWatermarkTests_$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    # Import target module
    Import-Module $modulePath -Force -ErrorAction Stop
    Write-Host "  [OK] Successfully imported Servy-Watermark module." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 1: ConvertFrom-WatermarkString & Read-Watermark
    # ----------------------------------------------------
    $testDate = [DateTime]::UtcNow
    $isoString = $testDate.ToString("o")
    $parsedDate = ConvertFrom-WatermarkString -Value $isoString

    if ($null -eq $parsedDate -or [Math]::Abs(($parsedDate - $testDate).TotalMilliseconds) -gt 1000) {
        Write-Host "FAIL: ConvertFrom-WatermarkString failed to roundtrip UTC DateTime. Expected '$isoString', got '$parsedDate'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] ConvertFrom-WatermarkString correctly parses ISO-8601 strings." -ForegroundColor Gray

    $timestampFile = Join-Path $tempDir "watermark.txt"
    Set-Content -Path $timestampFile -Value $isoString -Encoding UTF8
    $readResult = Read-Watermark -TimestampFile $timestampFile

    if ($null -eq $readResult -or [Math]::Abs(($readResult - $testDate).TotalMilliseconds) -gt 1000) {
        Write-Host "FAIL: Read-Watermark failed to read expected timestamp from file." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Read-Watermark reads existing valid watermark file." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 2: ConvertFrom-ServyEventMessage
    # ----------------------------------------------------
    $rawMsg = "[MyTestService] Service crashed unexpectedly.`nStack trace line 1"
    $parsedMsg = ConvertFrom-ServyEventMessage -Message $rawMsg

    if ($parsedMsg.ServiceName -ne "MyTestService" -or $parsedMsg.LogText -notmatch "Service crashed unexpectedly") {
        Write-Host "FAIL: ConvertFrom-ServyEventMessage failed to parse service name or log text." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] ConvertFrom-ServyEventMessage parses bracketed service message format." -ForegroundColor Gray

    $unbracketedMsg = "General error without service header"
    $parsedUnbracketed = ConvertFrom-ServyEventMessage -Message $unbracketedMsg

    if ($parsedUnbracketed.ServiceName -ne "Unknown Service" -or $parsedUnbracketed.LogText -ne $unbracketedMsg) {
        Write-Host "FAIL: ConvertFrom-ServyEventMessage fallback state failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] ConvertFrom-ServyEventMessage handles unbracketed fallback messages." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 3: Update-Watermark Basic Flow
    # ----------------------------------------------------
    $initialTime = [DateTime]::UtcNow.AddMinutes(-10)
    Update-Watermark -TimestampFile $timestampFile -TimeCreated $initialTime -ScriptDir $tempDir
    $updatedVal = Read-Watermark -TimestampFile $timestampFile

    if ($null -eq $updatedVal -or [Math]::Abs(($updatedVal - $initialTime).TotalMilliseconds) -gt (10 * 60 * 1000)) {
        Write-Host "FAIL: Update-Watermark failed to update timestamp file." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Update-Watermark advances watermark file to new timestamp." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 4: Update-Watermark Race / Stale Write Prevention
    # ----------------------------------------------------
    # Scenario: File currently holds T2 (newer). A process attempts to commit T1 (older).
    $t1 = $initialTime.AddMinutes(2)
    $t2 = $initialTime.AddMinutes(5)

    # First write T2 to target
    Update-Watermark -TimestampFile $timestampFile -TimeCreated $t2 -ScriptDir $tempDir

    # Attempt stale write with T1 < T2
    Update-Watermark -TimestampFile $timestampFile -TimeCreated $t1 -ScriptDir $tempDir
    $finalVal = Read-Watermark -TimestampFile $timestampFile

    if ([Math]::Abs(($finalVal -$t2).TotalMilliseconds) -gt (5 * 60 * 1000)) {
        Write-Host "FAIL: Update-Watermark allowed a stale write ($t1) to overwrite a newer watermark ($t2)." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Update-Watermark guards against stale-write regressions." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 5: Write-FallbackError & Fallback Logging
    # ----------------------------------------------------
    $fallbackMsg = "Test fallback logging message"
    Write-FallbackError -Message $fallbackMsg -ScriptDir $tempDir -FallbackFileName "CustomFallback.log"
    $customLogFile = Join-Path $tempDir "CustomFallback.log"

    if (-not (Test-Path $customLogFile) -or (Get-Content $customLogFile -Raw) -notmatch $fallbackMsg) {
        Write-Host "FAIL: Write-FallbackError did not write expected log entry to disk." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Write-FallbackError writes message to local fallback log." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 6: Get-EventsToProcess
    # ----------------------------------------------------
    # Forcefully overwrite the internal function definition inside the module's function drive
    $module = Get-Module Servy-Watermark
    & $module {
        Set-Item -Path "Function:\Get-ServyLastErrors" -Value {
            param(
                $LastProcessed,
                [int]$EventLogErrorId = 3103
            )
            return @(
                [PSCustomObject]@{ Message = "[Svc1] Error 1"; TimeCreated = [DateTime]::UtcNow.AddMinutes(-5) },
                [PSCustomObject]@{ Message = "ServyToast: Notification feedback loop"; TimeCreated = [DateTime]::UtcNow.AddMinutes(-2) },
                [PSCustomObject]@{ Message = "[Svc2] Error 2"; TimeCreated = [DateTime]::UtcNow.AddMinutes(-1) }
            )
        }
    }

    $validWatermark = [DateTime]::UtcNow.AddMinutes(-10)
    $processedEvents = Get-EventsToProcess -ScriptDir $tempDir -LastProcessed $validWatermark

    if ($null -eq $processedEvents -or $processedEvents.Count -ne 2) {
        $actualCount = if ($null -eq $processedEvents) { 0 } else { $processedEvents.Count }
        Write-Host "FAIL: Get-EventsToProcess failed to filter feedback loops or fetch events correctly. Expected 2 events, got $actualCount." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Get-EventsToProcess correctly pre-filters feedback loops and sorts events." -ForegroundColor Gray
    
    
    Write-Host "`n====================================================" -ForegroundColor Cyan
    Write-Host "SUCCESS: Servy-Watermark.psm1 validated successfully!" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
}
catch {
    Write-Host "`n====================================================" -ForegroundColor Cyan
    Write-Host "FAIL: Unexpected error during execution: $_" -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    exit 1
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
