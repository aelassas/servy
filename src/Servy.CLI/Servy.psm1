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
  Install-ServyService -Name "WexflowServer" -Path "C:\Program Files\dotnet\dotnet.exe" -StartupType "Automatic"

.EXAMPLE
  # Export a service configuration to XML
  Export-ServyServiceConfig -Name "WexflowServer" -ConfigFileType "xml" -Path "C:\WexflowServer.xml"
#>


$script:ServyCliPath = "C:\Program Files\Servy\servy-cli.exe"

function Show-ServyVersion {
    <#
    .SYNOPSIS
        Displays the version of the Servy CLI.

    .DESCRIPTION
        Wraps the Servy CLI `--version` command to show the current version
        of the Servy tool installed on the system.

    .EXAMPLE
        Show-ServyVersion
        # Displays the current version of Servy CLI.
    #>
    
    try {
        & $script:ServyCliPath "--version"
    }
    catch {
        Write-Error "Failed to get Servy CLI version: $_"
    }
}

function Show-ServyHelp {
    <#
    .SYNOPSIS
        Displays help information for the Servy CLI.

    .DESCRIPTION
        Wraps the Servy CLI `help` command to show usage information
        and details about all available commands and options.

    .EXAMPLE
        Show-ServyHelp
        # Displays help for the Servy CLI.
    #>
    
    try {
        & $script:ServyCliPath "help"
    }
    catch {
        Write-Error "Failed to display Servy CLI help: $_"
    }
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
        and optional pre-launch executables.

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

    .EXAMPLE
        Install-ServyService -Name "WexflowServer" `
            -Path "C:\Program Files\dotnet\dotnet.exe" `
            -Description "Wexflow Workflow Engine" `
            -StartupDir "C:\Program Files\Wexflow Server\Wexflow.Server" `
            -Params "Wexflow.Server.dll" `
            -StartupType "Automatic" `
            -Priority "Normal" `
            -Stdout "C:\Logs\WexflowServer.out.log" `
            -Stderr "C:\Logs\WexflowServer.err.log" `
            -EnableRotation `
            -RotationSize 10 `
            -EnableHealth `
            -HeartbeatInterval 30 `
            -MaxFailedChecks 3 `
            -RecoveryAction RestartService `
            -MaxRestartAttempts 5
    #>
  
  param(
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
    [string] $RotationSize,
    [switch] $EnableHealth,
    [string] $HeartbeatInterval,
    [string] $MaxFailedChecks,
    [ValidateSet("None", "RestartService", "RestartProcess", "RestartComputer")]
    [string] $RecoveryAction,
    [string] $MaxRestartAttempts,
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
    [string] $PreLaunchTimeout,
    [string] $PreLaunchRetryAttempts,
    [switch] $PreLaunchIgnoreFailure
  )

  function Add-Arg {
    param($list, $key, $value)
    if ($null -ne $value -and $value -ne "") {
      $list += @($key, "`"$value`"")
    }
    return $list
  }

  $argsList = @("install", "-n", "`"$Name`"", "-p", "`"$Path`"")

  $argsList = Add-Arg $argsList "--description" $Description
  $argsList = Add-Arg $argsList "--startupDir" $StartupDir
  $argsList = Add-Arg $argsList "--params" $Params
  $argsList = Add-Arg $argsList "--startupType" $StartupType
  $argsList = Add-Arg $argsList "--priority" $Priority
  $argsList = Add-Arg $argsList "--stdout" $Stdout
  $argsList = Add-Arg $argsList "--stderr" $Stderr
  if ($EnableRotation) { $argsList += "--enableRotation" }
  $argsList = Add-Arg $argsList "--rotationSize" $RotationSize
  if ($EnableHealth) { $argsList += "--enableHealth" }
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
  if ($PreLaunchIgnoreFailure) { $argsList += "--preLaunchIgnoreFailure" }

  try {
    & $script:ServyCliPath @argsList
  }
  catch {
    Write-Error "Failed to install service '$Name': $_"
  }
}

function Uninstall-ServyService {
  <#
    .SYNOPSIS
        Uninstalls a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `uninstall` command. 
        Requires Administrator privileges.

    .PARAMETER Name
        The name of the service to uninstall.

    .EXAMPLE
        Uninstall-Service -Name "WexflowServer"
    #>
  
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  $arguments = @(
    "uninstall"
    "-n", "`"$Name`""
  )

  try {
    & $script:ServyCliPath @arguments
  }
  catch {
    Write-Error "Failed to uninstall service '$Name': $_"
  }
}

function Start-ServyService {
  <#
    .SYNOPSIS
        Starts a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `start` command to start a service by its name.
        Requires Administrator privileges.

    .PARAMETER Name
        The name of the service to start. (Required)

    .EXAMPLE
        Start-ServyService -Name "WexflowServer"
        # Starts the service named 'WexflowServer'.
    #>
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  $arguments = @(
    "start"
    "-n", "`"$Name`""
  )

  try {
    & $script:ServyCliPath @arguments
  }
  catch {
    Write-Error "Failed to start service '$Name': $_"
  }
}

function Stop-ServyService {
    <#
    .SYNOPSIS
        Stops a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `stop` command to stop a service by its name.
        Requires Administrator privileges.

    .PARAMETER Name
        The name of the service to stop. (Required)

    .EXAMPLE
        Stop-ServyService -Name "WexflowServer"
        # Stops the service named 'WexflowServer'.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $arguments = @(
        "stop"
        "-n", "`"$Name`""
    )

    try {
        & $script:ServyCliPath @arguments
    }
    catch {
        Write-Error "Failed to stop service '$Name': $_"
    }
}

function Restart-ServyService {
    <#
    .SYNOPSIS
        Restarts a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `restart` command to restart a service by its name.
        Requires Administrator privileges.

    .PARAMETER Name
        The name of the service to restart. (Required)

    .EXAMPLE
        Restart-ServyService -Name "WexflowServer"
        # Restarts the service named 'WexflowServer'.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $arguments = @(
        "restart"
        "-n", "`"$Name`""
    )

    try {
        & $script:ServyCliPath @arguments
    }
    catch {
        Write-Error "Failed to restart service '$Name': $_"
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

    .PARAMETER Name
        The name of the service to check. (Required)

    .EXAMPLE
        Get-ServyServiceStatus -Name "WexflowServer"
        # Retrieves the current status of the service named 'WexflowServer'.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $arguments = @(
        "status"
        "-n", "`"$Name`""
    )

    try {
        & $script:ServyCliPath @arguments
    }
    catch {
        Write-Error "Failed to get status of service '$Name': $_"
    }
}

function Export-ServyServiceConfig {
    <#
    .SYNOPSIS
        Exports a Servy Windows service configuration to a file.

    .DESCRIPTION
        Wraps the Servy CLI `export` command to export the configuration of a service
        to a file. Supports XML and JSON file types. Requires Administrator privileges.

    .PARAMETER Name
        The name of the service to export. (Required)

    .PARAMETER ConfigFileType
        The export file type. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full path of the configuration file to export. (Required)

    .EXAMPLE
        Export-ServyServiceConfig -Name "WexflowServer" -ConfigFileType "json" -Path "C:\Configs\Wexflow.json"
        # Exports the configuration of 'WexflowServer' to a JSON file at the specified path.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [ValidateSet("xml","json")]
        [string]$ConfigFileType,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $arguments = @(
        "export"
        "-n", "`"$Name`""
        "-c", "`"$ConfigFileType`""
        "-p", "`"$Path`""
    )

    try {
        & $script:ServyCliPath @arguments
    }
    catch {
        Write-Error "Failed to export configuration for service '$Name': $_"
    }
}

function Import-ServyServiceConfig {
    <#
    .SYNOPSIS
        Imports a Windows service configuration into Servy's database.

    .DESCRIPTION
        Wraps the Servy CLI `import` command to import a service configuration file
        (XML or JSON) into Servy's database. Requires Administrator privileges.

    .PARAMETER ConfigFileType
        The configuration file type. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full path of the configuration file to import. (Required)

    .EXAMPLE
        Import-ServyServiceConfig -ConfigFileType "json" -Path "C:\Configs\Wexflow.json"
        # Imports the configuration file into Servy's database.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("xml","json")]
        [string]$ConfigFileType,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $arguments = @(
        "import"
        "-c", "`"$ConfigFileType`""
        "-p", "`"$Path`""
    )

    try {
        & $script:ServyCliPath @arguments
    }
    catch {
        Write-Error "Failed to import configuration from '$Path': $_"
    }
}

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
