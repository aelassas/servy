#Requires -Version 5.1
# Ensure the log function is loaded in the main script thread
$ScriptDir = $PSScriptRoot
$LogScriptPath = Join-Path $ScriptDir "Write-ServyLog.ps1"

if (-not (Test-Path $LogScriptPath)) {
    Write-Host "Error: Could not find Write-ServyLog.ps1 at $LogScriptPath" -ForegroundColor Red
    return
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
        Write-ServyLog -FilePath $FilePath -Message $Msg -MaxSizeBytes $MaxSize
        
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
$Jobs | Wait-Job | Out-Null

# --- EVALUATION AND AUDIT PASS ---
Write-Host "Analyzing log files for multi-process safety exceptions..." -ForegroundColor Cyan

# Merge the job's warning stream into the pipeline cleanly without using an invalid -Stream flag
$CapturedOutput = $Jobs | Receive-Job 3>&1

# Extract warnings matching our criteria
$Warnings = $CapturedOutput | Where-Object { $_ -match "Servy Critical Logging Failure" }

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

if ($TotalLines -eq $ExpectedLines -and -not $Warnings) {
    Write-Host "SUCCESS: 100% of concurrent log entries were structurally preserved without line drops!" -ForegroundColor Green
} else {
    $Deficit = $ExpectedLines - $TotalLines
    if ($Deficit -gt 0) {
        Write-Host "FAIL: Missing data detected! Lost $Deficit log frames due to un-serialized write collisions." -ForegroundColor Red
    } else {
        Write-Host "FAIL: Total line count matches, but tracking execution warnings were emitted." -ForegroundColor Red
    }
}