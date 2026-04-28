#Requires -Version 2.0
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

# Define a reasonable cap for CLI output (1MB limit) to prevent OOM
$script:ServyMaxBufferChars = 1048576

# ----------------------------------------------------------------
# Module Initialization
# ----------------------------------------------------------------

# Determine module folder
if ($PSVersionTable.PSVersion.Major -ge 3) {
  # PS3+ has automatic $PSScriptRoot
  $ModuleRoot = $PSScriptRoot
}
else {
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
  if ($pathSearch -and (Test-Path $pathSearch.Definition)) {
    $script:ServyCliPath = $pathSearch.Definition
  }
}

$script:ServyCliFound = Test-Path $script:ServyCliPath

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
  [CmdletBinding()]
  param(
    [System.Collections.ArrayList] $list, # Performance: Use ArrayList to avoid O(n^2) array copying
    [string] $key,  # Argument key
    [string] $value,# Argument value
    [switch] $Flag  # Indicates a flag without a value
  )

  $key = $key.Trim()

  if ($Flag) {
    [void]$list.Add($key)
    return $list # CRITICAL: Exit immediately to prevent fall-through
  }

  # Note: [string]::IsNullOrWhiteSpace is not available in .NET 3.5 (PS 2.0 default)
  elseif ($null -ne $value -and $value.Trim() -ne "") {
    # Fast path: no escaping needed if no special characters
    if ($value.IndexOf('"') -lt 0 -and $value.IndexOf('\') -lt 0) {
      [void]$list.Add("$($key)=`"$value`"")
      return $list
    }

    # Escape backslashes before quotes (must be done BEFORE escaping quotes)
    $escapedValue = $value -replace '(\\+)"', '$1$1\"'
    # Then escape any remaining standalone quotes
    $escapedValue = $escapedValue -replace '(?<!\\)"', '\"'
    # Double trailing backslashes
    $escapedValue = $escapedValue -replace '(\\+)$', '$1$1'

    [void]$list.Add("$($key)=`"$escapedValue`"")
  }

  return $list
}

function Format-SecureLogMessage {
  <#
    .SYNOPSIS
        Masks sensitive command-line arguments in log text.

    .DESCRIPTION
        This function parses raw stderr or stdout strings from the Servy CLI and scrubs 
        the values of known sensitive parameters. This prevents credentials, connection 
        strings, and environment variables from leaking into the persistent PowerShell 
        session $Error variable or local log files.

    .PARAMETER Text
        The raw string output (stdout, stderr, or exception message) to be scrubbed.

    .EXAMPLE
        $rawLog = 'Error 1: --password="MySecret" --user admin --preLaunchEnv "API_KEY=12345"'
        $safeLog = Format-SecureLogMessage -Text $rawLog
        # Returns: 'Error 1: --password "***" --user admin --preLaunchEnv "***"'
  #>
  param(
    [string]$Text
  )

  if ($null -eq $Text -or $Text.Trim() -eq "") {
      return $Text
  }

  # Define all CLI parameters that may contain sensitive data or injected variables
  $sensitiveFields = @(
    "params",
    "failureProgramParams",
    "password",
    "envVars",
    "preLaunchParams",
    "preLaunchEnv",
    "postLaunchParams",
    "preStopParams",
    "postStopParams"
  )

  # Construct the regex pattern dynamically
  # Breakdown:
  # (?i)                  : Case-insensitive evaluation
  # (--(?:...)[=\s]+)     : Group $1 -> Matches the flag (e.g., --password) plus trailing spaces or equals signs
  # ("[\]*"|'[^']*'|\S+)  : Group $2 -> Matches the value (handles double quotes, single quotes, or unquoted contiguous strings)
  $fieldsRegex = $sensitiveFields -join '|'
  $pattern = '(?i)(--(?:' + $fieldsRegex + ')[=\s]+)("[^"]*"|''[^'']*''|\S+)'

  # Replace the sensitive value with "***" while preserving the flag prefix
  return $Text -replace $pattern, '$1"***"'
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

    .PARAMETER EnvironmentVariables
        Accept secure environment variables.

    .NOTES
        This function is intended for internal use within the Servy PowerShell
        module and is not exported.

        Compatible with PowerShell 2.0 and later.

    .EXAMPLE
        Invoke-ServyCli "start" @("--name=MyService") $false "Failed to start service"

  #>
  [CmdletBinding()]
  param(
    [string] $Command,
    [array]  $Arguments,
    [switch] $Quiet,
    [string] $ErrorContext,
    [hashtable] $EnvironmentVariables
  )

  # Validate command (single token)
  if ($Command -match '\s') {
    throw "Command must be a single word without spaces: '$Command'"
  }

  if ($script:ServyTimeoutSeconds -lt 1) {
    throw "ServyTimeoutSeconds must be >= 1 (current: $($script:ServyTimeoutSeconds))"
  }  

  # Build argument list
  $finalArgs = @()
  if ($Command) { $finalArgs += $Command }
  if ($Arguments) { $finalArgs += $Arguments }
  if ($Quiet) { $finalArgs += "--quiet" }

  # Convert array to space-separated string to bypass PS argument mangling
  $argString = $finalArgs -join ' '

  # VALIDATE ARGUMENT LENGTH
  # Windows limit is 32,767 characters. We check against 32,000 to be safe
  # and account for the executable path length.
  if ($argString.Length -gt 32000) {
    throw "$($ErrorContext): Command-line arguments exceed Windows maximum length ($($argString.Length) characters). " +
    "To resolve this, shorten your environment variables/parameters or use the 'import' command " +
    "with a configuration file instead."
  }

  $process = $null
  $outEvent = $null
  $errorEvent = $null

  try {
    # 1. Create a standalone buffer object for this specific run.
    $stdoutBuffer = @{
        Lines     = New-Object System.Collections.ArrayList
        ByteCount = 0
        MaxChars  = $script:ServyMaxBufferChars
        Truncated = $false
    }
    
    $stderrBuffer = @{
        Lines     = New-Object System.Collections.ArrayList
        ByteCount = 0
        MaxChars  = $script:ServyMaxBufferChars
        Truncated = $false
    }

    if (-not $script:ServyCliFound -and -not (Test-Path $script:ServyCliPath)) {
      throw "Servy CLI not found at '$($script:ServyCliPath)'. The file may have been moved or deleted since the module was loaded. Try re-importing the module."
    }
    
    # Using .NET Process class is the most robust way in PS 2.0 to pass 
    # complex raw argument strings WHILE retaining pipeline output capture. 
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $script:ServyCliPath
    $psi.Arguments = $argString
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    # SECURITY: Inject environment variables securely directly into the child process block
    if ($EnvironmentVariables) {
      foreach ($key in $EnvironmentVariables.Keys) {
        $psi.EnvironmentVariables[$key] = $EnvironmentVariables[$key]
      }
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    # 2. Register events WITH -Action blocks. 
    # This securely consumes the output in the background preventing console duplication.
    $outEvent = Register-ObjectEvent -InputObject $process `
      -EventName "OutputDataReceived" `
      -MessageData $stdoutBuffer `
      -Action {
          if ($null -ne $EventArgs.Data) { 
              $buf = $Event.MessageData
              if ($buf.ByteCount -lt $buf.MaxChars) {
                  [void]$buf.Lines.Add($EventArgs.Data)
                  $buf.ByteCount += $EventArgs.Data.Length
              }
              elseif (-not $buf.Truncated) {
                  [void]$buf.Lines.Add("... [Output truncated to prevent memory exhaustion] ...")
                  $buf.Truncated = $true
              }
          }
      }

    $errorEvent = Register-ObjectEvent -InputObject $process `
      -EventName "ErrorDataReceived" `
      -MessageData $stderrBuffer `
      -Action {
          if ($null -ne $EventArgs.Data) { 
              $buf = $Event.MessageData
              if ($buf.ByteCount -lt $buf.MaxChars) {
                  [void]$buf.Lines.Add($EventArgs.Data)
                  $buf.ByteCount += $EventArgs.Data.Length
              }
              elseif (-not $buf.Truncated) {
                  [void]$buf.Lines.Add("... [Error output truncated to prevent memory exhaustion] ...")
                  $buf.Truncated = $true
              }
          }
      }

    try {
      $started = $process.Start()
    }
    catch [System.ComponentModel.Win32Exception] {
      throw "Failed to start Servy CLI: $($_.Exception.Message) (Win32 error $($_.Exception.NativeErrorCode)). Path: '$($script:ServyCliPath)'"
    }

    if (-not $started) {
      throw "Failed to start Servy CLI process '$($script:ServyCliPath)'. " +
      "Verify the file exists, is not locked, and the current user has execute permissions."
    }

    # 3. Begin Async reading
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    # 4. Block main thread for the specified timeout using a non-blocking loop
    # Yielding the thread with Start-Sleep allows the PS engine to execute the -Action blocks!
    $timeoutStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $timeoutMilliseconds = $script:ServyTimeoutSeconds * 1000
    $hasExited = $false

    while ($timeoutStopwatch.ElapsedMilliseconds -lt $timeoutMilliseconds) {
      if ($process.HasExited) {
        $hasExited = $true
        break
      }
      Start-Sleep -Milliseconds 50
    }

    if (-not $hasExited) {
      $killed = $false
      try {
        $process.Kill()
        $process.WaitForExit(5000)
        $killed = $process.HasExited
        if ($killed) { [void]$process.WaitForExit() }
      }
      catch { 
        Write-Warning "Failed to kill process: $_"
      }

      if ($killed) {
        throw "$($ErrorContext): Operation timed out after $($script:ServyTimeoutSeconds) seconds and was terminated."
      } else {
        throw "$($ErrorContext): Operation timed out after $($script:ServyTimeoutSeconds) seconds. WARNING: Failed to terminate the process (PID: $($process.Id)) - it may still be running."
      }
    } else {
      # CRITICAL: Since the process is confirmed dead, a parameterless WaitForExit() 
      # safely blocks just long enough to guarantee all async streams are flushed entirely.
      [void]$process.WaitForExit()
      # Give the PS engine a tiny window to process the final flushed -Action events
      Start-Sleep -Milliseconds 50
    }

    # 5. Extract safely from the memory buffers
    $stdout = if ($stdoutBuffer.Lines.Count -gt 0) { $stdoutBuffer.Lines -join [Environment]::NewLine } else { "" }
    $stderr = if ($stderrBuffer.Lines.Count -gt 0) { $stderrBuffer.Lines -join [Environment]::NewLine } else { "" }
    $exitCode = $process.ExitCode

    # 6. Emit results
    if (-not [string]::IsNullOrEmpty($stdout)) { 
      Write-Output $stdout.TrimEnd() 
    }

    if (-not [string]::IsNullOrEmpty($stderr)) {
      $scrubbedStderr = Format-SecureLogMessage -Text $stderr.TrimEnd()
      Write-Warning $scrubbedStderr
    }
  }
  catch {
    $partialOutput = ""
    
    # SAFETY NET: If the script aborted before normal collection,
    # pull directly from the buffers.
    if ($null -ne $stdoutBuffer -and $stdoutBuffer.Lines.Count -gt 0) {
        $stdout = $stdoutBuffer.Lines -join [Environment]::NewLine
    }
    if ($null -ne $stderrBuffer -and $stderrBuffer.Lines.Count -gt 0) {
        $stderr = $stderrBuffer.Lines -join [Environment]::NewLine
    }

    # SECURITY: Scrub stdout and stderr in the generic catch block
    if (-not [string]::IsNullOrEmpty($stdout)) { 
      $scrubbedStdout = Format-SecureLogMessage -Text $stdout.TrimEnd()
      $partialOutput += " Stdout: $scrubbedStdout" 
    }
    if (-not [string]::IsNullOrEmpty($stderr)) { 
      $scrubbedStderr = Format-SecureLogMessage -Text $stderr.TrimEnd()
      $partialOutput += " Stderr: $scrubbedStderr" 
    }
    
    throw "$($ErrorContext): $($_.Exception.Message)`n$partialOutput".TrimEnd()
  }
  finally {
    if ($null -ne $process) {
      try { $process.CancelOutputRead() } catch {}
      try { $process.CancelErrorRead() } catch {}
      try { [void]$process.WaitForExit(5000) } catch {}  # let in-flight events drain
    }    

    # CRITICAL: Clean up events gracefully
    if ($outEvent) {
      Unregister-Event -SourceIdentifier $outEvent.Name -ErrorAction SilentlyContinue
    }
    if ($errorEvent) {
      Unregister-Event -SourceIdentifier $errorEvent.Name -ErrorAction SilentlyContinue
    }
    
    if ($null -ne $process) {
      $process.Dispose()
    }
  }

  if ($null -ne $exitCode -and $exitCode -ne 0) {
    # SECURITY: Scrub stderr before pushing to the session-persistent exit code exception
    $scrubbedStderrFinal = Format-SecureLogMessage -Text $stderr
    $errorMessage = if ($null -ne $scrubbedStderrFinal -and $scrubbedStderrFinal.Trim() -ne "") { $scrubbedStderrFinal.TrimEnd() } else { "Unknown error" }
    
    throw "$($ErrorContext): Servy CLI exited with code $exitCode. Details: $errorMessage"
  }
}

function Assert-Administrator {
  <#
    .SYNOPSIS
        Verifies that the current PowerShell session is running with Administrator privileges.

    .DESCRIPTION
        Checks the security principal of the current Windows identity. If the user is not in
        the 'Administrator' role, the function throws a terminating error. This is used
        by Servy cmdlets that interact with the Service Control Manager (SCM).

    .EXAMPLE
        Assert-Administrator
        # Throws an error if not elevated; otherwise, allows the script to continue.

    .NOTES
        Requires the System.Security.Principal namespace.
  #>
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)

        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw "This operation requires Administrator privileges. Run PowerShell as Administrator."
        }
    }
    finally {
        $identity.Dispose()
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
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Command,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name,

    [switch] $Quiet
  )
  Assert-Administrator

  $argsList = @()
  $argsList = Add-Arg $argsList "--name" $Name

  Invoke-ServyCli -Command $Command -Quiet:$Quiet -Arguments $argsList -ErrorContext "Failed to $Command service '$Name'"
}

# ----------------------------------------------------------------
# Public Functions
# ----------------------------------------------------------------

function Get-ServyVersion {
  <#
    .SYNOPSIS
        Displays the version of the Servy CLI.

    .DESCRIPTION
        Wraps the Servy CLI `--version` command to show the current version
        of the Servy tool installed on the system.

    .PARAMETER Quiet
        Suppress spinner and run in non-interactive mode. Optional.

    .EXAMPLE
        Get-ServyVersion
        # Displays the current version of Servy CLI.
    #>
  [CmdletBinding()]
  param(
    [switch] $Quiet
  )

  Invoke-ServyCli -Command "--version" -Quiet:$Quiet -ErrorContext "Failed to get Servy CLI version"
}

function Get-ServyHelp {
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
        Get-ServyHelp
        # Displays help for the Servy CLI.

    .EXAMPLE
        Get-ServyHelp -Command "install"
        # Displays help for the install command.        
    #>
  [CmdletBinding()]
  param(
    [switch] $Quiet,
    [ValidateSet("install", "uninstall", "start", "stop", "restart", "status", "export", "import")]
    [string] $Command
  )

  $argsList = @()
  if ($Command) {
    $argsList = Add-Arg $argsList "--help" -Flag
    Invoke-ServyCli -Command $Command -Arguments $argsList -Quiet:$Quiet -ErrorContext "Failed to display Servy CLI help"
  }
  else {
    Invoke-ServyCli -Command "--help" -Quiet:$Quiet -ErrorContext "Failed to display Servy CLI help"
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

    .PARAMETER EnableConsoleUI
        Switch to enable the console user interface for the service. When enabled, stdout/stderr redirection is disabled.

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
        Date rotation type. Options: Daily, Weekly, Monthly, None. Optional.
        None disables date-based rotation; use when only size rotation is desired.

    .PARAMETER MaxRotations
        Maximum rotated log files to keep. 0 for unlimited. Optional.

    .PARAMETER UseLocalTimeForRotation
        If this switch is present, log rotation will be calculated using the server's local time (e.g., rotating at local midnight). 
        This is often preferred for manual log review and local troubleshooting.

        If this switch is omitted, the default behavior is to use Coordinated Universal Time (UTC). 
        This ensures a consistent, 24-hour rotation cycle that is unaffected by Daylight Saving Time transitions.

    .PARAMETER EnableHealth
        Switch to enable health monitoring. Optional.

    .PARAMETER HeartbeatInterval
        Heartbeat interval in seconds for health checks. Must be >= 5. Optional.

    .PARAMETER MaxFailedChecks
        Maximum number of failed health checks before triggering recovery. Optional.

    .PARAMETER RecoveryAction
        Recovery action on failure. Options: None, RestartService, RestartProcess, RestartComputer. Optional.

    .PARAMETER MaxRestartAttempts
        Maximum number of restart attempts after failure. Optional. Set to 0 for unlimited restart attempts.

    .PARAMETER FailureProgramPath
        Path to a failure program or script. Optional.

    .PARAMETER FailureProgramStartupDir
        Startup directory for the failure program. Optional.

    .PARAMETER FailureProgramParams
        Additional parameters for the failure program. Optional.

    .PARAMETER EnvVars
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
        When enabled, environment variables and process parameters are recorded in the Servy.Service.log file.
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

   .NOTES
        DEVELOPER NOTE: Parameter Naming Convention
        The dynamic argument builder in this function relies on a 1:1 mapping between 
        PowerShell parameter names and Servy CLI flag names. 
        
        Example: 
        CLI Flag: "--startupDir" -> PS Parameter: "$StartupDir"
        
        If a CLI flag is added that does not match the PS parameter name (ignoring casing 
        and leading dashes), the $PSBoundParameters.ContainsKey() check
        will fail, and the argument will not be passed to the executable.            
    #>
  [CmdletBinding()]
  param(
    # Execution Settings
    [switch] $Quiet,

    # Basic Information
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name,

    [ValidateNotNullOrEmpty()]
    [ValidateLength(1, 256)]
    [string] $DisplayName,

    [string] $Description,

    # Process Configuration
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [ValidateScript({ 
        if (Test-Path $_ -PathType Leaf) { $true } 
        else { throw "Executable not found: $_" } 
      })]
    [string] $Path,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Container) { $true } 
        else { throw "Startup directory not found: $_" } 
      })]
    [string] $StartupDir,

    [string] $Params,

    # Service Lifecycle and Priority
    [ValidateSet("Automatic", "AutomaticDelayedStart", "Manual", "Disabled")]
    [string] $StartupType,

    [ValidateSet("Idle", "BelowNormal", "Normal", "AboveNormal", "High", "RealTime")]
    [string] $Priority,

    [switch] $EnableConsoleUI,

    # Logging
    [ValidateScript({ 
        $parent = Split-Path $_ -Parent
        if (Test-Path $parent -PathType Container) { $true } 
        else { throw "Parent directory for Stdout does not exist: $parent" } 
      })]
    [string] $Stdout,

    [ValidateScript({ 
        $parent = Split-Path $_ -Parent
        if (Test-Path $parent -PathType Container) { $true } 
        else { throw "Parent directory for Stderr does not exist: $parent" } 
      })]
    [string] $Stderr,

    # Timeouts
    [ValidateRange(1, 86400)]
    [int] $StartTimeout,

    [ValidateRange(1, 86400)]
    [int] $StopTimeout,

    # Log Rotation
    [switch] $EnableRotation,

    [switch] $EnableSizeRotation,

    [ValidateRange(1, 10240)]
    [int] $RotationSize,

    [switch] $EnableDateRotation,

    [ValidateSet("Daily", "Weekly", "Monthly")]
    [string] $DateRotationType,

    [ValidateRange(0, 10000)]
    [int] $MaxRotations,

    [switch] $UseLocalTimeForRotation,

    # Health Monitoring
    [switch] $EnableHealth,

    [ValidateRange(5, 86400)]
    [int] $HeartbeatInterval,

    [ValidateRange(1, 2147483647)]
    [int] $MaxFailedChecks,

    # Recovery
    [ValidateSet("None", "RestartService", "RestartProcess", "RestartComputer")]
    [string] $RecoveryAction,

    [ValidateRange(0, 2147483647)]
    [int] $MaxRestartAttempts,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Leaf) { $true } 
        else { throw "Failure program executable not found: $_" } 
      })]
    [string] $FailureProgramPath,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Container) { $true } 
        else { throw "Failure program startup directory not found: $_" } 
      })]
    [string] $FailureProgramStartupDir,

    [string] $FailureProgramParams,

    # Environment and Dependencies
    [ValidatePattern('^([^= ]+=(?:\\=|\\;|\\"|\\\\|[^;])*)(; ?[^= ]+=(?:\\=|\\;|\\"|\\\\|[^;])*)*;?$')]
    [string] $EnvVars,

    [ValidatePattern('^[\w\s;-]+$')]
    [string] $Deps,

    # Identity
    [ValidatePattern('^.+\\.+$')]
    [string] $User,

    [ValidateNotNullOrEmpty()]
    [System.Security.SecureString]$Password,

    # Pre-launch
    [ValidateScript({ 
        if (Test-Path $_ -PathType Leaf) { $true } 
        else { throw "Pre-launch executable not found: $_" } 
      })]
    [string] $PreLaunchPath,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Container) { $true } 
        else { throw "Pre-launch startup directory not found: $_" } 
      })]
    [string] $PreLaunchStartupDir,

    [string] $PreLaunchParams,

    [ValidatePattern('^([^= ]+=(?:\\=|\\;|\\"|\\\\|[^;])*)(; ?[^= ]+=(?:\\=|\\;|\\"|\\\\|[^;])*)*;?$')]
    [string] $PreLaunchEnv,

    [ValidateScript({ 
        $parent = Split-Path $_ -Parent
        if (Test-Path $parent -PathType Container) { $true } 
        else { throw "Parent directory for PreLaunchStdout does not exist: $parent" } 
      })]
    [string] $PreLaunchStdout,

    [ValidateScript({ 
        $parent = Split-Path $_ -Parent
        if (Test-Path $parent -PathType Container) { $true } 
        else { throw "Parent directory for PreLaunchStderr does not exist: $parent" } 
      })]
    [string] $PreLaunchStderr,

    [ValidateRange(0, 86400)]
    [int] $PreLaunchTimeout,

    [ValidateRange(0, 2147483647)]
    [int] $PreLaunchRetryAttempts,

    [switch] $PreLaunchIgnoreFailure,

    # Post-launch
    [ValidateScript({ 
        if (Test-Path $_ -PathType Leaf) { $true } 
        else { throw "Post-launch executable not found: $_" } 
      })]
    [string] $PostLaunchPath,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Container) { $true } 
        else { throw "Post-launch startup directory not found: $_" } 
      })]
    [string] $PostLaunchStartupDir,

    [string] $PostLaunchParams,

    # Debug Logs
    [switch] $EnableDebugLogs,

    # Pre-stop
    [ValidateScript({ 
        if (Test-Path $_ -PathType Leaf) { $true } 
        else { throw "Pre-stop executable not found: $_" } 
      })]
    [string] $PreStopPath,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Container) { $true } 
        else { throw "Pre-stop startup directory not found: $_" } 
      })]
    [string] $PreStopStartupDir,

    [string] $PreStopParams,

    [ValidateRange(0, 86400)]
    [int] $PreStopTimeout,

    [switch] $PreStopLogAsError,

    # Post-stop
    [ValidateScript({ 
        if (Test-Path $_ -PathType Leaf) { $true } 
        else { throw "Post-stop executable not found: $_" } 
      })]
    [string] $PostStopPath,

    [ValidateScript({ 
        if (Test-Path $_ -PathType Container) { $true } 
        else { throw "Post-stop startup directory not found: $_" } 
      })]
    [string] $PostStopStartupDir,

    [string] $PostStopParams
  )

  Assert-Administrator

  $argsList = @()

  # 1. Explicit Parameter Mapping: CLI Flag => PowerShell Parameter Name
  $paramMapping = @{
      "--name"                     = "Name"
      "--displayName"              = "DisplayName"
      "--path"                     = "Path"
      "--description"              = "Description"
      "--startupDir"               = "StartupDir"
      "--params"                   = "Params"
      "--startupType"              = "StartupType"
      "--priority"                 = "Priority"
      "--stdout"                   = "Stdout"
      "--stderr"                   = "Stderr"
      "--startTimeout"             = "StartTimeout"
      "--stopTimeout"              = "StopTimeout"
      "--rotationSize"             = "RotationSize"
      "--dateRotationType"         = "DateRotationType"
      "--maxRotations"             = "MaxRotations"
      "--heartbeatInterval"        = "HeartbeatInterval"
      "--maxFailedChecks"          = "MaxFailedChecks"
      "--recoveryAction"           = "RecoveryAction"
      "--maxRestartAttempts"       = "MaxRestartAttempts"
      "--failureProgramPath"       = "FailureProgramPath"
      "--failureProgramStartupDir" = "FailureProgramStartupDir"
      "--failureProgramParams"     = "FailureProgramParams"
      "--envVars"                  = "EnvVars"
      "--deps"                     = "Deps"
      "--user"                     = "User"
      "--preLaunchPath"            = "PreLaunchPath"
      "--preLaunchStartupDir"      = "PreLaunchStartupDir"
      "--preLaunchParams"          = "PreLaunchParams"
      "--preLaunchEnv"             = "PreLaunchEnv"
      "--preLaunchStdout"          = "PreLaunchStdout"
      "--preLaunchStderr"          = "PreLaunchStderr"
      "--preLaunchTimeout"         = "PreLaunchTimeout"
      "--preLaunchRetryAttempts"   = "PreLaunchRetryAttempts"
      "--postLaunchPath"           = "PostLaunchPath"
      "--postLaunchStartupDir"     = "PostLaunchStartupDir"
      "--postLaunchParams"         = "PostLaunchParams"
      "--preStopPath"              = "PreStopPath"
      "--preStopStartupDir"        = "PreStopStartupDir"
      "--preStopParams"            = "PreStopParams"
      "--preStopTimeout"           = "PreStopTimeout"
      "--postStopPath"             = "PostStopPath"
      "--postStopStartupDir"       = "PostStopStartupDir"
      "--postStopParams"           = "PostStopParams"
  }

  # 2. Iterate through mapping to build arguments
  foreach ($entry in $paramMapping.GetEnumerator()) {
      $cliFlag   = $entry.Key
      $paramName = $entry.Value

      # Use $PSBoundParameters to ensure we only pass what the user explicitly provided
      if ($PSBoundParameters.ContainsKey($paramName)) {
          $val = $PSBoundParameters[$paramName]

          $argsList = Add-Arg $argsList $cliFlag $val
      }
  }

  # 3. Handle standalone Flags/Switches separately
  if ($EnableConsoleUI)                        { $argsList = Add-Arg $argsList "--enableConsoleUI" -Flag }
  if ($EnableRotation)                         { Write-Warning "-EnableRotation is deprecated. Use -EnableSizeRotation instead." }
  if ($EnableRotation -or $EnableSizeRotation) { $argsList = Add-Arg $argsList "--enableSizeRotation" -Flag }
  if ($EnableDateRotation)                     { $argsList = Add-Arg $argsList "--enableDateRotation" -Flag }
  if ($UseLocalTimeForRotation)                { $argsList = Add-Arg $argsList "--useLocalTimeForRotation" -Flag }
  if ($EnableHealth)                           { $argsList = Add-Arg $argsList "--enableHealth" -Flag }
  if ($PreLaunchIgnoreFailure)                 { $argsList = Add-Arg $argsList "--preLaunchIgnoreFailure" -Flag }
  if ($EnableDebugLogs)                        { $argsList = Add-Arg $argsList "--debug" -Flag }
  if ($PreStopLogAsError)                      { $argsList = Add-Arg $argsList "--preStopLogAsError" -Flag }

  # 4. Secure Password Marshaling (Memory Safety)
  $plainPassword = $null
  $secureEnv = @{}
  
  if ($null -ne $Password) {
      # SecureString -> Unmanaged BSTR
      $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
      try {
          # Unmanaged BSTR -> Managed String
          $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
          
          if ($null -ne $plainPassword -and $plainPassword.Length -gt 0) {
              $secureEnv["SERVY_PASSWORD"] = $plainPassword
          }
      }
      finally {
          # CRITICAL: Manual zero-out of the unmanaged memory buffer
          [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
      }
  }

  # 5. CLI Invocation with deterministic cleanup
  try {
      Invoke-ServyCli -Command "install" -Arguments $argsList -Quiet:$Quiet -EnvironmentVariables $secureEnv -ErrorContext "Failed to install service '$Name'"
  }
  finally {
      # Explicitly clear managed references to prevent sensitive data lingering in the heap
      $plainPassword = $null
      if ($secureEnv.ContainsKey("SERVY_PASSWORD")) {
          $secureEnv["SERVY_PASSWORD"] = $null
      }
      
      # Help GC by clearing the ArrayList
      $argsList = $null
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
        Uninstall-ServyService -Name "MyService"
    #>
  [CmdletBinding()]
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

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
  [CmdletBinding()]
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

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
  [CmdletBinding()]
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

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
  [CmdletBinding()]
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

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
  [CmdletBinding()]
  param(
    [switch] $Quiet,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
  )

  Invoke-ServyServiceCommand -Command "status" -Name $Name -Quiet:$Quiet
}

function Export-ServyServiceConfig {
    <#
    .SYNOPSIS
        Exports a Servy Windows service configuration to a file.

    .DESCRIPTION
        Wraps the Servy CLI `export` command to export the configuration of a service
        to a file. Supports XML and JSON file types. Requires Administrator privileges
        to read the service database.

    .PARAMETER Quiet
        Suppress the spinner and run the CLI in non-interactive mode. Optional.

    .PARAMETER Name
        The unique internal name of the service to export. This name is used to 
        locate the record in the database. (Required)

    .PARAMETER ConfigFileType
        The format of the export file. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full destination path where the configuration file will be saved. 
        The parent directory must exist and be writable. (Required)

    .EXAMPLE
        Export-ServyServiceConfig -Name "MyService" -ConfigFileType "json" -Path "C:\Configs\MyService.json"
        # Exports the configuration of 'MyService' to a JSON file at the specified path.

    .NOTES
        The function calls Assert-Administrator to ensure the session has the 
        necessary permissions to access the Servy ProgramData directory.
    #>
    [CmdletBinding()]
    param(
        [switch] $Quiet,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [ValidateSet("xml", "json")]
        [string] $ConfigFileType,

        # Export: Validate that the target directory is writable/exists
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({
            $parent = Split-Path $_ -Parent
            if ([string]::IsNullOrEmpty($parent)) { return $true }
            Test-Path $parent -PathType Container
        })]
        [string] $Path
    )

    # Enforce elevation to allow CLI access to %ProgramData%\Servy
    Assert-Administrator

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
        (XML or JSON) into Servy's database. If the service already exists, it 
        will be updated. Requires Administrator privileges to write to the 
        service database and potentially modify Windows services.

    .PARAMETER Quiet
        Suppress the spinner and run the CLI in non-interactive mode. Optional.

    .PARAMETER ConfigFileType
        The configuration file type being imported. Valid values are 'xml' or 'json'. (Required)

    .PARAMETER Path
        The full path of the source configuration file to import. The file 
        must exist and be readable. (Required)

    .PARAMETER Install
        If specified, the service will be automatically installed (or updated in the SCM) 
        immediately after the database import. Optional.

    .EXAMPLE
        Import-ServyServiceConfig -ConfigFileType "json" -Path "C:\Configs\MyService.json" -Install
        # Imports the configuration file into Servy's database and updates the Windows service.

    .NOTES
        The service name is derived from the content of the configuration file.
        The function calls Assert-Administrator to ensure the session is elevated.
    #>
    [CmdletBinding()]
    param(
        [switch] $Quiet,

        [Parameter(Mandatory = $true)]
        [ValidateSet("xml", "json")]
        [string] $ConfigFileType,

        # Import: Validate that the source file actually exists
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({ Test-Path $_ -PathType Leaf })]
        [string] $Path,

        [switch] $Install
    )

    # Enforce elevation to allow CLI to write to the database and manage services
    Assert-Administrator

    $argsList = @()
    $argsList = Add-Arg $argsList "--config" $ConfigFileType
    $argsList = Add-Arg $argsList "--path" $Path
    if ($Install) { $argsList = Add-Arg $argsList "--install" -Flag }

    Invoke-ServyCli -Command "import" -Arguments $argsList -Quiet:$Quiet -ErrorContext "Failed to import configuration from '$Path'"
}

# PS 2.0 Compatible Alias declaration
# We MUST use Export-ModuleMember in PS 2.0 to ensure 
# the aliases actually leave the module scope.

# 1. Define all public functions
$publicFunctions = @(
  'Get-ServyVersion',
  'Get-ServyHelp',
  'Install-ServyService',
  'Uninstall-ServyService',
  'Start-ServyService',
  'Stop-ServyService',
  'Restart-ServyService',
  'Get-ServyServiceStatus',
  'Export-ServyServiceConfig',
  'Import-ServyServiceConfig'
)

# 2. Define all aliases (including the legacy Show- ones)
$publicAliases = @(
  'Show-ServyVersion',
  'Show-ServyHelp'
)

# 3. Create the actual Aliases pointing to the new Get- functions
New-Alias -Name 'Show-ServyVersion' -Value 'Get-ServyVersion' -ErrorAction SilentlyContinue
New-Alias -Name 'Show-ServyHelp'    -Value 'Get-ServyHelp'    -ErrorAction SilentlyContinue

# 4. Export everything to the pipeline
Export-ModuleMember -Function $publicFunctions -Alias $publicAliases
# --- End of Servy.psm1 ---