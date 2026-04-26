<#
.SYNOPSIS
  Example scripts demonstrating the usage of the Servy PowerShell module to manage Windows services.

.DESCRIPTION
  Example scripts demonstrating usage of the Servy PowerShell module.

.NOTES
  Author: Akram El Assas
  Module: Servy
  Requires: PowerShell 2.0 or later
  Repository: https://github.com/aelassas/servy
#>

# ----------------------------------------------------------------
# Import the Servy PowerShell module (force reload if already imported)
# ----------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Import-Module (Join-Path $scriptDir "Servy.psm1") -Force

# ----------------------------------------------------------------
# Display the current version of Servy CLI
# ----------------------------------------------------------------
Get-ServyVersion -Quiet

# ----------------------------------------------------------------
# Install a new Windows service using Servy (splatting)
# ----------------------------------------------------------------
$installParams = @{
    Quiet                     = $true
    Name                      = "DummyService"
    DisplayName               = "Dummy Service"
    Description               = "Dummy Service"
    Path                      = "C:\Windows\System32\notepad.exe"
    StartupDir                = "C:\Windows\Temp"
    Params                    = "--param 2000"
    StartupType               = "Manual"
    Priority                  = "BelowNormal"
    StartTimeout              = 15
    StopTimeout               = 10
    EnableConsoleUI           = $false
    EnableSizeRotation        = $true
    RotationSize              = 1
    EnableDateRotation        = $true
    DateRotationType          = "Weekly"
    MaxRotations              = 3
    UseLocalTimeForRotation   = $true

    Stdout                    = "C:\Windows\Temp\dummy-stdout.log"
    Stderr                    = "C:\Windows\Temp\dummy-stderr.log"

    EnableHealth              = $true
    HeartbeatInterval         = 5
    MaxFailedChecks           = 1
    MaxRestartAttempts        = 2
    RecoveryAction            = "RestartService"

    FailureProgramPath        = "C:\Windows\System32\cmd.exe"
    FailureProgramStartupDir  = "C:\Windows\Temp"
    FailureProgramParams      = "/c exit 0 --param 2001"

    EnvVars                   = "var1=val1; var2=val2;"
    Deps                      = "Tcpip;Dnscache"
    # User                    = ".\DummySvc"
    # Password                = ("ChangeMe!" | ConvertTo-SecureString -AsPlainText -Force)

    PreLaunchPath             = "C:\Windows\System32\cmd.exe"
    PreLaunchStartupDir       = "C:\Windows\Temp"
    PreLaunchParams           = "/c exit 0 --param 2002"
    PreLaunchEnv              = "preVar1=val1; preVar2=val2;"
    PreLaunchStdout           = "C:\Windows\Temp\dummy-pre-stdout.log"
    PreLaunchStderr           = "C:\Windows\Temp\dummy-pre-stderr.log"
    PreLaunchTimeout          = 5
    PreLaunchRetryAttempts    = 1
    PreLaunchIgnoreFailure    = $true

    PostLaunchPath            = "C:\Windows\System32\cmd.exe"
    PostLaunchStartupDir      = "C:\Windows\Temp"
    PostLaunchParams          = "/c exit 0 --param 2003"

    EnableDebugLogs           = $true

    PreStopPath               = "C:\Windows\System32\cmd.exe"
    PreStopStartupDir         = "C:\Windows\Temp"
    PreStopParams             = "/c exit 0 --param 2004"
    PreStopTimeout            = 10
    PreStopLogAsError         = $true

    PostStopPath              = "C:\Windows\System32\cmd.exe"
    PostStopStartupDir        = "C:\Windows\Temp"
    PostStopParams            = "/c exit 0 --param 2005"
}

Install-ServyService @installParams

# ----------------------------------------------------------------
# Export the service configuration (XML)
# ----------------------------------------------------------------
$exportXmlParams = @{
    Quiet          = $true
    Name           = "DummyService"
    ConfigFileType = "xml"
    Path           = "C:\DummyService.xml"
}

Export-ServyServiceConfig @exportXmlParams

# ----------------------------------------------------------------
# Export the service configuration (JSON)
# ----------------------------------------------------------------
$exportJsonParams = @{
    Quiet          = $true
    Name           = "DummyService"
    ConfigFileType = "json"
    Path           = "C:\DummyService.json"
}

Export-ServyServiceConfig @exportJsonParams

# ----------------------------------------------------------------
# Import previously exported service configurations
# ----------------------------------------------------------------
$importXmlParams = @{
    Quiet          = $true
    ConfigFileType = "xml"
    Path           = "C:\DummyService.xml"
}

Import-ServyServiceConfig @importXmlParams

$importJsonParams = @{
    Quiet          = $true
    ConfigFileType = "json"
    Path           = "C:\DummyService.json"
}

Import-ServyServiceConfig @importJsonParams

# ----------------------------------------------------------------
# Service lifecycle
# ----------------------------------------------------------------
Start-ServyService   -Name "DummyService" -Quiet
Get-ServyServiceStatus -Name "DummyService" -Quiet
Stop-ServyService    -Name "DummyService" -Quiet
Restart-ServyService -Name "DummyService" -Quiet

# ----------------------------------------------------------------
# Uninstall the service
# ----------------------------------------------------------------
Uninstall-ServyService -Name "DummyService" -Quiet
