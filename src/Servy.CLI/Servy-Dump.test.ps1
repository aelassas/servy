#Requires -Version 2.0
<#
.SYNOPSIS
    Unit test harness for Servy-Dump.ps1 helper functions on net48 branch.

.DESCRIPTION
    Tests path resolution, destination normalization, service name sanitization,
    Win32 reserved device name prefixing, and collision disambiguation.
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Resolve script directory safely across PowerShell 2.0 ($MyInvocation) and PowerShell 3.0+ ($PSScriptRoot)
if ($PSVersionTable.PSVersion.Major -ge 3) {
    $scriptDir = $PSScriptRoot
}
else {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

# Dot-source Servy-Dump.ps1 to load its helper functions without executing the main script body.
# Pass dummy string for mandatory parameter to satisfy [ValidateNotNullOrEmpty()].
$scriptPath = Join-Path $scriptDir 'Servy-Dump.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    Write-Host "FAILED: Could not find Servy-Dump.ps1 at '$scriptPath'." -ForegroundColor Red
    exit 1
}

try {
    . $scriptPath -DestinationArchivePath "dummy"
}
catch {
    Write-Host "FAILED to dot-source Servy-Dump.ps1: $_" -ForegroundColor Red
    exit 1
}

$testsPassed = 0
$testsFailed = 0

function Assert-Equal {
    param(
        [string]$TestName,
        $Actual,
        $Expected
    )
    if ($Actual -eq $Expected) {
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:testsPassed++
    }
    else {
        Write-Host "  [FAIL] $TestName - Expected: '$Expected', Actual: '$Actual'" -ForegroundColor Red
        $script:testsFailed++
    }
}

Write-Host "Running Servy-Dump.ps1 function unit tests..." -ForegroundColor Cyan

# --- 1. Destination Path Resolution & Normalization Tests ---
Write-Host "`n1. Testing Resolve-ServyDumpDestinationPath..." -ForegroundColor Yellow

$res1 = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:\Backups\MyDump"
Assert-Equal "Appends .zip to extension-less file path" $res1 "C:\Backups\MyDump.zip"

$res2 = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:\Backups\MyDump.zip"
Assert-Equal "Preserves existing .zip extension" $res2 "C:\Backups\MyDump.zip"

$res3 = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:\Backups\TargetDir\"
Assert-Equal "Appends Servy_Dump.zip to trailing slash directory path" $res3 "C:\Backups\TargetDir\Servy_Dump.zip"

$res4 = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:\Backups\TargetDir/"
Assert-Equal "Appends Servy_Dump.zip to trailing forward-slash directory path" $res4 "C:\Backups\TargetDir\Servy_Dump.zip"

# --- 2. Service Name Sanitization & Collision Disambiguation Tests ---
Write-Host "`n2. Testing Get-ServySanitizedFileName..." -ForegroundColor Yellow

$invalidChars  = [System.IO.Path]::GetInvalidFileNameChars()
$reservedNames = @(
    'CON', 'PRN', 'AUX', 'NUL', 'CONIN$', 'CONOUT$',
    'COM1', 'COM2', 'COM3', 'COM4', 'COM5', 'COM6', 'COM7', 'COM8', 'COM9',
    'COM¹', 'COM²', 'COM³',
    'LPT1', 'LPT2', 'LPT3', 'LPT4', 'LPT5', 'LPT6', 'LPT7', 'LPT8', 'LPT9',
    'LPT¹', 'LPT²', 'LPT³'
)

$usedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

$name1 = Get-ServySanitizedFileName -ServiceName "StandardService" -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSet
Assert-Equal "Standard service name remains unchanged" $name1 "StandardService"

$name2 = Get-ServySanitizedFileName -ServiceName "Service:With/Invalid*Chars" -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSet
Assert-Equal "Invalid filename chars replaced with underscores" $name2 "Service_With_Invalid_Chars"

$name3 = Get-ServySanitizedFileName -ServiceName "CON" -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSet
Assert-Equal "Reserved device name CON is prefixed with underscore" $name3 "_CON"

$name4 = Get-ServySanitizedFileName -ServiceName "aux.txt" -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSet
Assert-Equal "Reserved device stem aux.txt is prefixed with underscore" $name4 "_aux.txt"

$name5 = Get-ServySanitizedFileName -ServiceName "CON " -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSet
Assert-Equal "Reserved device stem with trailing space CON  is prefixed" $name5 "_CON "

# Test collision disambiguation
$usedSetCollision = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
[void]$usedSetCollision.Add("MyService")

$col1 = Get-ServySanitizedFileName -ServiceName "MyService" -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSetCollision
Assert-Equal "First collision appends _1 suffix" $col1 "MyService_1"

$col2 = Get-ServySanitizedFileName -ServiceName "MyService" -InvalidChars $invalidChars -ReservedNames $reservedNames -UsedBaseNames $usedSetCollision
Assert-Equal "Second collision appends _2 suffix" $col2 "MyService_2"

# Summary
$summaryColor = if ($testsFailed -eq 0) { 'Green' } else { 'Red' }
Write-Host "`nTest Summary: $testsPassed passed, $testsFailed failed." -ForegroundColor $summaryColor

if ($testsFailed -gt 0) {
    exit 1
}
exit 0
