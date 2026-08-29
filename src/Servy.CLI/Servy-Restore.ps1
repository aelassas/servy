#Requires -Version 2.0
<#
.SYNOPSIS
    Restores Servy service configurations from a consolidated XML dump archive.

.DESCRIPTION
    Servy-Restore.ps1 verifies the integrity of a Servy backup archive against its SHA-256 sidecar file,
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
    If a directory path or trailing separator is provided, it writes to `$DumpArchivePath\Servy_Dump.zip`; if no file extension is specified, `.zip` is appended.
    Use `-Overwrite` to replace an existing archive.

.PARAMETER Install
    Optional switch parameter. When present, each imported service configuration is automatically installed
    into the Windows Service Control Manager.

.PARAMETER SkipIntegrityCheck
    Optional switch parameter. Allows restoring from an archive whether or not a .sha256 sidecar exists.

.PARAMETER MaxAllowedEntries
    Optional integer parameter. Specifies the maximum number of entries allowed in the dump archive
    to prevent zip bomb attacks during extraction. Defaults to 1000 (range: 1–100,000).

.PARAMETER MaxUncompressedBytes
    Optional 64-bit integer parameter. Specifies the maximum total uncompressed size (in bytes) allowed
    when extracting the dump archive. Defaults to 104857600 bytes / 100 MB (range: 1–10737418240 bytes / 10 GB).

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip"

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -Install

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -SkipIntegrityCheck

.NOTES
    SYSTEM REQUIREMENTS:
    - Operating System: Windows 7 SP1, Windows Server 2008 R2, or later.
    - PowerShell Version: Windows PowerShell 2.0 or higher.
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
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
    [long]$MaxUncompressedBytes = [long]104857600
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Render non-ASCII service names correctly on PS3+ while using $OutputEncoding on PS2
$previousOutputEncoding = $null
if ($PSVersionTable.PSVersion.Major -ge 3) {
    try { $previousOutputEncoding = [Console]::OutputEncoding } catch { }
    try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
}
else {
    try { $previousOutputEncoding = $OutputEncoding } catch { }
    try { $OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
}

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

    # Resolve script directory safely across PowerShell 2.0 ($MyInvocation) and PowerShell 3.0+ ($PSScriptRoot)
    if ($PSVersionTable.PSVersion.Major -ge 3) {
        $scriptDir = $PSScriptRoot
    }
    else {
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
    }

    # Resolve Servy PowerShell module location dynamically (supports portable and non-standard installs)
    $moduleCandidates = @(
        (Join-Path $scriptDir 'Servy.psm1'),
        (Join-Path $env:ProgramFiles 'Servy\Servy.psm1'),
        (Join-Path ${env:ProgramFiles(x86)} 'Servy\Servy.psm1')
    ) | Where-Object { $_ -and (Test-Path -Path $_) }

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

    # Resolve path safely across PowerShell 2.0 and 3.0+
    if ($PSVersionTable.PSVersion.Major -ge 3) {
        $resolvedArchivePath = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($DumpArchivePath)
    }
    else {
        if ([System.IO.Path]::IsPathRooted($DumpArchivePath)) {
            $resolvedArchivePath = [System.IO.Path]::GetFullPath($DumpArchivePath)
        }
        else {
            $resolvedArchivePath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).ProviderPath $DumpArchivePath))
        }
    }

    if (-not (Test-Path -Path $resolvedArchivePath)) {
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
        
        $sidecarText = [System.IO.File]::ReadAllText($sidecarPath)
        $expectedHash = ($sidecarText.Trim() -split '\s+')[0]
        
        $hashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        $stream = [System.IO.File]::OpenRead($resolvedArchivePath)
        try {
            $rawBytes = $hashAlgorithm.ComputeHash($stream)
            $hashBuilder = New-Object System.Text.StringBuilder
            foreach ($b in $rawBytes) { [void]$hashBuilder.Append($b.ToString("X2")) }
            $actualHash = $hashBuilder.ToString()
        }
        finally {
            $stream.Close()
            $stream.Dispose()
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
            $acl = Get-Acl -Path $tempExtractDir
            $acl.SetAccessRuleProtection($true, $false)
            foreach ($sid in @('S-1-5-32-544', 'S-1-5-18')) {
                $id = New-Object System.Security.Principal.SecurityIdentifier($sid)
                $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
                    $id, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
            }
            Set-Acl -Path $tempExtractDir -AclObject $acl
        }
        catch {
            Write-Host "WARNING: Could not restrict permissions on the extraction directory '$tempExtractDir': $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "It will hold UNENCRYPTED PLAIN-TEXT service configurations. Aborting to avoid exposing them." -ForegroundColor Red
            exit 4
        }

        Write-Host "Extracting dump archive '$resolvedArchivePath'..." -ForegroundColor Cyan

        # Safe entry-path validation and bounded archive extraction
        $rootPath = [System.IO.Path]::GetFullPath($tempExtractDir.TrimEnd('\') + '\')
        
        [long]$totalUncompressedSize = 0L

        $zipFileType = $null
        try {
            [void][System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
            if ([System.IO.Compression.ZipFile]) {
                $zipFileType = [System.IO.Compression.ZipFile]
            }
        }
        catch {
            try {
                Add-Type -AssemblyName "System.IO.Compression.FileSystem" -ErrorAction SilentlyContinue
                if ([System.IO.Compression.ZipFile]) {
                    $zipFileType = [System.IO.Compression.ZipFile]
                }
            }
            catch { }
        }

        if ($null -ne $zipFileType) {
            $zipFile = $zipFileType::OpenRead($resolvedArchivePath)
            try {
                if ($zipFile.Entries.Count -gt $MaxAllowedEntries) {
                    Write-Host "Archive contains $($zipFile.Entries.Count) entries, exceeding limit of $MaxAllowedEntries. Aborting." -ForegroundColor Red
                    exit 4
                }

                foreach ($entry in $zipFile.Entries) {
                    if ([string]::IsNullOrEmpty($entry.Name)) { continue }

                    if ($entry.Name -ne $entry.FullName) {
                        Write-Host "Archive entry '$($entry.FullName)' contains subdirectories. Non-flat dump archives are disallowed. Aborting." -ForegroundColor Red
                        exit 4
                    }

                    $targetPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($rootPath, $entry.FullName))

                    if (-not $targetPath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                        Write-Host "Archive entry '$($entry.FullName)' resolves outside staging directory. Aborting: malformed or hostile archive." -ForegroundColor Red
                        exit 4
                    }

                    if (-not $entry.Name.EndsWith('.xml', [System.StringComparison]::OrdinalIgnoreCase)) {
                        Write-Host "Archive entry '$($entry.FullName)' is not an XML configuration file. Aborting." -ForegroundColor Red
                        exit 4
                    }

                    # Stream-read and enforce MaxUncompressedBytes on actual decompressed bytes using explicit [long] casting
                    $in  = $entry.Open()
                    $out = [System.IO.File]::Create($targetPath)
                    try {
                        $buf = New-Object byte[] 81920
                        while (($n = $in.Read($buf, 0, $buf.Length)) -gt 0) {
                            $totalUncompressedSize = [long]($totalUncompressedSize + [long]$n)
                            if ($totalUncompressedSize -gt [long]$MaxUncompressedBytes) {
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
        }
        else {
            # Fallback for legacy environments without System.IO.Compression.FileSystem (.NET 3.5)
            if (Get-Command -Name "Expand-Archive" -ErrorAction SilentlyContinue) {
                Expand-Archive -Path $resolvedArchivePath -DestinationPath $tempExtractDir -Force
            }
            else {
                $shellApp = New-Object -ComObject Shell.Application
                $zipPackage = $shellApp.NameSpace($resolvedArchivePath)
                $destinationFolder = $shellApp.NameSpace($tempExtractDir)
                $destinationFolder.CopyHere($zipPackage.Items(), 20)

                while ($destinationFolder.Items().Count -lt $zipPackage.Items().Count) {
                    Start-Sleep -Milliseconds 500
                }
            }

            # Post-extraction validation fallback for legacy COM extraction
            $extractedItems = Get-ChildItem -Path $tempExtractDir -Recurse
            if ($null -ne $extractedItems -and @($extractedItems).Count -gt $MaxAllowedEntries) {
                Write-Host "Extracted archive exceeds limit of $MaxAllowedEntries items. Aborting." -ForegroundColor Red
                exit 4
            }

            foreach ($item in $extractedItems) {
                if ($item.PSIsContainer) {
                    Write-Host "Archive contains subdirectories. Non-flat dump archives are disallowed. Aborting." -ForegroundColor Red
                    exit 4
                }
                if (-not $item.Name.EndsWith('.xml', [System.StringComparison]::OrdinalIgnoreCase)) {
                    Write-Host "Archive entry '$($item.Name)' is not an XML configuration file. Aborting." -ForegroundColor Red
                    exit 4
                }
                if (-not $item.PSIsContainer) {
                    $totalUncompressedSize = [long]($totalUncompressedSize + [long]$item.Length)
                }
            }

            if ($totalUncompressedSize -gt [long]$MaxUncompressedBytes) {
                Write-Host "Uncompressed archive size ($totalUncompressedSize bytes) exceeds limit of $MaxUncompressedBytes bytes. Aborting to prevent resource exhaustion." -ForegroundColor Red
                exit 4
            }
        }

        # Enumerate all XML configuration files recursively in the extracted dump directory
        $xmlFiles = Get-ChildItem -Path $tempExtractDir -Recurse | Where-Object { -not $_.PSIsContainer -and $_.Name.EndsWith(".xml", [System.StringComparison]::OrdinalIgnoreCase) }

        if ($null -eq $xmlFiles -or @($xmlFiles).Count -eq 0) {
            Write-Host "No XML configuration files were found in the dump archive." -ForegroundColor Yellow
            exit 0
        }

        $xmlFileList = @($xmlFiles)
        Write-Host "Found $($xmlFileList.Count) service configuration file(s) to restore..." -ForegroundColor Cyan

        $imported = New-Object System.Collections.Generic.List[string]
        $failed   = New-Object System.Collections.Generic.List[object]

        # Iterate through extracted XML files and import each service configuration with per-item exception isolation
        foreach ($xmlFile in $xmlFileList) {
            Write-Host "Importing configuration from '$($xmlFile.Name)'..." -ForegroundColor Green

            try {
                if ($Install.IsPresent) {
                    Import-ServyServiceConfig -ConfigFileType "xml" -Path $xmlFile.FullName -Install
                }
                else {
                    Import-ServyServiceConfig -ConfigFileType "xml" -Path $xmlFile.FullName
                }
                $imported.Add($xmlFile.Name)
            }
            catch {
                Write-Host "  FAILED to import '$($xmlFile.Name)': $($_.Exception.Message)" -ForegroundColor Red
                
                # PowerShell 2.0 compatible property assignment for error array
                $errObj = New-Object PSObject
                $errObj | Add-Member -MemberType NoteProperty -Name "File" -Value $xmlFile.Name
                $errObj | Add-Member -MemberType NoteProperty -Name "Reason" -Value $_.Exception.Message
                $failed.Add($errObj)
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
        if ($null -ne $tempExtractDir -and (Test-Path -Path $tempExtractDir)) {
            Remove-Item -Path $tempExtractDir -Recurse -Force -ErrorAction SilentlyContinue

            if (Test-Path -Path $tempExtractDir) {
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
    # Restore original console output encoding if altered
    if ($null -ne $previousOutputEncoding) {
        if ($PSVersionTable.PSVersion.Major -ge 3) {
            try { [Console]::OutputEncoding = $previousOutputEncoding } catch { }
        }
        else {
            try { $OutputEncoding = $previousOutputEncoding } catch { }
        }
    }
}
