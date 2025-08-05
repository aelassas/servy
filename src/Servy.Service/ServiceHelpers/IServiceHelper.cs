using System.Diagnostics;

namespace Servy.Service
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
    }
}
