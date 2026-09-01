#Requires -Version 5.1
<#
.SYNOPSIS
    Unit test harness for Servy-Restore.ps1 helper functions.

.DESCRIPTION
    Tests path resolution, sidecar expected hash extraction, ACL hardening (inheritance break,
    explicit ACE purging, Administrators/SYSTEM exclusive FullControl), and archive entry validation guards
    (flat archive enforcement, path traversal defense, XML extension verification, and duplicate entry detection).
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Dot-source test harness shared utilities
$testCommonPath = Join-Path $PSScriptRoot 'TestCommon.ps1'
if (-not (Test-Path -LiteralPath $testCommonPath)) {
    Write-Host "FAILED: Could not find TestCommon.ps1 at '$testCommonPath'." -ForegroundColor Red
    exit 1
}
. $testCommonPath

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
Write-Host " Running Servy-Restore.ps1 Tests                    " -ForegroundColor Cyan
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

$hash2 = Get-ServySidecarExpectedHash -SidecarText "    ABCD1234EF   somefile.zip"
Assert-Equal "Trims leading whitespace and extracts hash" $hash2 "ABCD1234EF"

Assert-True "Whitespace-only sidecar yields null" ($null -eq (Get-ServySidecarExpectedHash -SidecarText "   `t`n"))

# --- 3. ACL Hardening Helper Tests ---
Write-Host "`n3. Testing Set-ServyHardenedFileAcl..." -ForegroundColor Yellow

$testTempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ServyRestore_AclTest_" + [System.IO.Path]::GetRandomFileName())
[void][System.IO.Directory]::CreateDirectory($testTempDir)

try {
    # Test 3.1: Hardening a Directory
    $testSubDir = Join-Path $testTempDir "StagingDir"
    [void][System.IO.Directory]::CreateDirectory($testSubDir)

    Set-ServyHardenedFileAcl -Path $testSubDir -IsDirectory

    $dirAcl = Get-Acl -LiteralPath $testSubDir
    Assert-True "Directory inheritance is protected" $dirAcl.AreAccessRulesProtected

    $dirRules = $dirAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
    Assert-Equal "Directory has exactly 2 explicit ACEs" $dirRules.Count 2

    $adminSid  = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
    $systemSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)

    $foundAdmin  = $false
    $foundSystem = $false

    foreach ($rule in $dirRules) {
        if ($rule.IdentityReference.Equals($adminSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.InheritanceFlags -eq "ContainerInherit, ObjectInherit" -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) {
            $foundAdmin = $true
        }
        elseif ($rule.IdentityReference.Equals($systemSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.InheritanceFlags -eq "ContainerInherit, ObjectInherit" -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) {
            $foundSystem = $true
        }
    }

    Assert-True "Directory grants FullControl with container/object inheritance to Administrators" $foundAdmin
    Assert-True "Directory grants FullControl with container/object inheritance to SYSTEM" $foundSystem

    # Test 3.2: Hardening a File & Purging Pre-existing ACEs
    $testFile = Join-Path $testTempDir "TargetFile.xml"
    [System.IO.File]::WriteAllText($testFile, "<configuration />")

    Set-ServyHardenedFileAcl -Path $testFile

    $fileAcl = Get-Acl -LiteralPath $testFile
    Assert-True "File inheritance is protected" $fileAcl.AreAccessRulesProtected

    $fileRules = $fileAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
    Assert-Equal "File has exactly 2 explicit ACEs after purging" $fileRules.Count 2

    $foundFileAdmin  = $false
    $foundFileSystem = $false

    foreach ($rule in $fileRules) {
        if ($rule.IdentityReference.Equals($adminSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) {
            $foundFileAdmin = $true
        }
        elseif ($rule.IdentityReference.Equals($systemSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) {
            $foundFileSystem = $true
        }
    }

    Assert-True "File grants FullControl to Administrators" $foundFileAdmin
    Assert-True "File grants FullControl to SYSTEM" $foundFileSystem
}
finally {
    if (Test-Path -LiteralPath $testTempDir) {
        Remove-Item -LiteralPath $testTempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- 4. Archive Entry Security & Validation Guard Tests ---
Write-Host "`n4. Testing Test-ServyDumpArchiveEntry..." -ForegroundColor Yellow

$rootPath = [System.IO.Path]::GetFullPath("C:\Temp\ServyRestore_Staging\")
$seenEntries = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

# Test 4.1: Directory entry (empty Name)
$e1 = Test-ServyDumpArchiveEntry -EntryName "" -EntryFullName "subfolder/" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Directory entry skipped safely" ($e1.IsValid -and $e1.IsDirectory)

# Test 4.2: Valid XML entry
$e2 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-Equal "Valid flat XML entry maps into the staging root" $e2.TargetPath ([System.IO.Path]::Combine($rootPath, "MyService.xml"))

# Test 4.3: Non-flat entry (subdirectory)
$e3 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "folder/MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Non-flat archive entry rejected" (-not $e3.IsValid -and $e3.ErrorMessage.Contains("contains subdirectories"))

# Test 4.4: Duplicate entry detection
$e4 = Test-ServyDumpArchiveEntry -EntryName "MyService.xml" -EntryFullName "MyService.xml" -RootPath $rootPath -SeenEntryNames $seenEntries
Assert-True "Duplicate archive entry rejected" (-not $e4.IsValid -and $e4.ErrorMessage.Contains("duplicate entry"))

# Test 4.5: Path traversal attack
$seenEntries2 = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$e5 = Test-ServyDumpArchiveEntry -EntryName "..\..\Evil.xml" -EntryFullName "..\..\Evil.xml" -RootPath $rootPath -SeenEntryNames $seenEntries2
Assert-True "Path traversal entry rejected" (-not $e5.IsValid -and ($e5.ErrorMessage.Contains("outside staging directory") -or $e5.ErrorMessage.Contains("subdirectories")))

# Test 4.6: Non-XML file extension
$seenEntries3 = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$e6 = Test-ServyDumpArchiveEntry -EntryName "Malware.exe" -EntryFullName "Malware.exe" -RootPath $rootPath -SeenEntryNames $seenEntries3
Assert-True "Non-XML archive entry rejected" (-not $e6.IsValid -and $e6.ErrorMessage.Contains("not an XML configuration file"))

# Test 4.7: Absolute-path entry
$seenEntries4 = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$e7 = Test-ServyDumpArchiveEntry -EntryName "C:\evil.xml" -EntryFullName "C:\evil.xml" -RootPath $rootPath -SeenEntryNames $seenEntries4
Assert-True "Absolute-path archive entry rejected" (-not $e7.IsValid -and $e7.ErrorMessage.Contains("outside staging directory"))

Invoke-TestSummary
