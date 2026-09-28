#requires -Version 5.1
<#
.SYNOPSIS
    Functional test for Set-ServyExePermissions.ps1 (#7136).

.DESCRIPTION
    Runs the hardening script against a throwaway %ProgramData%\Servy tree (ProgramData is redirected to a
    temporary directory for the child process) with NT AUTHORITY\LocalService as the target account, then
    reads the resulting ACLs back. Requires Windows and an elevated session, like the script itself; it is
    skipped otherwise (CI runners are elevated).
#>

$ErrorActionPreference = "Stop"
$scriptPath = Join-Path $PSScriptRoot "Set-ServyExePermissions.ps1"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Running Set-ServyExePermissions.ps1 Tests" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    Write-Host "SKIP: Set-ServyExePermissions.ps1 edits NTFS ACLs and only runs on Windows." -ForegroundColor Yellow
    exit 0
}

$principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "SKIP: Set-ServyExePermissions.ps1 requires an elevated session." -ForegroundColor Yellow
    exit 0
}

$FSR = [System.Security.AccessControl.FileSystemRights]
$IF  = [System.Security.AccessControl.InheritanceFlags]
$targetSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalServiceSid, $null)
$otherSid  = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::NetworkServiceSid, $null)
$targetAccount = $targetSid.Translate([System.Security.Principal.NTAccount]).Value

$failures = @()
function Assert-True([bool]$Condition, [string]$Message) {
    if ($Condition) {
        Write-Host "  [OK] $Message" -ForegroundColor Gray
    } else {
        Write-Host "  FAIL: $Message" -ForegroundColor Red
        $script:failures += $Message
    }
}

# Combined Allow rights a SID holds on a path, explicit and inherited.
function Get-AllowedRights([string]$Path, [System.Security.Principal.SecurityIdentifier]$Sid) {
    $rights = 0
    foreach ($rule in (Get-Acl -LiteralPath $Path).GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier])) {
        if ($rule.IdentityReference.Equals($Sid) -and $rule.AccessControlType -eq 'Allow') {
            $rights = $rights -bor [int]$rule.FileSystemRights
        }
    }
    return $rights
}

function Test-Rights([int]$Rights, $Flag) {
    return (($Rights -band [int]$Flag) -eq [int]$Flag)
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("servy-perm-test-" + [guid]::NewGuid().ToString('N'))
$servyDir = Join-Path $tempRoot "Servy"
$dbDir    = Join-Path $servyDir "db"
$savedProgramData = $env:ProgramData

try {
    New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
    $files = @(
        'Servy.Service.Net48.exe', 'Servy.Service.CLI.Net48.exe', 'Servy.Restarter.Net48.exe', 'handle64.exe',
        'Servy.Service.Net48.exe.config', 'Servy.Service.CLI.Net48.exe.config', 'Servy.Restarter.Net48.exe.config',
        'db\Servy.db'
    )
    foreach ($f in $files) {
        Set-Content -LiteralPath (Join-Path $servyDir $f) -Value "test" -Encoding ASCII
    }

    # Another service account hardened earlier holds an inherited Modify from the vault root: it must keep
    # its access to the database when the script converts that file's inherited ACEs to explicit ones.
    $acl = Get-Acl -LiteralPath $servyDir
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        $otherSid, "Modify", "ContainerInherit, ObjectInherit", "None", "Allow")))
    Set-Acl -LiteralPath $servyDir -AclObject $acl

    Assert-True (-not (Test-Rights (Get-AllowedRights $servyDir $targetSid) $FSR::Modify)) "precondition: the target has no Modify on the vault before the script runs"

    $env:ProgramData = $tempRoot
    $hostExe = (Get-Process -Id $PID).Path
    & $hostExe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -TargetAccount $targetAccount
    $exitCode = $LASTEXITCODE
    $env:ProgramData = $savedProgramData

    Assert-True ($exitCode -eq 0) "script exits 0 (got $exitCode)"

    # 1. The vault root: Modify, inherited by subfolders and files.
    $rootRule = @((Get-Acl -LiteralPath $servyDir).GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier]) |
        Where-Object { $_.IdentityReference.Equals($targetSid) -and $_.AccessControlType -eq 'Allow' -and (Test-Rights ([int]$_.FileSystemRights) $FSR::Modify) })
    Assert-True ($rootRule.Count -ge 1) "the target is granted Modify on %ProgramData%\Servy"
    Assert-True ($rootRule.Count -ge 1 -and (($rootRule[0].InheritanceFlags -band $IF::ContainerInherit) -eq $IF::ContainerInherit) -and (($rootRule[0].InheritanceFlags -band $IF::ObjectInherit) -eq $IF::ObjectInherit)) "the vault Modify grant is inherited by subfolders and files"

    # 2. The database: Read and Write, no Delete, other accounts' access preserved.
    $dbPath   = Join-Path $dbDir "Servy.db"
    $dbRights = Get-AllowedRights $dbPath $targetSid
    Assert-True ((Test-Rights $dbRights $FSR::Read) -and (Test-Rights $dbRights $FSR::Write)) "the target can read and write db\Servy.db"
    Assert-True (-not (Test-Rights $dbRights $FSR::Delete)) "the target has no Delete on db\Servy.db"
    Assert-True ((Get-Acl -LiteralPath $dbPath).AreAccessRulesProtected) "db\Servy.db no longer inherits from the vault"
    Assert-True (Test-Rights (Get-AllowedRights $dbPath $otherSid) $FSR::Modify) "another service account keeps its Modify on db\Servy.db"

    # 3. SQLite's side files created later still inherit Modify (WAL/SHM are created and deleted at run time).
    $walPath = Join-Path $dbDir "Servy.db-wal"
    Set-Content -LiteralPath $walPath -Value "" -Encoding ASCII
    Assert-True (Test-Rights (Get-AllowedRights $walPath $targetSid) $FSR::Modify) "a new file in db\ inherits the target's Modify"

    # 4. The binaries and configuration files stay hardened.
    $exeRights = Get-AllowedRights (Join-Path $servyDir 'Servy.Service.Net48.exe') $targetSid
    Assert-True ((Test-Rights $exeRights $FSR::ReadAndExecute) -and -not (Test-Rights $exeRights $FSR::WriteData)) "Servy.Service.Net48.exe stays Read & Execute for the target"
    # The restarter is extracted by the desktop app, the Manager and the CLI, never by the service account,
    # so like every other binary it is Read & Execute only: no Delete that would let the runner replace it.
    $restarterRights = Get-AllowedRights (Join-Path $servyDir 'Servy.Restarter.Net48.exe') $targetSid
    Assert-True ((Test-Rights $restarterRights $FSR::ReadAndExecute) -and -not (Test-Rights $restarterRights $FSR::WriteData)) "Servy.Restarter.Net48.exe is Read & Execute for the target"
    Assert-True (-not (Test-Rights $restarterRights $FSR::Delete)) "the target has no Delete on Servy.Restarter.Net48.exe"
    $cfgRights = Get-AllowedRights (Join-Path $servyDir 'Servy.Service.Net48.exe.config') $targetSid
    Assert-True ((Test-Rights $cfgRights $FSR::Read) -and -not (Test-Rights $cfgRights $FSR::WriteData)) "Servy.Service.Net48.exe.config stays Read for the target"
}
catch {
    Write-Host "FAIL: Unexpected error: $_" -ForegroundColor Red
    $failures += "unexpected error"
}
finally {
    $env:ProgramData = $savedProgramData
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "Set-ServyExePermissions.ps1 tests FAILED ($($failures.Count))." -ForegroundColor Red
    exit 1
}
Write-Host "All Set-ServyExePermissions.ps1 tests passed." -ForegroundColor Green
exit 0
