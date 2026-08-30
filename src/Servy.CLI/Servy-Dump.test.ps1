#Requires -Version 5.1
<#
.SYNOPSIS
    Unit test harness for Servy-Dump.ps1 helper functions.

.DESCRIPTION
    Tests path resolution, destination normalization, service name sanitization,
    Win32 reserved device name prefixing, collision disambiguation, and ACL hardening.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Dot-source Servy-Dump.ps1 to load its helper functions without executing the main script body.
# Pass a dummy string for mandatory parameters to satisfy [ValidateNotNullOrEmpty()].
$scriptPath = Join-Path $PSScriptRoot 'Servy-Dump.ps1'

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

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Servy-Dump.ps1 Unit Tests                  " -ForegroundColor Cyan
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

# --- 1. Destination Path Resolution & Normalization Tests ---
Write-Host "1. Testing Resolve-ServyDumpDestinationPath..." -ForegroundColor Yellow

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

# --- 3. ACL Hardening Helper Tests ---
Write-Host "`n3. Testing Set-ServyHardenedFileAcl..." -ForegroundColor Yellow

$tempTestFile = [System.IO.Path]::GetTempFileName()
$tempTestDir  = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyAclTest_" + [System.IO.Path]::GetRandomFileName())

try {
    [void][System.IO.Directory]::CreateDirectory($tempTestDir)

    # Hardening test on File
    Set-ServyHardenedFileAcl -Path $tempTestFile
    $fileAcl = Get-Acl -LiteralPath $tempTestFile

    Assert-True "File permission inheritance is protected/broken" $fileAcl.AreAccessRulesProtected

    $fileRules = $fileAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
    Assert-Equal "File ACL contains exactly 2 access control entries" $fileRules.Count 2

    $adminSid  = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
    $systemSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)

    $hasAdmin  = $false
    $hasSystem = $false

    foreach ($rule in $fileRules) {
        if ($rule.IdentityReference.Equals($adminSid) -and $rule.FileSystemRights -eq "FullControl")  { $hasAdmin  = $true }
        if ($rule.IdentityReference.Equals($systemSid) -and $rule.FileSystemRights -eq "FullControl") { $hasSystem = $true }
    }

    Assert-True "File ACL grants FullControl to Builtin Administrators" $hasAdmin
    Assert-True "File ACL grants FullControl to Local SYSTEM" $hasSystem

    # Hardening test on Directory
    Set-ServyHardenedFileAcl -Path $tempTestDir -IsDirectory
    $dirAcl = Get-Acl -LiteralPath $tempTestDir

    Assert-True "Directory permission inheritance is protected/broken" $dirAcl.AreAccessRulesProtected

    $dirRules = $dirAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
    Assert-Equal "Directory ACL contains exactly 2 access control entries" $dirRules.Count 2

    $hasDirAdmin  = $false
    $hasDirSystem = $false

    foreach ($rule in $dirRules) {
        if ($rule.IdentityReference.Equals($adminSid) -and $rule.InheritanceFlags -eq "ContainerInherit, ObjectInherit") { $hasDirAdmin  = $true }
        if ($rule.IdentityReference.Equals($systemSid) -and $rule.InheritanceFlags -eq "ContainerInherit, ObjectInherit") { $hasDirSystem = $true }
    }

    Assert-True "Directory ACL grants ContainerInherit+ObjectInherit to Administrators" $hasDirAdmin
    Assert-True "Directory ACL grants ContainerInherit+ObjectInherit to SYSTEM" $hasDirSystem
}
finally {
    if (Test-Path -LiteralPath $tempTestFile) { Remove-Item -LiteralPath $tempTestFile -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $tempTestDir)  { Remove-Item -LiteralPath $tempTestDir -Recurse -Force -ErrorAction SilentlyContinue }
}

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
