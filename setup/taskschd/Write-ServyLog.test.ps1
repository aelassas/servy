#Requires -Version 5.1

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Write-ServyLog.ps1 Tests              " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# Ensure the log function is loaded in the main script thread
$ScriptDir = $PSScriptRoot
$LogScriptPath = Join-Path $ScriptDir "Write-ServyLog.ps1"

if (-not (Test-Path $LogScriptPath)) {
    Write-Host "Error: Could not find Write-ServyLog.ps1 at $LogScriptPath" -ForegroundColor Red
    exit 1
}

# Define centralized test workload dimensions to eliminate magic number duplication
$WorkerCount      = 5
$WritesPerWorker  = 100

# Define test file boundaries
$TestLogPath = Join-Path $ScriptDir "test_output.log"
$MaxLogSize = 10240 # Force rotation quickly at a tiny 10 KB threshold

# Clean up remnants from prior test runs
Write-Host "Preparing test environment..." -ForegroundColor Cyan
if (Test-Path $TestLogPath) { Remove-Item $TestLogPath -Force }
Get-ChildItem -Path $ScriptDir -Filter "test_output_*.log" | Remove-Item -Force

Write-Host "Spawning concurrent log writers via background jobs..." -ForegroundColor Cyan
Write-Host "Target Log Path: $TestLogPath" -ForegroundColor DarkGray

# Definition block passed directly down into the isolated background processes
$WorkerScript = {
    param([string]$LogScript, [string]$FilePath, [int]$MaxSize, [int]$WorkerId, [int]$WritesCount)

    # Dot-source the logging mechanism inside the unique worker thread scope
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

# Merge ALL background streams (Success, Error, Warning, etc.) into our data pipeline
$CapturedOutput = $Jobs | Receive-Job *>&1

# Extract warnings or errors matching our criteria
$Warnings = $CapturedOutput | Where-Object { $_ -match "Servy Critical Logging Failure|Mutex timeout" }

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

# 2. Count total successfully written entries across active and rotated segments
Write-Host "Auditing total written line counts..." -ForegroundColor Cyan
$ActiveLines = 0
if (Test-Path $TestLogPath) {
    $ActiveLines = (Get-Content $TestLogPath).Count
}

$RotatedLines = 0
$RotatedFiles = Get-ChildItem -Path $ScriptDir -Filter "test_output_*.log"
foreach ($file in $RotatedFiles) {
    $RotatedLines += (Get-Content $file.FullName).Count
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
    } else {
        Write-Host "FAIL: Total line count matches, but tracking execution warnings were emitted." -ForegroundColor Red
    }
    exit 1
}

Write-Host "SUCCESS: 100% of concurrent log entries were structurally preserved without line drops!" -ForegroundColor Green

# --- PHASE 2: RETENTION PRUNING VERIFICATION ---
Write-Host "`nTesting log retention pruning..." -ForegroundColor Cyan
$PrunePath = Join-Path $ScriptDir "test_prune.log"
Get-ChildItem -Path $ScriptDir -Filter "test_prune*.log" -ErrorAction SilentlyContinue | Remove-Item -Force

. $LogScriptPath

# Each write carries its iteration number, so the retained set can be identified rather than
# only counted. Rotation N moves the file holding payload N, so the rotated segments carry
# PRUNE-01..PRUNE-07 and the active file keeps PRUNE-08.
$PruneWrites   = 8
$PruneBackups  = 3
for ($i = 1; $i -le $PruneWrites; $i++) {
    $payload = ("PRUNE-{0:D2} " -f $i) + ("X" * 48)
    Write-ServyLog -FilePath $PrunePath -Message $payload -MaxSizeBytes 1 -MaxBackupFiles $PruneBackups

    # Keep LastWriteTime strictly increasing: the pruning sort is on LastWriteTime, so equal
    # stamps would make which-three-survive ambiguous and this assertion flaky.
    Start-Sleep -Milliseconds 20
}

# The rotated filename embeds yyyyMMdd-HHmmss-fff, so lexical order is chronological order.
$backups = Get-ChildItem -Path $ScriptDir -Filter "test_prune_*.log" | Sort-Object Name
if ($backups.Count -ne $PruneBackups) {
    Write-Host "FAIL: Expected $PruneBackups retained backups for pruning test, found $($backups.Count)." -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    Get-ChildItem -Path $ScriptDir -Filter "test_prune*.log" -ErrorAction SilentlyContinue | Remove-Item -Force
    exit 1
}

# Retention must keep the NEWEST segments. Counting alone cannot see an inverted sort:
# dropping -Descending in Write-ServyLog.ps1 keeps the three OLDEST and still leaves three files.
$kept = $backups | ForEach-Object {
    $raw = Get-Content $_.FullName -Raw
    if ($raw -match '(PRUNE-\d{2})') { $Matches[1] } else { "<no-payload:$($_.Name)>" }
}
$expected = @(($PruneWrites - $PruneBackups)..($PruneWrites - 1) | ForEach-Object { "PRUNE-{0:D2}" -f $_ })
if (Compare-Object $kept $expected) {
    Write-Host "FAIL: Retention kept [$($kept -join ', ')] but should have kept the newest three [$($expected -join ', ')]." -ForegroundColor Red
    Write-Host "      A retention policy that keeps the oldest segments discards exactly the history an operator needs after an incident." -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    Get-ChildItem -Path $ScriptDir -Filter "test_prune*.log" -ErrorAction SilentlyContinue | Remove-Item -Force
    exit 1
}

# The active log must survive pruning and hold the final payload; nothing else asserts that
# the live file is not itself a pruning candidate.
$activeRaw = if (Test-Path $PrunePath) { Get-Content $PrunePath -Raw } else { "" }
$lastPayload = "PRUNE-{0:D2}" -f $PruneWrites
if ($activeRaw -notmatch [regex]::Escape($lastPayload)) {
    Write-Host "FAIL: Active log '$([System.IO.Path]::GetFileName($PrunePath))' should hold $lastPayload after pruning." -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Cyan
    Get-ChildItem -Path $ScriptDir -Filter "test_prune*.log" -ErrorAction SilentlyContinue | Remove-Item -Force
    exit 1
}

Get-ChildItem -Path $ScriptDir -Filter "test_prune*.log" -ErrorAction SilentlyContinue | Remove-Item -Force
Write-Host "PASS: Retention pruning kept the newest $PruneBackups backups [$($expected -join ', ')] and preserved the active log." -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan

exit 0
