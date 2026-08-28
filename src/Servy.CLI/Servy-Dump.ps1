#Requires -Version 5.1
<#
.SYNOPSIS
    Generates a consolidated Servy backup archive containing all service configurations in XML format.

.DESCRIPTION
    Servy-Dump.ps1 inspects the local Servy SQLite configuration database (%ProgramData%\Servy\db\Servy.db),
    retrieves all registered service definitions using Windows native winsqlite3.dll, and exports each service
    configuration into an individual XML file using the official Servy PowerShell module. The exported XML files
    are then compressed into a single zip archive.

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

# Ensure the script is executing with Administrator privileges
$currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
$adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator

if (-not $currentPrincipal.IsInRole($adminRole)) {
    Write-Host "Servy-Dump.ps1 requires Administrator privileges. Please re-run script in an elevated PowerShell session." -ForegroundColor Red
    exit 1
}

# Validate and import the official Servy PowerShell module
$servyModulePath = "C:\Program Files\Servy\Servy.psm1"

try {
    Import-Module -Name $servyModulePath -Force -ErrorAction Stop
}
catch {
    Write-Host "Failed to import Servy PowerShell module from '$servyModulePath': $_" -ForegroundColor Red
    exit 2
}

# Check if destination dump file already exists
$resolvedArchivePath = [System.IO.Path]::GetFullPath($DestinationArchivePath)

if (Test-Path -Path $resolvedArchivePath) {
    if (-not $Overwrite.IsPresent) {
        Write-Host "Destination dump file already exists: '$resolvedArchivePath'. Operation aborted to prevent overwriting." -ForegroundColor Red
        exit 3
    }
    Write-Host "Existing dump archive found. -Overwrite specified; target file will be replaced." -ForegroundColor Yellow
}

# Validate existence of the Servy SQLite database file
$dbPath = [System.IO.Path]::Combine($env:ProgramData, "Servy", "db", "Servy.db")

if (-not (Test-Path -Path $dbPath)) {
    Write-Host "Servy database not found at '$dbPath'. No services exist to export." -ForegroundColor Yellow
    exit 0
}

# Register C# P/Invoke wrapper targeting Windows native %SystemRoot%\System32\winsqlite3.dll
if (-not ([System.Management.Automation.PSTypeName]'ServyNativeWinSqlite').Type) {
    $sqliteBinding = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class ServyNativeWinSqlite
{
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_finalize(IntPtr stmt);

    public static List<string> GetServiceNames(string dbPath)
    {
        var result = new List<string>();
        IntPtr db;
        // SQLITE_OPEN_READONLY = 1
        if (sqlite3_open_v2(dbPath, out db, 1, IntPtr.Zero) != 0) return result;

        IntPtr stmt;
        if (sqlite3_prepare_v2(db, "SELECT Name FROM Services", -1, out stmt, IntPtr.Zero) == 0)
        {
            // SQLITE_ROW = 100
            while (sqlite3_step(stmt) == 100)
            {
                IntPtr ptr = sqlite3_column_text(stmt, 0);
                if (ptr != IntPtr.Zero)
                {
                    result.Add(Marshal.PtrToStringAnsi(ptr));
                }
            }
            sqlite3_finalize(stmt);
        }
        sqlite3_close(db);
        return result;
    }
}
"@
    Add-Type -TypeDefinition $sqliteBinding
}

# Create an isolated temporary directory for staging exported XML files
$tempStagingDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyDump_" + [System.IO.Path]::GetRandomFileName())
[void][System.IO.Directory]::CreateDirectory($tempStagingDir)

try {
    # Query Servy SQLite database via Windows native winsqlite3.dll
    $serviceNames = [ServyNativeWinSqlite]::GetServiceNames($dbPath)

    if ($null -eq $serviceNames -or $serviceNames.Count -eq 0) {
        Write-Host "No services were found in the database at '$dbPath'." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Found $($serviceNames.Count) service(s) to export..." -ForegroundColor Cyan

    # Export each service configuration into individual XML files inside the temporary staging directory
    $invalidChars = [System.IO.Path]::GetInvalidFileNameChars()

    foreach ($serviceName in $serviceNames) {
        # Sanitize service name for safe filesystem usage
        $safeFileName = $serviceName
        foreach ($char in $invalidChars) {
            $safeFileName = $safeFileName.Replace($char, '_')
        }

        $xmlExportPath = [System.IO.Path]::Combine($tempStagingDir, "$safeFileName.xml")

        Write-Host "Exporting configuration for '$serviceName' -> '$safeFileName.xml'..." -ForegroundColor Green

        # Invoke Servy cmdlet to generate the XML dump for the current service
        Export-ServyServiceConfig -Name $serviceName -ConfigFileType "Xml" -Path $xmlExportPath
    }

    # Ensure output parent directory exists before creating zip file
    $parentDir = [System.IO.Path]::GetDirectoryName($resolvedArchivePath)
    if (-not [string]::IsNullOrEmpty($parentDir) -and -not (Test-Path -Path $parentDir)) {
        [void][System.IO.Directory]::CreateDirectory($parentDir)
    }

    # Compress the staging directory containing XML dumps into the target zip file
    Write-Host "Compressing exported configurations into zip archive..." -ForegroundColor Cyan

    $compressParams = @{
        Path             = "$tempStagingDir\*"
        DestinationPath  = $resolvedArchivePath
        CompressionLevel = "Optimal"
    }

    if ($Overwrite.IsPresent) {
        $compressParams['Force'] = $true
    }

    Compress-Archive @compressParams

    # Display completion status and critical security warning
    Write-Host "`nServy configuration dump completed successfully!" -ForegroundColor Green
    Write-Host "Dump location: $resolvedArchivePath" -ForegroundColor Cyan

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
}
finally {
    # Clean up temporary staging directory and XML files
    if (Test-Path -Path $tempStagingDir) {
        Remove-Item -Path $tempStagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
