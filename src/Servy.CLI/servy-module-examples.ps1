<#
.SYNOPSIS
  Example scripts demonstrating the usage of the Servy PowerShell module to manage Windows services.

.DESCRIPTION
  This module contains sample commands showing how to install, start, stop, restart, export/import configuration,
  check status, display help, display version, and uninstall Windows services using the Servy CLI PowerShell module.

.NOTES
  Author: Akram El Assas
  Module: Servy
  Requires: PowerShell 5.1 or later
  Repository: https://github.com/aelassas/servy

.EXAMPLE
  # Display the current version of Servy CLI
  Show-ServyVersion

.EXAMPLE
  # Install a new Windows service
  Install-ServyService -Name "DummyService" -Path "C:\Program Files\dotnet\dotnet.exe" -StartupType "Automatic"

.EXAMPLE
  # Export service configuration
  Export-ServyServiceConfig -Name "DummyService" -ConfigFileType "xml" -Path "C:\DummyService.xml"

.EXAMPLE
  # Start, stop, and restart a service
  Start-ServyService -Name "DummyService"
  Stop-ServyService -Name "DummyService"
  Restart-ServyService -Name "DummyService"
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
  -EnableHealth `
  -HeartbeatInterval 5 `
  -MaxFailedChecks 1 `
  -MaxRestartAttempts 2 `
  -RecoveryAction RestartService `
  -FailureProgramPath "C:\Windows\System32\cmd.exe" `
  -FailureProgramStartupDir "C:\Windows\Temp" `
  -FailureProgramParams "/c exit 0 --param 2001" `
  -PreLaunchPath "C:\Windows\System32\cmd.exe" `
  -PreLaunchStartupDir "C:\Windows\Temp" `
  -PreLaunchParams "/c exit 0 --param 2002" `
  -PostLaunchPath "C:\Windows\System32\cmd.exe" `
  -PostLaunchStartupDir "C:\Windows\Temp" `
  -PostLaunchParams "/c exit 0 --param 2003"

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
