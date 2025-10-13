<#
.SYNOPSIS
  Servy PowerShell module to manage Windows services using the Servy CLI.

.DESCRIPTION
  This module provides functions to install, uninstall, start, stop, restart,
  export and import configurations, and check the status of Windows services
  via the Servy CLI.

  Functions included:
    - Show-ServyVersion
    - Show-ServyHelp
    - Install-ServyService
    - Uninstall-ServyService
    - Start-ServyService
    - Stop-ServyService
    - Restart-ServyService
    - Get-ServyServiceStatus
    - Export-ServyServiceConfig
    - Import-ServyServiceConfig

.NOTES
  Author      : Akram El Assas
  Module Name : Servy
  Requires    : PowerShell 5.1 or later
  Repository  : https://github.com/aelassas/servy

.EXAMPLE
  # Display the current Servy CLI version
  Show-ServyVersion

.EXAMPLE
  # Install a new service
  Install-ServyService -Name "MyService" -Path "C:\Services\MyService.exe" -StartupType "Automatic"

.EXAMPLE
  # Export a service configuration to XML
  Export-ServyServiceConfig -Name "MyService" -ConfigFileType "xml" -Path "C:\MyService.xml"
#>

$script:ServyCliPath = "C:\Program Files\Servy\servy-cli.exe"

<#
.SYNOPSIS
    Checks if the Servy CLI executable exists at the configured path.

.DESCRIPTION
    This helper function validates that the Servy CLI is present at the path
    specified by $script:ServyCliPath. If the file does not exist, it throws
    an error. This prevents repeated boilerplate checks in every Servy function.

.EXAMPLE
    Test-ServyCliPath
    # Throws an error if Servy CLI is not found, otherwise continues silently.
#>
function Test-ServyCliPath {
  if (-not (Test-Path $script:ServyCliPath)) {
    throw "Servy CLI not found at path: $script:ServyCliPath"
  }
}

function Show-ServyVersion {
  <#
    .SYNOPSIS
        Displays the version of the Servy CLI.

    .DESCRIPTION
        Wraps the Servy CLI `--version` command to show the current version
        of the Servy tool installed on the system.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .EXAMPLE
        Show-ServyVersion
        # Displays the current version of Servy CLI.
    #>
  param(
    [switch] $Quiet
  )
 
  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("--version")

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to get Servy CLI version: $_"
    exit 1
  }
}

function Show-ServyHelp {
  <#
    .SYNOPSIS
        Displays help information for the Servy CLI.

    .DESCRIPTION
        Wraps the Servy CLI `help` command to show usage information
        and details about all available commands and options.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .EXAMPLE
        Show-ServyHelp
        # Displays help for the Servy CLI.
    #>
  param(
    [switch] $Quiet
  )

  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("--help")

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to display Servy CLI help: $_"
    exit 1
  }
}

function Add-Arg {
  <#
  .SYNOPSIS
      Adds a key-value argument to a list of command-line arguments.

  .DESCRIPTION
      This helper function appends a command-line argument in the form:
          key="value"
      to an existing .NET generic list of strings (`System.Collections.Generic.List[string]`),
      but only if the value is not null or empty.
      Useful for efficiently building CLI argument lists dynamically
      without creating new array copies.

  .PARAMETER list
      The existing generic list of arguments (`System.Collections.Generic.List[string]`)
      to which the new argument will be added.

  .PARAMETER key
      The name of the argument or option (e.g., "--startupDir").

  .PARAMETER value
      The value associated with the argument. Only added if not null or empty.

  .OUTPUTS
      Returns the updated generic list of arguments including the new key-value pair.

  .EXAMPLE
      $argsList = [System.Collections.Generic.List[string]]::new()
      $argsList = Add-Arg $argsList "--startupDir" "C:\MyApp"
      # Result: $argsList contains '--startupDir="C:\MyApp"'
  #>
  param(
    [System.Collections.Generic.List[string]] $list, # Existing argument list
    [string] $key, # Argument key
    [string] $value # Argument value
  )

  # Only add the argument if a non-empty value is provided
  if ($null -ne $value -and $value -ne "") {
    # Add the argument in the form key="value"
    $list.Add("$key=`"$value`"")
  }

  # Return the same instance without PowerShell array coercion  
  return ,$list  # the comma prevents unrolling / array coercion
}

function Install-ServyService {
  <#
    .SYNOPSIS
        Installs a new Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `install` command to create a Windows service from any
        executable. This function allows configuring service name, description, process path,
        startup directory, parameters, startup type, process priority, logging, health monitoring,
        recovery actions, environment variables, dependencies, service account credentials,
        and optional pre-launch and post-launch executables.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The unique name of the service to install. (Required)

    .PARAMETER Path
        Path to the executable process to run as the service. (Required)

    .PARAMETER Description
        Optional descriptive text about the service.

    .PARAMETER StartupDir
        The startup/working directory for the service process. Optional.

    .PARAMETER Params
        Additional parameters for the service process. Optional.

    .PARAMETER StartupType
        Startup type of the service. Options: Automatic, Manual, Disabled. Optional.

    .PARAMETER Priority
        Process priority. Options: Idle, BelowNormal, Normal, AboveNormal, High, RealTime. Optional.

    .PARAMETER Stdout
        File path for capturing standard output logs. Optional.

    .PARAMETER Stderr
        File path for capturing standard error logs. Optional.

    .PARAMETER EnableRotation
        Switch to enable log rotation. Optional.

    .PARAMETER RotationSize
        Maximum log file size in bytes before rotation. Must be >= 1 MB. Optional.

    .PARAMETER EnableHealth
        Switch to enable health monitoring. Optional.

    .PARAMETER HeartbeatInterval
        Heartbeat interval in seconds for health checks. Must be >= 5. Optional.

    .PARAMETER MaxFailedChecks
        Maximum number of failed health checks before triggering recovery. Optional.

    .PARAMETER RecoveryAction
        Recovery action on failure. Options: None, RestartService, RestartProcess, RestartComputer. Optional.

    .PARAMETER MaxRestartAttempts
        Maximum number of restart attempts after failure. Optional.

    .PARAMETER FailureProgramPath
        Path to a failure program or script. Optional.

    .PARAMETER FailureProgramStartupDir
        Startup directory for the failure program. Optional.

    .PARAMETER FailureProgramParams
        Additional parameters for the failure program. Optional.

    .PARAMETER Env
        Environment variables for the service process. Format: Name=Value;Name=Value. Optional.

    .PARAMETER Deps
        Windows service dependencies (by service name, not display name). Optional.

    .PARAMETER User
        Service account username (e.g., .\username or DOMAIN\username). Optional.

    .PARAMETER Password
        Password for the service account. Optional.

    .PARAMETER PreLaunchPath
        Path to a pre-launch executable or script. Optional.

    .PARAMETER PreLaunchStartupDir
        Startup directory for the pre-launch executable. Optional.

    .PARAMETER PreLaunchParams
        Additional parameters for the pre-launch executable. Optional.

    .PARAMETER PreLaunchEnv
        Environment variables for the pre-launch executable. Optional.

    .PARAMETER PreLaunchStdout
        File path for capturing pre-launch stdout. Optional.

    .PARAMETER PreLaunchStderr
        File path for capturing pre-launch stderr. Optional.

    .PARAMETER PreLaunchTimeout
        Timeout (seconds) for the pre-launch executable. Must be >= 5. Optional.

    .PARAMETER PreLaunchRetryAttempts
        Number of retry attempts for the pre-launch executable. Optional.

    .PARAMETER PreLaunchIgnoreFailure
        Switch to ignore pre-launch failure and start service anyway. Optional.

    .PARAMETER PostLaunchPath
        Path to a post-launch executable or script. Optional.

    .PARAMETER PostLaunchStartupDir
        Startup directory for the post-launch executable. Optional.

    .PARAMETER PostLaunchParams
        Additional parameters for the post-launch executable. Optional.

    .EXAMPLE
        Install-ServyService -Name "MyService" `
            -Path "C:\Services\MyService.exe" `
            -Description "Wexflow Workflow Engine" `
            -StartupDir "C:\Program Files\Wexflow Server\Wexflow.Server" `
            -Params "Wexflow.Server.dll" `
            -StartupType "Automatic" `
            -Priority "Normal" `
            -Stdout "C:\Logs\MyService.out.log" `
            -Stderr "C:\Logs\MyService.err.log" `
            -EnableRotation `
            -RotationSize 10 `
            -EnableHealth `
            -HeartbeatInterval 30 `
            -MaxFailedChecks 3 `
            -RecoveryAction RestartService `
            -MaxRestartAttempts 5
    #>
  
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string] $Name,

    [string] $Description,

    [Parameter(Mandatory = $true)]
    [string] $Path,

    [string] $StartupDir,
    [string] $Params,
    [ValidateSet("Automatic", "Manual", "Disabled")]
    [string] $StartupType,
    [ValidateSet("Idle", "BelowNormal", "Normal", "AboveNormal", "High", "RealTime")]
    [string] $Priority,
    [string] $Stdout,
    [string] $Stderr,
    [switch] $EnableRotation,
    [int] $RotationSize,
    [switch] $EnableHealth,
    [int] $HeartbeatInterval,
    [int] $MaxFailedChecks,
    [ValidateSet("None", "RestartService", "RestartProcess", "RestartComputer")]
    [string] $RecoveryAction,
    [int] $MaxRestartAttempts,
    [string] $FailureProgramPath,
    [string] $FailureProgramStartupDir,
    [string] $FailureProgramParams,
    [string] $Env,
    [string] $Deps,
    [string] $User,
    [string] $Password,

    # Pre-launch
    [string] $PreLaunchPath,
    [string] $PreLaunchStartupDir,
    [string] $PreLaunchParams,
    [string] $PreLaunchEnv,
    [string] $PreLaunchStdout,
    [string] $PreLaunchStderr,
    [int] $PreLaunchTimeout,
    [int] $PreLaunchRetryAttempts,
    [switch] $PreLaunchIgnoreFailure,

    # Post-launch
    [string] $PostLaunchPath,
    [string] $PostLaunchStartupDir,
    [string] $PostLaunchParams
  )
  
  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("install")

  if ($Quiet) { $argsList.Add("--quiet") }

  $argsList = Add-Arg $argsList "--name" $Name
  $argsList = Add-Arg $argsList "--path" $Path
  $argsList = Add-Arg $argsList "--description" $Description
  $argsList = Add-Arg $argsList "--startupDir" $StartupDir
  $argsList = Add-Arg $argsList "--params" $Params
  $argsList = Add-Arg $argsList "--startupType" $StartupType
  $argsList = Add-Arg $argsList "--priority" $Priority
  $argsList = Add-Arg $argsList "--stdout" $Stdout
  $argsList = Add-Arg $argsList "--stderr" $Stderr
  if ($EnableRotation) { $argsList.Add("--enableRotation") }
  $argsList = Add-Arg $argsList "--rotationSize" $RotationSize
  if ($EnableHealth) { $argsList.Add("--enableHealth") }
  $argsList = Add-Arg $argsList "--heartbeatInterval" $HeartbeatInterval
  $argsList = Add-Arg $argsList "--maxFailedChecks" $MaxFailedChecks
  $argsList = Add-Arg $argsList "--recoveryAction" $RecoveryAction
  $argsList = Add-Arg $argsList "--maxRestartAttempts" $MaxRestartAttempts
  $argsList = Add-Arg $argsList "--failureProgramPath" $FailureProgramPath
  $argsList = Add-Arg $argsList "--failureProgramStartupDir" $FailureProgramStartupDir
  $argsList = Add-Arg $argsList "--failureProgramParams" $FailureProgramParams
  $argsList = Add-Arg $argsList "--env" $Env
  $argsList = Add-Arg $argsList "--deps" $Deps
  $argsList = Add-Arg $argsList "--user" $User
  $argsList = Add-Arg $argsList "--password" $Password

  $argsList = Add-Arg $argsList "--preLaunchPath" $PreLaunchPath
  $argsList = Add-Arg $argsList "--preLaunchStartupDir" $PreLaunchStartupDir
  $argsList = Add-Arg $argsList "--preLaunchParams" $PreLaunchParams
  $argsList = Add-Arg $argsList "--preLaunchEnv" $PreLaunchEnv
  $argsList = Add-Arg $argsList "--preLaunchStdout" $PreLaunchStdout
  $argsList = Add-Arg $argsList "--preLaunchStderr" $PreLaunchStderr
  $argsList = Add-Arg $argsList "--preLaunchTimeout" $PreLaunchTimeout
  $argsList = Add-Arg $argsList "--preLaunchRetryAttempts" $PreLaunchRetryAttempts
  if ($PreLaunchIgnoreFailure) { $argsList.Add("--preLaunchIgnoreFailure") }

  $argsList = Add-Arg $argsList "--postLaunchPath" $PostLaunchPath
  $argsList = Add-Arg $argsList "--postLaunchStartupDir" $PostLaunchStartupDir
  $argsList = Add-Arg $argsList "--postLaunchParams" $PostLaunchParams

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to install service '$Name': $_"
    exit 1
  }
}

function Uninstall-ServyService {
  <#
    .SYNOPSIS
        Uninstalls a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `uninstall` command. 
        Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The name of the service to uninstall.

    .EXAMPLE
        Uninstall-Service -Name "MyService"
    #>
  
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string]$Name
  )
  
  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("uninstall")

  $argsList = Add-Arg $argsList "--name" $Name

  if ($Quiet) { $argsList.Add("--quiet") }
  
  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to uninstall service '$Name': $_"
    exit 1
  }
}

function Start-ServyService {
  <#
    .SYNOPSIS
        Starts a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `start` command to start a service by its name.
        Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The name of the service to start. (Required)

    .EXAMPLE
        Start-ServyService -Name "MyService"
        # Starts the service named 'MyService'.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("start")

  $argsList = Add-Arg $argsList "--name" $Name

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to start service '$Name': $_"
    exit 1
  }
}

function Stop-ServyService {
  <#
    .SYNOPSIS
        Stops a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `stop` command to stop a service by its name.
        Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The name of the service to stop. (Required)

    .EXAMPLE
        Stop-ServyService -Name "MyService"
        # Stops the service named 'MyService'.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string]$Name
  )
    
  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }
    
  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("stop")

  $argsList = Add-Arg $argsList "--name" $Name

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to stop service '$Name': $_"
    exit 1
  }
}

function Restart-ServyService {
  <#
    .SYNOPSIS
        Restarts a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `restart` command to restart a service by its name.
        Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The name of the service to restart. (Required)

    .EXAMPLE
        Restart-ServyService -Name "MyService"
        # Restarts the service named 'MyService'.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("restart")

  $argsList = Add-Arg $argsList "--name" $Name

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to restart service '$Name': $_"
    exit 1
  }
}

function Get-ServyServiceStatus {
  <#
    .SYNOPSIS
        Retrieves the current status of a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `status` command to get the status of a service by its name.
        Possible status results: Stopped, StartPending, StopPending, Running, ContinuePending, PausePending, Paused.
        Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The name of the service to check. (Required)

    .EXAMPLE
        Get-ServyServiceStatus -Name "MyService"
        # Retrieves the current status of the service named 'MyService'.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("status")

  $argsList = Add-Arg $argsList "--name" $Name

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to get status of service '$Name': $_"
    exit 1
  }
}

function Export-ServyServiceConfig {
  <#
    .SYNOPSIS
        Exports a Servy Windows service configuration to a file.

    .DESCRIPTION
        Wraps the Servy CLI `export` command to export the configuration of a service
        to a file. Supports XML and JSON file types. Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The name of the service to export. (Required)

    .PARAMETER ConfigFileType
        The export file type. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full path of the configuration file to export. (Required)

    .EXAMPLE
        Export-ServyServiceConfig -Name "MyService" -ConfigFileType "json" -Path "C:\Configs\Wexflow.json"
        # Exports the configuration of 'MyService' to a JSON file at the specified path.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [ValidateSet("xml", "json")]
    [string]$ConfigFileType,

    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("export")

  $argsList = Add-Arg $argsList "--name" $Name
  $argsList = Add-Arg $argsList "--config" $ConfigFileType
  $argsList = Add-Arg $argsList "--path" $Path

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to export configuration for service '$Name': $_"
    exit 1
  }
}

function Import-ServyServiceConfig {
  <#
    .SYNOPSIS
        Imports a Windows service configuration into Servy's database.

    .DESCRIPTION
        Wraps the Servy CLI `import` command to import a service configuration file
        (XML or JSON) into Servy's database. Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER ConfigFileType
        The configuration file type. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full path of the configuration file to import. (Required)

    .EXAMPLE
        Import-ServyServiceConfig -ConfigFileType "json" -Path "C:\Configs\Wexflow.json"
        # Imports the configuration file into Servy's database.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateSet("xml", "json")]
    [string]$ConfigFileType,

    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  try {
    Test-ServyCliPath
  }
  catch {
    Write-Error $_
    exit 1
  }

  $argsList = [System.Collections.Generic.List[string]]::new()
  $argsList.Add("import")

  $argsList = Add-Arg $argsList "--config" $ConfigFileType
  $argsList = Add-Arg $argsList "--path" $Path

  if ($Quiet) { $argsList.Add("--quiet") }

  try {
    & $script:ServyCliPath $argsList.ToArray()
  }
  catch {
    Write-Error "Failed to import configuration from '$Path': $_"
    exit 1
  }
}

# Export all public functions of the Servy module
Export-ModuleMember -Function Show-ServyVersion
Export-ModuleMember -Function Show-ServyHelp
Export-ModuleMember -Function Install-ServyService
Export-ModuleMember -Function Uninstall-ServyService
Export-ModuleMember -Function Start-ServyService
Export-ModuleMember -Function Stop-ServyService
Export-ModuleMember -Function Restart-ServyService
Export-ModuleMember -Function Get-ServyServiceStatus
Export-ModuleMember -Function Export-ServyServiceConfig
Export-ModuleMember -Function Import-ServyServiceConfig
