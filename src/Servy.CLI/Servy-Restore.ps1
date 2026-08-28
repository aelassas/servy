#Requires -Version 2.0
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
    [switch]$Install
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Set external process pipeline encoding safely (restricts Win32 console code page mutation to PS 3.0+)
if ($PSVersionTable.PSVersion.Major -ge 3) {
    try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
}
try { $OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

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
    # PS3+ has automatic $PSScriptRoot
    $scriptDir = $PSScriptRoot
}
else {
    # PS2 does not have $PSScriptRoot
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

# Validate existence of the specified dump archive file
$resolvedArchivePath = [System.IO.Path]::GetFullPath($DumpArchivePath)

if (-not (Test-Path -Path $resolvedArchivePath)) {
    Write-Host "Specified dump archive file does not exist: '$resolvedArchivePath'." -ForegroundColor Red
    exit 3
}

# Create an isolated temporary directory for extracting XML files
$tempExtractDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ServyRestore_" + [System.IO.Path]::GetRandomFileName())
[void][System.IO.Directory]::CreateDirectory($tempExtractDir)

try {
    Write-Host "Extracting dump archive '$resolvedArchivePath'..." -ForegroundColor Cyan

    # 1. Attempt PowerShell 5.0+ Expand-Archive if available
    if (Get-Command -Name "Expand-Archive" -ErrorAction SilentlyContinue) {
        Expand-Archive -Path $resolvedArchivePath -DestinationPath $tempExtractDir -Force
    }
    # 2. Attempt .NET Framework 4.5+ System.IO.Compression.ZipFile
    else {
        try {
            [void][System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
            [System.IO.Compression.ZipFile]::ExtractToDirectory($resolvedArchivePath, $tempExtractDir)
        }
        catch {
            # 3. Fallback for Windows 7 / PowerShell 2.0 native COM Shell.Application zip extraction
            $shellApp = New-Object -ComObject Shell.Application
            $zipPackage = $shellApp.NameSpace($resolvedArchivePath)
            $destinationFolder = $shellApp.NameSpace($tempExtractDir)
            
            # CopyHere flags: 4 = Do not display progress dialog, 16 = Respond with "Yes to All" for any dialog
            $destinationFolder.CopyHere($zipPackage.Items(), 20)

            # Wait for asynchronous COM extraction operation to finalize
            while ($destinationFolder.Items().Count -lt $zipPackage.Items().Count) {
                Start-Sleep -Milliseconds 500
            }
        }
    }

    # Enumerate all XML configuration files in the extracted dump directory (PS 2.0 compatible filter)
    $xmlFiles = Get-ChildItem -Path $tempExtractDir | Where-Object { -not $_.PSIsContainer -and $_.Name.EndsWith(".xml", [System.StringComparison]::OrdinalIgnoreCase) }

    if ($null -eq $xmlFiles -or @($xmlFiles).Count -eq 0) {
        Write-Host "No XML configuration files were found in the dump archive." -ForegroundColor Yellow
        exit 0
    }

    $xmlFileList = @($xmlFiles)
    Write-Host "Found $($xmlFileList.Count) service configuration file(s) to restore..." -ForegroundColor Cyan

    # Iterate through extracted XML files and import each service configuration
    foreach ($xmlFile in $xmlFileList) {
        Write-Host "Importing configuration from '$($xmlFile.Name)'..." -ForegroundColor Green

        if ($Install.IsPresent) {
            Import-ServyServiceConfig -ConfigFileType "Xml" -Path $xmlFile.FullName -Install
        }
        else {
            Import-ServyServiceConfig -ConfigFileType "Xml" -Path $xmlFile.FullName
        }
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
    # Clean up temporary extraction directory and extracted XML files
    if (Test-Path -Path $tempExtractDir) {
        Remove-Item -Path $tempExtractDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
