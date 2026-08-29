#Requires -Version 5.1
<#
.SYNOPSIS
    Restores Servy service configurations from a consolidated XML dump archive.

.DESCRIPTION
    Servy-Restore.ps1 extracts a Servy backup archive containing individual service XML configuration files and
    imports each configuration into Servy using the official Servy PowerShell module (Import-ServyServiceConfig).
    
    If the -Install switch parameter is supplied, the script also installs each imported service into the Windows
    Service Control Manager (SCM).

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

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip"

.EXAMPLE
    .\Servy-Restore.ps1 -DumpArchivePath "C:\Backups\Servy_Dump.zip" -Install

.NOTES
    SYSTEM REQUIREMENTS:
    - Operating System: Windows 10, Windows 11, or Windows Server 2016 and later.
    - PowerShell Version: Windows PowerShell 5.1 or PowerShell 7+ (Core).
    - Servy Core Components: Servy CLI and Servy PowerShell module (Servy.psm1) must be installed in %ProgramFiles%\Servy or portable root.
    - Archive Support: Native PowerShell Microsoft.PowerShell.Archive module (Expand-Archive).
    - Execution Privileges: Administrator privileges are required to interact with Servy configurations and managing Windows services.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'Specify path to the Servy dump zip archive (e.g., "C:\Backups\Servy_Backup.zip").')]
    [ValidateNotNullOrEmpty()]
    [string]$DumpArchivePath,

    [Parameter(Mandatory = $false, HelpMessage = 'Optionally install each service into Windows SCM after import.')]
    [switch]$Install
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

    # Validate existence of the specified dump archive file
    $resolvedArchivePath = [System.IO.Path]::GetFullPath($DumpArchivePath)

    if (-not (Test-Path -Path $resolvedArchivePath)) {
        Write-Host "Specified dump archive file does not exist: '$resolvedArchivePath'." -ForegroundColor Red
        exit 3
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
    } catch { }

    try {
        Write-Host "Extracting dump archive '$resolvedArchivePath'..." -ForegroundColor Cyan
        Expand-Archive -Path $resolvedArchivePath -DestinationPath $tempExtractDir -Force

        # Enumerate all XML configuration files in the extracted dump directory
        $xmlFiles = Get-ChildItem -Path $tempExtractDir -Filter "*.xml" -File

        if ($null -eq $xmlFiles -or @($xmlFiles).Count -eq 0) {
            Write-Host "No XML configuration files were found in the dump archive." -ForegroundColor Yellow
            exit 0
        }

        $xmlFileList = @($xmlFiles)
        Write-Host "Found $($xmlFileList.Count) service configuration file(s) to restore..." -ForegroundColor Cyan

        # Iterate through extracted XML files and import each service configuration
        foreach ($xmlFile in $xmlFileList) {
            Write-Host "Importing configuration from '$($xmlFile.Name)'..." -ForegroundColor Green

            # Build splatting hashtable for Import-ServyServiceConfig
            $importParams = @{
                ConfigFileType = "Xml"
                Path           = $xmlFile.FullName
            }

            if ($Install.IsPresent) {
                $importParams['Install'] = $true
            }

            # Invoke Servy cmdlet to import (and optionally install) the service configuration
            Import-ServyServiceConfig @importParams
        }

        # Display completion status and critical security notice
        Write-Host "`nServy configuration restore completed successfully!" -ForegroundColor Green

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
    # Restore host console encoding state
    [Console]::OutputEncoding = $previousOutputEncoding
}
