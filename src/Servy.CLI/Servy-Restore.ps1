#Requires -Version 5.1
<#
.SYNOPSIS
    Restores Servy service configurations from a consolidated XML dump archive.

.DESCRIPTION
    Servy-Restore.ps1 verifies the integrity of a Servy dump archive against its SHA-256 sidecar file,
    safely extracts individual service XML configuration files into an ACL-hardened temporary location, and
    imports each configuration into Servy using the official Servy PowerShell module (Import-ServyServiceConfig).

    If the -Install switch parameter is supplied, the script also installs each imported service into the Windows
    Service Control Manager (SCM).

    Per-service import errors are caught gracefully; every file in the archive is processed regardless of earlier
    failures. If at least one service imports successfully and one or more fail, an exit code of 7 is returned to
    flag an incomplete restore.

    EXIT CODES:
    - 0 : Success. All service configurations were successfully imported (or no XML files exist in the archive).
    - 1 : Execution Failure. The script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Import Failure. The official Servy PowerShell module (Servy.psm1) could not be located or imported.
    - 3 : Target Missing. The specified dump archive file does not exist.
    - 4 : I/O & Extraction Failure. The archive path is invalid, the archive could not be extracted, ACL hardening failed, malformed entries were detected, the -MaxAllowedEntries or -MaxUncompressedBytes safety limit was exceeded, or an unexpected runtime error occurred.
    - 5 : Checksum Verification Failure. The .sha256 sidecar is missing (without -SkipIntegrityCheck), could not be read, or a hash mismatch was detected.
    - 6 : Complete Import Failure. No service configurations could be imported from the archive.
    - 7 : Partial Import Warning. The restore completed, but one or more services failed to import.

    CRITICAL SECURITY NOTICE:
    The dump archive being restored contains highly sensitive information, including unencrypted execution
    parameters, command-line arguments, and process environment variables.
    Service logon credentials (Usernames and Passwords) are intentionally excluded from configuration exports.
    Importing configurations resets all service logon accounts to 'LocalSystem' by default.
    You must manually re-enter Logon Usernames and Passwords via Servy Manager, servy-cli, or the Servy PowerShell
    module for any services that require specific custom service runner accounts.

.PARAMETER DumpArchivePath
    Mandatory path specifying the Servy dump zip archive file to restore (e.g., 'C:\Backups\Servy_Dump.zip').
    The file must exist; otherwise the script exits with code 3.

.PARAMETER Install
    Optional switch parameter. When present, each imported service configuration is automatically installed
    into the Windows Service Control Manager.

.PARAMETER SkipIntegrityCheck
    Optional switch parameter. Skips SHA-256 sidecar verification entirely: the archive is restored
    without an integrity check, whether the .sha256 sidecar is absent, stale, or mismatching.

.PARAMETER MaxAllowedEntries
    Optional integer parameter. Specifies the maximum number of entries allowed in the dump archive
    to prevent zip bomb attacks during extraction. Defaults to 1000 (range: 1-100,000).

.PARAMETER MaxUncompressedBytes
    Optional 64-bit integer parameter. Specifies the maximum total uncompressed size (in bytes) allowed
    when extracting the dump archive. Defaults to 104857600 bytes / 100 MB (range: 1-10737418240 bytes / 10 GB).

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip"

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -Install

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -SkipIntegrityCheck

.NOTES
    SYSTEM REQUIREMENTS:
    - Operating System: Windows 10, Windows 11, or Windows Server 2016 and later.
    - PowerShell Version: Windows PowerShell 5.1 or PowerShell 7+ (Core).
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
    - Archive Support: System.IO.Compression.FileSystem (.NET 4.5+ assembly).
    - Execution Privileges: Administrator privileges are required to interact with Servy configurations and manage Windows services.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'Specify path to the Servy dump zip archive (e.g., "C:\Backups\Servy_Dump.zip").')]
    [ValidateNotNullOrEmpty()]
    [string]$DumpArchivePath,

    [Parameter(Mandatory = $false, HelpMessage = 'Optionally install each service into Windows SCM after import.')]
    [switch]$Install,

    [Parameter(Mandatory = $false, HelpMessage = 'Skip SHA-256 sidecar verification entirely, whether the sidecar is absent, stale, or mismatching.')]
    [switch]$SkipIntegrityCheck,

    [Parameter(Mandatory = $false, HelpMessage = 'Maximum number of entries permitted in the archive (default: 1000).')]
    [ValidateRange(1, 100000)]
    [int]$MaxAllowedEntries = 1000,

    [Parameter(Mandatory = $false, HelpMessage = 'Maximum total uncompressed byte size permitted during extraction (default: 104857600 = 100 MB).')]
    [ValidateRange(1, 10737418240)] # Up to 10 GB max safety ceiling
    [long]$MaxUncompressedBytes = 104857600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Resolves and normalizes the target dump archive path for Servy-Restore.

.DESCRIPTION
    Resolves relative paths against the PowerShell provider path context.

.PARAMETER DumpArchivePath
    Mandatory path specifying the Servy dump zip archive file to restore.

.PARAMETER PSCmdletContext
    Optional PSCmdlet context for provider path resolution.

.OUTPUTS
    System.String - The resolved absolute archive path.
#>
function Resolve-ServyRestoreDumpPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DumpArchivePath,

        [Parameter(Mandatory = $false)]
        $PSCmdletContext
    )

    if ($null -ne $PSCmdletContext) {
        return $PSCmdletContext.GetUnresolvedProviderPathFromPSPath($DumpArchivePath)
    }
    else {
        if ([System.IO.Path]::IsPathRooted($DumpArchivePath)) {
            return [System.IO.Path]::GetFullPath($DumpArchivePath)
        }
        else {
            return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).ProviderPath $DumpArchivePath))
        }
    }
}

<#
.SYNOPSIS
    Parses the expected SHA-256 checksum string from sidecar file content.

.DESCRIPTION
    Extracts the leading hex checksum token from sidecar text content formatted as '<hash> *<filename>'.

.PARAMETER SidecarText
    Mandatory sidecar file text content string.

.OUTPUTS
    System.String - The extracted expected SHA-256 hash string.
#>
function Get-ServySidecarExpectedHash {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SidecarText
    )

    if ([string]::IsNullOrWhiteSpace($SidecarText)) {
        return $null
    }

    return ($SidecarText.Trim() -split '\s+')[0]
}

<#
.SYNOPSIS
    Validates an individual zip archive entry against security and format rules.

.DESCRIPTION
    Verifies flat directory structure, duplicate entry prevention, path traversal safety, and .xml extension requirements.

.PARAMETER EntryName
    Mandatory entry short name within the archive.

.PARAMETER EntryFullName
    Mandatory full path name of the entry within the archive.

.PARAMETER RootPath
    Mandatory full target extraction root directory path.

.PARAMETER SeenEntryNames
    Mandatory HashSet tracking previously seen entry names for duplicate detection.

.OUTPUTS
    Hashtable containing 'IsValid', 'IsDirectory', 'TargetPath', and 'ErrorMessage'.
#>
function Test-ServyDumpArchiveEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$EntryName,

        [Parameter(Mandatory = $true)]
        [string]$EntryFullName,

        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        $SeenEntryNames
    )

    if ([string]::IsNullOrEmpty($EntryName)) {
        return @{ IsValid = $true; IsDirectory = $true; TargetPath = $null; ErrorMessage = $null }
    }

    if ($EntryName -ne $EntryFullName) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "Archive entry '$EntryFullName' contains subdirectories. Non-flat dump archives are disallowed. Aborting." }
    }

    if (-not $SeenEntryNames.Add($EntryName)) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "Archive contains duplicate entry '$EntryName'. Aborting: malformed dump archive." }
    }

    $targetPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($RootPath, $EntryFullName))

    if (-not $targetPath.StartsWith($RootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "Archive entry '$EntryFullName' resolves outside staging directory. Aborting: malformed or hostile archive." }
    }

    if (-not $EntryName.EndsWith('.xml', [System.StringComparison]::OrdinalIgnoreCase)) {
        return @{ IsValid = $false; IsDirectory = $false; TargetPath = $null; ErrorMessage = "Archive entry '$EntryFullName' is not an XML configuration file. Aborting." }
    }

    return @{ IsValid = $true; IsDirectory = $false; TargetPath = $targetPath; ErrorMessage = $null }
}

# If dot-sourced for testing, return immediately without executing main script body
if ($MyInvocation.InvocationName -eq '.') {
    return
}

# Render non-ASCII service names correctly in console output while preserving original session encoding
$previousOutputEncoding   = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$tempExtractDir = $null

try {
    # Ensure the script is executing with Administrator privileges
    $currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
    $adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator

    if (-not $currentPrincipal.IsInRole($adminRole)) {
        Write-Host "Servy-Restore.ps1 requires Administrator privileges. Please re-run script in an elevated PowerShell session." -ForegroundColor Red
        exit 1
    }

    # Resolve Servy PowerShell module location dynamically (supports portable and non-standard installs)
    $moduleCandidates = @(
        (Join-Path $PSScriptRoot 'Servy.psm1'),
        (Join-Path $env:ProgramFiles 'Servy\Servy.psm1')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $servyModulePath = $moduleCandidates | Select-Object -First 1

    if (-not $servyModulePath) {
        Write-Host "Servy PowerShell module (Servy.psm1) was not found next to this script or in %ProgramFiles%\Servy." -ForegroundColor Red
        exit 2
    }

    try {
        Import-Module -Name $servyModulePath -Force -ErrorAction Stop
    }
    catch {
        Write-Host "Failed to import Servy PowerShell module from '$servyModulePath': $_" -ForegroundColor Red
        exit 2
    }

    # Catch-all for archive path resolution (e.g. invalid path characters or invalid drive letters)
    try {
        $resolvedArchivePath = Resolve-ServyRestoreDumpPath -DumpArchivePath $DumpArchivePath -PSCmdletContext $PSCmdlet
    }
    catch {
        Write-Host "Invalid dump archive path specified '$DumpArchivePath': $_" -ForegroundColor Red
        exit 4
    }

    if (-not (Test-Path -LiteralPath $resolvedArchivePath)) {
        Write-Host "Specified dump archive file does not exist: '$resolvedArchivePath'." -ForegroundColor Red
        exit 3
    }

    # Verify archive integrity against SHA-256 sidecar file if present
    $sidecarPath = "$resolvedArchivePath.sha256"

    if ($SkipIntegrityCheck.IsPresent) {
        Write-Host "WARNING: Integrity verification skipped (-SkipIntegrityCheck specified)." -ForegroundColor Yellow
    }
    elseif (Test-Path -LiteralPath $sidecarPath) {
        Write-Host "Verifying archive integrity against SHA-256 sidecar..." -ForegroundColor Cyan

        try {
            $sidecarText  = [System.IO.File]::ReadAllText($sidecarPath)
            $expectedHash = Get-ServySidecarExpectedHash -SidecarText $sidecarText
            $actualHash   = (Get-FileHash -LiteralPath $resolvedArchivePath -Algorithm SHA256).Hash
        }
        catch {
            Write-Host "Failed to read checksum files or compute hash for verification: $_" -ForegroundColor Red
            exit 5
        }

        if (-not [string]::Equals($expectedHash, $actualHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "Archive checksum mismatch! Expected SHA-256 '$expectedHash', but calculated '$actualHash'. Aborting restore." -ForegroundColor Red
            exit 5
        }
        Write-Host "Archive SHA-256 checksum successfully verified." -ForegroundColor Green
    }
    else {
        Write-Host "No SHA-256 sidecar file found at '$sidecarPath'." -ForegroundColor Red
        Write-Host "To proceed without integrity verification, re-run with the -SkipIntegrityCheck switch." -ForegroundColor Red
        exit 5
    }

    # Create an isolated temporary directory for extracting XML files inside try/finally scope
    $tempExtractDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyRestore_" + [System.IO.Path]::GetRandomFileName())

    try {
        [void][System.IO.Directory]::CreateDirectory($tempExtractDir)

        # Restrict staging directory permissions to Administrators and SYSTEM exclusively
        try {
            Set-ServyHardenedFileAcl -Path $tempExtractDir -IsDirectory
        }
        catch {
            Write-Host "WARNING: Could not restrict permissions on the extraction directory '$tempExtractDir': $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "It will hold UNENCRYPTED PLAIN-TEXT service configurations. Aborting to avoid exposing them." -ForegroundColor Red
            exit 4
        }

        Write-Host "Extracting dump archive '$resolvedArchivePath'..." -ForegroundColor Cyan

        # Entry-path validation and bounded extraction
        Add-Type -AssemblyName "System.IO.Compression.FileSystem"

        $rootPath = [System.IO.Path]::GetFullPath($tempExtractDir.TrimEnd('\') + '\')
        $zipFile  = [System.IO.Compression.ZipFile]::OpenRead($resolvedArchivePath)

        $totalUncompressedSize = 0L
        $seenEntryNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

        try {
            if ($zipFile.Entries.Count -gt $MaxAllowedEntries) {
                Write-Host "Archive contains $($zipFile.Entries.Count) entries, exceeding the limit of $MaxAllowedEntries. Aborting." -ForegroundColor Red
                exit 4
            }

            foreach ($entry in $zipFile.Entries) {
                $validation = Test-ServyDumpArchiveEntry -EntryName $entry.Name -EntryFullName $entry.FullName -RootPath $rootPath -SeenEntryNames $seenEntryNames

                if (-not $validation.IsValid) {
                    Write-Host $validation.ErrorMessage -ForegroundColor Red
                    exit 4
                }

                if ($validation.IsDirectory) { continue }

                $targetPath = $validation.TargetPath

                # Stream-read and enforce MaxUncompressedBytes on actual decompressed bytes to prevent zip bombs with forged metadata length
                $in  = $entry.Open()
                $out = [System.IO.File]::Create($targetPath)
                try {
                    $buf = New-Object byte[] 81920
                    while (($n = $in.Read($buf, 0, $buf.Length)) -gt 0) {
                        $totalUncompressedSize += $n
                        if ($totalUncompressedSize -gt $MaxUncompressedBytes) {
                            Write-Host "Uncompressed data exceeds limit of $MaxUncompressedBytes bytes. Aborting to prevent resource exhaustion." -ForegroundColor Red
                            exit 4
                        }
                        $out.Write($buf, 0, $n)
                    }
                }
                finally {
                    $out.Dispose()
                    $in.Dispose()
                }
            }
        }
        finally {
            $zipFile.Dispose()
        }

        # Enumerate the XML configuration files in the extracted dump directory (archive layout is enforced flat)
        $xmlFiles = Get-ChildItem -LiteralPath $tempExtractDir -Filter "*.xml" -File

        if ($null -eq $xmlFiles) {
            Write-Host "No XML configuration files were found in the dump archive." -ForegroundColor Yellow
            exit 0
        }

        $xmlFileList = @($xmlFiles)
        Write-Host "Found $($xmlFileList.Count) service configuration file(s) to restore..." -ForegroundColor Cyan

        $imported = New-Object System.Collections.Generic.List[string]
        $failed   = New-Object System.Collections.Generic.List[object]

        # Iterate through extracted XML files and import each service configuration with isolated error handling
        foreach ($xmlFile in $xmlFileList) {
            Write-Host "Importing configuration from '$($xmlFile.Name)'..." -ForegroundColor Green

            # Build splatting hashtable for Import-ServyServiceConfig
            $importParams = @{
                ConfigFileType = "xml"
                Path           = $xmlFile.FullName
            }

            if ($Install.IsPresent) {
                $importParams['Install'] = $true
            }

            try {
                # Invoke Servy cmdlet to import (and optionally install) the service configuration
                Import-ServyServiceConfig @importParams
                $imported.Add($xmlFile.Name)
            }
            catch {
                Write-Host "  FAILED to import '$($xmlFile.Name)': $($_.Exception.Message)" -ForegroundColor Red
                $failed.Add([PSCustomObject]@{ File = $xmlFile.Name; Reason = $_.Exception.Message })
            }
        }

        # If zero configurations succeeded, terminate with complete failure exit code
        if ($imported.Count -eq 0) {
            Write-Host "No service configurations could be imported from the archive." -ForegroundColor Red
            exit 6
        }

        # Display completion status and critical security notice
        if ($failed.Count -gt 0) {
            Write-Host "`nServy configuration restore completed with warnings!" -ForegroundColor Yellow
            Write-Host "Successfully imported $($imported.Count) of $($xmlFileList.Count) service(s)." -ForegroundColor Cyan
            Write-Host "`nThe following service file(s) FAILED to import:" -ForegroundColor Red
            $failed | Format-Table -AutoSize | Out-String | Write-Host
        }
        else {
            Write-Host "`nServy configuration restore completed successfully!" -ForegroundColor Green
            Write-Host "Successfully imported $($imported.Count) of $($xmlFileList.Count) service(s)." -ForegroundColor Cyan
        }

        Write-Host @"

================================================================================
CRITICAL SECURITY NOTICE:
================================================================================
The restored dump archive contains highly sensitive information!
- Service execution parameters, environment variables, and startup arguments
  were restored from unencrypted plain-text XML configuration files.
- Ensure the backup zip file is stored securely and access is restricted.

NOTE ON SERVICE RESTORATION & CREDENTIALS:
- Service logon Usernames and Passwords were NOT exported for security reasons.
- Restoring configurations via Servy-Restore.ps1 automatically resets all
  service logon accounts to 'LocalSystem' by default.
- You must manually re-enter Logon Usernames and Passwords via Servy Manager,
  servy-cli, or the PowerShell module for any services that require specific
  custom service runner accounts.
================================================================================
"@ -ForegroundColor Yellow

        if ($failed.Count -gt 0) {
            exit 7    # Restore completed, but one or more services failed to import
        }
    }
    catch {
        Write-Host "`nServy configuration restore FAILED: $_" -ForegroundColor Red
        exit 4
    }
    finally {
        # Clean up temporary extraction directory and extracted XML files with explicit failure reporting
        if (Test-Path -LiteralPath $tempExtractDir) {
            Remove-Item -LiteralPath $tempExtractDir -Recurse -Force -ErrorAction SilentlyContinue

            if (Test-Path -LiteralPath $tempExtractDir) {
                Write-Host @"

================================================================================
WARNING: EXTRACTION CLEANUP FAILURE DETECTED
================================================================================
The temporary extraction directory could not be fully removed:
  $tempExtractDir

It contains UNENCRYPTED PLAIN-TEXT service configurations.
Please delete this directory manually to prevent credential/config leaks.
================================================================================
"@ -ForegroundColor Red
            }
        }
    }
}
finally {
    # Restore host console encoding
    try { [Console]::OutputEncoding = $previousOutputEncoding } catch { }
}
