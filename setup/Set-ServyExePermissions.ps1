#Requires -Version 2.0
<#
.SYNOPSIS
    Mandatory Security Hardening: Hardens Servy .NET Framework 4.8 executable and library permissions to Read & Execute to prevent privilege escalation and binary tampering.

.DESCRIPTION
    Mandatory security script for hardening Servy .NET Framework 4.8 service runner accounts. Servy requires
    directory-level 'Modify' permissions on %ProgramData%\Servy to write database logs, process state, and runtime
    recovery files. However, leaving binaries and loaded assemblies with inherited 'Modify' access allows a
    compromised service process or unprivileged runner account to tamper with, replace, or hijack core executables and DLLs.

    This script enforces Servy's Single Trust Boundary security model by breaking permission inheritance on core
    executable files and DLL assemblies, restricting the target runner account to strict 'Read & Execute' rights.
    This ensures the service runner can execute required binaries without being able to overwrite, replace, or DLL-hijack
    them, protecting against unprivileged binary replacement and local privilege escalation vectors. Full Control is
    explicitly preserved for SYSTEM and Administrators using language-agnostic Well-Known SIDs. Manually added explicit ACEs
    for third-party principals (both users and groups) are audited and preserved.

    Hardened Target Files:
    - Servy.Service.Net48.exe
    - Servy.Service.CLI.Net48.exe (Note: May not be present on a fresh install; start the service with the CLI once so Servy.Service.CLI.Net48.exe gets copied to %ProgramData%\Servy)
    - Servy.Restarter.Net48.exe (Note: May not be present on a fresh install; start the service once so Servy.Restarter.Net48.exe gets copied to %ProgramData%\Servy)
    - handle64.exe
    - All *.dll files in %ProgramData%\Servy

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

# Ensure the script is executing with Administrator privileges (PS 2.0 / .NET Framework compatible)
$currentIdentity  = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
$adminRole        = [System.Security.Principal.WindowsBuiltInRole]::Administrator
if (-not $currentPrincipal.IsInRole($adminRole)) {
    Write-Host "Set-ServyExePermissions.ps1 requires Administrator privileges. Please re-run script in an elevated PowerShell session." -ForegroundColor Red
    exit 1
}

# .NET Framework 4.8 executable target list
$staticExeNames = @(
    'Servy.Service.Net48.exe',
    'Servy.Service.CLI.Net48.exe',
    'Servy.Restarter.Net48.exe',
    'handle64.exe'
)

$programDataDir = [System.IO.Path]::Combine($env:ProgramData, "Servy")

if (-not (Test-Path -Path $programDataDir)) {
    Write-Warning "Directory '$programDataDir' does not exist. No changes applied."
    exit 0
}

# Dynamically validate and resolve account existence via Windows LSA.
# First attempt: Direct LSA translation handles built-in principals, domain accounts, and standard formats.
$targetNTAccount = $null
$targetSid       = $null
try {
    $ntAccountCandidate = New-Object System.Security.Principal.NTAccount($TargetAccount)
    $targetSid          = $ntAccountCandidate.Translate([System.Security.Principal.SecurityIdentifier])
    $targetNTAccount    = $ntAccountCandidate
}
catch {
    # Second attempt: Fall back for relative notation ('.\User') pointing to a local machine account (PS 2.0 compatible)
    if ($TargetAccount.Length -ge 2 -and $TargetAccount.Substring(0, 2) -eq ".\") {
        $localUser = "$env:COMPUTERNAME\" + $TargetAccount.Substring(2)
        try {
            $ntAccountCandidate = New-Object System.Security.Principal.NTAccount($localUser)
            $targetSid          = $ntAccountCandidate.Translate([System.Security.Principal.SecurityIdentifier])
            $targetNTAccount    = $ntAccountCandidate
            $TargetAccount       = $localUser
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

# Discover all .dll files in %ProgramData%\Servy (PS 2.0 compatible file discovery without -File flag)
$dllFiles = Get-ChildItem -Path $programDataDir -Filter "*.dll" -ErrorAction SilentlyContinue |
    Where-Object { -not $_.PSIsContainer } |
    Select-Object -ExpandProperty Name

# Combine static executables and discovered DLLs
$targetFiles = @($staticExeNames) + @($dllFiles)

Write-Host "Securing Servy (.NET Framework 4.8) binary and library files in: $programDataDir" -ForegroundColor Cyan
Write-Host "Target Account: $TargetAccount" -ForegroundColor Yellow

$hardened = @()
$skipped  = @()

foreach ($fileName in $targetFiles) {
    if ([string]::IsNullOrEmpty($fileName)) { continue }

    $filePath = [System.IO.Path]::Combine($programDataDir, $fileName)

    if (-not (Test-Path -Path $filePath)) {
        $skipped += $fileName
        continue
    }

    try {
        Write-Host "Hardening permissions on '$fileName'..." -ForegroundColor Green

        $acl = Get-Acl -Path $filePath

        # Explicitly set owner to Builtin Administrators to avoid owner SID mismatch errors during Set-Acl
        $acl.SetOwner($adminSid)

        # 1. Break inheritance and purge existing inherited permissions ($isProtected = $true, $preserveInheritance = $false)
        $acl.SetAccessRuleProtection($true, $false)

        # 2. Purge explicit grants for broad unprivileged groups (Users, Authenticated Users, Everyone)
        # and explicit rules for the target account to ensure a clean state before applying ReadAndExecute.
        # Manual explicit ACEs for custom third-party principals (both users and groups) are intentionally preserved.
        $explicitRules = $acl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
        foreach ($rule in $explicitRules) {
            $ruleSid = $rule.IdentityReference
            if ($ruleSid.Equals($targetSid) -or (
                $rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow -and (
                    $ruleSid.Equals($builtinUsersSid) -or
                    $ruleSid.Equals($authUsersSid) -or
                    $ruleSid.Equals($everyoneSid)
                )
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
        Set-Acl -Path $filePath -AclObject $acl

        # 5. Audit surviving explicit ACEs for transparency (Target + Manual Users & Groups)
        $postAcl = Get-Acl -Path $filePath
        $survivingRules = $postAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
        $manualAllowRules = @()
        $manualDenyRules  = @()
        $appliedTargetRules = @()

        foreach ($rule in $survivingRules) {
            $sid = $rule.IdentityReference
            $name = try { $sid.Translate([System.Security.Principal.NTAccount]).Value } catch { $sid.Value }

            if ($sid.Equals($targetSid)) {
                $appliedTargetRules += "$name [$($rule.FileSystemRights) - $($rule.AccessControlType)]"
            }
            elseif (-not ($sid.Equals($adminSid) -or $sid.Equals($systemSid))) {
                $ruleEntry = "$name [$($rule.FileSystemRights) - $($rule.AccessControlType)]"
                if ($rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Deny) {
                    $manualDenyRules += $ruleEntry
                } else {
                    $manualAllowRules += $ruleEntry
                }
            }
        }

        if ($appliedTargetRules.Count -gt 0) {
            foreach ($tRule in $appliedTargetRules) {
                Write-Host "  [Target Granted] $tRule" -ForegroundColor Cyan
            }
        }

        if ($manualAllowRules.Count -gt 0) {
            Write-Host "  [Note] Preserved manual explicit ACE(s) (Users & Groups) on '$fileName':" -ForegroundColor Yellow
            foreach ($mRule in $manualAllowRules) {
                Write-Host "    - $mRule" -ForegroundColor Yellow
            }
        }

        if ($manualDenyRules.Count -gt 0) {
            Write-Host "  [WARNING] Preserved manual explicit Deny ACE(s) on '$fileName':" -ForegroundColor Red
            foreach ($dRule in $manualDenyRules) {
                Write-Host "    - $dRule" -ForegroundColor Red
            }
        }

        Write-Host "Successfully hardened '$fileName'." -ForegroundColor Green
        $hardened += $fileName
    }
    catch {
        Write-Host "FAILED to harden '$fileName': $_" -ForegroundColor Red
        $skipped += $fileName
    }
}

Write-Host "`nHardened $($hardened.Count) of $($targetFiles.Count) files." -ForegroundColor Cyan

if ($skipped.Count -gt 0) {
    Write-Warning ("Not hardened: {0}" -f ($skipped -join ', '))
    Write-Warning "These files will inherit Modify access from '$programDataDir' when Servy creates them."
    Write-Warning "Start the service once so every binary is extracted, then RE-RUN this script."
    exit 2
}

Write-Host "Executable and library permission hardening complete." -ForegroundColor Cyan
