<#
.SYNOPSIS
  Servy PowerShell module to manage Windows services using the Servy CLI.

.DESCRIPTION
  PowerShell module to manage Windows services using the Servy CLI.
  See the module manifest (Servy.psd1) for full description.

.NOTES
  Author      : Akram El Assas
  Module Name : Servy
  Requires    : PowerShell 2.0 or later
  Repository  : https://github.com/aelassas/servy

.EXAMPLE
  See servy-module-examples.ps1 for complete usage examples.
#>

# ----------------------------------------------------------------
# Execution Settings
# ----------------------------------------------------------------
# Maximum time (in seconds) to wait for a CLI command to complete.
# This prevents the script from hanging indefinitely if the CLI blocks 
# on I/O or network calls. Default is 10 minutes.
$script:ServyTimeoutSeconds = 600

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
# $env:ProgramW6432 explicitly points to 'C:\Program Files' on 64-bit Windows
# even if the current PowerShell session is 32-bit (x86).
$script:ServyProgramFilesPath = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
if (-not (Test-Path $script:ServyCliPath)) {
  $script:ServyCliPath = Join-Path $script:ServyProgramFilesPath "Servy\servy-cli.exe"
}

# 3. Check system PATH
if (-not (Test-Path $script:ServyCliPath)) {
  $pathSearch = Get-Command "servy-cli.exe" -CommandType Application -ErrorAction SilentlyContinue
  if ($pathSearch -and $pathSearch.Definition -and (Test-Path $pathSearch.Definition)) {
    $script:ServyCliPath = $pathSearch.Definition
  }
}

if ($null -eq $script:ServyCliPath -or -not (Test-Path $script:ServyCliPath)) {
  throw @"
Servy CLI ('servy-cli.exe') was not found. Searched locations:
1. Local module folder: $ModuleRoot
2. Program Files: $($script:ServyProgramFilesPath)\Servy
3. System PATH
Please ensure Servy is installed or the CLI executable is in the module directory.
"@.Trim()
}

# ----------------------------------------------------------------
# Private Helper Functions
# ----------------------------------------------------------------

function Add-Arg {
  <#
  .SYNOPSIS
      Adds a key-value argument or a standalone flag to a list of command-line arguments.

  .DESCRIPTION
      This helper function appends a command-line argument in the form:
          key="value"
      to an existing array of strings if a value is provided and not empty.
      If the -Flag switch is used, it simply appends the key as a standalone argument:
          key

  .PARAMETER list
      The existing array of arguments to which the new argument will be added.

  .PARAMETER key
      The name of the argument or option (e.g., "--startupDir" or "--enableHealth").

  .PARAMETER value
      The value associated with the argument. Only added if not null or empty and -Flag is not specified.

  .PARAMETER Flag
      Switch indicating that this argument is a standalone flag without a value.

  .OUTPUTS
      Returns the updated array of arguments including the new argument.

  .EXAMPLE
      $argsList = @()
      $argsList = Add-Arg $argsList "--startupDir" "C:\MyApp"
      $argsList = Add-Arg $argsList "--enableHealth" -Flag
      # Result: $argsList contains '--startupDir="C:\MyApp"' and '--enableHealth'
  #>
  param(
    $list,          # Existing argument list (Array)
    [string] $key,  # Argument key
    [string] $value,# Argument value
    [switch] $Flag  # Indicates a flag without a value
  )

  # 1. If it's a flag, simply append the key
  if ($Flag) {
    # If it's a flag, simply append the key
    [array]$list += $key.Trim()
  }

  # 2. Robust check for null or empty strings
  # Note: [string]::IsNullOrWhiteSpace is not available in .NET 3.5 (PS 2.0 default)
  elseif ($null -ne $value -and $value.Trim() -ne "") {

    # For Windows command-line parsing via ProcessStartInfo.Arguments:
    # Escape internal double quotes with backslashes (Windows convention).
    # This is DIFFERENT from PowerShell's "" escaping.
    $escapedValue = $value.Replace('"', '\"')

    # Double trailing backslashes so they don't escape the closing quote
    if ($escapedValue.EndsWith('\')) {
        $escapedValue += '\'
    }

    # 3. Explicitly cast to array during addition to prevent string concatenation 
    # if $list somehow became a single string.
    [array]$list += "$($key.Trim())=`"$escapedValue`""
  }

  # 4. Array integrity is preserved by the assignment context at all call sites.
  # No unary comma needed.
  return $list
}

function Invoke-ServyCli {
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
  param(
    [string] $Command,
    [array]  $Arguments,
    [switch] $Quiet,
    [string] $ErrorContext
  )

  # Build argument list
  $finalArgs = @()
  if ($Command)   { $finalArgs += $Command }
  if ($Arguments) { $finalArgs += $Arguments }
  if ($Quiet)     { $finalArgs += "--quiet" }

  # Convert array to space-separated string to bypass PS argument mangling
  $argString = $finalArgs -join ' '
  $process = $null
  $stdout = $null
  $stderr = $null
  $exitCode = 0  # Initialize a variable to hold the exit code

  try {
    # Using .NET Process class is the most robust way in PS 2.0 to pass 
    # complex raw argument strings WHILE retaining pipeline output capture.  
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $script:ServyCliPath
    $psi.Arguments = $argString
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    
    # ASYNCHRONOUS: Prevent deadlock by reading stderr asynchronously
    # We use a script-scoped array to collect lines because PS 2.0 events 
    # run in a separate scope.
    # Generate unique variable name
    $errorVarName = "ServyError_" + [Guid]::NewGuid().ToString("N")
    New-Variable -Name $errorVarName -Value (New-Object System.Collections.ArrayList) -Scope Global

    # REGISTER EVENT NATIVELY
    # Register-ObjectEvent is the "official" PS 2.0 way to handle .NET events safely.
    $errorEvent = Register-ObjectEvent -InputObject $process `
        -EventName "ErrorDataReceived" `
        -Action ([ScriptBlock]::Create(@"
            if (`$EventArgs.Data) { 
                [void]`$global:$errorVarName.Add(`$EventArgs.Data) 
            }
"@))

    $started = $process.Start()

    if (-not $started) {
      throw "Failed to start Servy CLI process '$($script:ServyCliPath)'. " +
            "Verify the file exists, is not locked, and the current user has execute permissions."
    }

    # BEGIN ASYNC READ
    $process.BeginErrorReadLine()

    # Read stdout synchronously
    try { $stdout = $process.StandardOutput.ReadToEnd() } catch { }

    # Start the wait with the defined timeout
    $hasExited = $process.WaitForExit($script:ServyTimeoutSeconds * 1000)

    if (-not $hasExited) {
        # Handle Timeout: The process is still running!
        # We must manually kill it to prevent orphaned processes.
        try { $process.Kill() } catch { }

        throw "$($ErrorContext): Operation timed out after $($script:ServyTimeoutSeconds) seconds and was terminated."
    }

    # COLLECT stderr
    # Convert our collected array back into a string
    $stderr = (Get-Variable -Name $errorVarName -Scope Global -ValueOnly) -join [Environment]::NewLine

    # CRITICAL: Capture the exit code while the process object is still active
    $exitCode = $process.ExitCode

    if (-not [string]::IsNullOrEmpty($stdout)) { 
      Write-Output $stdout.TrimEnd() 
    }
  }
  catch {
    # Ensure we still try to get whatever stderr we have if a crash occurs
    try { 
        $list = Get-Variable -Name $errorVarName -Scope Global -ValueOnly -ErrorAction SilentlyContinue
        if ($list) { $stderr = $list -join [Environment]::NewLine }
    } catch {}

    $partialOutput = ""
    if (-not [string]::IsNullOrEmpty($stdout)) { $partialOutput += " Stdout: $($stdout.TrimEnd())" }
    if (-not [string]::IsNullOrEmpty($stderr)) { $partialOutput += " Stderr: $($stderr.TrimEnd())" }
    throw "$($ErrorContext): $_$partialOutput"
  }
  finally {
    # CRITICAL: Clean up events and global variables even if the code fails
    if ($errorEvent) {
      Unregister-Event -SourceIdentifier $errorEvent.Name -ErrorAction SilentlyContinue
    }
    Remove-Variable -Name $errorVarName -Scope Global -ErrorAction SilentlyContinue

    if ($null -ne $process) {
      $process.Dispose()
    }
  }

  # Use the locally captured $exitCode variable instead of the $process object
  if ($exitCode -ne 0) {
    $errorMessage = if (-not [string]::IsNullOrEmpty($stderr)) { $stderr.TrimEnd() } else { "Unknown error" }
    throw "$($ErrorContext): Servy CLI exited with code $exitCode. Details: $errorMessage"
  }
}

function Invoke-ServyServiceCommand {
  <#
  .SYNOPSIS
      Executes a specific service management command via the Servy CLI.

  .DESCRIPTION
      Wraps the Servy CLI to perform actions such as start, stop, or restart on a 
      specified service. It handles argument construction and provides context 
      for error reporting.

  .PARAMETER Command
      The service command to execute (e.g., 'start', 'stop', 'restart').

  .PARAMETER Name
      The unique name of the service to target.

  .PARAMETER Quiet
      If set, suppresses non-essential output from the CLI.

  .EXAMPLE
      Invoke-ServyServiceCommand -Command "start" -Name "Wexflow" -Quiet
      Starts the 'Wexflow' service silently.
  #>
    [cmdletbinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string] $Command,

        [Parameter(Mandatory=$true)]
        [string] $Name,

        [switch] $Quiet
    )

    $argsList = @()
    # Assuming Add-Arg is a helper that appends '--name' and the service name
    $argsList = Add-Arg $argsList "--name" $Name

    # FIX: Changed -Command Command to -Command $Command
    Invoke-ServyCli -Command $Command -Quiet:$Quiet -Arguments $argsList -ErrorContext "Failed to $Command service '$Name'"
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

  Invoke-ServyCli -Command "--version" -Quiet:$Quiet -ErrorContext "Failed to get Servy CLI version"
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

    .EXAMPLE
        Show-ServyHelp -Command "install"
        # Displays help for the install command.        
    #>
  param(
    [switch] $Quiet,
    [ValidateSet("install", "uninstall", "start", "stop", "restart", "status", "export", "import")]
    [string] $Command
  )

  $argsList = @()
  if ($Command) { $argsList = Add-Arg $argsList "--help" -Flag }

  Invoke-ServyCli -Command $Command -Arguments $argsList -Quiet:$Quiet -ErrorContext "Failed to display Servy CLI help"
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

        The Post-launch executable operates in a fire-and-forget mode , meaning it does not support 
        the full range of configuration options such as stdout/stderr redirection or retry attempts 
        that are available for the Pre-launch executable.

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
        Maximum log file size in Megabytes (MB) before rotation. Must be >= 1 MB. Optional.

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
        Timeout (seconds) for the pre-launch executable. Must be >= 0. 
        Set the timeout to 0 to run the pre-launch hook in fire-and-forget mode. When set to 0, 
        the hook is started and the service is launched immediately without waiting for completion. 
        Use this only for tasks that do not affect the service's ability to start or run correctly.
        Stdout/Stderr redirection and retries are not available in fire-and-forget mode.
        Optional.

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
        
    .PARAMETER PreStopPath
        Path to a pre-stop executable or script. Optional.

    .PARAMETER PreStopStartupDir
        Startup directory for the pre-stop executable. Optional.

    .PARAMETER PreStopParams
        Additional parameters for the pre-stop executable. Optional.

    .PARAMETER PreStopTimeout
        Timeout (seconds) for the pre-stop executable. Must be >= 0. Optional.        
        Set to 0 for fire and forget.

    .PARAMETER PreStopLogAsError
        Switch to treat pre-stop failures as error. Optional.

    .PARAMETER PostStopPath
        Path to a post-stop executable or script. Optional.

    .PARAMETER PostStopStartupDir
        Startup directory for the post-stop executable. Optional.

    .PARAMETER PostStopParams
        Additional parameters for the post-stop executable. Optional.

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
    [ValidateNotNullOrEmpty()]
    [string] $Name,

    [string] $DisplayName,

    [string] $Description,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Path,

    [string] $StartupDir,
    [string] $Params,
    [ValidateSet("Automatic", "AutomaticDelayedStart", "Manual", "Disabled")]
    [string] $StartupType,
    [ValidateSet("Idle", "BelowNormal", "Normal", "AboveNormal", "High", "RealTime")]
    [string] $Priority,
    [string] $Stdout,
    [string] $Stderr,
    [ValidateRange(1, 2147483647)]
    [int] $StartTimeout,
    [ValidateRange(1, 2147483647)]
    [int] $StopTimeout,
    [switch] $EnableRotation,
    [switch] $EnableSizeRotation,
    [ValidateRange(1, 2147483647)]
    [int] $RotationSize,
    [switch] $EnableDateRotation,
    [ValidateSet("Daily", "Weekly", "Monthly")]
    [string] $DateRotationType,
    [ValidateRange(0, 2147483647)]
    [int] $MaxRotations,
    [switch] $EnableHealth,
    [ValidateRange(5, 2147483647)]
    [int] $HeartbeatInterval,
    [ValidateRange(1, 2147483647)]
    [int] $MaxFailedChecks,
    [ValidateSet("None", "RestartService", "RestartProcess", "RestartComputer")]
    [string] $RecoveryAction,
    [ValidateRange(1, 2147483647)]
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
    [ValidateRange(0, 2147483647)]
    [int] $PreLaunchTimeout,
    [string] $PreLaunchRetryAttempts,
    [switch] $PreLaunchIgnoreFailure,

    # Post-launch
    [string] $PostLaunchPath,
    [string] $PostLaunchStartupDir,
    [string] $PostLaunchParams,

    # Debug Logs
    [switch] $EnableDebugLogs,

    # Pre-stop
    [string] $PreStopPath,
    [string] $PreStopStartupDir,
    [string] $PreStopParams,
    [ValidateRange(0, 2147483647)]
    [int] $PreStopTimeout,
    [switch] $PreStopLogAsError,

    # Post-stop
    [string] $PostStopPath,
    [string] $PostStopStartupDir,
    [string] $PostStopParams

  )

  $argsList = @()

  # 1. Define parameter pairs for PS 2.0 compatibility
  $paramPairs = @(
    @("--name",                     $Name),
    @("--displayName",              $DisplayName),
    @("--path",                     $Path),
    @("--description",              $Description),
    @("--startupDir",               $StartupDir),
    @("--params",                   $Params),
    @("--startupType",              $StartupType),
    @("--priority",                 $Priority),
    @("--stdout",                   $Stdout),
    @("--stderr",                   $Stderr),
    @("--startTimeout",             $StartTimeout),
    @("--stopTimeout",              $StopTimeout),
    @("--rotationSize",             $RotationSize),
    @("--dateRotationType",         $DateRotationType),
    @("--maxRotations",             $MaxRotations),
    @("--heartbeatInterval",        $HeartbeatInterval),
    @("--maxFailedChecks",          $MaxFailedChecks),
    @("--recoveryAction",           $RecoveryAction),
    @("--maxRestartAttempts",       $MaxRestartAttempts),
    @("--failureProgramPath",       $FailureProgramPath),
    @("--failureProgramStartupDir", $FailureProgramStartupDir),
    @("--failureProgramParams",     $FailureProgramParams),
    @("--env",                      $Env),
    @("--deps",                     $Deps),
    @("--user",                     $User),
    @("--password",                 $Password),
    @("--preLaunchPath",            $PreLaunchPath),
    @("--preLaunchStartupDir",      $PreLaunchStartupDir),
    @("--preLaunchParams",          $PreLaunchParams),
    @("--preLaunchEnv",             $PreLaunchEnv),
    @("--preLaunchStdout",          $PreLaunchStdout),
    @("--preLaunchStderr",          $PreLaunchStderr),
    @("--preLaunchTimeout",         $PreLaunchTimeout),
    @("--preLaunchRetryAttempts",   $PreLaunchRetryAttempts),
    @("--postLaunchPath",           $PostLaunchPath),
    @("--postLaunchStartupDir",     $PostLaunchStartupDir),
    @("--postLaunchParams",         $PostLaunchParams),
    @("--preStopPath",              $PreStopPath),
    @("--preStopStartupDir",        $PreStopStartupDir),
    @("--preStopParams",            $PreStopParams),
    @("--preStopTimeout",           $PreStopTimeout),
    @("--postStopPath",             $PostStopPath),
    @("--postStopStartupDir",       $PostStopStartupDir),
    @("--postStopParams",           $PostStopParams)
  )

  # 2. Iterate through pairs to build arguments
  foreach ($pair in $paramPairs) {
    $argsList = Add-Arg $argsList $pair[0] $pair[1]
  }

  # 3. Handle switch/flag parameters separately
  if ($EnableRotation) { Write-Warning "-EnableRotation is deprecated. Use -EnableSizeRotation instead." }
  if ($EnableRotation -or $EnableSizeRotation) { $argsList = Add-Arg $argsList "--enableSizeRotation" -Flag }
  if ($EnableDateRotation)                       { $argsList = Add-Arg $argsList "--enableDateRotation" -Flag }
  if ($EnableHealth)                             { $argsList = Add-Arg $argsList "--enableHealth" -Flag }
  if ($PreLaunchIgnoreFailure)                   { $argsList = Add-Arg $argsList "--preLaunchIgnoreFailure" -Flag }
  if ($EnableDebugLogs)                          { $argsList = Add-Arg $argsList "--debug" -Flag }
  if ($PreStopLogAsError)                        { $argsList = Add-Arg $argsList "--preStopLogAsError" -Flag }

  # 4. Invoke CLI
  Invoke-ServyCli -Command "install" -Arguments $argsList -Quiet:$Quiet -ErrorContext "Failed to install service '$Name'"
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
        Uninstall-ServyService -Name "MyService"
    #>
  
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name
  
  Invoke-ServyServiceCommand -Command "uninstall" -Name $Name -Quiet:$Quiet
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
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  Invoke-ServyServiceCommand -Command "start" -Name $Name -Quiet:$Quiet
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
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  Invoke-ServyServiceCommand -Command "stop" -Name $Name -Quiet:$Quiet
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
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  Invoke-ServyServiceCommand -Command "restart" -Name $Name -Quiet:$Quiet
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
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  Invoke-ServyServiceCommand -Command "status" -Name $Name -Quiet:$Quiet
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
        Export-ServyServiceConfig -Name "MyService" -ConfigFileType "json" -Path "C:\Configs\MyService.json"
        # Exports the configuration of 'MyService' to a JSON file at the specified path.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [ValidateSet("xml", "json")]
    [string] $ConfigFileType,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Path
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name
  $argsList = Add-Arg $argsList "--config" $ConfigFileType
  $argsList = Add-Arg $argsList "--path" $Path

  Invoke-ServyCli -Command "export" -Arguments $argsList -Quiet:$Quiet -ErrorContext "Failed to export configuration for service '$Name'"
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

    .PARAMETER Install
        Install the service after import. If the service is already installed, restarting it is required for changes to take effect.
        Optional.

    .EXAMPLE
        Import-ServyServiceConfig -ConfigFileType "json" -Path "C:\Configs\MyService.json" -Install
        # Imports the configuration file into Servy's database.
    
    .NOTES
        The service name is read from the configuration file during import.
        No -Name parameter is needed.
    #>
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateSet("xml", "json")]
    [string] $ConfigFileType,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Path,
    [switch] $Install
  )

  $argsList = @()
  $argsList = Add-Arg $argsList "--config" $ConfigFileType
  $argsList = Add-Arg $argsList "--path" $Path
  if ($Install) { $argsList = Add-Arg $argsList "--install" -Flag }

  Invoke-ServyCli -Command "import" -Arguments $argsList -Quiet:$Quiet -ErrorContext "Failed to import configuration from '$Path'"
}
