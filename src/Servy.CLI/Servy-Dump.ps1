#Requires -Version 5.1
<#
.SYNOPSIS
    Generates a consolidated Servy backup archive containing all service configurations in XML format.

.DESCRIPTION
    Servy-Dump.ps1 inspects the local Servy SQLite configuration database (%ProgramData%\Servy\db\Servy.db),
    retrieves all registered service definitions using Windows native winsqlite3.dll, and exports each service
    configuration into an individual XML file using the official Servy PowerShell module. The exported XML files
    are then compressed into a single zip archive along with a SHA-256 sidecar file for integrity verification.

    Per-service export errors are caught gracefully. If at least one service exports successfully and one or more
    fail, the zip archive is still generated and an exit code of 7 is returned to flag an incomplete backup to
    automated workflows.

    EXIT CODES:
    - 0 : Success. All registered service configurations were successfully exported and archived (or no services exist).
    - 1 : Execution Failure. The script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Import Failure. The official Servy PowerShell module (Servy.psm1) could not be located or imported.
    - 3 : Target Conflict. The destination archive file already exists and the -Overwrite switch was not specified.
    - 4 : I/O & Inspection Failure. The database could not be read, the target destination path/directory is unwritable, or ACL hardening failed.
    - 5 : Setup Compilation Failure. Failed to compile native SQLite dynamic P/Invoke assembly bindings.
    - 6 : Complete Export Failure. No service configurations could be exported; no output archive was generated.
    - 7 : Partial Export Warning. The dump archive was successfully created, but one or more services failed to export.
    - 8 : Archive Staging Mismatch. Staged configuration count does not match exported count; dump aborted.

    CRITICAL SECURITY NOTICE:
    The generated backup archive is highly sensitive. Exported XML configuration files contain sensitive plain-text
    data including execution parameters, command-line arguments, and process environment variables.
    Note that Windows Service Account credentials (Usernames and Passwords) are intentionally excluded from exports.
    When restoring configurations via Servy Manager, servy-cli, or Servy-Restore.ps1, all imported services will
    default to 'LocalSystem' and passwords must be re-entered manually for security reasons.

.PARAMETER DestinationArchivePath
    Mandatory path specifying the target zip archive destination file (e.g., 'C:\Backups\Servy_Dump.zip').

.PARAMETER Overwrite
    Optional switch parameter. Forces the script to overwrite the destination dump archive if it already exists.

.EXAMPLE
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip"

.EXAMPLE
    .\Servy-Dump.ps1 -DestinationArchivePath "C:\Backups\Servy_Dump.zip" -Overwrite

.NOTES
    SYSTEM REQUIREMENTS:
    - Operating System: Windows 10, Windows 11, or Windows Server 2016 and later (requires native %SystemRoot%\System32\winsqlite3.dll).
    - PowerShell Version: Windows PowerShell 5.1 or PowerShell 7+ (Core).
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
    - SQLite Engine: Windows native WinRT/Win32 SQLite library (winsqlite3.dll); no external SQLite DLL drivers required.
    - Execution Privileges: Administrator privileges are required to interact with %ProgramData%\Servy and invoke Servy cmdlets.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'Specify target archive output file path (e.g., "C:\Backups\Servy_Backup.zip").')]
    [ValidateNotNullOrEmpty()]
    [string]$DestinationArchivePath,

    [Parameter(Mandatory = $false, HelpMessage = 'Force overwrite of the target dump archive if it already exists.')]
    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Render non-ASCII service names correctly in console output while preserving original session encoding
$previousOutputEncoding   = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$createdParentPath = $null

try {
    # Ensure the script is executing with Administrator privileges
    $currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
    $adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator

    if (-not $currentPrincipal.IsInRole($adminRole)) {
        Write-Host "Servy-Dump.ps1 requires Administrator privileges. Please re-run script in an elevated PowerShell session." -ForegroundColor Red
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
    $resolvedArchivePath = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($DestinationArchivePath)

    if ([string]::IsNullOrEmpty([System.IO.Path]::GetExtension($resolvedArchivePath))) {
        $resolvedArchivePath += '.zip'
        Write-Host "No file extension specified; normalized destination to '$resolvedArchivePath'." -ForegroundColor Yellow
    }

    # Check if destination dump file already exists
    if (Test-Path -LiteralPath $resolvedArchivePath) {
        if (-not $Overwrite.IsPresent) {
            Write-Host "Destination dump file already exists: '$resolvedArchivePath'. Operation aborted to prevent overwriting." -ForegroundColor Red
            exit 3
        }
        Write-Host "Existing dump archive found. -Overwrite specified; target file will be replaced." -ForegroundColor Yellow
    }

    # Prove destination parent directory is created and writable BEFORE exporting
    $parentDir = [System.IO.Path]::GetDirectoryName($resolvedArchivePath)

    if (-not [string]::IsNullOrEmpty($parentDir)) {
        if (-not (Test-Path -LiteralPath $parentDir)) {
            try {
                [void][System.IO.Directory]::CreateDirectory($parentDir)
                $createdParentPath = $parentDir
            }
            catch {
                Write-Host "Cannot create target destination directory '$parentDir': $_" -ForegroundColor Red
                exit 4
            }
        }

        # Write probe confirmation
        $probeFile = [System.IO.Path]::Combine($parentDir, ".servydump_probe_" + [System.IO.Path]::GetRandomFileName())
        try {
            [System.IO.File]::WriteAllBytes($probeFile, @())
            Remove-Item -LiteralPath $probeFile -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Target destination directory '$parentDir' is not writable: $_" -ForegroundColor Red
            exit 4
        }
    }

    # Validate existence of the Servy SQLite database file
    $dbPath = [System.IO.Path]::Combine($env:ProgramData, "Servy", "db", "Servy.db")

    if (-not (Test-Path -LiteralPath $dbPath)) {
        Write-Host "Servy database not found at '$dbPath'. No services exist to export." -ForegroundColor Yellow
        exit 0
    }

    # Register C# P/Invoke wrapper targeting Windows native %SystemRoot%\System32\winsqlite3.dll with UTF-16 marshaling
    if (-not ([System.Management.Automation.PSTypeName]'ServyNativeWinSqlite16').Type) {
        $sqliteBinding = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class ServyNativeWinSqlite16
{
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2(byte[] filenameUtf8, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text16", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int iCol);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_finalize(IntPtr stmt);

    public static List<string> GetServiceNames(string dbPath)
    {
        List<string> result = new List<string>();
        IntPtr db;
        
        byte[] pathUtf8 = System.Text.Encoding.UTF8.GetBytes(dbPath + "\0");
        int rc = sqlite3_open_v2(pathUtf8, out db, 0x1 /* SQLITE_OPEN_READONLY */, IntPtr.Zero);
        if (rc != 0)
        {
            if (db != IntPtr.Zero) sqlite3_close(db);
            throw new InvalidOperationException(string.Format("sqlite3_open_v2 failed on '{0}' with result code {1}.", dbPath, rc));
        }

        try
        {
            IntPtr stmt;
            rc = sqlite3_prepare_v2(db, "SELECT Name FROM Services ORDER BY Name", -1, out stmt, IntPtr.Zero);
            if (rc != 0)
            {
                throw new InvalidOperationException(string.Format("sqlite3_prepare_v2 failed with result code {0}.", rc));
            }

            try
            {
                int stepRc;
                while ((stepRc = sqlite3_step(stmt)) == 100) // SQLITE_ROW = 100
                {
                    IntPtr ptr = sqlite3_column_text16(stmt, 0);
                    if (ptr != IntPtr.Zero)
                    {
                        result.Add(Marshal.PtrToStringUni(ptr));
                    }
                }
                if (stepRc != 101) // SQLITE_DONE
                {
                    throw new InvalidOperationException(string.Format("sqlite3_step failed with result code {0}; service list may be incomplete.", stepRc));
                }
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }
        finally
        {
            sqlite3_close(db);
        }

        return result;
    }
}
"@
        try {
            Add-Type -TypeDefinition $sqliteBinding
        }
        catch {
            Write-Host "Failed to compile winsqlite3 P/Invoke binding assembly: $_" -ForegroundColor Red
            exit 5
        }
    }

    # Create an isolated temporary directory for staging exported XML files
    $tempStagingDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyDump_" + [System.IO.Path]::GetRandomFileName())
    [void][System.IO.Directory]::CreateDirectory($tempStagingDir)

    # Restrict staging directory permissions to Administrators and SYSTEM exclusively
    try {
        $acl = Get-Acl -LiteralPath $tempStagingDir
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($sid in @('S-1-5-32-544', 'S-1-5-18')) {
            $id = New-Object System.Security.Principal.SecurityIdentifier($sid)
            $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
                $id, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        }
        Set-Acl -LiteralPath $tempStagingDir -AclObject $acl
    }
    catch {
        Write-Host "WARNING: Could not restrict permissions on the staging directory '$tempStagingDir': $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "It will hold UNENCRYPTED PLAIN-TEXT service configurations. Aborting to avoid exposing them." -ForegroundColor Red
        exit 4
    }

    try {
        # Query Servy SQLite database via Windows native winsqlite3.dll
        try {
            $serviceNames = [ServyNativeWinSqlite16]::GetServiceNames($dbPath)
        }
        catch {
            Write-Host "Failed to query Servy database at '$dbPath': $($_.Exception.Message)" -ForegroundColor Red
            exit 4
        }

        if ($null -eq $serviceNames -or $serviceNames.Count -eq 0) {
            Write-Host "No services were found in the database at '$dbPath'." -ForegroundColor Yellow
            exit 0
        }

        Write-Host "Found $($serviceNames.Count) service(s) to export..." -ForegroundColor Cyan

        $exported      = New-Object System.Collections.Generic.List[string]
        $failed        = New-Object System.Collections.Generic.List[object]
        $usedBaseNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        $invalidChars  = [System.IO.Path]::GetInvalidFileNameChars()

        # Export each service configuration into individual XML files with per-item exception isolation
        foreach ($serviceName in $serviceNames) {
            # Sanitize service name for safe filesystem usage
            $baseFileName = $serviceName
            foreach ($char in $invalidChars) {
                $baseFileName = $baseFileName.Replace($char, '_')
            }

            # Disambiguate names that sanitize onto an existing file
            $candidateName = $baseFileName
            $suffixCounter = 1
            while (-not $usedBaseNames.Add($candidateName)) {
                $candidateName = "{0}_{1}" -f $baseFileName, $suffixCounter
                $suffixCounter++
            }

            if ($candidateName -ne $baseFileName) {
                Write-Host "  Name collision: '$serviceName' sanitizes to '$baseFileName'; writing '$candidateName.xml' instead." -ForegroundColor Yellow
            }

            $xmlExportPath = [System.IO.Path]::Combine($tempStagingDir, "$candidateName.xml")

            Write-Host "Exporting configuration for '$serviceName' -> '$candidateName.xml'..." -ForegroundColor Green

            try {
                Export-ServyServiceConfig -Name $serviceName -ConfigFileType "xml" -Path $xmlExportPath
                $exported.Add($serviceName)
            }
            catch {
                Write-Host "  FAILED to export '$serviceName': $($_.Exception.Message)" -ForegroundColor Red
                $failed.Add([PSCustomObject]@{ Service = $serviceName; Reason = $_.Exception.Message })
            }
        }

        # If zero configurations succeeded, terminate without creating an empty archive
        if ($exported.Count -eq 0) {
            Write-Host "No service configurations could be exported. No dump archive was generated." -ForegroundColor Red
            exit 6
        }

        # Assert staged configuration count matches successful exports before compressing
        $stagedXmlFiles = Get-ChildItem -LiteralPath $tempStagingDir -Filter "*.xml" -File
        $stagedCount    = if ($null -eq $stagedXmlFiles) { 0 } else { @($stagedXmlFiles).Count }

        if ($stagedCount -ne $exported.Count) {
            Write-Host "Expected $($exported.Count) exported configuration(s) but staged $stagedCount. Refusing to write an incomplete archive." -ForegroundColor Red
            exit 8
        }

        # Compress the staging directory containing XML dumps into the target zip file
        Write-Host "Compressing exported configurations into zip archive..." -ForegroundColor Cyan

        $stagedItemsToCompress = Get-ChildItem -LiteralPath $tempStagingDir -File | Select-Object -ExpandProperty FullName

        $compressParams = @{
            LiteralPath      = $stagedItemsToCompress
            DestinationPath  = $resolvedArchivePath
            CompressionLevel = "Optimal"
        }

        if ($Overwrite.IsPresent) {
            $compressParams['Force'] = $true
        }

        try {
            Compress-Archive @compressParams
        }
        catch {
            Write-Host "`nServy configuration dump FAILED during compression: $_" -ForegroundColor Red
            Write-Host "No archive was produced at '$resolvedArchivePath'." -ForegroundColor Red
            exit 4
        }

        # Emit SHA-256 sidecar hash file for integrity verification
        $hashValue = (Get-FileHash -LiteralPath $resolvedArchivePath -Algorithm SHA256).Hash
        $sidecarPath = "$resolvedArchivePath.sha256"
        [System.IO.File]::WriteAllText($sidecarPath, "$hashValue *$([System.IO.Path]::GetFileName($resolvedArchivePath))`n", (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "SHA-256 checksum sidecar written -> '$sidecarPath'" -ForegroundColor Cyan

        # Display completion status and critical security warning
        Write-Host "`nServy configuration dump completed!" -ForegroundColor Green
        Write-Host "Successfully exported $($exported.Count) of $($serviceNames.Count) service(s)." -ForegroundColor Cyan
        Write-Host "Dump location: $resolvedArchivePath" -ForegroundColor Cyan

        if ($failed.Count -gt 0) {
            Write-Host "`nThe following service(s) FAILED to export and were NOT included in the dump archive:" -ForegroundColor Red
            $failed | Format-Table -AutoSize | Out-String | Write-Host
        }

        Write-Host @"

================================================================================
CRITICAL SECURITY WARNING:
================================================================================
The generated dump archive contains highly sensitive information!
- Service execution parameters, environment variables, and startup arguments
  are stored in unencrypted plain-text within the exported XML files.
- Protect this file accordingly and restrict access to authorized admins.

NOTE ON SERVICE RESTORATION:
- Service logon Usernames and Passwords are NOT exported for security reasons.
- Restoring this backup via Servy-Restore.ps1, servy-cli, or Servy Manager will
  automatically set all service logon accounts to 'LocalSystem' by default.
- You must manually re-enter Logon Usernames and Passwords for any services that
  require specific custom service runner accounts.
================================================================================
"@ -ForegroundColor Yellow

        if ($failed.Count -gt 0) {
            exit 7    # Archive generated successfully, but incomplete
        }
    }
    finally {
        # Clean up temporary staging directory and XML files with explicit failure reporting
        if (Test-Path -LiteralPath $tempStagingDir) {
            Remove-Item -LiteralPath $tempStagingDir -Recurse -Force -ErrorAction SilentlyContinue

            if (Test-Path -LiteralPath $tempStagingDir) {
                Write-Host @"

================================================================================
WARNING: STAGING CLEANUP FAILURE DETECTED
================================================================================
The temporary staging directory could not be fully removed:
  $tempStagingDir

It contains UNENCRYPTED PLAIN-TEXT service configurations.
Please delete this directory manually to prevent credential/config leaks.
================================================================================
"@ -ForegroundColor Red
            }
        }
    }
}
finally {
    # If parent directory was created during execution but dump failed before creating archive, clean up orphaned folder
    if ($null -ne $createdParentPath -and (Test-Path -LiteralPath $createdParentPath) -and -not (Test-Path -LiteralPath $resolvedArchivePath)) {
        $parentItems = Get-ChildItem -LiteralPath $createdParentPath -ErrorAction SilentlyContinue
        if ($null -eq $parentItems -or @($parentItems).Count -eq 0) {
            Remove-Item -LiteralPath $createdParentPath -Force -ErrorAction SilentlyContinue
        }
    }

    # Restore host console encoding state if previously captured
    if ($null -ne $previousOutputEncoding) {
        try { [Console]::OutputEncoding = $previousOutputEncoding } catch { }
    }
}
