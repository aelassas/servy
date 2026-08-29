#Requires -Version 5.1
<#
.SYNOPSIS
    Restores Servy service configurations from a consolidated XML dump archive.

.DESCRIPTION
    Servy-Restore.ps1 verifies the integrity of a Servy backup archive against its SHA-256 sidecar file,
    safely extracts individual service XML configuration files into an ACL-hardened temporary location, and
    imports each configuration into Servy using the official Servy PowerShell module (Import-ServyServiceConfig).
    
    If the -Install switch parameter is supplied, the script also installs each imported service into the Windows
    Service Control Manager (SCM).

    Per-service import errors are caught gracefully. If at least one service imports successfully, remaining files
    in the archive will continue to process and an exit code of 7 is returned to flag an incomplete restore.

    EXIT CODES:
    - 0 : Success. All service configurations were successfully imported (or no XML files exist in the archive).
    - 1 : Execution Failure. The script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Import Failure. The official Servy PowerShell module (Servy.psm1) could not be located or imported.
    - 3 : Target Missing. The specified dump archive file does not exist.
    - 4 : I/O & Extraction Failure. The archive could not be extracted, ACL hardening failed, or malformed entries were detected.
    - 5 : Checksum Verification Failure. The .sha256 sidecar is missing (without -SkipIntegrityCheck) or hash mismatch detected.
    - 6 : Complete Import Failure. No service configurations could be imported from the archive.
    - 7 : Partial Import Warning. The restore completed, but one or more services failed to import.

    CRITICAL SECURITY NOTICE:
    The backup archive being restored contains highly sensitive information, including unencrypted execution
    parameters, command-line arguments, and process environment variables.
    Service logon credentials (Usernames and Passwords) are intentionally excluded from configuration exports.
    Importing configurations resets all service logon accounts to 'LocalSystem' by default.
    You must manually re-enter Logon Usernames and Passwords via Servy Manager, servy-cli, or the Servy PowerShell
    module for any services that require specific custom service runner accounts.

.PARAMETER DumpArchivePath
    Mandatory path specifying the target zip archive file to restore (e.g., 'C:\Backups\Servy_Dump.zip').

.PARAMETER Install
    Optional switch parameter. When present, each imported service configuration is automatically installed
    into the Windows Service Control Manager.

.PARAMETER SkipIntegrityCheck
    Optional switch parameter. Allows restoring from an archive when no .sha256 sidecar hash file exists.

.PARAMETER MaxAllowedEntries
    Optional integer parameter. Specifies the maximum number of entries allowed in the dump archive
    to prevent zip bomb attacks during extraction. Defaults to 1000.

.PARAMETER MaxUncompressedBytes
    Optional 64-bit integer parameter. Specifies the maximum total uncompressed size (in bytes) allowed
    when extracting the dump archive. Defaults to 104857600 bytes (100 MB).

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
    - Execution Privileges: Administrator privileges are required to interact with Servy configurations and managing Windows services.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'Specify path to the Servy dump zip archive (e.g., "C:\Backups\Servy_Backup.zip").')]
    [ValidateNotNullOrEmpty()]
    [string]$DumpArchivePath,

    [Parameter(Mandatory = $false, HelpMessage = 'Optionally install each service into Windows SCM after import.')]
    [switch]$Install,

    [Parameter(Mandatory = $false, HelpMessage = 'Skip SHA-256 sidecar checksum verification if sidecar file is absent.')]
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

# Render non-ASCII service names correctly in console output while preserving original session encoding
$previousOutputEncoding   = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

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
        (Join-Path $env:ProgramFiles 'Servy\Servy.psm1'),
        (Join-Path ${env:ProgramFiles(x86)} 'Servy\Servy.psm1')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $servyModulePath = $moduleCandidates | Select-Object -First 1

    if (-not $servyModulePath) {
        Write-Host "Servy PowerShell module (Servy.psm1) was not found next to this script, in %ProgramFiles%\Servy, or in %ProgramFiles(x86)%\Servy." -ForegroundColor Red
        exit 2
    }

    try {
        Import-Module -Name $servyModulePath -Force -ErrorAction Stop
    }
    catch {
        Write-Host "Failed to import Servy PowerShell module from '$servyModulePath': $_" -ForegroundColor Red
        exit 2
    }

    # Resolve against the PowerShell location, not the process working directory:
    # [Environment]::CurrentDirectory does not follow Set-Location on Windows PowerShell 5.1.
    $resolvedArchivePath = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($DumpArchivePath)

    if (-not (Test-Path -LiteralPath $resolvedArchivePath)) {
        Write-Host "Specified dump archive file does not exist: '$resolvedArchivePath'." -ForegroundColor Red
        exit 3
    }

    # Verify archive integrity against SHA-256 sidecar file if present
    $sidecarPath = "$resolvedArchivePath.sha256"

    if (Test-Path -LiteralPath $sidecarPath) {
        Write-Host "Verifying archive integrity against SHA-256 sidecar..." -ForegroundColor Cyan
        $sidecarText  = [System.IO.File]::ReadAllText($sidecarPath)
        $expectedHash = ($sidecarText.Trim() -split '\s+')[0]
        $actualHash   = (Get-FileHash -LiteralPath $resolvedArchivePath -Algorithm SHA256).Hash

        if (-not [string]::Equals($expectedHash, $actualHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "Archive checksum mismatch! Expected SHA-256 '$expectedHash', but calculated '$actualHash'. Aborting restore." -ForegroundColor Red
            exit 5
        }
        Write-Host "Archive SHA-256 checksum successfully verified." -ForegroundColor Green
    }
    elseif (-not $SkipIntegrityCheck.IsPresent) {
        Write-Host "No SHA-256 sidecar file found at '$sidecarPath'." -ForegroundColor Red
        Write-Host "To proceed without integrity verification, re-run with the -SkipIntegrityCheck switch." -ForegroundColor Red
        exit 5
    }
    else {
        Write-Host "WARNING: Integrity verification skipped (-SkipIntegrityCheck specified)." -ForegroundColor Yellow
    }

    # Create an isolated temporary directory for extracting XML files
    $tempExtractDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyRestore_" + [System.IO.Path]::GetRandomFileName())
    [void][System.IO.Directory]::CreateDirectory($tempExtractDir)

    # Restrict staging directory permissions to Administrators and SYSTEM exclusively
    try {
        $acl = Get-Acl -LiteralPath $tempExtractDir
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($sid in @('S-1-5-32-544', 'S-1-5-18')) {
            $id = New-Object System.Security.Principal.SecurityIdentifier($sid)
            $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
                $id, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        }
        Set-Acl -LiteralPath $tempExtractDir -AclObject $acl
    }
    catch {
        Write-Host "WARNING: Could not restrict permissions on the extraction directory '$tempExtractDir': $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "It will hold UNENCRYPTED PLAIN-TEXT service configurations. Aborting to avoid exposing them." -ForegroundColor Red
        exit 4
    }

    try {
        Write-Host "Extracting dump archive '$resolvedArchivePath'..." -ForegroundColor Cyan

        # Entry-path validation and bounded extraction
        Add-Type -AssemblyName "System.IO.Compression.FileSystem"

        $rootPath = [System.IO.Path]::GetFullPath($tempExtractDir.TrimEnd('\') + '\')
        $zipFile  = [System.IO.Compression.ZipFile]::OpenRead($resolvedArchivePath)

        $totalUncompressedSize = 0L

        try {
            if ($zipFile.Entries.Count -gt $MaxAllowedEntries) {
                Write-Host "Archive contains $($zipFile.Entries.Count) entries, exceeding the limit of $MaxAllowedEntries. Aborting." -ForegroundColor Red
                exit 4
            }

            foreach ($entry in $zipFile.Entries) {
                # Skip directory entries
                if ([string]::IsNullOrEmpty($entry.Name)) { continue }

                # Ensure flat archive assumption: entry name must match FullName
                if ($entry.Name -ne $entry.FullName) {
                    Write-Host "Archive entry '$($entry.FullName)' contains subdirectories. Non-flat dump archives are disallowed. Aborting." -ForegroundColor Red
                    exit 4
                }

                $targetPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($rootPath, $entry.FullName))

                if (-not $targetPath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                    Write-Host "Archive entry '$($entry.FullName)' resolves outside staging directory. Aborting: malformed or hostile archive." -ForegroundColor Red
                    exit 4
                }

                if ([System.IO.Path]::GetExtension($entry.Name).ToLowerInvariant() -ne '.xml') {
                    Write-Host "Archive entry '$($entry.FullName)' is not an XML configuration file. Aborting." -ForegroundColor Red
                    exit 4
                }

                $totalUncompressedSize += $entry.Length
                if ($totalUncompressedSize -gt $MaxUncompressedBytes) {
                    Write-Host "Uncompressed archive size ($totalUncompressedSize bytes) exceeds limit of $MaxUncompressedBytes bytes. Aborting to prevent resource exhaustion." -ForegroundColor Red
                    exit 4
                }

                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $targetPath, $true)
            }
        }
        finally {
            $zipFile.Dispose()
        }

        # Enumerate all XML configuration files recursively in the extracted dump directory
        $xmlFiles = Get-ChildItem -LiteralPath $tempExtractDir -Filter "*.xml" -File -Recurse

        if ($null -eq $xmlFiles -or @($xmlFiles).Count -eq 0) {
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
            exit 7    # Restore completed successfully, but incomplete
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
    # Restore host console encoding state if previously captured
    if ($null -ne $previousOutputEncoding) {
        try { [Console]::OutputEncoding = $previousOutputEncoding } catch { }
    }
}
