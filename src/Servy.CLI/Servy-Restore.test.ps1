#Requires -Version 5.1
<#
.SYNOPSIS
    Unit test harness for Servy-Restore.ps1 helper functions.

.DESCRIPTION
    Tests path resolution, sidecar expected hash extraction, and archive entry validation guards
    (flat archive enforcement, path traversal defense, XML extension verification, and duplicate entry detection).
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Dot-source Servy-Restore.ps1 to load its helper functions without executing the main script body.
# Pass a dummy string for mandatory parameters to satisfy [ValidateNotNullOrEmpty()].
$scriptPath = Join-Path $PSScriptRoot 'Servy-Restore.ps1'

if (-not (Test-Path -LiteralPath $scriptPath)) {
    Write-Host "FAILED: Could not find Servy-Restore.ps1 at '$scriptPath'." -ForegroundColor Red
    exit 1
}

try {
    . $scriptPath -DumpArchivePath "dummy"
}
catch {
    Write-Host "FAILED to dot-source Servy-Restore.ps1: $_" -ForegroundColor Red
    exit 1
}

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Servy-Restore.ps1 Tests               " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

$script:TotalTests  = 0
$script:PassedTests = 0
$script:FailedTests = 0

function Assert-Equal {
    param(
        [string]$TestName,
        $Actual,
        $Expected
    )
    $script:TotalTests++
    if ($Actual -eq $Expected) {
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:PassedTests++
    }
    else {
        Write-Host "  [FAIL] $TestName - Expected: '$Expected', Actual: '$Actual'" -ForegroundColor Red
        $script:FailedTests++
    }
}

function Assert-True {
    param(
        [string]$TestName,
        [bool]$Condition
    )
    $script:TotalTests++
    if ($Condition) {
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:PassedTests++
    }
    else {
        Write-Host "  [FAIL] $TestName - Expected condition to be True" -ForegroundColor Red
        $script:FailedTests++
    }
}

# --- 1. Dump Path Resolution Tests ---
Write-Host "1. Testing Resolve-ServyRestoreDumpPath..." -ForegroundColor Yellow

$path1 = Resolve-ServyRestoreDumpPath -DumpArchivePath "C:\Backups\Servy_Dump.zip"
Assert-Equal "Absolute path resolution" $path1 "C:\Backups\Servy_Dump.zip"

# --- 2. Sidecar Expected Hash Extraction Tests ---
Write-Host "`n2. Testing Get-ServySidecarExpectedHash..." -ForegroundColor Yellow

$sidecarContent = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855 *Servy_Dump.zip`n"
$hash1 = Get-ServySidecarExpectedHash -SidecarText $sidecarContent
Assert-Equal "Extracts hash from sidecar text" $hash1 "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"

$hash2 = Get-ServySidecarExpectedHash -SidecarText "  ABCD1234EF   somefile.zip"
Assert-Equal "Trims leading whitespace and extracts hash" $hash2 "ABCD1234EF"

# --- 3. Archive Entry Security & Validation Guard Tests ---
Write-Host "`n3. Testing Test-ServyDumpArchiveEntry..." -ForegroundColor Yellow

$rootPath = [System.IO.Path]::GetFullPath("C:\Temp\ServyRestore_Staging\")
$seenEntries = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

# Test 3.1: Directory entry (empty Name)
$e1 = Test-ServyDumpArchiveEntry -EntryName "" -EntryFullName "subfolder/" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Directory entry skipped safely" ($e1.IsValid -and $e1.IsDirectory)

# Test 3.2: Valid XML entry
$e2 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Valid flat XML entry accepted" ($e2.IsValid -and -not $e2.IsDirectory -and $e2.TargetPath.EndsWith("MyService.xml"))

# Test 3.3: Non-flat entry (subdirectory)
$e3 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "folder/MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Non-flat archive entry rejected" (-not $e3.IsValid -and $e3.ErrorMessage.Contains("contains subdirectories"))

# Test 3.4: Duplicate entry detection
$e4 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Duplicate archive entry rejected" (-not $e4.IsValid -and $e4.ErrorMessage.Contains("duplicate entry"))

# Test 3.5: Path traversal attack
$seenEntries2 = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$e5 = Test-ServyDumpArchiveEntry -EntryName "..\..\Evil.xml" -EntryFullName "..\..\Evil.xml" -RootPath $rootPath -SeenEntryNames $seenEntries2
Assert-True "Path traversal entry rejected" (-not $e5.IsValid -and ($e5.ErrorMessage.Contains("outside staging directory") -or $e5.ErrorMessage.Contains("subdirectories")))

# Test 3.6: Non-XML file extension
$seenEntries3 = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$e6 = Test-ServyDumpArchiveEntry -EntryName "Malware.exe" -EntryFullName "Malware.exe" -RootPath $rootPath -SeenEntryNames $seenEntries3
Assert-True "Non-XML archive entry rejected" (-not $e6.IsValid -and $e6.ErrorMessage.Contains("not an XML configuration file"))

# ----------------------------------------------------------------
# Summary Output
# ----------------------------------------------------------------
Write-Host "`n====================================================" -ForegroundColor Cyan
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
