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

if (-not (Test-Path -LiteralPath $modulePath)) {
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

    $rawMsg = "[MyTestService] [EventID: 3003] Service crashed unexpectedly.`nStack trace line 1"
    $parsedMsg = ConvertFrom-ServyEventMessage -Message $rawMsg

    if ($parsedMsg.ServiceName -ne "MyTestService" -or $parsedMsg.LogText -ne $rawMsg) {
        Write-Host "FAIL: ConvertFrom-ServyEventMessage failed to parse service name or retain complete log text." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] ConvertFrom-ServyEventMessage parses service name and retains full bracketed message body." -ForegroundColor Gray

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
    # Arrange: a watermark file that holds an OLDER timestamp than the one being committed
    $olderTime = [DateTime]::UtcNow.AddMinutes(-10)
    $newerTime = $olderTime.AddMinutes(5)
    $advanceFile = Join-Path $tempDir "advance.txt"
    Set-Content -Path $advanceFile -Value $olderTime.ToString("o") -Encoding UTF8

    # Act
    Update-Watermark -TimestampFile $advanceFile -TimeCreated $newerTime -ScriptDir $tempDir
    $updatedVal = Read-Watermark -TimestampFile $advanceFile

    # Assert: the file now holds the NEWER timestamp (1 s tolerance for the ISO round-trip only)
    if ($null -eq $updatedVal -or [Math]::Abs(($updatedVal - $newerTime).TotalMilliseconds) -gt 1000) {
        Write-Host "FAIL: Update-Watermark failed to advance the timestamp file. Expected '$($newerTime.ToString("o"))', got '$updatedVal'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Update-Watermark advances watermark file to new timestamp." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 4: Update-Watermark Race / Stale Write Prevention
    # ----------------------------------------------------
    # Scenario: File currently holds T2 (newer). A process attempts to commit T1 (older).
    # Arrange
    $t1 = $olderTime.AddMinutes(2)
    $t2 = $olderTime.AddMinutes(5)
    $staleFile = Join-Path $tempDir "stale.txt"
    Update-Watermark -TimestampFile $staleFile -TimeCreated $t2 -ScriptDir $tempDir
    $seeded = Read-Watermark -TimestampFile $staleFile
    if ($null -eq $seeded -or [Math]::Abs(($seeded - $t2).TotalMilliseconds) -gt 1000) {
        Write-Host "FAIL: Update-Watermark did not seed the first-run watermark with T2." -ForegroundColor Red
        exit 1
    }

    # Act: attempt the stale write with T1 < T2
    Update-Watermark -TimestampFile $staleFile -TimeCreated $t1 -ScriptDir $tempDir
    $finalVal = Read-Watermark -TimestampFile $staleFile

    # Assert: T2 survives (the tolerance must stay far below the 3-minute T2-T1 gap)
    if ($null -eq $finalVal -or [Math]::Abs(($finalVal - $t2).TotalMilliseconds) -gt 1000) {
        Write-Host "FAIL: Update-Watermark allowed a stale write ($t1) to overwrite a newer watermark ($t2)." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Update-Watermark guards against stale-write regressions." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 4b: Update-Watermark heals a corrupt watermark file
    # ----------------------------------------------------
    # Arrange
    $corruptFile = Join-Path $tempDir "corrupt.txt"
    Set-Content -Path $corruptFile -Value "not-a-timestamp" -Encoding UTF8
    $healTime = [DateTime]::UtcNow.AddMinutes(-3)

    # Act
    Update-Watermark -TimestampFile $corruptFile -TimeCreated $healTime -ScriptDir $tempDir
    $healed = Read-Watermark -TimestampFile $corruptFile

    # Assert
    if ($null -eq $healed -or [Math]::Abs(($healed - $healTime).TotalMilliseconds) -gt 1000) {
        Write-Host "FAIL: Update-Watermark did not overwrite a corrupt watermark file (got '$healed')." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Update-Watermark heals a corrupt watermark file." -ForegroundColor Gray

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
    # Arrange: stub returns events in a deliberately non-chronological order. The events travel
    # in a global because the stub scriptblock runs later, outside this scope, and cannot see local variables.
    $base = [DateTime]::UtcNow
    $global:WatermarkTestEvents = @(
        [PSCustomObject]@{ Message = "[Svc2] Error 2"; TimeCreated = $base.AddMinutes(-1) },
        [PSCustomObject]@{ Message = "ServyToast: Notification feedback loop"; TimeCreated = $base.AddMinutes(-2) },
        [PSCustomObject]@{ Message = "ServyFailureEmail: Notification feedback loop"; TimeCreated = $base.AddMinutes(-3) },
        [PSCustomObject]@{ Message = "Servy Notification Error: Notification feedback loop"; TimeCreated = $base.AddMinutes(-4) },
        [PSCustomObject]@{ Message = "[Svc1] Error 1"; TimeCreated = $base.AddMinutes(-5) }
    )
    # Forcefully overwrite the internal function definition inside the module's function drive
    $module = Get-Module Servy-Watermark
    & $module {
        Set-Item -Path "Function:\Get-ServyLastErrors" -Value {
            param(
                $LastProcessed,
                [int]$EventLogErrorId = 3103
            )
            return $global:WatermarkTestEvents
        }
    }

    # Act: normal run with a valid watermark
    $validWatermark = $base.AddMinutes(-10)
    $processedEvents = @(Get-EventsToProcess -ScriptDir $tempDir -LastProcessed $validWatermark)

    # Assert: both genuine events survive, the feedback event is dropped, oldest first
    $names = ($processedEvents | ForEach-Object { $_.Message }) -join '|'
    if ($names -ne "[Svc1] Error 1|[Svc2] Error 2") {
        Write-Host "FAIL: Get-EventsToProcess returned '$names'; expected '[Svc1] Error 1|[Svc2] Error 2' (feedback event dropped, chronological order)." -ForegroundColor Red
        exit 1
    }

    # Act: first run (no watermark)
    $firstRun = @(Get-EventsToProcess -ScriptDir $tempDir -LastProcessed $null)

    # Assert: only the newest valid event is returned
    if ($firstRun.Count -ne 1 -or $firstRun[0].Message -ne "[Svc2] Error 2") {
        Write-Host "FAIL: Get-EventsToProcess first run should return only '[Svc2] Error 2'." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Get-EventsToProcess correctly pre-filters feedback loops and sorts events." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 7: Get-ServyLastErrors event-ID filter (#7539)
    # ----------------------------------------------------
    # Arrange: load the real query function into this scope and shadow Get-WinEvent with a stub that
    # returns Level 2 'Servy' events on both sides of each edge of the 3000-3199 error ranges.
    . (Join-Path $scriptDir "Get-ServyLastErrors.ps1")
    $global:WatermarkTestWinEvents = @(
        [PSCustomObject]@{ Id = 0;    Message = "[Svc] id 0";    TimeCreated = $base.AddMinutes(-8) },
        [PSCustomObject]@{ Id = 2999; Message = "[Svc] id 2999"; TimeCreated = $base.AddMinutes(-7) },
        [PSCustomObject]@{ Id = 3000; Message = "[Svc] id 3000"; TimeCreated = $base.AddMinutes(-6) },
        [PSCustomObject]@{ Id = 3001; Message = "[Svc] id 3001"; TimeCreated = $base.AddMinutes(-5) },
        [PSCustomObject]@{ Id = 3103; Message = "[Svc] id 3103"; TimeCreated = $base.AddMinutes(-4) },
        [PSCustomObject]@{ Id = 3199; Message = "[Svc] id 3199"; TimeCreated = $base.AddMinutes(-3) },
        [PSCustomObject]@{ Id = 3200; Message = "[Svc] id 3200"; TimeCreated = $base.AddMinutes(-2) },
        [PSCustomObject]@{ Id = 1;    Message = "[Svc] id 1";    TimeCreated = $base.AddMinutes(-1) }
    )
    function Get-WinEvent {
        [CmdletBinding()]
        param([hashtable]$FilterHashtable, [int]$MaxEvents)
        return $global:WatermarkTestWinEvents
    }

    # Act: a normal run (watermark set) and a first run (no watermark)
    $normalIds = (@(Get-ServyLastErrors -LastProcessed $base.AddMinutes(-10)) | ForEach-Object { $_.Id }) -join ','
    $firstRunIds = (@(Get-ServyLastErrors -LastProcessed $null) | ForEach-Object { $_.Id }) -join ','

    # Assert: only the IDs inside 3000-3199 are returned, on both paths
    if ($normalIds -ne "3000,3001,3103,3199" -or $firstRunIds -ne "3000,3001,3103,3199") {
        Write-Host "FAIL: Get-ServyLastErrors returned event IDs '$normalIds' (normal run) and '$firstRunIds' (first run); expected '3000,3001,3103,3199' for both." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Get-ServyLastErrors keeps only the Servy error event IDs (3000-3199)." -ForegroundColor Gray

    # ----------------------------------------------------
    # Test 8: Get-EventsToProcess per-run cap (#7539)
    # ----------------------------------------------------
    # Arrange: thirteen genuine events, three over the default cap of 10, served newest first by the
    # Get-ServyLastErrors stub installed in Test 6.
    $global:WatermarkTestEvents = @(1..13 | Sort-Object -Descending | ForEach-Object {
        [PSCustomObject]@{ Message = "[Svc$_] Error $_"; TimeCreated = $base.AddMinutes(-20 + $_) }
    })
    $capLogFile = Join-Path $tempDir "ServyWatermarkErrors.log"
    if (Test-Path -LiteralPath $capLogFile) { Remove-Item -LiteralPath $capLogFile -Force }

    # Act
    $capped = @(Get-EventsToProcess -ScriptDir $tempDir -LastProcessed $base.AddMinutes(-30) 3>$null)
    $explicitCap = @(Get-EventsToProcess -ScriptDir $tempDir -LastProcessed $base.AddMinutes(-30) -MaxEventsPerRun 2 3>$null)

    # Assert: the newest ten survive, oldest first, the three oldest are skipped and the skip is logged
    $cappedNames = ($capped | ForEach-Object { $_.Message }) -join '|'
    $expectedNames = (4..13 | ForEach-Object { "[Svc$_] Error $_" }) -join '|'
    if ($cappedNames -ne $expectedNames) {
        Write-Host "FAIL: Get-EventsToProcess returned '$cappedNames'; expected the newest ten events oldest first: '$expectedNames'." -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path -LiteralPath $capLogFile) -or (Get-Content -LiteralPath $capLogFile -Raw) -notmatch 'skipping the 3 oldest') {
        Write-Host "FAIL: Get-EventsToProcess did not record the three skipped events in ServyWatermarkErrors.log." -ForegroundColor Red
        exit 1
    }
    if ((($explicitCap | ForEach-Object { $_.Message }) -join '|') -ne "[Svc12] Error 12|[Svc13] Error 13") {
        Write-Host "FAIL: Get-EventsToProcess -MaxEventsPerRun 2 did not return only the newest two events." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Get-EventsToProcess caps a run at the newest events and logs the skipped ones." -ForegroundColor Gray


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
    Remove-Variable -Name WatermarkTestEvents -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name WatermarkTestWinEvents -Scope Global -ErrorAction SilentlyContinue
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
