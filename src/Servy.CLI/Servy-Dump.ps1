#Requires -Version 2.0
<#
.SYNOPSIS
    Generates a consolidated Servy backup archive containing all service configurations in XML format.

.DESCRIPTION
    Servy-Dump.ps1 inspects the local Servy SQLite configuration database (%ProgramData%\Servy\db\Servy.db),
    retrieves all registered service definitions using local Servy SQLite assemblies or native Win32 APIs,
    and exports each service configuration into an individual XML file using the official Servy PowerShell module.
    The exported XML files are then compressed into a single zip archive.

    Per-service export errors are caught gracefully. If at least one service exports successfully and one or more
    fail, the zip archive is still generated and an exit code of 7 is returned to flag an incomplete backup to
    automated workflows.

    EXIT CODES:
    - 0 : Success. All registered service configurations were successfully exported and archived (or no services exist).
    - 1 : Execution Failure. The script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Import Failure. The official Servy PowerShell module (Servy.psm1) could not be located or imported.
    - 3 : Target Conflict. The destination archive file already exists and the -Overwrite switch was not specified.
    - 4 : I/O & Inspection Failure. The database could not be read, or the target destination path/directory is unwritable.
    - 6 : Complete Export Failure. No service configurations could be exported; no output archive was generated.
    - 7 : Partial Export Warning. The dump archive was successfully created, but one or more services failed to export.

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
    - Operating System: Windows 7 SP1, Windows Server 2008 R2, or later.
    - PowerShell Version: Windows PowerShell 2.0 or higher.
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
    - SQLite Engine: Prefers System.Data.SQLite.dll / e_sqlite3.dll in Servy directory, with dynamic fallback to winsqlite3.dll / sqlite3.dll.
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

    # Determine base Servy installation directory for native and managed assembly resolution
    $servyBinDir = [System.IO.Path]::GetDirectoryName($servyModulePath)

    # Resolve and normalize archive path extension up-front
    $resolvedArchivePath = [System.IO.Path]::GetFullPath($DestinationArchivePath)

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
        Write-Host "Existing dump archive found. -Overwrite specified; replacing target file." -ForegroundColor Yellow
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
    $dbPath = [System.IO.Path]::Combine($env:ProgramData, "Servy\db\Servy.db")

    if (-not (Test-Path -LiteralPath $dbPath)) {
        Write-Host "Servy database not found at '$dbPath'. No services exist to export." -ForegroundColor Yellow
        exit 0
    }

    # -----------------------------------------------------------------------------
    # Database Inspection Layer
    # 1. Attempt ADO.NET using System.Data.SQLite.dll (present in Servy net48 build)
    # 2. Dynamic P/Invoke wrapper safe for PowerShell 2.0 / .NET 3.5 CLR using UTF-16
    # -----------------------------------------------------------------------------

    $serviceNames = New-Object System.Collections.Generic.List[string]
    $managedSqliteDll = [System.IO.Path]::Combine($servyBinDir, "System.Data.SQLite.dll")

    $usedAdoNet = $false

    if (Test-Path -LiteralPath $managedSqliteDll) {
        try {
            [void][System.Reflection.Assembly]::LoadFrom($managedSqliteDll)
            
            $connectionString = "Data Source=$dbPath;Version=3;Read Only=True;"
            $connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)
            
            try {
                $connection.Open()
                $command = $connection.CreateCommand()
                $command.CommandText = "SELECT Name FROM Services ORDER BY Name"
                $reader = $command.ExecuteReader()
                
                while ($reader.Read()) {
                    if (-not $reader.IsDBNull(0)) {
                        $serviceNames.Add($reader.GetString(0))
                    }
                }
                $reader.Close()
                $usedAdoNet = $true
            }
            finally {
                if ($connection.State -eq [System.Data.ConnectionState]::Open) {
                    $connection.Close()
                }
                $connection.Dispose()
            }
        }
        catch {
            $usedAdoNet = $false
        }
    }

    if (-not $usedAdoNet) {
        if (-not ([System.Management.Automation.PSTypeName]'ServySafePs2Sqlite16').Type) {
            $sqliteBinding = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class ServySafePs2Sqlite16
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Open16Delegate([MarshalAs(UnmanagedType.LPWStr)] string filename, out IntPtr ppDb);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CloseDelegate(IntPtr db);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PrepareV2Delegate(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int StepDelegate(IntPtr stmt);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ColumnText16Delegate(IntPtr stmt, int iCol);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FinalizeDelegate(IntPtr stmt);

    public static List<string> GetServiceNames(string dbPath, string servyDir)
    {
        List<string> result = new List<string>();
        string[] candidates = new string[] {
            System.IO.Path.Combine(servyDir, "e_sqlite3.dll"),
            "winsqlite3.dll",
            "sqlite3.dll"
        };

        IntPtr hModule = IntPtr.Zero;
        foreach (string lib in candidates)
        {
            try
            {
                hModule = LoadLibrary(lib);
                if (hModule != IntPtr.Zero) break;
            }
            catch { }
        }

        if (hModule == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to load native SQLite library (e_sqlite3.dll, winsqlite3.dll, or sqlite3.dll).");
        }

        IntPtr pOpen16   = GetProcAddress(hModule, "sqlite3_open16");
        IntPtr pClose    = GetProcAddress(hModule, "sqlite3_close");
        IntPtr pPrepare  = GetProcAddress(hModule, "sqlite3_prepare_v2");
        IntPtr pStep     = GetProcAddress(hModule, "sqlite3_step");
        IntPtr pText16   = GetProcAddress(hModule, "sqlite3_column_text16");
        IntPtr pFinalize = GetProcAddress(hModule, "sqlite3_finalize");

        if (pOpen16 == IntPtr.Zero || pClose == IntPtr.Zero || pPrepare == IntPtr.Zero ||
            pStep == IntPtr.Zero || pText16 == IntPtr.Zero || pFinalize == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to resolve native SQLite function exports.");
        }

        Open16Delegate open16         = (Open16Delegate)Marshal.GetDelegateForFunctionPointer(pOpen16, typeof(Open16Delegate));
        CloseDelegate close           = (CloseDelegate)Marshal.GetDelegateForFunctionPointer(pClose, typeof(CloseDelegate));
        PrepareV2Delegate prepareV2   = (PrepareV2Delegate)Marshal.GetDelegateForFunctionPointer(pPrepare, typeof(PrepareV2Delegate));
        StepDelegate step             = (StepDelegate)Marshal.GetDelegateForFunctionPointer(pStep, typeof(StepDelegate));
        ColumnText16Delegate colText16 = (ColumnText16Delegate)Marshal.GetDelegateForFunctionPointer(pText16, typeof(ColumnText16Delegate));
        FinalizeDelegate finalize     = (FinalizeDelegate)Marshal.GetDelegateForFunctionPointer(pFinalize, typeof(FinalizeDelegate));

        IntPtr db;
        int rc = open16(dbPath, out db);
        if (rc != 0)
        {
            if (db != IntPtr.Zero) close(db);
            throw new InvalidOperationException(string.Format("sqlite3_open16 failed on '{0}' with result code {1}.", dbPath, rc));
        }

        try
        {
            IntPtr stmt;
            rc = prepareV2(db, "SELECT Name FROM Services ORDER BY Name", -1, out stmt, IntPtr.Zero);
            if (rc != 0)
            {
                throw new InvalidOperationException(string.Format("sqlite3_prepare_v2 failed with result code {0}.", rc));
            }

            try
            {
                // SQLITE_ROW = 100
                while (step(stmt) == 100)
                {
                    IntPtr ptr = colText16(stmt, 0);
                    if (ptr != IntPtr.Zero)
                    {
                        result.Add(Marshal.PtrToStringUni(ptr));
                    }
                }
            }
            finally
            {
                finalize(stmt);
            }
        }
        finally
        {
            close(db);
        }

        return result;
    }
}
"@
            Add-Type -TypeDefinition $sqliteBinding
        }

        try {
            $serviceNames = [ServySafePs2Sqlite16]::GetServiceNames($dbPath, $servyBinDir)
        }
        catch {
            Write-Host "Failed to query Servy database at '$dbPath': $($_.Exception.Message)" -ForegroundColor Red
            exit 4
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
    } catch { }

    try {
        if ($null -eq $serviceNames -or $serviceNames.Count -eq 0) {
            Write-Host "No services were found in the database at '$dbPath'." -ForegroundColor Yellow
            exit 0
        }

        Write-Host "Found $($serviceNames.Count) service(s) to export..." -ForegroundColor Cyan

        $exported = New-Object System.Collections.Generic.List[string]
        $failed   = New-Object System.Collections.Generic.List[object]
        $invalidChars = [System.IO.Path]::GetInvalidFileNameChars()

        # Export each service configuration into individual XML files with per-item exception isolation
        foreach ($serviceName in $serviceNames) {
            # Sanitize service name for safe filesystem usage
            $safeFileName = $serviceName
            foreach ($char in $invalidChars) {
                $safeFileName = $safeFileName.Replace($char, '_')
            }

            $xmlExportPath = [System.IO.Path]::Combine($tempStagingDir, "$safeFileName.xml")

            Write-Host "Exporting configuration for '$serviceName' -> '$safeFileName.xml'..." -ForegroundColor Green

            try {
                Export-ServyServiceConfig -Name $serviceName -ConfigFileType "Xml" -Path $xmlExportPath
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

        # Compress staging directory into target zip archive
        Write-Host "Compressing exported configurations into zip archive..." -ForegroundColor Cyan

        if (Get-Command -Name "Compress-Archive" -ErrorAction SilentlyContinue) {
            $compressParams = @{
                Path             = "$tempStagingDir\*"
                DestinationPath  = $resolvedArchivePath
            }
            if ($Overwrite.IsPresent) {
                $compressParams['Force'] = $true
            }
            Compress-Archive @compressParams
        }
        else {
            try {
                if ($Overwrite.IsPresent -and (Test-Path -LiteralPath $resolvedArchivePath)) {
                    Remove-Item -LiteralPath $resolvedArchivePath -Force -ErrorAction SilentlyContinue
                }
                [void][System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
                [System.IO.Compression.ZipFile]::CreateFromDirectory($tempStagingDir, $resolvedArchivePath)
            }
            catch {
                if ($Overwrite.IsPresent -and (Test-Path -LiteralPath $resolvedArchivePath)) {
                    Remove-Item -LiteralPath $resolvedArchivePath -Force -ErrorAction SilentlyContinue
                }
                Set-Content -Path $resolvedArchivePath -Value ("PK" + [char]5 + [char]6 + ("`0" * 18))
                $shellApp = New-Object -ComObject Shell.Application
                $zipPackage = $shellApp.NameSpace($resolvedArchivePath)
                $sourceItems = $shellApp.NameSpace($tempStagingDir).Items()
                $zipPackage.CopyHere($sourceItems)

                while ($zipPackage.Items().Count -lt $sourceItems.Count) {
                    Start-Sleep -Milliseconds 500
                }
            }
        }

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
