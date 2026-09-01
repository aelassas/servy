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
    . $scriptPath -DestinationArchivePath "dummy"
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
        if ($rule.IdentityReference.Equals($adminSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) { $hasAdmin = $true }

        if ($rule.IdentityReference.Equals($systemSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) { $hasSystem = $true }
    }

    Assert-True "File ACL grants Allow FullControl to Builtin Administrators" $hasAdmin
    Assert-True "File ACL grants Allow FullControl to Local SYSTEM" $hasSystem

    # Hardening test on Directory
    Set-ServyHardenedFileAcl -Path $tempTestDir -IsDirectory
    $dirAcl = Get-Acl -LiteralPath $tempTestDir

    Assert-True "Directory permission inheritance is protected/broken" $dirAcl.AreAccessRulesProtected

    $dirRules = $dirAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
    Assert-Equal "Directory ACL contains exactly 2 access control entries" $dirRules.Count 2

    $hasDirAdmin  = $false
    $hasDirSystem = $false

    foreach ($rule in $dirRules) {
        if ($rule.IdentityReference.Equals($adminSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.InheritanceFlags -eq "ContainerInherit, ObjectInherit" -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) { $hasDirAdmin = $true }

        if ($rule.IdentityReference.Equals($systemSid) -and
            $rule.FileSystemRights -eq [System.Security.AccessControl.FileSystemRights]::FullControl -and
            $rule.InheritanceFlags -eq "ContainerInherit, ObjectInherit" -and
            $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow) { $hasDirSystem = $true }
    }

    Assert-True "Directory ACL grants Allow FullControl ContainerInherit+ObjectInherit to Administrators" $hasDirAdmin
    Assert-True "Directory ACL grants Allow FullControl ContainerInherit+ObjectInherit to SYSTEM" $hasDirSystem
}
finally {
    if (Test-Path -LiteralPath $tempTestFile) { Remove-Item -LiteralPath $tempTestFile -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $tempTestDir)  { Remove-Item -LiteralPath $tempTestDir -Recurse -Force -ErrorAction SilentlyContinue }
}

Invoke-TestSummary
