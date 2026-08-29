#Requires -Version 5.1
<#
.SYNOPSIS
    Mandatory Security Hardening: Hardens Servy executable permissions to Read & Execute to prevent privilege escalation and binary tampering.

.DESCRIPTION
    Mandatory security script for hardening Servy service runner accounts. Servy requires directory-level
    'Modify' permissions on %ProgramData%\Servy to write database logs, process state, and runtime recovery files.
    However, leaving binaries with inherited 'Modify' access allows a compromised service process or unprivileged
    runner account to tamper with, replace, or hijack core executables.

    This script enforces Servy's Single Trust Boundary security model by breaking permission inheritance on core
    executable files and restricting the target runner account to strict 'Read & Execute' rights. This ensures the
    service runner can execute required binaries without being able to overwrite or replace them, protecting against
    unprivileged binary replacement and local privilege escalation vectors. Full Control is explicitly preserved for
    SYSTEM and Administrators using language-agnostic Well-Known SIDs. Manually added explicit ACEs for third-party
    principals are audited and preserved.

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
    - Built-in Windows principals: 'LocalService', 'NetworkService', 'NT AUTHORITY\LocalService'
    - Group Managed Service Account (gMSA): 'DOMAIN\gMSAAccount$' or 'gMSAAccount$'

.EXAMPLE
    .\Set-ServyExePermissions.ps1 -TargetAccount "MYDOMAIN\svc-servy"

.EXAMPLE
    .\Set-ServyExePermissions.ps1 -TargetAccount "LocalService"

.NOTES
    - Execution Requires Administrator Privileges: Modifying ACLs and breaking permission inheritance in %ProgramData%\Servy
      requires an elevated PowerShell session.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'Specify target account (e.g., "DOMAIN\User", "LocalService", "NT AUTHORITY\LocalService", or "DOMAIN\gMSA$").')]
    [ValidateNotNullOrEmpty()]
    [string]$TargetAccount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Ensure the script is executing with Administrator privileges
$currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
$adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator
if (-not $currentPrincipal.IsInRole($adminRole)) {
    Write-Host "Set-ServyExePermissions.ps1 requires Administrator privileges. Please re-run script in an elevated PowerShell session." -ForegroundColor Red
    exit 1
}

# Modern .NET executable target list
$exeNames = @(
    'Servy.Service.exe',
    'Servy.Service.CLI.exe',
    'Servy.Restarter.exe'
)

$isArm64 = ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') -or ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64')

if ($isArm64) {
    $exeNames += 'handle64a.exe'
} else {
    $exeNames += 'handle64.exe'
}

$programDataDir = [System.IO.Path]::Combine($env:ProgramData, "Servy")

if (-not (Test-Path -Path $programDataDir)) {
    Write-Warning "Directory '$programDataDir' does not exist. No changes applied."
    exit 0
}

# Dynamically validate and resolve account existence via Windows LSA.
# First attempt: Direct LSA translation handles built-in principals, domain accounts, and standard formats.
$targetNTAccount = $null
try {
    $ntAccountCandidate = New-Object System.Security.Principal.NTAccount($TargetAccount)
    [void]$ntAccountCandidate.Translate([System.Security.Principal.SecurityIdentifier])
    $targetNTAccount = $ntAccountCandidate
}
catch {
    # Second attempt: Fall back for relative notation ('.\User') pointing to a local machine account
    if ($TargetAccount.StartsWith(".\")) {
        $localUser = "$env:COMPUTERNAME\" + $TargetAccount.Substring(2)
        try {
            $ntAccountCandidate = New-Object System.Security.Principal.NTAccount($localUser)
            [void]$ntAccountCandidate.Translate([System.Security.Principal.SecurityIdentifier])
            $targetNTAccount = $ntAccountCandidate
            $TargetAccount   = $localUser
        }
        catch {
            # Let fallback fail through to final error handler below
        }
    }
}

if ($null -eq $targetNTAccount) {
    throw "Account '$TargetAccount' could not be resolved on this machine or domain. " +
          "Use the form shown by services.msc (e.g., 'LocalService', 'NT AUTHORITY\LocalService', " +
          "'DOMAIN\svc-servy', or 'DOMAIN\gMSAAccount$')."
}

# Define Well-Known SIDs for language-agnostic administrative control
$adminSid           = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
$systemSid          = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
$builtinUsersSid    = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinUsersSid, $null)
$authUsersSid       = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::AuthenticatedUserSid, $null)
$everyoneSid        = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::WorldSid, $null)
$targetSid          = $targetNTAccount.Translate([System.Security.Principal.SecurityIdentifier])

Write-Host "Securing Servy executable files in: $programDataDir" -ForegroundColor Cyan
Write-Host "Target Account: $TargetAccount" -ForegroundColor Yellow

$hardened = @()
$skipped  = @()
foreach ($exeName in $exeNames) {
    $exePath = [System.IO.Path]::Combine($programDataDir, $exeName)

    if (-not (Test-Path -Path $exePath)) {
        $skipped += $exeName
        continue
    }

    try {
        Write-Host "Hardening permissions on '$exeName'..." -ForegroundColor Green

        $acl = Get-Acl -Path $exePath

        # Explicitly set owner to Builtin Administrators to avoid owner SID mismatch errors during Set-Acl
        $acl.SetOwner($adminSid)

        # 1. Break inheritance and remove inherited permissions in 1 pass ($isProtected = $true, $preserveInheritance = $false)
        $acl.SetAccessRuleProtection($true, $false)

        # 2. Purge explicit grants for broad unprivileged groups (Users, Authenticated Users, Everyone)
        # and explicit rules for the target account to ensure a clean state before applying ReadAndExecute.
        # Manual explicit ACEs for custom third-party principals are intentionally preserved.
        $explicitRules = $acl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
        foreach ($rule in $explicitRules) {
            $ruleSid = $rule.IdentityReference
            if ($rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow -and (
                $ruleSid.Equals($builtinUsersSid) -or
                $ruleSid.Equals($authUsersSid) -or
                $ruleSid.Equals($everyoneSid) -or
                $ruleSid.Equals($targetSid)
            )) {
                [void]$acl.RemoveAccessRule($rule)
            }
        }

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

        # Commit ACL to disk
        Set-Acl -Path $exePath -AclObject $acl

        # 5. Audit surviving explicit ACEs for transparency (Target + Manual Users & Groups)
        $postAcl = Get-Acl -Path $exePath
        $survivingRules = $postAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
        $manualRules = @()
        $appliedTargetRules = @()

        foreach ($rule in $survivingRules) {
            $sid = $rule.IdentityReference
            $name = try { $sid.Translate([System.Security.Principal.NTAccount]).Value } catch { $sid.Value }

            if ($sid.Equals($targetSid)) {
                $appliedTargetRules += "$name [$($rule.FileSystemRights) - $($rule.AccessControlType)]"
            }
            elseif (-not ($sid.Equals($adminSid) -or $sid.Equals($systemSid))) {
                $manualRules += "$name [$($rule.FileSystemRights) - $($rule.AccessControlType)]"
            }
        }

        if ($appliedTargetRules.Count -gt 0) {
            foreach ($tRule in $appliedTargetRules) {
                Write-Host "  [Target Granted] $tRule" -ForegroundColor Cyan
            }
        }

        if ($manualRules.Count -gt 0) {
            Write-Host "  [Note] Preserved manual explicit ACE(s) (Users & Groups) on '$exeName':" -ForegroundColor Yellow
            foreach ($mRule in $manualRules) {
                Write-Host "    - $mRule" -ForegroundColor Yellow
            }
        }

        Write-Host "Successfully hardened '$exeName'." -ForegroundColor Green
        $hardened += $exeName
    }
    catch {
        Write-Host "FAILED to harden '$exeName': $_" -ForegroundColor Red
        $skipped += $exeName
    }
}

Write-Host "`nHardened $($hardened.Count) of $($exeNames.Count) executables." -ForegroundColor Cyan

if ($skipped.Count -gt 0) {
    Write-Warning ("Not hardened: {0}" -f ($skipped -join ', '))
    Write-Warning "These files will inherit Modify access from '$programDataDir' when Servy creates them."
    Write-Warning "Start the service once so every binary is extracted, then RE-RUN this script."
    exit 2
}

Write-Host "Executable permission hardening complete." -ForegroundColor Green
