<#
.SYNOPSIS
    Hardens Servy .NET Framework 4.8 executable permissions by breaking inheritance and granting Read & Execute rights.

.DESCRIPTION
    Breaks permission inheritance on Servy .NET Framework 4.8 binary executables in %ProgramData%\Servy and grants
    explicit Read & Execute rights to a target user, domain account, gMSA, or local account.
    Preserves Full Control for SYSTEM and Administrators using Well-Known SIDs.

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
    [Parameter(Mandatory = $true, HelpMessage = "Specify target account (e.g., 'DOMAIN\User', '.\User', or 'DOMAIN\gMSA$').")]
    [ValidateNotNullOrEmpty()]
    [ValidatePattern('^(?i)(?:(?:\.|[a-z0-9_.-]+)\\)?[a-z0-9_.-]+\$?$')]
    [string]$TargetAccount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# .NET Framework 4.8 executable target list
$exeNames = @(
    'Servy.Service.Net48.exe',
    'Servy.Service.CLI.Net48.exe',
    'Servy.Restarter.Net48.exe'
)

$programDataDir = [System.IO.Path]::Combine($env:ProgramData, "Servy")

if (-not (Test-Path -Path $programDataDir)) {
    Write-Warning "Directory '$programDataDir' does not exist. No changes applied."
    exit 0
}

# Resolve relative notation '.\User' to actual computer name
if ($TargetAccount.StartsWith(".\")) {
    $TargetAccount = "$env:COMPUTERNAME\" + $TargetAccount.Substring(2)
}

# Strict-mode safe instantiation of NTAccount
$targetNTAccount = New-Object System.Security.Principal.NTAccount($TargetAccount)

# Define Well-Known SIDs for language-agnostic administrative control
$adminSid  = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
$systemSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)

Write-Host "Securing Servy (.NET Framework 4.8) executable files in: $programDataDir" -ForegroundColor Cyan
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
