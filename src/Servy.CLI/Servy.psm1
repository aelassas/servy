<#
.SYNOPSIS
  Servy PowerShell module to manage Windows services using the Servy CLI.

.DESCRIPTION
  This module provides functions to install, uninstall, start, stop, restart,
  export and import configurations, and check the status of Windows services
  via the Servy CLI.

  The module works with both installed and portable versions of Servy:

  - Installed version: If Servy is installed, the module will automatically
    locate the CLI executable.

  - Portable version: If using a portable version, place this module in the
    same folder as `servy-cli.exe`. The module will automatically detect and
    use the local executable.

  Note: Most service management operations require Administrator privileges.

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
  Requires    : PowerShell 2.0 or later
  Repository  : https://github.com/aelassas/servy

.EXAMPLE
  # Display the current Servy CLI version
  Show-ServyVersion

.EXAMPLE
  # Install a new service
  Install-ServyService -Name "MyService" -Path "C:\Apps\MyApp\MyApp.exe" -StartupType "Automatic"

.EXAMPLE
  # Export a service configuration to XML
  Export-ServyServiceConfig -Name "MyService" -ConfigFileType "xml" -Path "C:\MyService.xml"
#>

# ----------------------------------------------------------------
# Module Initialization
# ----------------------------------------------------------------

# Determine module folder
if ($PSVersionTable.PSVersion.Major -ge 3) {
    # PS3+ has automatic $PSScriptRoot
    $ModuleRoot = $PSScriptRoot
} else {
    # PS2 does not have $PSScriptRoot
    $ModuleRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

# 1. Check local module folder
$script:ServyCliPath = Join-Path $ModuleRoot "servy-cli.exe"

# 2. Check 64-bit Program Files directory
if (-not (Test-Path $script:ServyCliPath)) {
    # $env:ProgramW6432 explicitly points to 'C:\Program Files' on 64-bit Windows
    # even if the current PowerShell session is 32-bit (x86).
    $basePath = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
    $script:ServyCliPath = Join-Path $basePath "Servy\servy-cli.exe"
}

# 3. Check system PATH
if (-not (Test-Path $script:ServyCliPath)) {
    $pathSearch = Get-Command "servy-cli.exe" -ErrorAction SilentlyContinue
    if ($pathSearch) { $script:ServyCliPath = $pathSearch.Definition }
}

# ----------------------------------------------------------------
# Private Helper Functions
# ----------------------------------------------------------------

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
    throw "Servy CLI not found at path: $($script:ServyCliPath)"
  }
}

function Add-Arg {
  <#
  .SYNOPSIS
      Adds a key-value argument to a list of command-line arguments.

  .DESCRIPTION
      This helper function appends a command-line argument in the form:
          key=value
      to an existing array of strings,
      but only if the value is not null or empty.

  .PARAMETER list
      The existing array of arguments to which the new argument will be added.

  .PARAMETER key
      The name of the argument or option (e.g., "--startupDir").

  .PARAMETER value
      The value associated with the argument. Only added if not null or empty.

  .OUTPUTS
      Returns the updated array of arguments including the new key-value pair.

  .EXAMPLE
      $argsList = @()
      $argsList = Add-Arg $argsList "--startupDir" "C:\MyApp"
      # Result: $argsList contains '--startupDir="C:\MyApp"'
  #>
  param(
    $list,          # Existing argument list (Array)
    [string] $key,  # Argument key
    [string] $value # Argument value
  )

  # 1. Ensure $list is an array, even if $null was passed
  if ($null -eq $list) { 
      $list = @() 
  }

  # 2. Robust check for null or empty strings
  # Note: [string]::IsNullOrWhiteSpace is not available in .NET 3.5 (PS 2.0 default)
  if ($null -ne $value -and $value.Trim() -ne "") {

      # 3. Explicitly cast to array during addition to prevent string concatenation 
      # if $list somehow became a single string.
      [array]$list += "$($key.Trim())=$value"
  }

  # 4. The unary comma (,) is essential in PS 2.0 to prevent 
  # PowerShell from "unrolling" the array into individual objects.
  return ,$list
}

<#
.SYNOPSIS
    Internal helper to execute the Servy CLI.

.DESCRIPTION
    Builds and executes a Servy CLI command with the provided arguments.
    This function centralizes CLI invocation logic, including command
    construction, quiet mode handling, and error propagation.

    It ensures the Servy CLI path is validated before execution and throws
    a terminating error with contextual information if the command fails.

.PARAMETER Command
    The Servy CLI command to execute (for example: install, uninstall, start).

.PARAMETER Arguments
    An array of additional command-line arguments to pass to the Servy CLI.

.PARAMETER Quiet
    When specified, adds the --quiet flag to suppress interactive output.

.PARAMETER ErrorContext
    A contextual error message describing the operation being performed.
    This message is included in any thrown exception.

.NOTES
    This function is intended for internal use within the Servy PowerShell
    module and is not exported.

    Compatible with PowerShell 2.0 and later.

.EXAMPLE
    Invoke-ServyCli "start" @("--name=MyService") $false "Failed to start service"

#>
function Invoke-ServyCli {
    param(
        [string] $Command,
        [array]  $Arguments,
        [switch] $Quiet,
        [string] $ErrorContext
    )

    Test-ServyCliPath

    [array]$finalArgs = @()
    if ($null -ne $Command -and $Command -ne "") { $finalArgs += $Command }
    if ($Arguments) { $finalArgs += $Arguments }
    if ($Quiet) { $finalArgs += "--quiet" }

    try {
        & $script:ServyCliPath $finalArgs   
        
        if ($LASTEXITCODE -ne 0) {
          throw "Servy CLI exited with code $LASTEXITCODE"
        }          
    }
    catch {
        throw "$($ErrorContext): $_"
    }
}

# ----------------------------------------------------------------
# Public Functions
# ----------------------------------------------------------------

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

  $invokeParams = @{
    Arguments    = @("--version")
    Quiet        = $Quiet
    ErrorContext = "Failed to get Servy CLI version"
  }

  Invoke-ServyCli @invokeParams
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

    .PARAMETER Command
        Specific command to show help for. Optional.

    .EXAMPLE
        Show-ServyHelp
        # Displays help for the Servy CLI.
    #>
  param(
    [switch] $Quiet,
    [ValidateSet("install", "uninstall", "start", "stop", "restart", "status", "export", "import")]
    [string] $Command
  )

  $invokeParams = @{
    Command      = "help"
    Arguments    = if ($Command) { @($Command) } else { @() }
    Quiet        = $Quiet
    ErrorContext = "Failed to display Servy CLI help"
  }

  Invoke-ServyCli @invokeParams
}

function Install-ServyService {
  <#
    .SYNOPSIS
        Installs a new Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `install`command to create a Windows service from any
        executable. This function allows configuring service name, description, process path,
        startup directory, parameters, startup type, process priority, logging, health monitoring,
        recovery actions, environment variables, dependencies, service account credentials,
        and optional pre-launch and post-launch executables.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER Name
        The unique name of the service to install. (Required)

    .PARAMETER DisplayName
        The display name of the service to install. Optional.
        The human-readable name shown in the Windows Services console (services.msc). 
        If left empty, the service name will be used instead. The Display Name can be changed later.

    .PARAMETER Path
        Path to the executable process to run as the service. (Required)

    .PARAMETER Description
        Optional descriptive text about the service.

    .PARAMETER StartupDir
        The startup/working directory for the service process. Optional.

    .PARAMETER Params
        Additional parameters for the service process. Optional.

    .PARAMETER StartupType
        Startup type of the service. Options: Automatic, AutomaticDelayedStart, Manual, Disabled. Optional.

    .PARAMETER Priority
        Process priority. Options: Idle, BelowNormal, Normal, AboveNormal, High, RealTime. Optional.

    .PARAMETER Stdout
        File path for capturing standard output logs. Optional.

    .PARAMETER Stderr
        File path for capturing standard error logs. Optional.

    .PARAMETER StartTimeout
        Timeout in seconds to wait for the process to start successfully before considering the startup as failed. 
        Must be >= 1 second. Optional.
        Defaults to 10 seconds.

    .PARAMETER StopTimeout
        Timeout in seconds to wait for the process to exit.
        Must be >= 1 second. Optional.
        Defaults to 5 seconds.

    .PARAMETER EnableRotation
        Deprecated. Switch to enable size-based log rotation.
        This switch is kept only for backward compatibility.
        Use -EnableSizeRotation instead.

    .PARAMETER EnableSizeRotation
        Switch to enable size-based log rotation. Optional.

    .PARAMETER RotationSize
        Maximum log file size in bytes before rotation. Must be >= 1 MB. Optional.

    .PARAMETER EnableDateRotation
        Enable date-based log rotation based on the date interval specified by -DateRotationType. Optional.
        When both size-based and date-based rotation are enabled, size rotation takes precedence.

    .PARAMETER DateRotationType
        Date rotation type. Options: Daily, Weekly, Monthly. Optional.

    .PARAMETER MaxRotations
        Maximum rotated log files to keep. 0 for unlimited. Optional.

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

    .PARAMETER EnableDebugLogs
        Switch to enable debug logs. Optional.
        When enabled, environment variables and process parameters are recorded in the Windows Event Log. 
        Not recommended for production environments, as these logs may contain sensitive information.
        
    .EXAMPLE
        Install-ServyService -Name "MyService" `
            -Path "C:\Apps\MyApp\MyApp.exe" `
            -Description "My Service" `
            -StartupDir "C:\Apps\MyApp" `
            -Params "--port 8000" `
            -StartupType "Automatic" `
            -Priority "Normal" `
            -Stdout "C:\Logs\MyService.out.log" `
            -Stderr "C:\Logs\MyService.err.log" `
            -EnableRotation `
            -RotationSize 10 `
            -MaxRotations 0 `
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

    [string] $DisplayName,

    [string] $Description,

    [Parameter(Mandatory = $true)]
    [string] $Path,

    [string] $StartupDir,
    [string] $Params,
    [ValidateSet("Automatic", "AutomaticDelayedStart", "Manual", "Disabled")]
    [string] $StartupType,
    [ValidateSet("Idle", "BelowNormal", "Normal", "AboveNormal", "High", "RealTime")]
    [string] $Priority,
    [string] $Stdout,
    [string] $Stderr,
    [string] $StartTimeout,
    [string] $StopTimeout,
    [switch] $EnableRotation,
    [switch] $EnableSizeRotation,
    [string] $RotationSize,
    [switch] $EnableDateRotation,
    [ValidateSet("Daily", "Weekly", "Monthly")]
    [string] $DateRotationType,
    [string] $MaxRotations,
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
    [switch] $PreLaunchIgnoreFailure,

    # Post-launch
    [string] $PostLaunchPath,
    [string] $PostLaunchStartupDir,
    [string] $PostLaunchParams,

    # Debug Logs
    [switch] $EnableDebugLogs
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name
  $argsList = Add-Arg $argsList "--displayName" $DisplayName
  $argsList = Add-Arg $argsList "--path" $Path
  $argsList = Add-Arg $argsList "--description" $Description
  $argsList = Add-Arg $argsList "--startupDir" $StartupDir
  $argsList = Add-Arg $argsList "--params" $Params
  $argsList = Add-Arg $argsList "--startupType" $StartupType
  $argsList = Add-Arg $argsList "--priority" $Priority
  $argsList = Add-Arg $argsList "--stdout" $Stdout
  $argsList = Add-Arg $argsList "--stderr" $Stderr
  $argsList = Add-Arg $argsList "--startTimeout" $StartTimeout
  $argsList = Add-Arg $argsList "--stopTimeout" $StopTimeout
  if ($EnableRotation) { Write-Warning "-EnableRotation is deprecated. Use -EnableSizeRotation instead." }
  if ($EnableRotation -or $EnableSizeRotation) { $argsList += "--enableSizeRotation" }
  $argsList = Add-Arg $argsList "--rotationSize" $RotationSize
  if ($EnableDateRotation) { $argsList += "--enableDateRotation" }
  $argsList = Add-Arg $argsList "--dateRotationType" $DateRotationType
  $argsList = Add-Arg $argsList "--maxRotations" $MaxRotations
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

  $argsList = Add-Arg $argsList "--postLaunchPath" $PostLaunchPath
  $argsList = Add-Arg $argsList "--postLaunchStartupDir" $PostLaunchStartupDir
  $argsList = Add-Arg $argsList "--postLaunchParams" $PostLaunchParams

  if ($EnableDebugLogs) { $argsList += "--debug" }

  $invokeParams = @{
    Command      = "install"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to install service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Uninstall-ServyService {
  <#
    .SYNOPSIS
        Uninstalls a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `uninstall`command. 
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
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name
  
  $invokeParams = @{
    Command      = "uninstall"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to uninstall service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Start-ServyService {
  <#
    .SYNOPSIS
        Starts a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `start`command to start a service by its name.
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
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  $invokeParams = @{
    Command      = "start"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to start service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Stop-ServyService {
  <#
    .SYNOPSIS
        Stops a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `stop`command to stop a service by its name.
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
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  $invokeParams = @{
    Command      = "stop"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to stop service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Restart-ServyService {
  <#
    .SYNOPSIS
        Restarts a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `restart`command to restart a service by its name.
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
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  $invokeParams = @{
    Command      = "restart"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to restart service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Get-ServyServiceStatus {
  <#
    .SYNOPSIS
        Retrieves the current status of a Windows service using Servy.

    .DESCRIPTION
        Wraps the Servy CLI `status`command to get the status of a service by its name.
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
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  $invokeParams = @{
    Command      = "status"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to get status of service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Export-ServyServiceConfig {
  <#
    .SYNOPSIS
        Exports a Servy Windows service configuration to a file.

    .DESCRIPTION
        Wraps the Servy CLI `export`command to export the configuration of a service
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
        Export-ServyServiceConfig -Name "MyService" -ConfigFileType "json" -Path "C:\Configs\MyService.json"
        # Exports the configuration of 'MyService' to a JSON file at the specified path.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [ValidateSet("xml", "json")]
    [string] $ConfigFileType,

    [Parameter(Mandatory = $true)]
    [string] $Path
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name
  $argsList = Add-Arg $argsList "--config" $ConfigFileType
  $argsList = Add-Arg $argsList "--path" $Path

  $invokeParams = @{
    Command      = "export"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to export configuration for service '$Name'"
  }

  Invoke-ServyCli @invokeParams
}

function Import-ServyServiceConfig {
  <#
    .SYNOPSIS
        Imports a Windows service configuration into Servy's database.

    .DESCRIPTION
        Wraps the Servy CLI `import`command to import a service configuration file
        (XML or JSON) into Servy's database. Requires Administrator privileges.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .PARAMETER ConfigFileType
        The configuration file type. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full path of the configuration file to import. (Required)

    .PARAMETER Install
        Install the service after import. If the service is already installed, restarting it is required for changes to take effect.
        Optional.

    .EXAMPLE
        Import-ServyServiceConfig -ConfigFileType "json" -Path "C:\Configs\MyService.json" -Install
        # Imports the configuration file into Servy's database.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateSet("xml", "json")]
    [string] $ConfigFileType,

    [Parameter(Mandatory = $true)]
    [string] $Path,
    [switch] $Install
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--config" $ConfigFileType
  $argsList = Add-Arg $argsList "--path" $Path
  if ($Install) { $argsList += "--install" }

  $invokeParams = @{
    Command      = "import"
    Arguments    = $argsList
    Quiet        = $Quiet
    ErrorContext = "Failed to import configuration from '$Path'"
  }

  Invoke-ServyCli @invokeParams
}

# ----------------------------------------------------------------
# Export all public functions of the Servy module
# ----------------------------------------------------------------

$publicFunctions = @(
    "Show-ServyVersion",
    "Show-ServyHelp",
    "Install-ServyService",
    "Uninstall-ServyService",
    "Start-ServyService",
    "Stop-ServyService",
    "Restart-ServyService",
    "Get-ServyServiceStatus",
    "Export-ServyServiceConfig",
    "Import-ServyServiceConfig"
)

foreach ($fn in $publicFunctions) {
    if (-not (Get-Command $fn -CommandType Function -ErrorAction SilentlyContinue)) {
        throw "Public function '$fn' is missing"
    }
}

Export-ModuleMember -Function $publicFunctions
