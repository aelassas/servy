<#
.SYNOPSIS
  Example scripts demonstrating the usage of the Servy PowerShell module to manage Windows services.

.DESCRIPTION
  This module contains sample commands showing how to install, start, stop, restart, export/import configuration,
  check status, display help, display version, and uninstall Windows services using the Servy CLI PowerShell module.

.NOTES
  Author: Akram El Assas
  Module: Servy
  Requires: PowerShell 2.0 or later
  Repository: https://github.com/aelassas/servy

.EXAMPLE
  # Display the current version of Servy CLI
  Show-ServyVersion

.EXAMPLE
  # Install a new Windows service
  Install-ServyService -Name "MyService" -Path "C:\Apps\MyApp\MyApp.exe" -StartupType "Automatic"

.EXAMPLE
  # Export service configuration
  Export-ServyServiceConfig -Name "MyService" -ConfigFileType "xml" -Path "C:\MyService.xml"

.EXAMPLE
  # Start, stop, and restart a service
  Start-ServyService -Name "MyService"
  Stop-ServyService -Name "MyService"
  Restart-ServyService -Name "MyService"
#>

# ----------------------------------------------------------------
# Import the Servy PowerShell module (force reload if already imported)
# ----------------------------------------------------------------
Import-Module ".\Servy.psm1" -Force

# ----------------------------------------------------------------
# Display the current version of Servy CLI
# ----------------------------------------------------------------
Show-ServyVersion -Quiet

# ----------------------------------------------------------------
# Display full help information for Servy CLI
# ----------------------------------------------------------------
# Show-ServyHelp

# ----------------------------------------------------------------
# Install a new Windows service using Servy
# ----------------------------------------------------------------
Install-ServyService `
  -Quiet `
  -Name "DummyService" `
  -Description "Dummy Service" `
  -Path "C:\Windows\System32\notepad.exe" `
  -StartupDir "C:\Windows\Temp" `
  -Params "--param 2000" `
  -StartupType "Manual" `
  -Priority "BelowNormal" `
  -StartTimeout 15 `
  -StopTimeout 10 `
  -EnableSizeRotation `
  -RotationSize 1 `
  -EnableDateRotation `
  -DateRotationType "Weekly" `
  -MaxRotations 3 `
  -Stdout "C:\Windows\Temp\dummy-stdout.log" `
  -Stderr "C:\Windows\Temp\dummy-stderr.log" `
  -EnableHealth `
  -HeartbeatInterval 5 `
  -MaxFailedChecks 1 `
  -MaxRestartAttempts 2 `
  -RecoveryAction RestartService `
  -FailureProgramPath "C:\Windows\System32\cmd.exe" `
  -FailureProgramStartupDir "C:\Windows\Temp" `
  -FailureProgramParams "/c exit 0 --param 2001" `
  -Env "var1=val1; var2=val2;" `
  -PreLaunchPath "C:\Windows\System32\cmd.exe" `
  -PreLaunchStartupDir "C:\Windows\Temp" `
  -PreLaunchParams "/c exit 0 --param 2002" `
  -PreLaunchEnv "preVar1=val1; preVar2=val2;" `
  -PreLaunchStdout "C:\Windows\Temp\dummy-pre-stdout.log" `
  -PreLaunchStderr "C:\Windows\Temp\dummy-pre-stderr.log" `
  -PreLaunchTimeout 5 `
  -PreLaunchRetryAttempts 1 `
  -PostLaunchPath "C:\Windows\System32\cmd.exe" `
  -PostLaunchStartupDir "C:\Windows\Temp" `
  -PostLaunchParams "/c exit 0 --param 2003" `
  -EnableDebugLogs

# ----------------------------------------------------------------
# Export the service configuration to a file (XML)
# ----------------------------------------------------------------
Export-ServyServiceConfig `
  -Quiet `
  -Name "DummyService" `
  -ConfigFileType "xml" `
  -Path "C:\DummyService.xml"

# ----------------------------------------------------------------
# Export the service configuration to a file (JSON)
# ----------------------------------------------------------------
Export-ServyServiceConfig `
  -Quiet `
  -Name "DummyService" `
  -ConfigFileType "json" `
  -Path "C:\DummyService.json"

# ----------------------------------------------------------------
# Import previously exported service configurations
# ----------------------------------------------------------------
Import-ServyServiceConfig `
  -Quiet `
  -ConfigFileType "xml" `
  -Path "C:\DummyService.xml"

Import-ServyServiceConfig `
  -Quiet `
  -ConfigFileType "json" `
  -Path "C:\DummyService.json"

# ----------------------------------------------------------------
# Start the service
# ----------------------------------------------------------------
Start-ServyService -Name "DummyService" -Quiet

# ----------------------------------------------------------------
# Get the current status of the service
# ----------------------------------------------------------------
Get-ServyServiceStatus -Name "DummyService" -Quiet

# ----------------------------------------------------------------
# Stop the service
# ----------------------------------------------------------------
Stop-ServyService -Name "DummyService" -Quiet

# ----------------------------------------------------------------
# Restart the service
# ----------------------------------------------------------------
Restart-ServyService -Name "DummyService" -Quiet

# ----------------------------------------------------------------
# Uninstall the service
# ----------------------------------------------------------------
Uninstall-ServyService -Name "DummyService" -Quiet
