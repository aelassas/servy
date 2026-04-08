#if DEBUG
using System.Reflection;
#else
using Servy.Core.Config;
#endif
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using System.Diagnostics;
using System.ServiceProcess;
using Servy.Core.Data;

namespace Servy.Service.Helpers
{
    /// <inheritdoc />
    public class ServiceHelper : IServiceHelper
    {

        #region Private Fields

        private readonly ICommandLineProvider _commandLineProvider;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceHelper"/> class.
        /// </summary>
        /// <param name="commandLineProvider">The provider used to access system command line arguments.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="commandLineProvider"/> is null.</exception>
        public ServiceHelper(ICommandLineProvider commandLineProvider)
        {
            _commandLineProvider = commandLineProvider ?? throw new ArgumentNullException(nameof(commandLineProvider));
        }

        #endregion

        #region IServiceHelper implementation

        /// <inheritdoc />
        public string[] GetSanitizedArgs()
        {
            var args = _commandLineProvider.GetArgs();
            return args.Select(a => a.Trim(' ', '"')).ToArray();
        }

        /// <inheritdoc />
        public void LogStartupArguments(ILogger logger, string[] args, StartOptions options)
        {
            if (options == null)
            {
                logger?.Error("StartOptions is null.");
                return;
            }

            string envVarsFormatted = EnvironmentVariablesToString(options.EnvironmentVariables);
            string preLaunchEnvVarsFormatted = EnvironmentVariablesToString(options.PreLaunchEnvironmentVariables);

            // 1. PUBLIC DATA: Logged to both Local Log and Windows Event Log (logger?.Info)
            logger?.Info(
                  $"[Startup Parameters]\n" +
                  "--------Main-------------------\n" +
                  $"- serviceName: {options.ServiceName}\n" +
                  $"- realExePath: {options.ExecutablePath}\n" +
                  $"- workingDir: {options.WorkingDirectory}\n" +
                  $"- priority: {options.Priority}\n" +
                  $"- startTimeoutInSeconds: {options.StartTimeout}\n" +
                  $"- stopTimeoutInSeconds: {options.StopTimeout}\n\n" +

                  "--------Logging----------------\n" +
                  $"- stdoutFilePath: {options.StdOutPath}\n" +
                  $"- stderrFilePath: {options.StdErrPath}\n" +
                  $"- enableSizeRotation: {options.EnableSizeRotation}\n" +
                  $"- rotationSizeInBytes: {options.RotationSizeInBytes}\n" +
                  $"- enableDateRotation: {options.EnableDateRotation}\n" +
                  $"- dateRotationType: {options.DateRotationType}\n" +
                  $"- maxRotations: {options.MaxRotations}\n" +
                  $"- useLocalTimeForRotation: {options.UseLocalTimeForRotation}\n\n" +

                  "--------Recovery---------------\n" +
                  $"- heartbeatInterval: {options.HeartbeatInterval}\n" +
                  $"- maxFailedChecks: {options.MaxFailedChecks}\n" +
                  $"- recoveryAction: {options.RecoveryAction}\n" +
                  $"- maxRestartAttempts: {options.MaxRestartAttempts}\n" +
                  $"- failureProgramPath: {options.FailureProgramPath}\n" +
                  $"- failureProgramWorkingDirectory: {options.FailureProgramWorkingDirectory}\n\n" +

                  "--------Pre-Launch-------------\n" +
                  $"- preLaunchExecutablePath: {options.PreLaunchExecutablePath}\n" +
                  $"- preLaunchWorkingDirectory: {options.PreLaunchWorkingDirectory}\n" +
                  $"- preLaunchStdOutPath: {options.PreLaunchStdoutPath}\n" +
                  $"- preLaunchStdErrPath: {options.PreLaunchStderrPath}\n" +
                  $"- preLaunchTimeout: {options.PreLaunchTimeout}\n" +
                  $"- preLaunchRetryAttempts: {options.PreLaunchRetryAttempts}\n" +
                  $"- preLaunchIgnoreFailure: {options.PreLaunchIgnoreFailure}\n\n" +

                  "--------Post-Launch------------\n" +
                  $"- postLaunchExecutablePath: {options.PostLaunchExecutablePath}\n" +
                  $"- postLaunchWorkingDirectory: {options.PostLaunchWorkingDirectory}\n\n" +

                  "--------Pre-Stop-------------\n" +
                  $"- preStopExecutablePath: {options.PreStopExecutablePath}\n" +
                  $"- preStopWorkingDirectory: {options.PreStopWorkingDirectory}\n" +
                  $"- preStopTimeout: {options.PreStopTimeout}\n" +
                  $"- preStopLogAsError: {options.PreStopLogAsError}\n\n" +

                  "--------Post-Stop-------------\n" +
                  $"- postStopExecutablePath: {options.PostStopExecutablePath}\n" +
                  $"- postStopWorkingDirectory: {options.PostStopWorkingDirectory}\n"
            );

            // 2. SENSITIVE DATA: Logged to Local Text Logs ONLY (Servy.Service.log)
            // This is only triggered if EnableDebugLogs is true.
            if (options.EnableDebugLogs)
            {
                Logger.Info(
                    $"[Startup Parameters - SENSITIVE DATA]\n" +
                    "NOTE: This section contains sensitive parameters including executable arguments and environment variables.\n" +
                    "--------Main (Sensitive)-------\n" +
                    $"- realArgs: {options.ExecutableArgs}\n\n" +

                    "--------Recovery (Sensitive)---\n" +
                    $"- failureProgramArgs: {options.FailureProgramArgs}\n\n" +

                    "--------Advanced (Sensitive)---\n" +
                    $"- environmentVariables: {envVarsFormatted}\n\n" +

                    "--------Pre-Launch (Sensitive)-\n" +
                    $"- preLaunchExecutableArgs: {options.PreLaunchExecutableArgs}\n" +
                    $"- preLaunchEnvironmentVariables: {preLaunchEnvVarsFormatted}\n\n" +

                    "--------Post-Launch (Sensitive)\n" +
                    $"- postLaunchExecutableArgs: {options.PostLaunchExecutableArgs}\n\n" +

                    "--------Pre-Stop (Sensitive)---\n" +
                    $"- preStopExecutableArgs: {options.PreStopExecutableArgs}\n\n" +

                    "--------Post-Stop (Sensitive)--\n" +
                    $"- postStopExecutableArgs: {options.PostStopExecutableArgs}\n"
                );
            }
        }

        /// <inheritdoc />
        public void EnsureValidWorkingDirectory(StartOptions options, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(options.WorkingDirectory) ||
                !Helper.IsValidPath(options.WorkingDirectory) ||
                !Directory.Exists(options.WorkingDirectory))
            {
                var system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
                options.WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath) ?? system32;
                logger?.Warn($"Working directory fallback applied: {options.WorkingDirectory}");
            }
        }

        /// <inheritdoc />
        public StartOptions? InitializeStartup(IServiceRepository serviceRepository, ILogger logger)
        {
            var fullArgs = _commandLineProvider.GetArgs();
            var options = StartOptionsParser.Parse(serviceRepository, fullArgs);

            LogStartupArguments(logger, fullArgs, options);

            if (!ValidateStartupOptions(logger, options))
            {
                return null;
            }

            return options;
        }

        /// <inheritdoc />
        public string[] GetArgs()
            => _commandLineProvider.GetArgs();

        /// <inheritdoc />
        public StartOptions? ParseOptions(IServiceRepository serviceRepository, string[] fullArgs)
            => StartOptionsParser.Parse(serviceRepository, fullArgs);

        /// <inheritdoc />
        public bool ValidateAndLog(StartOptions options, ILogger logger, string[] fullArgs)
        {
            LogStartupArguments(logger, fullArgs, options);

            if (!ValidateStartupOptions(logger, options))
            {
                return false;
            }

            return true;
        }


        /// <inheritdoc />
        public void RestartProcess(
            IProcessWrapper process,
            Action<string, string, string, List<EnvironmentVariable>> startProcess,
            string realExePath,
            string realArgs,
            string workingDir,
            List<EnvironmentVariable> environmentVariables,
            ILogger logger,
            int stopTimeoutMs)
        {
            try
            {
                logger?.Info("Restarting child process...");

                if (process != null && !process.HasExited)
                {
                    // Capture lineage BEFORE stopping
                    var parentPid = 0;
                    var parentStartTime = DateTime.MinValue;
                    try
                    {
                        parentPid = process.Id;
                        parentStartTime = process.StartTime;
                    }
                    catch (Exception ex)
                    {
                        /* Process already dead, can't get children anyway */
                        logger?.Warn($"RestartProcess error while getting process PID and StartTime: {ex.Message}");
                    }

                    process.Stop(stopTimeoutMs);
                    process.StopDescendants(parentPid, parentStartTime, stopTimeoutMs);
                }

                startProcess?.Invoke(realExePath, realArgs, workingDir, environmentVariables);

                logger?.Info("Process restarted.");
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to restart process: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public void RestartService(ILogger logger, string serviceName)
        {
            try
            {
#if DEBUG
                var exePath = Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(exePath);
#else
                var dir = AppConfig.ProgramDataPath;
#endif
                var restarter = Path.Combine(dir!, "Servy.Restarter.exe");

                if (!File.Exists(restarter))
                {
                    logger?.Error("Servy.Restarter.exe not found.");
                    return;
                }

                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = restarter,
                    Arguments = serviceName,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    if (process == null)
                    {
                        logger?.Error("Failed to start Servy.Restarter.exe.");
                        return;
                    }

                    if (!process.WaitForExit(240_000))
                    {
                        logger?.Error("Servy.Restarter.exe did not exit within 4 minutes.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to launch restarter: {ex}");
            }
        }

        /// <inheritdoc />
        public void RestartComputer(ILogger logger)
        {
            try
            {
                using (Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 0 /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    // The using block ensures the native process handle is closed 
                    // immediately after the process is launched, preventing a 
                    // handle leak in the calling application.
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to restart computer: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void RequestAdditionalTime(ServiceBase service, int milliseconds, ILogger logger)
        {
            if (service == null) return;

            try
            {
                service.RequestAdditionalTime(milliseconds);
                logger?.Info($"Requested additional {milliseconds} ms for service operation.");
            }
            catch (InvalidOperationException)
            {
                // SCM no longer accepts wait hints (service likely exiting)
            }
            catch (Exception ex)
            {
                // Last-resort safety: never let SCM signaling crash the service
                logger?.Error($"RequestAdditionalTime failed: {ex.Message}");
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Converts a list of <see cref="EnvironmentVariable"/> objects to a formatted string.
        /// Each variable is formatted as "Name=Value" and separated by "; ".
        /// Returns "(null)" if the list is null.
        /// </summary>
        /// <param name="vars">The list of environment variables to format.</param>
        /// <returns>A formatted string representing the environment variables.</returns>
        private static string EnvironmentVariablesToString(List<EnvironmentVariable> vars)
        {
            string envVarsFormatted = vars != null
                ? string.Join("; ", vars.Select(ev => $"{ev.Name}={ev.Value}"))
                : "(null)";

            return envVarsFormatted;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Validates the critical configuration paths and service identity within the startup options.
        /// </summary>
        /// <remarks>
        /// This method performs a series of integrity checks on:
        /// <list type="bullet">
        /// <item><description>Required fields (Service Name and Main Executable Path).</description></item>
        /// <item><description>The existence and validity of primary, failure, pre-launch, and post-launch executable paths.</description></item>
        /// <item><description>The validity of associated working directories for all configured processes.</description></item>
        /// </list>
        /// Any validation failure is logged as an error to the provided <paramref name="logger"/>.
        /// </remarks>
        /// <param name="logger">The logger instance used to report specific validation errors.</param>
        /// <param name="options">The <see cref="StartOptions"/> instance containing the configuration to validate.</param>
        /// <returns>
        /// <c>true</c> if all mandatory paths and directories are valid; otherwise, <c>false</c>.
        /// </returns>
        private bool ValidateStartupOptions(ILogger logger, StartOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                logger?.Error("Executable path not provided.");
                return false;
            }

            if (string.IsNullOrEmpty(options.ServiceName))
            {
                logger?.Error("Service name empty");
                return false;
            }

            if (!ProcessHelper.ValidatePath(options.ExecutablePath))
            {
                logger?.Error($"Process path {options.ExecutablePath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.FailureProgramPath) && !ProcessHelper.ValidatePath(options.FailureProgramPath))
            {
                logger?.Error($"Failure program path {options.FailureProgramPath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PreLaunchExecutablePath) && !ProcessHelper.ValidatePath(options.PreLaunchExecutablePath))
            {
                logger?.Error($"Pre-launch process path {options.PreLaunchExecutablePath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PostLaunchExecutablePath) && !ProcessHelper.ValidatePath(options.PostLaunchExecutablePath))
            {
                logger?.Error($"Post-launch process path {options.PostLaunchExecutablePath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.WorkingDirectory) && !ProcessHelper.ValidatePath(options.WorkingDirectory, false))
            {
                logger?.Error($"Process working directory {options.WorkingDirectory} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.FailureProgramWorkingDirectory) && !ProcessHelper.ValidatePath(options.FailureProgramWorkingDirectory, false))
            {
                logger?.Error($"Failure program working directory {options.FailureProgramWorkingDirectory} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory) && !ProcessHelper.ValidatePath(options.PreLaunchWorkingDirectory, false))
            {
                logger?.Error($"Pre-launch process working directory {options.PreLaunchWorkingDirectory} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PostLaunchWorkingDirectory) && !ProcessHelper.ValidatePath(options.PostLaunchWorkingDirectory, false))
            {
                logger?.Error($"Post-launch process working directory {options.PostLaunchWorkingDirectory} is invalid.");
                return false;
            }

            return true;
        }

        #endregion
    }
}
