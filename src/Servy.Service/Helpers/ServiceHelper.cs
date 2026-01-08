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
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Servy.Service.Helpers
{
    /// <inheritdoc />
    public class ServiceHelper : IServiceHelper
    {

        #region Private Fields

        private readonly ICommandLineProvider _commandLineProvider;

        #endregion

        #region Constructors

        public ServiceHelper(ICommandLineProvider commandLineProvider)
        {
            _commandLineProvider = commandLineProvider;
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

            if (logger != null)
            {
                logger.Prefix = options.ServiceName;
            }

            //logger?.Info($"[Args] {string.Join(" ", args)}");
            //logger?.Info($"[Args] fullArgs Length: {args.Length}");

            string envVarsFormatted = EnvironmentVariablesToString(options.EnvironmentVariables);
            string preLaunchEnvVarsFormatted = EnvironmentVariablesToString(options.PreLaunchEnvironmentVariables);

            logger?.Info(
                  $"[Startup Parameters]\n" +
                  "--------Main-------------------\n" +
                  $"- serviceName: {options.ServiceName}\n" +
                  $"- realExePath: {options.ExecutablePath}\n" +
                  // realArgs are not logged to avoid exposing sensitive information
                  GetDebugLog(options, $"- realArgs: {options.ExecutableArgs}\n") +
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
                  $"- maxRotations: {options.MaxRotations}\n\n" +

                  "--------Recovery---------------\n" +
                  $"- heartbeatInterval: {options.HeartbeatInterval}\n" +
                  $"- maxFailedChecks: {options.MaxFailedChecks}\n" +
                  $"- recoveryAction: {options.RecoveryAction}\n" +
                  $"- maxRestartAttempts: {options.MaxRestartAttempts}\n" +
                  $"- failureProgramPath: {options.FailureProgramPath}\n" +
                  $"- failureProgramWorkingDirectory: {options.FailureProgramWorkingDirectory}\n" +
                  // failureProgramArgs are not logged to avoid exposing sensitive information
                  GetDebugLog(options, $"- failureProgramArgs: {options.FailureProgramArgs}\n") +
                  "\n" +

                  GetDebugLog(options, "--------Advanced---------------\n") +
                  // environmentVariables are not logged to avoid exposing sensitive information
                  GetDebugLog(options, $"- environmentVariables: {envVarsFormatted}\n\n") +

                  "--------Pre-Launch-------------\n" +
                  $"- preLaunchExecutablePath: {options.PreLaunchExecutablePath}\n" +
                  $"- preLaunchWorkingDirectory: {options.PreLaunchWorkingDirectory}\n" +
                  // preLaunchExecutableArgs are not logged to avoid exposing sensitive information
                  GetDebugLog(options, $"- preLaunchExecutableArgs: {options.PreLaunchExecutableArgs}\n") +
                  // preLaunchEnvironmentVariables are not logged to avoid exposing sensitive information
                  GetDebugLog(options, $"- preLaunchEnvironmentVariables: {preLaunchEnvVarsFormatted}\n") +
                  $"- preLaunchStdOutPath: {options.PreLaunchStdOutPath}\n" +
                  $"- preLaunchStdErrPath: {options.PreLaunchStdErrPath}\n" +
                  $"- preLaunchTimeout: {options.PreLaunchTimeout}\n" +
                  $"- preLaunchRetryAttempts: {options.PreLaunchRetryAttempts}\n" +
                  $"- preLaunchIgnoreFailure: {options.PreLaunchIgnoreFailure}\n\n" +

                  "--------Post-Launch------------\n" +
                  $"- postLaunchExecutablePath: {options.PostLaunchExecutablePath}\n" +
                  $"- postLaunchWorkingDirectory: {options.PostLaunchWorkingDirectory}\n" +
                  // postLaunchExecutableArgs are not logged to avoid exposing sensitive information
                  GetDebugLog(options, $"- postLaunchExecutableArgs: {options.PostLaunchExecutableArgs}\n")
              //"-------------------------------\n"
              );
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
                logger?.Warning($"Working directory fallback applied: {options.WorkingDirectory}");
            }
        }

        /// <inheritdoc />
        public bool ValidateStartupOptions(ILogger logger, StartOptions options)
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

            if (!Helper.IsValidPath(options.ExecutablePath) || !File.Exists(options.ExecutablePath))
            {
                logger?.Error($"Executable not found: {options.ExecutablePath}");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public StartOptions InitializeStartup(ILogger logger)
        {
            //var fullArgs = GetSanitizedArgs();
            var fullArgs = _commandLineProvider.GetArgs();
            var options = StartOptionsParser.Parse(fullArgs);

            LogStartupArguments(logger, fullArgs, options);

            if (!ValidateStartupOptions(logger, options))
            {
                return null;
            }

            return options;
        }

        /// <inheritdoc />
        public void RestartProcess(
            IProcessWrapper process,
            Action<string, string, string, List<EnvironmentVariable>> startProcess,
            string realExePath,
            string realArgs,
            string workingDir,
            List<EnvironmentVariable> environmentVariables,
            ILogger logger)
        {
            try
            {
                logger?.Info("Restarting child process...");

                if (process != null && !process.HasExited)
                {
                    const int timeoutMs = 10_000;
                    process.Stop(timeoutMs);
                    process.StopDescendants(timeoutMs);
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
                var restarter = Path.Combine(dir, "Servy.Restarter.exe");

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

                    if (process.ExitCode != 0)
                    {
                        logger?.Error($"Servy.Restarter.exe exited with code {process.ExitCode}.");
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 0 /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to restart computer: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void RequestAdditionalTime(ServiceBase service, int milliseconds, ILogger logger)
        {
            service.RequestAdditionalTime(milliseconds);
            logger?.Info($"Requested additional {milliseconds}ms for service operation.");
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

        /// <summary>
        /// Returns the specified debug log message if debug logging is enabled; otherwise, returns an empty string.
        /// </summary>
        /// <param name="options">The start options that specify whether debug logging is enabled.</param>
        /// <param name="message">The message to include in the debug log output.</param>
        /// <returns>The debug message if <see cref="StartOptions.EnableDebugLogs"/> is true; otherwise, an empty string.</returns>
        private static string GetDebugLog(StartOptions options, string message)
        {
            return options.EnableDebugLogs ? message : string.Empty;
        }

        #endregion

    }
}
