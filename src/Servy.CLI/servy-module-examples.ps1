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
  Install-ServyService -Name "WexflowServer" -Path "C:\Program Files\dotnet\dotnet.exe" -StartupType "Automatic"

.EXAMPLE
  # Export service configuration
  Export-ServyServiceConfig -Name "WexflowServer" -ConfigFileType "xml" -Path "C:\WexflowServer.xml"

.EXAMPLE
  # Start, stop, and restart a service
  Start-ServyService -Name "WexflowServer"
  Stop-ServyService -Name "WexflowServer"
  Restart-ServyService -Name "WexflowServer"
#>

# ----------------------------------------------------------------
# Import the Servy PowerShell module (force reload if already imported)
# ----------------------------------------------------------------
Import-Module .\Servy.psm1 -Force

# ----------------------------------------------------------------
# Display the current version of Servy CLI
# ----------------------------------------------------------------
Show-ServyVersion

# ----------------------------------------------------------------
# Display full help information for Servy CLI
# ----------------------------------------------------------------
# Show-ServyHelp

# ----------------------------------------------------------------
# Install a new Windows service using Servy
# ----------------------------------------------------------------
Install-ServyService `
  -Name "WexflowServer" `
  -Description "Wexflow Workflow Engine" `
  -Path "C:\Program Files\dotnet\dotnet.exe" `
  -StartupDir "C:\Program Files\Wexflow Server\Wexflow.Server" `
  -Params "Wexflow.Server.dll" `
  -StartupType "Automatic" `
  -EnableHealth `
  -HeartbeatInterval 5 `
  -MaxFailedChecks 1 `
  -MaxRestartAttempts 2 `
  -RecoveryAction RestartService

# ----------------------------------------------------------------
# Export the service configuration to a file (XML)
# ----------------------------------------------------------------
Export-ServyServiceConfig `
  -Name "WexflowServer" `
  -ConfigFileType "xml" `
  -Path "C:\WexflowServer.xml"

# ----------------------------------------------------------------
# Export the service configuration to a file (JSON)
# ----------------------------------------------------------------
Export-ServyServiceConfig `
  -Name "WexflowServer" `
  -ConfigFileType "json" `
  -Path "C:\WexflowServer.json"

# ----------------------------------------------------------------
# Import previously exported service configurations
# ----------------------------------------------------------------
Import-ServyServiceConfig `
  -ConfigFileType "xml" `
  -Path "C:\WexflowServer.xml"

Import-ServyServiceConfig `
  -ConfigFileType "json" `
  -Path "C:\WexflowServer.json"

# ----------------------------------------------------------------
# Start the service
# ----------------------------------------------------------------
Start-ServyService -Name "WexflowServer"

# ----------------------------------------------------------------
# Get the current status of the service
# ----------------------------------------------------------------
Get-ServyServiceStatus -Name "WexflowServer"

# ----------------------------------------------------------------
# Stop the service
# ----------------------------------------------------------------
Stop-ServyService -Name "WexflowServer"

# ----------------------------------------------------------------
# Restart the service
# ----------------------------------------------------------------
Restart-ServyService -Name "WexflowServer"

# ----------------------------------------------------------------
# Uninstall the service
# ----------------------------------------------------------------
Uninstall-ServyService -Name "WexflowServer"
