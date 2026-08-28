#Requires -Version 2.0
<#
.SYNOPSIS
    Generates a consolidated Servy backup archive containing all service configurations in XML format.

.DESCRIPTION
    Servy-Dump.ps1 inspects the local Servy SQLite configuration database (%ProgramData%\Servy\db\Servy.db),
    retrieves all registered service definitions using local Servy SQLite assemblies or native Win32 APIs,
    and exports each service configuration into an individual XML file using the official Servy PowerShell module.
    The exported XML files are then compressed into a single zip archive.

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
    - SQLite Engine: Prefers System.Data.SQLite.dll / e_sqlite3.dll in %ProgramFiles%\Servy (net48), with dynamic fallback to winsqlite3.dll / sqlite3.dll.
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
    Write-Host "Existing dump archive found. -Overwrite specified; replacing target file." -ForegroundColor Yellow
    Remove-Item -Path $resolvedArchivePath -Force -ErrorAction SilentlyContinue
}

# Validate existence of the Servy SQLite database file
$dbPath = [System.IO.Path]::Combine($env:ProgramData, "Servy\db\Servy.db")

if (-not (Test-Path -Path $dbPath)) {
    Write-Host "Servy database not found at '$dbPath'. No services exist to export." -ForegroundColor Yellow
    exit 0
}

# -----------------------------------------------------------------------------
# Database Inspection Layer
# 1. Attempt ADO.NET using System.Data.SQLite.dll (present in Servy net48 build)
# 2. Fall back to P/Invoke targeting e_sqlite3.dll, winsqlite3.dll, or sqlite3.dll
# -----------------------------------------------------------------------------

$serviceNames = New-Object System.Collections.Generic.List[string]
$servyBinDir = "C:\Program Files\Servy"
$managedSqliteDll = [System.IO.Path]::Combine($servyBinDir, "System.Data.SQLite.dll")

$usedAdoNet = $false

if (Test-Path -Path $managedSqliteDll) {
    try {
        [void][System.Reflection.Assembly]::LoadFrom($managedSqliteDll)
        
        $connectionString = "Data Source=$dbPath;Version=3;Read Only=True;"
        $connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)
        
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT Name FROM Services"
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
        # Fall back to native P/Invoke if assembly loading or instantiation fails
        $usedAdoNet = $false
    }
}

if (-not $usedAdoNet) {
    if (-not ([System.Management.Automation.PSTypeName]'ServyNativeMultiSqlite').Type) {
        $sqliteBinding = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class ServyNativeMultiSqlite
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    // e_sqlite3.dll (Servy binary bundle)
    [DllImport("e_sqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int esqlite3_open_v2([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("e_sqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int esqlite3_close(IntPtr db);

    [DllImport("e_sqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int esqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport("e_sqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    private static extern int esqlite3_step(IntPtr stmt);

    [DllImport("e_sqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr esqlite3_column_text(IntPtr stmt, int iCol);

    [DllImport("e_sqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int esqlite3_finalize(IntPtr stmt);

    // winsqlite3.dll (Windows 10 / Server 2016+)
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int winsqlite3_open_v2([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int winsqlite3_close(IntPtr db);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int winsqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    private static extern int winsqlite3_step(IntPtr stmt);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr winsqlite3_column_text(IntPtr stmt, int iCol);

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int winsqlite3_finalize(IntPtr stmt);

    // sqlite3.dll (Standard Fallback)
    [DllImport("sqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport("sqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport("sqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport("sqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

    [DllImport("sqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_finalize(IntPtr stmt);

    public static List<string> GetServiceNames(string dbPath, string servyDir)
    {
        var result = new List<string>();
        IntPtr db;

        if (!string.IsNullOrEmpty(servyDir))
        {
            SetDllDirectory(servyDir);
        }

        // 1. Try e_sqlite3.dll (Servy net48 installation folder)
        if (LoadLibrary("e_sqlite3.dll") != IntPtr.Zero)
        {
            if (esqlite3_open_v2(dbPath, out db, 1, IntPtr.Zero) == 0)
            {
                IntPtr stmt;
                if (esqlite3_prepare_v2(db, "SELECT Name FROM Services", -1, out stmt, IntPtr.Zero) == 0)
                {
                    while (esqlite3_step(stmt) == 100)
                    {
                        IntPtr ptr = esqlite3_column_text(stmt, 0);
                        if (ptr != IntPtr.Zero)
                        {
                            result.Add(Marshal.PtrToStringAnsi(ptr));
                        }
                    }
                    esqlite3_finalize(stmt);
                }
                esqlite3_close(db);
                return result;
            }
        }

        // 2. Try winsqlite3.dll (Windows 10 / Server 2016+)
        if (LoadLibrary("winsqlite3.dll") != IntPtr.Zero)
        {
            if (winsqlite3_open_v2(dbPath, out db, 1, IntPtr.Zero) == 0)
            {
                IntPtr stmt;
                if (winsqlite3_prepare_v2(db, "SELECT Name FROM Services", -1, out stmt, IntPtr.Zero) == 0)
                {
                    while (winsqlite3_step(stmt) == 100)
                    {
                        IntPtr ptr = winsqlite3_column_text(stmt, 0);
                        if (ptr != IntPtr.Zero)
                        {
                            result.Add(Marshal.PtrToStringAnsi(ptr));
                        }
                    }
                    winsqlite3_finalize(stmt);
                }
                winsqlite3_close(db);
                return result;
            }
        }

        #pragma warning disable 0168
        // 3. Fallback to standard sqlite3.dll
        if (sqlite3_open_v2(dbPath, out db, 1, IntPtr.Zero) == 0)
        {
            IntPtr stmt;
            if (sqlite3_prepare_v2(db, "SELECT Name FROM Services", -1, out stmt, IntPtr.Zero) == 0)
            {
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
        }
        return result;
    }
}
"@
        Add-Type -TypeDefinition $sqliteBinding
    }

    $serviceNames = [ServyNativeMultiSqlite]::GetServiceNames($dbPath, $servyBinDir)
}

# Create an isolated temporary directory for staging exported XML files
$tempStagingDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyDump_" + [System.IO.Path]::GetRandomFileName())
[void][System.IO.Directory]::CreateDirectory($tempStagingDir)

try {
    if ($null -eq $serviceNames -or $serviceNames.Count -eq 0) {
        Write-Host "No services were found in the database at '$dbPath'." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Found $($serviceNames.Count) service(s) to export..." -ForegroundColor Cyan

    # Export each service configuration into individual XML files inside the temporary staging directory
    $invalidChars = [System.IO.Path]::GetInvalidFileNameChars()

    foreach ($serviceName in $serviceNames) {
        $safeFileName = $serviceName
        foreach ($char in $invalidChars) {
            $safeFileName = $safeFileName.Replace($char, '_')
        }

        $xmlExportPath = [System.IO.Path]::Combine($tempStagingDir, "$safeFileName.xml")

        Write-Host "Exporting configuration for '$serviceName' -> '$safeFileName.xml'..." -ForegroundColor Green

        # Invoke Servy cmdlet to generate XML configuration dump
        Export-ServyServiceConfig -Name $serviceName -ConfigFileType "Xml" -Path $xmlExportPath
    }

    # Ensure target output directory exists
    $parentDir = [System.IO.Path]::GetDirectoryName($resolvedArchivePath)
    if (-not [string]::IsNullOrEmpty($parentDir) -and -not (Test-Path -Path $parentDir)) {
        [void][System.IO.Directory]::CreateDirectory($parentDir)
    }

    # Compress staging directory into target zip archive (PowerShell 2.0 / .NET Framework compatible compression)
    Write-Host "Compressing exported configurations into zip archive..." -ForegroundColor Cyan

    if (Get-Command -Name "Compress-Archive" -ErrorAction SilentlyContinue) {
        Compress-Archive -Path "$tempStagingDir\*" -DestinationPath $resolvedArchivePath -Force
    }
    else {
        try {
            [void][System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
            [System.IO.Compression.ZipFile]::CreateFromDirectory($tempStagingDir, $resolvedArchivePath)
        }
        catch {
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
