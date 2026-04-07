using Servy.Core.Data;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
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
        /// Retrieves the full command-line arguments for the current process.
        /// </summary>
        /// <returns>An array of strings containing the command-line arguments.</returns>
        string[] GetArgs();

        /// <summary>
        /// Parses the command-line arguments and loads the service configuration from the repository.
        /// </summary>
        /// <param name="serviceRepository">The repository used to fetch service-specific configurations.</param>
        /// <param name="fullArgs">The full set of command-line arguments to parse.</param>
        /// <returns>
        /// A <see cref="StartOptions"/> object if parsing is successful; otherwise, <c>null</c>.
        /// </returns>
        StartOptions? ParseOptions(IServiceRepository serviceRepository, string[] fullArgs);

        /// <summary>
        /// Performs a comprehensive validation of the startup options and logs the results.
        /// </summary>
        /// <remarks>
        /// This method logs the startup parameters (including sensitive data if debug logging is enabled) 
        /// and verifies that all critical paths and configurations are valid before the service starts.
        /// </remarks>
        /// <param name="options">The startup options to validate.</param>
        /// <param name="logger">The logger instance (typically a scoped/promoted logger) used for reporting.</param>
        /// <param name="fullArgs">The original command-line arguments for logging purposes.</param>
        /// <returns>
        /// <c>true</c> if the options are valid and the service can proceed; otherwise, <c>false</c>.
        /// </returns>
        bool ValidateAndLog(StartOptions options, ILogger logger, string[] fullArgs);

        /// <summary>
        /// Ensures the working directory specified in the options is valid.
        /// If not valid, sets a fallback working directory and logs a warning.
        /// </summary>
        /// <param name="options">The startup options containing the working directory to validate.</param>
        /// <param name="eventLog">The event log to write warnings to.</param>
        void EnsureValidWorkingDirectory(StartOptions options, ILogger logger);

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
        /// <param name="stopTimeoutMs">Timeout in milliseconds to wait for the process to stop.</param>
        void RestartProcess(
            IProcessWrapper process,
            Action<string, string, string, List<EnvironmentVariable>> startProcess,
            string realExePath,
            string realArgs,
            string workingDir,
            List<EnvironmentVariable> environmentVariables,
            ILogger logger,
            int stopTimeoutMs);

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
