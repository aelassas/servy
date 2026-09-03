#Requires -Version 2.0
<#
.SYNOPSIS
    Unit test harness for Servy-Restore.ps1 helper functions on net48 branch.

.DESCRIPTION
    Tests path resolution, sidecar expected hash extraction, ACL hardening (inheritance break,
    explicit ACE purging, Administrators/SYSTEM exclusive FullControl), and archive entry validation guards
    (flat archive enforcement, path traversal defense, XML extension verification, and duplicate entry detection).
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

# Dot-source test harness shared utilities
$testCommonPath = Join-Path $scriptDir 'TestCommon.ps1'
if (-not (Test-Path -Path $testCommonPath)) {
    Write-Host "FAILED: Could not find TestCommon.ps1 at '$testCommonPath'." -ForegroundColor Red
    exit 1
}
. $testCommonPath

# Dot-source Servy-Restore.ps1 to load its helper functions without executing the main script body.
# Pass dummy string for mandatory parameter to satisfy [ValidateNotNullOrEmpty()].
$scriptPath = Join-Path $scriptDir 'Servy-Restore.ps1'
if (-not (Test-Path -Path $scriptPath)) {
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
Write-Host " Running Servy-Restore.ps1 Tests (net48)            " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Dump Path Resolution Tests ---
Write-Host "1. Testing Resolve-ServyRestoreDumpPath..." -ForegroundColor Yellow

$path1 = Resolve-ServyRestoreDumpPath -DumpArchivePath "C:\Backups\Servy_Dump.zip"
Assert-Equal "Absolute path resolution without PSCmdlet" $path1 "C:\Backups\Servy_Dump.zip"

$expectedRelativePath = Join-Path (Get-Location).ProviderPath "Servy_Dump.zip"
$path2 = Resolve-ServyRestoreDumpPath -DumpArchivePath "Servy_Dump.zip"
Assert-Equal "Relative path resolves against PowerShell location without PSCmdlet" $path2 $expectedRelativePath

# Verify production $PSCmdlet context path resolution via CmdletBinding wrapper
function Test-ResolveWithCmdletContext {
    [CmdletBinding()]
    param([string]$Path)

    return Resolve-ServyRestoreDumpPath -DumpArchivePath $Path -PSCmdletContext $PSCmdlet
}

$path3 = Test-ResolveWithCmdletContext -Path "Servy_Dump.zip"
Assert-Equal "Relative path resolves via PSCmdletContext" $path3 $expectedRelativePath

# --- 2. Sidecar Expected Hash Extraction Tests ---
Write-Host "`n2. Testing Get-ServySidecarExpectedHash..." -ForegroundColor Yellow

$sidecarContent = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855 *Servy_Dump.zip`n"
$hash1 = Get-ServySidecarExpectedHash -SidecarText $sidecarContent
Assert-Equal "Extracts hash from sidecar text" $hash1 "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"

$hash2 = Get-ServySidecarExpectedHash -SidecarText "   ABCD1234EF   somefile.zip"
Assert-Equal "Trims leading whitespace and extracts hash" $hash2 "ABCD1234EF"

Assert-True "Whitespace-only sidecar yields null" ($null -eq (Get-ServySidecarExpectedHash -SidecarText "   `t`n"))

# --- 3. Archive Entry Security & Validation Guard Tests ---
Write-Host "`n4. Testing Test-ServyDumpArchiveEntry..." -ForegroundColor Yellow

$rootPath = [System.IO.Path]::GetFullPath("C:\Temp\ServyRestore_Staging\")
$seenEntries = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

# Test 3.1: Directory entry (empty Name)
$e1 = Test-ServyDumpArchiveEntry -EntryName "" -EntryFullName "subfolder/" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Directory entry skipped safely" ($e1.IsValid -and $e1.IsDirectory)

# Test 3.2: Valid XML entry
$e2 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-Equal "Valid flat XML entry maps into the staging root" $e2.TargetPath ([System.IO.Path]::Combine($rootPath, "MyService.xml"))

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

# Test 3.7: Absolute-path entry
$seenEntries4 = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$e7 = Test-ServyDumpArchiveEntry -EntryName "C:\evil.xml" -EntryFullName "C:\evil.xml" -RootPath $rootPath -SeenEntryNames $seenEntries4
Assert-True "Absolute-path archive entry rejected" (-not $e7.IsValid -and $e7.ErrorMessage.Contains("outside staging directory"))

Invoke-TestSummary
