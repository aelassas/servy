#Requires -Version 5.1
<#
.SYNOPSIS
    Validates that the Task Scheduler publish.yml exclusion filtering logic works correctly.
.DESCRIPTION
    Creates an ephemeral local sandbox environment, runs the hardened recursive copy block,
    and verifies that excluded items (*.test.ps1, .dat, .log, smtp-cred.xml) are blocked
    while standard payloads are preserved.
#>

# Enforce script-root execution safety bounds
$ScriptDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
Set-Location -Path $ScriptDir

# ---------------------------------------------------------------------
# SETUP TEMPORARY SANDBOX ENVIRONMENT
# ---------------------------------------------------------------------
$SandboxRoot = Join-Path $ScriptDir "PublishTestSandbox"
$sourcePath  = Join-Path $SandboxRoot "setup_taskschd"
$pkg         = Join-Path $SandboxRoot "staging_out"
$destPath    = Join-Path $pkg "taskschd"

# Clean up any leftover previous test contexts
if (Test-Path $SandboxRoot) { Remove-Item -Path $SandboxRoot -Recurse -Force -ErrorAction SilentlyContinue }

# Initialize source and staging directories
New-Item -Path $sourcePath -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $sourcePath "SubFolder") -ItemType Directory -Force | Out-Null

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Initializing Hardened Copy Publish Filtering Tests" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Define mock test matrix data pairs [Relative Path, Should Copy]
$MockFiles = @(
    @("ServySecurity.ps1", $true),
    @("TaskConfig.xml", $true),
    @("SubFolder\NestedScript.ps1", $true),
    @("smtp-cred.xml", $false),       # Blacklisted file rule
    @("ServySecurity.test.ps1", $false), # Hardened exclusion wildcard rule
    @("SubFolder\Nested.test.ps1", $false), # Nested wildcard rule
    @("state.dat", $false),          # Prohibited extension rule
    @("trace.log", $false),          # Prohibited extension rule
    @("temp.ps1", $false)            # Prohibited temp.ps1 rule
)

# Populate test matrix data onto the filesystem
foreach ($file in $MockFiles) {
    $filePath = Join-Path $sourcePath $file[0]
    $parentDir = Split-Path $filePath -Parent
    if (-not (Test-Path $parentDir)) { New-Item -Path $parentDir -ItemType Directory -Force | Out-Null }
    [System.IO.File]::WriteAllText($filePath, "Mock Publish Target Data Payload context.")
}

# ---------------------------------------------------------------------
# EXECUTE PUBLISH PIPELINE LOGIC UNDER TEST
# ---------------------------------------------------------------------
Write-Host "Executing filter staging copy block..." -ForegroundColor Gray

if (-not (Test-Path $destPath)) { New-Item -Path $destPath -ItemType Directory -Force | Out-Null }

Get-ChildItem -Path $sourcePath -Recurse -File |
    Where-Object { 
        $_.Name -notin @('smtp-cred.xml') -and 
        $_.Extension -notin @('.dat','.log') -and 
        $_.Name -notin @('temp.ps1') -and 
        $_.Name -notlike '*.test.ps1'
    } |
    ForEach-Object {
        # Calculate relative path to maintain subdirectory structure in the destination
        $rel = $_.FullName.Substring((Resolve-Path $sourcePath).Path.Length + 1)
        $target = Join-Path $destPath $rel
        $parent = Split-Path $target -Parent

        # Ensure the subdirectory exists in the staging folder
        if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
     
        Copy-Item -Path $_.FullName -Destination $target -Force
    }

# ---------------------------------------------------------------------
# EVALUATE RESULTS & EXCLUSION MATRIX
# ---------------------------------------------------------------------
Write-Host "`nEvaluating structural test conditions..." -ForegroundColor Cyan
Write-Host "----------------------------------------------------------" -ForegroundColor Cyan

$Passed = 0
$Failed = 0

foreach ($file in $MockFiles) {
    $expectedPath = Join-Path $destPath $file[0]
    $exists = Test-Path $expectedPath
    $shouldExist = $file[1]

    if ($exists -eq $shouldExist) {
        Write-Host "[PASS] " -ForegroundColor Green -NoNewline
        if ($shouldExist) {
            Write-Host "Correctly Copied  : " -ForegroundColor Gray -NoNewline
        } else {
            Write-Host "Correctly Excluded: " -ForegroundColor DarkGray -NoNewline
        }
        Write-Host "$($file[0])" -ForegroundColor Gray
        $Passed++
    } else {
        Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
        if ($shouldExist) {
            Write-Host "Missing Target File   : " -ForegroundColor White -BackgroundColor Red -NoNewline
        } else {
            Write-Host "Leaked Excluded File  : " -ForegroundColor White -BackgroundColor Red -NoNewline
        }
        Write-Host "$($file[0])" -ForegroundColor White -BackgroundColor Red
        $Failed++
    }
}

# ---------------------------------------------------------------------
# TEARDOWN & CLEANUP REPORT
# ---------------------------------------------------------------------
Write-Host "----------------------------------------------------------" -ForegroundColor Cyan
if ($Failed -eq 0) {
    Write-Host "ALL $Passed PIPELINE FILTERS PASSED COMPLIANCE CHECKS!" -ForegroundColor Green
} else {
    Write-Host "EXCLUSION FAILURES DETECTED: $Passed Passed, $Failed Failed." -ForegroundColor Red
}

# Safely scrub local workspace changes
if (Test-Path $SandboxRoot) { Remove-Item -Path $SandboxRoot -Recurse -Force | Out-Null }
Write-Host "==========================================================" -ForegroundColor Cyan
