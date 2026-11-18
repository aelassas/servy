#Requires -Version 5.1

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Write-ServyLog.ps1 Tests                   " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# Locate Write-ServyLog.ps1 next to this script; each worker and Phase 2 dot-source it later
$ScriptDir = $PSScriptRoot
$LogScriptPath = Join-Path $ScriptDir "Write-ServyLog.ps1"

if (-not (Test-Path -LiteralPath $LogScriptPath)) {
    Write-Host "Error: Could not find Write-ServyLog.ps1 at $LogScriptPath" -ForegroundColor Red
    exit 1
}

# Define centralized test workload dimensions to eliminate magic number duplication
$WorkerCount      = 5
$WritesPerWorker  = 100

# Define test file boundaries
$TestLogPath = Join-Path $ScriptDir "test_output.log"
$MaxLogSize = 10240 # Force rotation quickly at a tiny 10 KB threshold
$DirRoot = Join-Path $ScriptDir "test_logdir_[1]" # Phase 4 creates it; the brackets are wildcard characters

# All four phases write their artifacts into $ScriptDir, so removing them is the suite's own
# responsibility on every exit path, not only before the next run starts.
function Remove-TestArtifacts {
    if (Test-Path -LiteralPath $TestLogPath) { Remove-Item -LiteralPath $TestLogPath -Force -ErrorAction SilentlyContinue }
    Get-ChildItem -LiteralPath $ScriptDir -Filter "test_output_*.log" -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem -LiteralPath $ScriptDir -Filter "test_prune*.log"   -ErrorAction SilentlyContinue | Remove-Item -Force
    if (Test-Path -LiteralPath $DirRoot) { Remove-Item -LiteralPath $DirRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

# Clear anything a previous run left behind before starting.
Write-Host "Preparing test environment..." -ForegroundColor Cyan
Remove-TestArtifacts

try {
    Write-Host "Spawning concurrent log writers via background jobs..." -ForegroundColor Cyan
    Write-Host "Target Log Path: $TestLogPath" -ForegroundColor DarkGray

    # Definition block passed directly down into the isolated background processes
    $WorkerScript = {
        param([string]$LogScript, [string]$FilePath, [int]$MaxSize, [int]$WorkerId, [int]$WritesCount)

        # Dot-source the logging mechanism inside each worker process
        . $LogScript

        # Rapidly blast messages to stress test locking and trigger rotation races
        $rng = [System.Random]::new()
        for ($i = 1; $i -le $WritesCount; $i++) {
            $Msg = "Worker {0:D2} | Payload Sequence {1:D3} | Testing Mutex Integrity" -f $WorkerId, $i
            Write-ServyLog -FilePath $FilePath -Message $Msg -MaxSizeBytes $MaxSize -MaxBackupFiles 0

            # Micro-sleep to vary execution interleaving slightly
            [System.Threading.Thread]::Sleep($rng.Next(1, 5))
        }
    }

    # Launch concurrent workers simultaneously based on centralized configurations
    $Jobs = @()
    for ($id = 1; $id -le $WorkerCount; $id++) {
        $Jobs += Start-Job -ScriptBlock $WorkerScript -ArgumentList $LogScriptPath, $TestLogPath, $MaxLogSize, $id, $WritesPerWorker
    }

    Write-Host "Waiting for all concurrent workers to complete processing..." -ForegroundColor Yellow
    $finished = $Jobs | Wait-Job -Timeout 120
    $finishedCount = 0
    if ($null -ne $finished) {
        $finishedCount = $finished.Count
    }

    if ($null -eq $finished -or $finishedCount -lt $Jobs.Count) {
        $runningCount = $Jobs.Count - $finishedCount
        Write-Host "FAIL: $runningCount worker job(s) still running after 120s - probable mutex deadlock in Write-ServyLog." -ForegroundColor Red
        $Jobs | Stop-Job
        $Jobs | Remove-Job -Force
        exit 1
    }

    # --- EVALUATION AND AUDIT PASS ---
    Write-Host "Analyzing log files for multi-process safety exceptions..." -ForegroundColor Cyan

    # Merge ALL background streams (Success, Error, Warning, etc.) into our data pipeline.
    # Job warnings are re-emitted on the host and do not survive *>&1 (measured on pwsh 7.6),
    # so collect them through -WarningVariable and append their messages to the audited set.
    $JobWarnings = @()
    $CapturedOutput = @($Jobs | Receive-Job *>&1 -WarningVariable JobWarnings -WarningAction SilentlyContinue) +
                      @($JobWarnings | ForEach-Object { $_.Message })

    # Extract warnings or errors matching our criteria
    $Warnings = $CapturedOutput | Where-Object { $_ -match "Servy Critical Logging Failure|Mutex timeout|abandoned log mutex" }

    if ($Warnings) {
        Write-Host "FAIL: Swallowed I/O exceptions or Mutex timeouts detected:" -ForegroundColor Red
        $Warnings | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkRed }
    } else {
        Write-Host "PASS: Zero unhandled or swallowed I/O serialization errors encountered." -ForegroundColor Green
    }

    # Clean up background jobs safely now that data collection has concluded
    $Jobs | Remove-Job -Force
    Write-Host "All background writers finished execution." -ForegroundColor Green
    Write-Host "--------------------------------------------------" -ForegroundColor Gray

    # Count total successfully written entries across active and rotated segments
    Write-Host "Auditing total written line counts..." -ForegroundColor Cyan
    $ActiveLines = 0
    if (Test-Path -LiteralPath $TestLogPath) {
        $ActiveLines = (Get-Content -LiteralPath $TestLogPath).Count
    }

    $RotatedLines = 0
    $RotatedFiles = Get-ChildItem -LiteralPath $ScriptDir -Filter "test_output_*.log"
    foreach ($file in $RotatedFiles) {
        $RotatedLines += (Get-Content -LiteralPath $file.FullName).Count
    }

    $TotalLines = $ActiveLines + $RotatedLines
    $ExpectedLines = $WorkerCount * $WritesPerWorker # Derived cleanly from centralized configuration variables

    Write-Host "  Active Log Lines:  $ActiveLines" -ForegroundColor Gray
    Write-Host "  Rotated Log Lines: $RotatedLines (Spread across $($RotatedFiles.Count) historical files)" -ForegroundColor Gray
    Write-Host "  Total Written:     $TotalLines / $ExpectedLines expected lines." -ForegroundColor White

    if ($TotalLines -ne $ExpectedLines -or $Warnings) {
        $Deficit = $ExpectedLines - $TotalLines
        if ($Deficit -gt 0) {
            Write-Host "FAIL: Missing data detected! Lost $Deficit log frames due to un-serialized write collisions." -ForegroundColor Red
        } elseif ($Deficit -lt 0) {
            Write-Host "FAIL: $(-$Deficit) more lines than the $ExpectedLines written - a write was duplicated." -ForegroundColor Red
        } else {
            Write-Host "FAIL: Total line count matches, but tracking execution warnings were emitted." -ForegroundColor Red
        }
        exit 1
    }

    Write-Host "SUCCESS: 100% of concurrent log entries were structurally preserved without line drops!" -ForegroundColor Green

    # --- PHASE 2: RETENTION PRUNING VERIFICATION ---
    Write-Host "`nTesting log retention pruning..." -ForegroundColor Cyan
    $PrunePath = Join-Path $ScriptDir "test_prune.log"

    . $LogScriptPath

    # Each write carries its iteration number, so the retained set can be identified rather than
    # only counted. Rotation N moves the file holding payload N, so the rotated segments carry
    # PRUNE-01..PRUNE-07 and the active file keeps PRUNE-08.
    # Arrange: a sibling log that matches the pruning glob (test_prune_*.log) but carries no rotation
    # stamp. It is by far the oldest file, so a prune that trusted the glob alone would delete it first (#4994).
    $SiblingPath = Join-Path $ScriptDir "test_prune_sibling.log"
    Set-Content -LiteralPath $SiblingPath -Value "sibling live log" -Encoding UTF8
    (Get-Item -LiteralPath $SiblingPath).LastWriteTime = (Get-Date).AddDays(-1)

    $PruneWrites   = 8
    $PruneBackups  = 3
    # Act: eight writes; every write after the first forces a rotation, then a prune down to $PruneBackups backups
    for ($i = 1; $i -le $PruneWrites; $i++) {
        $payload = ("PRUNE-{0:D2} " -f $i) + ("X" * 48)
        Write-ServyLog -FilePath $PrunePath -Message $payload -MaxSizeBytes 1 -MaxBackupFiles $PruneBackups

        # Keep LastWriteTime strictly increasing: the pruning sort is on LastWriteTime, so equal
        # stamps would make which-three-survive ambiguous and this assertion flaky.
        Start-Sleep -Milliseconds 120
    }

    # Assert: the sibling is not a rotated segment of test_prune.log, so pruning must leave it alone
    if (-not (Test-Path -LiteralPath $SiblingPath)) {
        Write-Host "FAIL: Retention pruning deleted 'test_prune_sibling.log', a file that only shares the log's name prefix and is not one of its rotated segments." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }
    Remove-Item -LiteralPath $SiblingPath -Force

    # The rotated filename embeds yyyyMMdd_HHmmss, so lexical order is chronological order.
    $backups = Get-ChildItem -LiteralPath $ScriptDir -Filter "test_prune_*.log" | Sort-Object Name
    if ($backups.Count -ne $PruneBackups) {
        Write-Host "FAIL: Expected $PruneBackups retained backups for pruning test, found $($backups.Count)." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # Retention must keep the NEWEST segments. Counting alone cannot see an inverted sort:
    # dropping -Descending in Write-ServyLog.ps1 keeps the three OLDEST and still leaves three files.
    $kept = $backups | ForEach-Object {
        $raw = Get-Content -LiteralPath $_.FullName -Raw
        if ($raw -match '(PRUNE-\d{2})') { $Matches[1] } else { "<no-payload:$($_.Name)>" }
    }
    $expected = @(($PruneWrites - $PruneBackups)..($PruneWrites - 1) | ForEach-Object { "PRUNE-{0:D2}" -f $_ })
    if (Compare-Object $kept $expected) {
        Write-Host "FAIL: Retention kept [$($kept -join ', ')] but should have kept the newest three [$($expected -join ', ')]." -ForegroundColor Red
        Write-Host "      A retention policy that keeps the oldest segments discards exactly the history an operator needs after an incident." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # The active log must survive pruning and hold the final payload; nothing else asserts that
    # the live file is not itself a pruning candidate.
    $activeRaw = if (Test-Path -LiteralPath $PrunePath) { Get-Content -LiteralPath $PrunePath -Raw } else { "" }
    $lastPayload = "PRUNE-{0:D2}" -f $PruneWrites
    if ($activeRaw -notmatch [regex]::Escape($lastPayload)) {
        Write-Host "FAIL: Active log '$([System.IO.Path]::GetFileName($PrunePath))' should hold $lastPayload after pruning." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    Write-Host "PASS: Retention pruning kept the newest $PruneBackups backups [$($expected -join ', ')] and preserved the active log." -ForegroundColor Green

    # --- PHASE 3: LINE FORMAT, ENCODING AND CULTURE VERIFICATION ---
    Write-Host "`nTesting log line format, encoding and culture invariance..." -ForegroundColor Cyan

    # Arrange: a fresh log, written under a culture whose default calendar is not Gregorian (#3920)
    $FormatPath = Join-Path $ScriptDir "test_prune_format.log"
    $inv = [System.Globalization.CultureInfo]::InvariantCulture
    $originalCulture = [System.Threading.Thread]::CurrentThread.CurrentCulture
    $utcBefore = (Get-Date).ToUniversalTime()
    try {
        [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('th-TH')

        # Act: one plain write, then one size-forced write so exactly one rotated segment exists
        Write-ServyLog -FilePath $FormatPath -Message "FORMAT-PROBE"
        $formatBytes = [System.IO.File]::ReadAllBytes($FormatPath)
        Write-ServyLog -FilePath $FormatPath -Message "FORMAT-ROTATE" -MaxSizeBytes 1 -MaxBackupFiles 0
    }
    finally {
        [System.Threading.Thread]::CurrentThread.CurrentCulture = $originalCulture
    }
    $utcAfter = (Get-Date).ToUniversalTime()

    # Assert: UTF-8 without a byte order mark (#2057)
    if ($formatBytes.Length -ge 3 -and $formatBytes[0] -eq 0xEF -and $formatBytes[1] -eq 0xBB -and $formatBytes[2] -eq 0xBF) {
        Write-Host "FAIL: The log file starts with a UTF-8 byte order mark; Write-ServyLog must write UTF-8 without one." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # Assert: one line, '[yyyy-MM-dd HH:mm:ss.fffZ] <message>' (#5880)
    $formatLine = [System.Text.Encoding]::UTF8.GetString($formatBytes).TrimEnd("`r", "`n")
    if ($formatLine -notmatch '^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})Z\] FORMAT-PROBE$') {
        Write-Host "FAIL: Log line '$formatLine' is not '[yyyy-MM-dd HH:mm:ss.fffZ] FORMAT-PROBE'." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # Assert: the stamp is the UTC time of the write, in the Gregorian calendar whatever the culture
    $stampStyles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
    $stamp = [datetime]::ParseExact($Matches[1], 'yyyy-MM-dd HH:mm:ss.fff', $inv, $stampStyles)
    if ($stamp -lt $utcBefore.AddSeconds(-1) -or $stamp -gt $utcAfter.AddSeconds(1)) {
        Write-Host "FAIL: Log stamp '$($Matches[1])Z' is not the UTC time of the write (between $($utcBefore.ToString('o', $inv)) and $($utcAfter.ToString('o', $inv)))." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # Assert: the rotated segment's name carries the Gregorian local date as well
    $expectedPrefix = "test_prune_format_" + (Get-Date).ToString('yyyy', $inv)
    $rotated = @(Get-ChildItem -LiteralPath $ScriptDir -Filter "test_prune_format_*.log")
    if ($rotated.Count -ne 1 -or -not $rotated[0].Name.StartsWith($expectedPrefix, [System.StringComparison]::Ordinal)) {
        Write-Host "FAIL: Expected one rotated segment named '$expectedPrefix...', found [$($rotated.Name -join ', ')]." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    Write-Host "PASS: Log lines are '[yyyy-MM-dd HH:mm:ss.fffZ] message' in UTC, UTF-8 without BOM, and culture-invariant." -ForegroundColor Green

    # --- PHASE 4: LOG DIRECTORY VERIFICATION ---
    Write-Host "`nTesting a missing log directory with wildcard characters in its name..." -ForegroundColor Cyan

    # Arrange: a log directory that does not exist yet and whose name holds wildcard characters (#7013)
    $DirLog    = Join-Path (Join-Path $DirRoot "logs") "nested.log"
    $DirLogDir = Split-Path $DirLog

    # Act: the first write has to create the directory; each later write forces a rotation,
    # then a prune down to one backup
    $dirWarnings = @()
    for ($i = 1; $i -le 4; $i++) {
        Write-ServyLog -FilePath $DirLog -Message ("DIR-{0:D2}" -f $i) -MaxSizeBytes 1 -MaxBackupFiles 1 -WarningVariable +dirWarnings -WarningAction SilentlyContinue
        Start-Sleep -Milliseconds 120
    }

    # Assert: no write was dropped (a missing directory surfaces as 'Servy Critical Logging Failure')
    if ($dirWarnings.Count -gt 0) {
        Write-Host "FAIL: Writing into a new log directory raised: $($dirWarnings -join '; ')" -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # Assert: rotation ran inside the bracketed directory, so the active log holds only the last write
    $dirActive = if (Test-Path -LiteralPath $DirLog) { Get-Content -LiteralPath $DirLog -Raw } else { "" }
    if ($dirActive -notmatch 'DIR-04' -or $dirActive -match 'DIR-0[123]') {
        Write-Host "FAIL: Active log in '$DirLogDir' should hold only DIR-04 but holds [$($dirActive.Trim())] - rotation did not run." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    # Assert: pruning ran inside the bracketed directory, so exactly the newest backup is left
    $dirBackups = @(Get-ChildItem -LiteralPath $DirLogDir -Filter "nested_*.log")
    if ($dirBackups.Count -ne 1 -or (Get-Content -LiteralPath $dirBackups[0].FullName -Raw) -notmatch 'DIR-03') {
        Write-Host "FAIL: Expected one backup holding DIR-03 in '$DirLogDir', found [$($dirBackups.Name -join ', ')] - pruning did not run." -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }

    Write-Host "PASS: A missing, bracketed log directory is created, rotated in and pruned." -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan

}
finally {
    # Runs on the success path and on every exit above, so the source tree is left clean.
    Remove-TestArtifacts
}

exit 0
