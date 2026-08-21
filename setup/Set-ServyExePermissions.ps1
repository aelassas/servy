#Requires -Version 2.0
<#
.SYNOPSIS
    Hardens Servy .NET Framework 4.8 executable and library permissions to Read & Execute to prevent privilege escalation and binary tampering (Mandatory Security Hardening).

.DESCRIPTION
    Mandatory security script for hardening Servy .NET Framework 4.8 service runner accounts. Servy requires
    directory-level 'Modify' permissions on %ProgramData%\Servy to write database logs, process state, and runtime
    recovery files. However, leaving binaries and loaded assemblies with inherited 'Modify' access allows a
    compromised service process or unprivileged runner account to tamper with, replace, or hijack core executables and DLLs.

    This script enforces Servy's Single Trust Boundary security model by breaking permission inheritance on core
    executable files and DLL assemblies, restricting the target runner account to strict 'Read & Execute' rights.
    This ensures the service runner can execute required binaries without being able to overwrite, replace, or DLL-hijack
    them, protecting against unprivileged binary replacement and local privilege escalation vectors. Full Control is
    explicitly preserved for SYSTEM and Administrators using language-agnostic Well-Known SIDs.

    Hardened Target Files:
    - Servy.Service.Net48.exe
    - Servy.Service.CLI.Net48.exe (Note: May not be present on a fresh install; start the service with the CLI once so Servy.Service.CLI.Net48.exe gets copied to %ProgramData%\Servy)
    - Servy.Restarter.Net48.exe (Note: May not be present on a fresh install; start the service once so Servy.Restarter.Net48.exe gets copied to %ProgramData%\Servy)
    - All *.dll files in %ProgramData%\Servy

.PARAMETER TargetAccount
    Mandatory account identifier receiving Read & Execute permissions. Supported formats:
    - Active Directory user/group: 'DOMAIN\Username'
    - Local computer user/group: 'COMPUTERNAME\Username'
    - Local computer relative notation: '.\Username'
    - Group Managed Service Account (gMSA): 'DOMAIN\gMSAAccount$' or 'gMSAAccount$'

.EXAMPLE
    .\Set-ServyExePermissions.ps1 -TargetAccount "MYDOMAIN\svc-servy"

.EXAMPLE
    .\Set-ServyExePermissions.ps1 -TargetAccount ".\test_svc"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'Specify target account like DOMAIN\User, .\User, or DOMAIN\gMSA$')]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^(?i)(?:(?:\.|[a-z0-9_.-]+)\\)?[a-z0-9_.-]+\$?$')]
    [string]$TargetAccount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# .NET Framework 4.8 executable target list
$staticExeNames = @(
    'Servy.Service.Net48.exe',
    'Servy.Service.CLI.Net48.exe',
    'Servy.Restarter.Net48.exe'
)

$programDataDir = [System.IO.Path]::Combine($env:ProgramData, "Servy")

if (-not (Test-Path -Path $programDataDir)) {
    Write-Warning "Directory '$programDataDir' does not exist. No changes applied."
    exit 0
}

# Resolve relative notation '.\User' to actual computer name (PS 2.0 compatible)
if ($TargetAccount.Length -ge 2 -and $TargetAccount.Substring(0, 2) -eq ".\") {
    $TargetAccount = "$env:COMPUTERNAME\" + $TargetAccount.Substring(2)
}

# Strict-mode safe instantiation of NTAccount
$targetNTAccount = New-Object System.Security.Principal.NTAccount($TargetAccount)

# Define Well-Known SIDs for language-agnostic administrative control
$adminSid  = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
$systemSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)

# Discover all .dll files in %ProgramData%\Servy (PS 2.0 compatible file discovery without -File flag)
$dllFiles = Get-ChildItem -Path $programDataDir -Filter "*.dll" -ErrorAction SilentlyContinue | 
    Where-Object { -not $_.PSIsContainer } | 
    Select-Object -ExpandProperty Name

# Combine static executables and discovered DLLs
$targetFiles = @($staticExeNames) + @($dllFiles)

Write-Host "Securing Servy (.NET Framework 4.8) binary and library files in: $programDataDir" -ForegroundColor Cyan
Write-Host "Target Account: $TargetAccount" -ForegroundColor Yellow

foreach ($fileName in $targetFiles) {
    if ([string]::IsNullOrEmpty($fileName)) { continue }

    $filePath = [System.IO.Path]::Combine($programDataDir, $fileName)

    if (-not (Test-Path -Path $filePath)) {
        if ($fileName -eq 'Servy.Restarter.Net48.exe') {
            Write-Host "Skipping '$fileName' (file not found). Note: Start the service once so Servy.Restarter.Net48.exe gets copied to %ProgramData%\Servy." -ForegroundColor Yellow
        } else {
            Write-Host "Skipping '$fileName' (file not found)." -ForegroundColor Gray
        }
        continue
    }

    Write-Host "Hardening permissions on '$fileName'..." -ForegroundColor Green

    # Execute two ACL passes: Pass 1 converts inheritance to explicit rules; Pass 2 purges old rules and locks down Read & Execute.
    for ($pass = 1; $pass -le 2; $pass++) {
        $acl = Get-Acl -Path $filePath

        # 1. Break inheritance and convert existing inherited permissions to explicit ACEs
        $acl.SetAccessRuleProtection($true, $true)

        # 2. Atomic purge of all existing explicit ACEs (Modify, FullControl, etc.) for target account
        $acl.PurgeAccessRules($targetNTAccount)

        # 3. Ensure SYSTEM and Administrators retain Full Control via SIDs
        $adminRule  = New-Object System.Security.AccessControl.FileSystemAccessRule($adminSid, "FullControl", "Allow")
        $systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule($systemSid, "FullControl", "Allow")
        $acl.SetAccessRule($adminRule)
        $acl.SetAccessRule($systemRule)

        # 4. Grant explicit ReadAndExecute access to target account
        $targetRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $targetNTAccount,
            "ReadAndExecute",
            "Allow"
        )
        $acl.SetAccessRule($targetRule)

        # Commit ACL pass to disk
        Set-Acl -Path $filePath -AclObject $acl
    }

    Write-Host "Successfully hardened '$fileName'." -ForegroundColor Green
}

Write-Host "`nExecutable and library permission hardening complete." -ForegroundColor Cyan
