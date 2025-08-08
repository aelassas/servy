using Servy.Core;
using Servy.Core.Helpers;
using Servy.Service.CommandLine;
using Servy.Service.Logging;
using Servy.Service.ProcessManagement;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Servy.Service.ServiceHelpers
{
    /// <inheritdoc />
    public class ServiceHelper : IServiceHelper
    {
        private IntPtr _jobHandle = IntPtr.Zero;

        private readonly ICommandLineProvider _commandLineProvider;

        public ServiceHelper(ICommandLineProvider commandLineProvider)
        {
            _commandLineProvider = commandLineProvider;
        }

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

            logger?.Info($"[Args] {string.Join(" ", args)}");
            logger?.Info($"[Args] fullArgs Length: {args.Length}");

            logger?.Info(
              $"[Startup Parameters]\n" +
              $"- serviceName: {options.ServiceName}\n" +
              $"- realExePath: {options.ExecutablePath}\n" +
              $"- realArgs: {options.ExecutableArgs}\n" +
              $"- workingDir: {options.WorkingDirectory}\n" +
              $"- priority: {options.Priority}\n" +
              $"- stdoutFilePath: {options.StdOutPath}\n" +
              $"- stderrFilePath: {options.StdErrPath}\n" +
              $"- rotationSizeInBytes: {options.RotationSizeInBytes}\n" +
              $"- heartbeatInterval: {options.HeartbeatInterval}\n" +
              $"- maxFailedChecks: {options.MaxFailedChecks}\n" +
              $"- recoveryAction: {options.RecoveryAction}\n" +
              $"- maxRestartAttempts: {options.MaxRestartAttempts}"
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
            var fullArgs = GetSanitizedArgs();
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
            Action terminateJobObject,
            Action<string, string, string> startProcess,
            string realExePath,
            string realArgs,
            string workingDir,
            ILogger logger)
        {
            try
            {
                logger?.Info("Restarting child process...");

                if (process != null && !process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit();
                }

                terminateJobObject?.Invoke();
                startProcess?.Invoke(realExePath, realArgs, workingDir);

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
                var exePath = Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(exePath);
                var restarter = Path.Combine(dir, "Servy.Restarter.exe");

                if (!File.Exists(restarter))
                {
                    logger?.Error("Servy.Restarter.exe not found.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = restarter,
                    Arguments = serviceName,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
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
        /// <summary>
        /// Creates a Windows Job Object to manage child processes lifetime and resource limits.
        /// </summary>
        /// <param name="logger">Logger to record errors and information.</param>
        /// <returns>True if the job object was created successfully, false otherwise.</returns>
        public bool CreateJobObject(ILogger logger)
        {
            try
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero)
                {
                    logger?.Error("Failed to create Job Object.");
                    return false;
                }

                // Setup job object limits here if needed (optional)
                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"Exception while creating Job Object: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Assigns a process to the created Job Object.
        /// </summary>
        /// <param name="process">Process wrapper for the process to assign.</param>
        /// <param name="logger">Logger to record errors and information.</param>
        /// <returns>True if assignment succeeded, false otherwise.</returns>
        public bool AssignProcessToJobObject(IProcessWrapper process, ILogger logger)
        {
            if (_jobHandle == IntPtr.Zero)
            {
                logger?.Error("Job Object not created yet.");
                return false;
            }

            try
            {
                bool success = AssignProcessToJobObject(_jobHandle, process.ProcessHandle);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    logger?.Error($"Failed to assign process to Job Object. Win32 Error: {error}");
                }
                return success;
            }
            catch (Exception ex)
            {
                logger?.Error($"Exception while assigning process to Job Object: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public void TerminateChildProcesses()
        {
            if (_jobHandle != IntPtr.Zero && _jobHandle != new IntPtr(-1))
            {
                NativeMethods.CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
        }

        #region Native Methods

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        #endregion

    }
}
