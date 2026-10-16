#Requires -Version 5.1

<#
.SYNOPSIS
    Unit test harness for Servy-Dump.ps1 helper functions.

.DESCRIPTION
    Tests path resolution, destination normalization, service name sanitization,
    Win32 reserved device name prefixing, and collision disambiguation, the staged-file move that keeps the archive's hardened ACL (ACL hardening itself is covered by Servy.test.ps1), and the -Overwrite order that removes a stale sidecar only after the archive move.
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

# Dot-source Servy-Dump.ps1 to load its helper functions without executing the main script body.
# Pass a dummy string for mandatory parameters to satisfy [ValidateNotNullOrEmpty()].
$scriptPath = Join-Path $PSScriptRoot 'Servy-Dump.ps1'

if (-not (Test-Path -LiteralPath $scriptPath)) {
    Write-Host "FAILED: Could not find Servy-Dump.ps1 at '$scriptPath'." -ForegroundColor Red
    exit 1
}

try {
    . $scriptPath -DestinationArchivePath "dummy" -Confirm:$false
}
catch {
    Write-Host "FAILED to dot-source Servy-Dump.ps1: $_" -ForegroundColor Red
    exit 1
}

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Servy-Dump.ps1 Tests                        " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

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

# Test production PSCmdlet context branch via cmdlet wrapper
function Test-ResolveWithCmdletContext {
    [CmdletBinding()]
    param([string]$Path)

    return Resolve-ServyDumpDestinationPath -DestinationArchivePath $Path -PSCmdletContext $PSCmdlet
}

$resCmdlet1 = Test-ResolveWithCmdletContext -Path "C:\Backups\MyDump"
Assert-Equal "Resolves via PSCmdletContext branch" $resCmdlet1 "C:\Backups\MyDump.zip"

# Test relative path resolution across both PSCmdlet and fallback branches
$expectedRelativePath = [System.IO.Path]::Combine((Get-Location).ProviderPath, "MyDump.zip")

$resRelativeFallback = Resolve-ServyDumpDestinationPath -DestinationArchivePath "MyDump"
Assert-Equal "Resolves relative path via fallback branch against location" $resRelativeFallback $expectedRelativePath

$resRelativeCmdlet = Test-ResolveWithCmdletContext -Path "MyDump"
Assert-Equal "Resolves relative path via PSCmdletContext branch against location" $resRelativeCmdlet $expectedRelativePath

# Test existing directory promotion without trailing slash (#6299 regression check)
$tempExistingDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyDumpTestDir_" + [System.IO.Path]::GetRandomFileName())
try {
    [void][System.IO.Directory]::CreateDirectory($tempExistingDir)
    $expectedExistingDirDumpPath = [System.IO.Path]::Combine($tempExistingDir, "Servy_Dump.zip")

    $resExistingDir = Resolve-ServyDumpDestinationPath -DestinationArchivePath $tempExistingDir
    Assert-Equal "Existing directory without trailing slash promotes to Servy_Dump.zip" $resExistingDir $expectedExistingDirDumpPath
}
finally {
    if (Test-Path -LiteralPath $tempExistingDir) {
        Remove-Item -LiteralPath $tempExistingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Test drive-root destination guard (#6311 regression check)
$resDriveRootSlash = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:\"
Assert-Equal "Drive root C:\ resolves to C:\Servy_Dump.zip" $resDriveRootSlash "C:\Servy_Dump.zip"

$resDriveRootNoSlash = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:"
Assert-Equal "Drive root C: resolves to C:\Servy_Dump.zip" $resDriveRootNoSlash "C:\Servy_Dump.zip"

# Test trailing-whitespace directory path normalization
$resTrailingSpaceDir = Resolve-ServyDumpDestinationPath -DestinationArchivePath "C:\Backups\TargetDir\  "
Assert-Equal "Trims trailing whitespace on directory-style path" $resTrailingSpaceDir "C:\Backups\TargetDir\Servy_Dump.zip"

# --- 2. Service Name Sanitization & Collision Disambiguation Tests ---
Write-Host "`n2. Testing Get-ServySanitizedFileName..." -ForegroundColor Yellow

$invalidChars  = [System.IO.Path]::GetInvalidFileNameChars()

# Dynamically pull $reservedNames from the production script context to guarantee parity
$reservedNames = $script:reservedNames

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

# --- 3. Staged File Move Tests (#7538) ---
Write-Host "`n3. Testing Move-ServyDumpStagedFile..." -ForegroundColor Yellow

$moveTestRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyDumpMoveTest_" + [System.IO.Path]::GetRandomFileName())
try {
    # Arrange
    $moveStagingDir = [System.IO.Path]::Combine($moveTestRoot, "staging")
    $moveTargetDir  = [System.IO.Path]::Combine($moveTestRoot, "target")
    [void][System.IO.Directory]::CreateDirectory($moveStagingDir)
    [void][System.IO.Directory]::CreateDirectory($moveTargetDir)
    $stagedFile = [System.IO.Path]::Combine($moveStagingDir, "Servy_Dump.zip")
    $targetFile = [System.IO.Path]::Combine($moveTargetDir, "Servy_Dump.zip")
    [System.IO.File]::WriteAllText($stagedFile, "first")

    # Give the staged file a protected ACL that the target directory would not hand down to a new file
    $currentUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $stagedAcl      = Get-Acl -LiteralPath $stagedFile
    $stagedAcl.SetAccessRuleProtection($true, $false)
    $stagedAcl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($currentUserSid, "FullControl", "Allow")))
    Set-Acl -LiteralPath $stagedFile -AclObject $stagedAcl
    $expectedDacl = (Get-Acl -LiteralPath $stagedFile).GetSecurityDescriptorSddlForm([System.Security.AccessControl.AccessControlSections]::Access)

    # Act
    Move-ServyDumpStagedFile -StagedPath $stagedFile -DestinationPath $targetFile

    # Assert
    $movedAcl = Get-Acl -LiteralPath $targetFile
    Assert-True "Moved file no longer exists in the staging directory" (-not (Test-Path -LiteralPath $stagedFile))
    Assert-True "Moved file keeps its protected (non-inherited) ACL" $movedAcl.AreAccessRulesProtected
    Assert-Equal "Moved file keeps the DACL it was given in staging" $movedAcl.GetSecurityDescriptorSddlForm([System.Security.AccessControl.AccessControlSections]::Access) $expectedDacl

    # Arrange
    [System.IO.File]::WriteAllText($stagedFile, "second")

    # Act
    $moveWithoutOverwriteThrew = $false
    try {
        Move-ServyDumpStagedFile -StagedPath $stagedFile -DestinationPath $targetFile
    }
    catch {
        $moveWithoutOverwriteThrew = $true
    }

    # Assert
    Assert-True "Move without -Overwrite fails when the destination exists" $moveWithoutOverwriteThrew
    Assert-Equal "Move without -Overwrite leaves the existing destination untouched" ([System.IO.File]::ReadAllText($targetFile)) "first"

    # Act
    Move-ServyDumpStagedFile -StagedPath $stagedFile -DestinationPath $targetFile -Overwrite

    # Assert
    Assert-Equal "Move with -Overwrite replaces the existing destination" ([System.IO.File]::ReadAllText($targetFile)) "second"
}
finally {
    if (Test-Path -LiteralPath $moveTestRoot) {
        Remove-Item -LiteralPath $moveTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- 4. Overwrite Replacement Order Tests (#7548) ---
Write-Host "`n4. Testing the -Overwrite replacement order of the archive and its sidecar..." -ForegroundColor Yellow

# Arrange
$dumpScriptText     = [System.IO.File]::ReadAllText($scriptPath)
$archiveMoveIndex   = $dumpScriptText.IndexOf('Move-ServyDumpStagedFile -StagedPath $stagedArchivePath', [StringComparison]::Ordinal)
$sidecarRemoveIndex = $dumpScriptText.IndexOf('Remove-Item -LiteralPath $sidecarPath', [StringComparison]::Ordinal)
if ($archiveMoveIndex -lt 0 -or $sidecarRemoveIndex -lt 0) {
    throw "Could not locate the archive move or the stale-sidecar removal in Servy-Dump.ps1"
}

# Act
$sidecarRemovedAfterArchiveMove = $sidecarRemoveIndex -gt $archiveMoveIndex

# Assert
Assert-True "Pre-existing sidecar is removed only after the archive move into place" $sidecarRemovedAfterArchiveMove

Invoke-TestSummary
