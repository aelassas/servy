using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace Servy.Service.Helpers
{
    /// <summary>
    /// Defines methods to assist with service startup operations,
    /// including argument sanitization, logging, validation, and initialization of startup options.
    /// </summary>
    public interface IServiceHelper
    {
        /// <summary>
        /// Retrieves and sanitizes the command line arguments for the current process,
        /// trimming spaces and quotes.
        /// </summary>
        /// <returns>An array of sanitized argument strings.</returns>
        string[] GetSanitizedArgs();

        /// <summary>
        /// Logs the startup arguments and parsed options to the specified event log.
        /// </summary>
        /// <param name="eventLog">The event log to write entries to.</param>
        /// <param name="args">The raw command line arguments.</param>
        /// <param name="options">The parsed startup options.</param>
        void LogStartupArguments(ILogger logger, string[] args, StartOptions options);

        /// <summary>
        /// Ensures the working directory specified in the options is valid.
        /// If not valid, sets a fallback working directory and logs a warning.
        /// </summary>
        /// <param name="options">The startup options containing the working directory to validate.</param>
        /// <param name="eventLog">The event log to write warnings to.</param>
        void EnsureValidWorkingDirectory(StartOptions options, ILogger logger);

        /// <summary>
        /// Validates the essential startup options, such as executable path and service name,
        /// and logs errors to the event log if invalid.
        /// </summary>
        /// <param name="eventLog">The event log to write errors to.</param>
        /// <param name="options">The startup options to validate.</param>
        /// <returns>True if the options are valid; otherwise, false.</returns>
        bool ValidateStartupOptions(ILogger logger, StartOptions options);

        /// <summary>
        /// Initializes startup options by parsing command line arguments,
        /// logging them, and validating the resulting options.
        /// </summary>
        /// <param name="eventLog">The event log to write logs and errors to.</param>
        /// <returns>The initialized and validated <see cref="StartOptions"/>, or null if invalid.</returns>
        StartOptions InitializeStartup(ILogger logger);

        /// <summary>
        /// Attempts to restart the given process by:
        /// 1. Killing it if it's still running.
        /// 2. Cleaning up job resources (via <paramref name="terminateJobObject"/>).
        /// 3. Starting the process again with the original path, arguments, and working directory.
        /// </summary>
        /// <param name="process">The process wrapper to restart.</param>
        /// <param name="startProcess">Callback to restart the process.</param>
        /// <param name="realExePath">Path to the executable.</param>
        /// <param name="realArgs">Command-line arguments.</param>
        /// <param name="workingDir">Working directory for the process.</param>
        /// <param name="environmentVariables">Environment variables.</param>
        /// <param name="logger">Logger instance.</param>
        void RestartProcess(
            IProcessWrapper process,
            Action<string, string, string, List<EnvironmentVariable>> startProcess,
            string realExePath,
            string realArgs,
            string workingDir,
            List<EnvironmentVariable> environmentVariables,
            ILogger logger);

        /// <summary>
        /// Attempts to restart the Windows service associated with the current process.
        /// </summary>
        /// <param name="logger">Loggers.</param>
        /// <remarks>
        /// This should be used when the service is registered with the Service Control Manager.
        /// </remarks>
        void RestartService(ILogger logger, string serviceName);

        /// <summary>
        /// Restarts the computer.
        /// </summary>
        /// <param name="logger">Loggers.</param>
        /// <remarks>
        /// This operation requires appropriate privileges and will cause a system reboot.
        /// Use with extreme caution.
        /// </remarks>
        void RestartComputer(ILogger logger);

        /// <summary>
        /// Informs the Service Control Manager (SCM) that the service needs additional time to start,
        /// stop, pause, or continue before the operation is considered failed.
        /// </summary>
        /// <param name="service">The service instance.</param>
        /// <param name="milliseconds">
        /// The number of milliseconds to add to the service timeout. This value extends the default 
        /// SCM timeout for the current operation (e.g., OnStart or OnStop).
        /// </param>
        /// <param name="logger">Logger.</param>
        /// <remarks>
        /// Use this method in <see cref="OnStart"/>, <see cref="OnStop"/>, <see cref="OnPause"/>, 
        /// or <see cref="OnContinue"/> when the operation may take longer than the default SCM timeout.
        /// Calling this method has no effect if the service is not running under the SCM (for example, 
        /// during unit tests or console execution).
        /// </remarks>
        void RequestAdditionalTime(ServiceBase service, int milliseconds, ILogger logger);
    }
}
