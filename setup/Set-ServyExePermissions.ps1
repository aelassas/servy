#Requires -Version 5.1
<#
.SYNOPSIS
    Hardens Servy executable permissions to Read & Execute to prevent privilege escalation and binary tampering (Mandatory Security Hardening).

.DESCRIPTION
    Mandatory security script for hardening Servy service runner accounts. Servy requires directory-level
    'Modify' permissions on %ProgramData%\Servy to write database logs, process state, and runtime recovery files.
    However, leaving binaries with inherited 'Modify' access allows a compromised service process or unprivileged
    runner account to tamper with, replace, or hijack core executables.

    This script enforces Servy's Single Trust Boundary security model by breaking permission inheritance on core
    executable files and restricting the target runner account to strict 'Read & Execute' rights. This ensures the
    service runner can execute required binaries without being able to overwrite or replace them, protecting against
    unprivileged binary replacement and local privilege escalation vectors. Full Control is explicitly preserved for
    SYSTEM and Administrators using language-agnostic Well-Known SIDs.

    Hardened Executable Files:
    - Servy.Service.exe
    - Servy.Service.CLI.exe (Note: May not be present on a fresh install; start the service with the CLI once so Servy.Service.CLI.exe gets copied to %ProgramData%\Servy)
    - Servy.Restarter.exe (Note: May not be present on a fresh install; start the service once so Servy.Restarter.exe gets copied to %ProgramData%\Servy)
    - handle64.exe / handle64a.exe

.PARAMETER TargetAccount
    Mandatory account identifier receiving Read & Execute permissions. Supported formats:
    - Active Directory user/group: 'DOMAIN\Username'
    - Local computer user/group: 'COMPUTERNAME\Username'
    - Local computer relative notation: '.\Username'
    - Built-in Windows principals: 'NT AUTHORITY\LocalService', 'NT AUTHORITY\NetworkService'
    - Group Managed Service Account (gMSA): 'DOMAIN\gMSAAccount$' or 'gMSAAccount$'

.EXAMPLE
    .\Set-ServyExePermissions.ps1 -TargetAccount "MYDOMAIN\svc-servy"

.EXAMPLE
    .\Set-ServyExePermissions.ps1 -TargetAccount "NT AUTHORITY\LocalService"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Specify target account (e.g., 'DOMAIN\User', 'NT AUTHORITY\LocalService', or 'DOMAIN\gMSA$').")]
    [ValidateNotNullOrEmpty()]
    [string]$TargetAccount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Modern .NET executable target list
$exeNames = @(
    'Servy.Service.exe',
    'Servy.Service.CLI.exe',
    'Servy.Restarter.exe',
    'handle64.exe',
    'handle64a.exe'
)

$programDataDir = [System.IO.Path]::Combine($env:ProgramData, "Servy")

if (-not (Test-Path -Path $programDataDir)) {
    Write-Warning "Directory '$programDataDir' does not exist. No changes applied."
    exit 0
}

# Normalize relative notation '.\User' while preserving built-in NT AUTHORITY accounts
$builtInNames = @(
    'LocalSystem', 'System',
    'LocalService', 'Local Service',
    'NetworkService', 'Network Service'
)

if ($TargetAccount.StartsWith(".\")) {
    $bare = $TargetAccount.Substring(2)
    $TargetAccount = if ($builtInNames -contains $bare) {
        $bare
    } else {
        "$env:COMPUTERNAME\$bare"
    }
}

# Validate account existence using Windows API resolution rather than regex pattern matching.
# Translating to a SecurityIdentifier (SID) ensures the account resolves on this machine/domain.
try {
    $targetNTAccount = New-Object System.Security.Principal.NTAccount($TargetAccount)
    [void]$targetNTAccount.Translate([System.Security.Principal.SecurityIdentifier])
}
catch {
    throw "Account '$TargetAccount' could not be resolved on this machine. " +
          "Use the form shown by services.msc, e.g. 'NT AUTHORITY\LocalService', " +
          "'DOMAIN\svc-servy', or 'DOMAIN\gMSAAccount$'."
}

# Define Well-Known SIDs for language-agnostic administrative control
$adminSid  = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
$systemSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)

Write-Host "Securing Servy executable files in: $programDataDir" -ForegroundColor Cyan
Write-Host "Target Account: $TargetAccount" -ForegroundColor Yellow

foreach ($exeName in $exeNames) {
    $exePath = [System.IO.Path]::Combine($programDataDir, $exeName)

    if (-not (Test-Path -Path $exePath)) {
        Write-Host "Skipping '$exeName' (file not found)." -ForegroundColor Gray
        continue
    }

    Write-Host "Hardening permissions on '$exeName'..." -ForegroundColor Green

    # Execute two ACL passes: Pass 1 converts inheritance to explicit rules; Pass 2 purges old rules and locks down Read & Execute.
    for ($pass = 1; $pass -le 2; $pass++) {
        $acl = Get-Acl -Path $exePath

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
        Set-Acl -Path $exePath -AclObject $acl
    }

    Write-Host "Successfully hardened '$exeName'." -ForegroundColor Green
}

Write-Host "`nExecutable permission hardening complete." -ForegroundColor Cyan
