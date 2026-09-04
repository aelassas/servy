#Requires -Version 2.0
<#
.SYNOPSIS
    Mandatory Security Hardening: Hardens Servy .NET Framework 4.8 executable, library, and configuration permissions to prevent privilege escalation, binary tampering, and unauthorized configuration modification.

.DESCRIPTION
    Mandatory security script for hardening Servy .NET Framework 4.8 service runner accounts. Servy requires
    directory-level 'Modify' permissions on %ProgramData%\Servy to write database logs, process state, and runtime
    recovery files. However, leaving binaries, loaded assemblies, and configuration files with inherited 'Modify' access
    allows a compromised service process or unprivileged runner account to tamper with, replace, or hijack core executables, DLLs, or app settings.

    This script enforces Servy's Single Trust Boundary security model by breaking permission inheritance on core
    executable files, DLL assemblies, and configuration files (*.exe.config), restricting the target runner account to strict 'Read & Execute'
    rights for executables/DLLs and 'Read' rights for configuration files.
    This ensures the service runner can execute required binaries and read settings without being able to overwrite, replace,
    or DLL-hijack them, protecting against unprivileged binary replacement and local privilege escalation vectors. Full Control is
    explicitly preserved for SYSTEM and Administrators using language-agnostic Well-Known SIDs. The owner of each hardened
    file is set to Builtin Administrators. Manually added explicit ACEs for third-party principals (both users and groups) are audited and preserved.

    Hardened Target Files:
    - Servy.Service.Net48.exe
    - Servy.Service.CLI.Net48.exe (Note: May not be present on a fresh install; start the service with the CLI once so Servy.Service.CLI.Net48.exe gets copied to %ProgramData%\Servy)
    - Servy.Restarter.Net48.exe (Note: May not be present on a fresh install; start the service once so Servy.Restarter.Net48.exe gets copied to %ProgramData%\Servy)
    - handle64.exe
    - Servy.Service.Net48.exe.config
    - Servy.Service.CLI.Net48.exe.config
    - Servy.Restarter.Net48.exe.config
    - All *.dll files in %ProgramData%\Servy

    EXIT CODES:
    - 0 : Success. All target files were present and successfully hardened.
    - 1 : Privilege Error. Script is not running in an elevated PowerShell session with Administrator privileges.
    - 2 : Directory or File Missing. Target directory (%ProgramData%\Servy) does not exist, or one or more target binaries/libraries are missing and must be extracted before hardening.
    - 3 : Hardening Error. One or more present target files failed ACL modification due to locks, owner change failures, or security exceptions.
    - 4 : Account Error. -TargetAccount could not be resolved by LSA or is an invalid target.

.PARAMETER TargetAccount
    Mandatory account identifier receiving Read & Execute (for binaries/libraries) or Read (for configuration) permissions. Supported formats:
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

function Get-SidDisplayName {
    <#
        .SYNOPSIS
            Translates a SecurityIdentifier to an NTAccount display string with fallback to raw SID.
    #>
    param([System.Security.Principal.SecurityIdentifier]$Sid)
    try {
        return $Sid.Translate([System.Security.Principal.NTAccount]).Value
    }
    catch {
        return $Sid.Value
    }
}

function Test-ServyAdminGroupMember {
    <#
        .SYNOPSIS
            Determines whether a SID is a member of BUILTIN\Administrators (not merely equal to it).

        .DESCRIPTION
            Step 4's ACL logic only special-cases a target that IS BUILTIN\Administrators or SYSTEM.
            A named account that is merely a MEMBER of Administrators still inherits FullControl via
            the group ACE granted in step 3, regardless of any explicit ReadAndExecute/Read ACE written
            for that account. This function checks for that membership so the script can report the
            true effective access instead of the explicit ACE it just wrote.

            Returns $true (confirmed member), $false (confirmed not a member by a complete enumeration),
            or $null (could not be determined - e.g. gMSA or remote accounts that cannot be impersonated
            for an S4U check, or an unreachable/RODC-limited domain controller or unexpanded nested groups
            for the WinNT fallback).

        .PARAMETER AccountName
            The account identifier as supplied by the caller (e.g. 'DOMAIN\User', 'LocalService').

        .PARAMETER Sid
            The resolved SecurityIdentifier for AccountName.

        .PARAMETER AdminSid
            The well-known SecurityIdentifier for BUILTIN\Administrators.
    #>
    param(
        [string]$AccountName,
        [System.Security.Principal.SecurityIdentifier]$Sid,
        [System.Security.Principal.SecurityIdentifier]$AdminSid
    )

    # Fast path: construct a token for the account and ask it directly. This is the only check that
    # correctly follows nested domain/global group membership, but it requires the "Act as part of the
    # operating system" privilege and S4U logon support, so it fails for gMSAs and many service accounts.
    try {
        $identity = New-Object System.Security.Principal.WindowsIdentity($AccountName)
        try {
            $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
            return $principal.IsInRole($AdminSid)
        }
        finally {
            $identity.Dispose()
        }
    }
    catch {
        # Expected for gMSA/service accounts and most non-interactive principals; fall back below.
    }

    # Fallback: enumerate the local Administrators group's members via ADSI and compare SIDs directly.
    # This only sees direct members of the local group (no recursive nested-group expansion), but it
    # requires no special privilege and works for domain accounts added directly to the local group.
    try {
        # Resolve the group's CURRENT name from its well-known SID rather than assuming the literal
        # string "Administrators": the built-in group is commonly renamed as a hardening measure, and
        # the WinNT provider binds by name, not by well-known SID.
        $adminGroupName = "Administrators"
        try {
            $resolvedName = $AdminSid.Translate([System.Security.Principal.NTAccount]).Value
            $adminGroupName = $resolvedName -replace '^.*\\', ''
        }
        catch {
            # Translation failed; fall back to the default English name below.
        }

        # A definite "not a member" is only sound if every member was readable and none was a
        # group that could contain the target. Otherwise the honest answer is "unknown".
        $enumerationComplete = $true
        $adminGroup = [ADSI]"WinNT://./$adminGroupName,group"
        foreach ($member in $adminGroup.Invoke("Members")) {
            try {
                # Retrieve byte array objectSID directly from ADSI object or compare WinNT Path
                $sidBytes = $member.GetType().InvokeMember("objectSID", [System.Reflection.BindingFlags]::GetProperty, $null, $member, $null)
                if ($null -ne $sidBytes) {
                    $memberSid = New-Object System.Security.Principal.SecurityIdentifier($sidBytes, 0)
                    if ($memberSid.Equals($Sid)) {
                        return $true
                    }
                }

                $memberClass = $member.GetType().InvokeMember("Class", [System.Reflection.BindingFlags]::GetProperty, $null, $member, $null)
                if ($memberClass -eq 'Group') {
                    # Nested group: the target could be a member of it and we do not expand.
                    $enumerationComplete = $false
                }
            }
            catch {
                # Unreadable member (orphaned SID, RODC-limited lookup) - an unchecked member.
                $enumerationComplete = $false
                # Fall back to ADSI WinNT AdsPath string comparison if objectSID is inaccessible
                try {
                    $adsPath = $member.GetType().InvokeMember("AdsPath", [System.Reflection.BindingFlags]::GetProperty, $null, $member, $null)
                    if ($adsPath -like "*/$($AccountName.Replace('\', '/'))" -or $adsPath -like "*/$($Sid.Value)") {
                        return $true
                    }
                }
                catch { }
            }
        }

        if (-not $enumerationComplete) {
            return $null
        }

        return $false
    }
    catch {
        Write-Warning "Unable to enumerate local Administrators group membership for '$AccountName': $_"
        return $null
    }
}

try {
    # .NET Framework 4.8 executable target list
    $staticExeNames = @(
        'Servy.Service.Net48.exe',
        'Servy.Service.CLI.Net48.exe',
        'Servy.Restarter.Net48.exe',
        'handle64.exe'
    )

    # .NET Framework 4.8 configuration target list
    $staticConfigNames = @(
        'Servy.Service.Net48.exe.config',
        'Servy.Service.CLI.Net48.exe.config',
        'Servy.Restarter.Net48.exe.config'
    )

    $programDataDir = [System.IO.Path]::Combine($env:ProgramData, "Servy")

    if (-not (Test-Path -Path $programDataDir)) {
        Write-Warning "Directory '$programDataDir' does not exist. No changes applied."
        Write-Warning "Start the service once so %ProgramData%\Servy and its binaries are extracted, then RE-RUN this script."
        exit 2
    }

    # Dynamically validate and resolve account existence via Windows LSA.
    # First attempt: Direct LSA translation handles built-in principals, domain accounts, and standard formats.
    $targetNTAccount = $null
    $targetSid        = $null
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
        Write-Host "Account '$TargetAccount' could not be resolved on this machine or domain." -ForegroundColor Red
        Write-Host "Use the form shown by services.msc (e.g., 'LocalService', 'NT AUTHORITY\LocalService', 'DOMAIN\svc-servy', or 'DOMAIN\gMSAAccount$')." -ForegroundColor Red
        exit 4
    }

    # Define Well-Known SIDs for language-agnostic administrative control
    $adminSid           = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
    $systemSid          = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
    $builtinUsersSid    = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinUsersSid, $null)
    $authUsersSid       = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::AuthenticatedUserSid, $null)
    $everyoneSid        = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::WorldSid, $null)

    # Refuse broad unprivileged groups that step 2 exists to purge
    if ($targetSid.Equals($everyoneSid) -or $targetSid.Equals($builtinUsersSid) -or $targetSid.Equals($authUsersSid)) {
        Write-Host "Account '$TargetAccount' is a broad unprivileged group that this script exists to remove." -ForegroundColor Red
        Write-Host "Specify the specific service runner account instead." -ForegroundColor Red
        exit 4
    }

    # A target that is a MEMBER of BUILTIN\Administrators (not just equal to it) still inherits FullControl
    # via the group ACE granted in step 3, regardless of any explicit ReadAndExecute/Read ACE written below.
    $targetIsAdminMember = $null
    if (-not ($targetSid.Equals($adminSid) -or $targetSid.Equals($systemSid))) {
        $targetIsAdminMember = Test-ServyAdminGroupMember -AccountName $TargetAccount -Sid $targetSid -AdminSid $adminSid
    }

    if ($targetIsAdminMember -eq $true) {
        Write-Host "[WARNING] '$TargetAccount' is a member of BUILTIN\Administrators, which retains FullControl." -ForegroundColor Cyan
        Write-Host "          Its effective access will be FullControl, not the ReadAndExecute/Read this script writes." -ForegroundColor Cyan
        Write-Host "          Use a non-administrative service account for the trust boundary this script establishes." -ForegroundColor Cyan
    }
    elseif ($null -eq $targetIsAdminMember -and -not ($targetSid.Equals($adminSid) -or $targetSid.Equals($systemSid))) {
        Write-Warning "Could not verify whether '$TargetAccount' is a member of BUILTIN\Administrators. Manually confirm its effective access after hardening."
    }

    # Discover all .dll files in %ProgramData%\Servy (PS 2.0 compatible file discovery without -File flag)
    $dllFiles = Get-ChildItem -Path $programDataDir -Filter "*.dll" -ErrorAction SilentlyContinue |
        Where-Object { -not $_.PSIsContainer } |
        Select-Object -ExpandProperty Name

    # Build unified target map with respective permissions
    $targetFiles = @()

    foreach ($exe in $staticExeNames) {
        $targetFiles += @{ Name = $exe; Rights = "ReadAndExecute" }
    }

    foreach ($dll in $dllFiles) {
        if (-not [string]::IsNullOrEmpty($dll)) {
            $targetFiles += @{ Name = $dll; Rights = "ReadAndExecute" }
        }
    }

    foreach ($cfg in $staticConfigNames) {
        $targetFiles += @{ Name = $cfg; Rights = "Read" }
    }

    Write-Host "Securing Servy (.NET Framework 4.8) binary, library, and configuration files in: $programDataDir" -ForegroundColor Cyan
    Write-Host "Target Account: $TargetAccount" -ForegroundColor Yellow

    $hardened       = @()
    $missing        = @()
    $failed         = @()
    $skippedConfigs = @()

    foreach ($item in $targetFiles) {
        $fileName       = $item.Name
        $requiredRights = $item.Rights

        if ([string]::IsNullOrEmpty($fileName)) { continue }

        $filePath = [System.IO.Path]::Combine($programDataDir, $fileName)

        if (-not (Test-Path -Path $filePath)) {
            if ($staticConfigNames -contains $fileName) {
                Write-Host "Skipping hardening for missing configuration file '$fileName'." -ForegroundColor Yellow
                $skippedConfigs += $fileName
            } else {
                $missing += $fileName
            }
            continue
        }

        try {
            Write-Host "Hardening permissions on '$fileName' ($requiredRights)..." -ForegroundColor Green

            $acl = Get-Acl -Path $filePath

            # Inspect previous owner prior to setting Builtin Administrators owner
            $previousOwnerSid = $acl.GetOwner([System.Security.Principal.SecurityIdentifier])
            $ownerChanged = -not $previousOwnerSid.Equals($adminSid)
            $previousOwnerName = if ($ownerChanged) { Get-SidDisplayName $previousOwnerSid } else { $null }

            # Explicitly set owner to Builtin Administrators to avoid owner SID mismatch errors during Set-Acl
            $acl.SetOwner($adminSid)

            # 1. Break inheritance and purge existing inherited permissions ($isProtected = $true, $preserveInheritance = $false)
            $acl.SetAccessRuleProtection($true, $false)

            # 2. Purge explicit grants for broad unprivileged groups (Users, Authenticated Users, Everyone)
            # and explicit rules for the target account to ensure a clean state before applying target rights.
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

            # 4. Grant explicit ReadAndExecute or Read access to target account
            if ($targetSid.Equals($adminSid) -or $targetSid.Equals($systemSid)) {
                Write-Host "  Target '$TargetAccount' is a protected administrative principal; FullControl retained, no $requiredRights downgrade applied." -ForegroundColor Yellow
            } else {
                $targetRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
                    $targetNTAccount,
                    $requiredRights,
                    "Allow"
                )
                $acl.SetAccessRule($targetRule)

                # The explicit ACE above does NOT establish the trust boundary if the target is also a member
                # of BUILTIN\Administrators: that group ACE (step 3) grants FullControl and always wins.
                if ($targetIsAdminMember -eq $true) {
                    Write-Host "  [WARNING] '$TargetAccount' is a member of BUILTIN\Administrators; effective access is FullControl, not $requiredRights." -ForegroundColor Cyan
                }
            }

            # Commit ACL to disk
            Set-Acl -Path $filePath -AclObject $acl

            # Audit owner replacement only AFTER successful Set-Acl commit
            if ($ownerChanged) {
                Write-Host "  [Owner] replaced '$previousOwnerName' -> 'BUILTIN\Administrators'" -ForegroundColor Cyan
            }

            # 5. Audit surviving explicit ACEs for transparency (Target + Manual Users & Groups)
            $postAcl = Get-Acl -Path $filePath
            $survivingRules = $postAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier])
            $manualAllowRules = @()
            $manualDenyRules  = @()
            $appliedTargetRules = @()

            foreach ($rule in $survivingRules) {
                $sid = $rule.IdentityReference
                $name = Get-SidDisplayName $sid

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

            foreach ($tRule in $appliedTargetRules) {
                if ($targetIsAdminMember -eq $true) {
                    Write-Host "  [Target Granted] $tRule (WARNING: effective access is FullControl via BUILTIN\Administrators membership)" -ForegroundColor Cyan
                } else {
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
            $failed += $fileName
        }
    }

    Write-Host "`nHardened $($hardened.Count) of $($targetFiles.Count) files." -ForegroundColor Cyan

    if ($skippedConfigs.Count -gt 0) {
        Write-Host "Skipped configuration file(s) (not present): $($skippedConfigs -join ', ')" -ForegroundColor Yellow
    }

    if ($failed.Count -gt 0) {
        Write-Warning ("Hardening failed on: {0}" -f ($failed -join ', '))
        Write-Warning "Check file locks, permissions, or system security settings before re-running this script."
        exit 3
    }

    if ($missing.Count -gt 0) {
        Write-Warning ("Not hardened (missing): {0}" -f ($missing -join ', '))
        Write-Warning "These files will inherit Modify access from '$programDataDir' when Servy creates them."
        Write-Warning "Start the service once so every binary is extracted, then RE-RUN this script."
        exit 2
    }

    Write-Host "Executable, library, and configuration permission hardening complete." -ForegroundColor Cyan

    if ($targetIsAdminMember -eq $true) {
        Write-Host "[WARNING] '$TargetAccount' is a member of BUILTIN\Administrators, which retains FullControl." -ForegroundColor Cyan
        Write-Host "          The explicit ACEs written above do NOT establish the single trust boundary this" -ForegroundColor Cyan
        Write-Host "          script promises. Use a non-administrative service account instead." -ForegroundColor Cyan
    }
    elseif ($null -eq $targetIsAdminMember -and -not ($targetSid.Equals($adminSid) -or $targetSid.Equals($systemSid))) {
        Write-Warning "Could not verify whether '$TargetAccount' is a member of BUILTIN\Administrators. Manually confirm its effective access."
    }
}
catch {
    Write-Host "Unhandled error during execution: $_" -ForegroundColor Red
    exit 3
}
