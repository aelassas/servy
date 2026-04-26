using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace Servy.Service.Helpers
{
    /// <inheritdoc />
    public class ServiceHelper : IServiceHelper
    {
        #region Logging Security

        /// <summary>
        /// A collection of keywords used to identify potentially sensitive information 
        /// in configuration keys or environment variable names.
        /// </summary>
        private static readonly HashSet<string> SensitiveKeyWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Core Credentials ---
            "PASSWORD", "PWD", "PASSPHRASE", "PIN", "USERPWD",

            // --- Web & Mobile Auth (JWT/OAuth) ---
            "TOKEN", "AUTH", "CREDENTIAL", "BEARER", "JWT",
            "SESSION", "COOKIE", "CLIENT_SECRET",

            // --- Cloud & Infrastructure (AWS/Azure/GCP) ---
            "SECRET", "SAS", "ACCOUNTKEY", "ACCESSKEY", "SKEY",
            "SIGNATURE", "TENANT_ID", // Often sensitive when paired with secrets

            // --- Databases & Storage ---
            "CONNECTIONSTRING", "CONNSTR", "DSN", "DATABASE_URL",
            "PROVIDER_CONNECTION_STRING",

            // --- Cryptography & Identity ---
            "KEY", "PRIVATE", "CERTIFICATE", "CERT", "THUMBPRINT",
            "PFX", "PEM", "SALT", "PEPPER",

            // --- API Service Identifiers ---
            "API",          // Catches API_KEY, API_SECRET, etc.
            "APP_SECRET",
            "BROWSER_KEY",
            "WEBHOOK_URL"   // These often contain embedded tokens
        };

        /// <summary>
        /// A specialized regex for matching sensitive keys. 
        /// Uses the same boundary logic as MaskingRegex to avoid false positives like 'MONKEY_TYPE'.
        /// </summary>
        private static readonly Regex KeyMatcherRegex = new Regex(
            @"(?i)(?<![a-zA-Z0-9])(" + string.Join("|", SensitiveKeyWords.Select(Regex.Escape)) + @")(?![a-zA-Z0-9])",
            RegexOptions.Compiled,
            AppConfig.InputRegexTimeout);

        /// <summary>
        /// A compiled regular expression designed to identify and mask sensitive credentials 
        /// within raw command-line argument strings.
        /// </summary>
        /// <remarks>
        /// Added support for quoted values and preserved separators.
        /// The value pattern handles both quoted strings and standard tokens.
        /// </remarks>
        private static readonly Regex MaskingRegex = new Regex(
            // 1. Keyword: Negative lookarounds allow _, ., and - as valid boundaries without consuming them
            @"(?i)(?<![a-zA-Z0-9])(" + string.Join("|", SensitiveKeyWords.Select(Regex.Escape)) + @")(?![a-zA-Z0-9])" +

            // 2. Separator & Value (Two Branches)
            @"(?:" +
                // BRANCH A: Explicit Separators (:, =, /)
                // Aggressively consumes spaces for unquoted strings (e.g., "KEY=---BEGIN RSA---") 
                // as long as the next word isn't another CLI flag.
                @"(\s*[:=]\s*|/)" +
                @"(?:""[^""]*""|'[^']*'|(?:[^\s""']+(?:\s+(?![\-/]+[a-zA-Z])[^\s""']+)*))" +
                @"|" +
                // BRANCH B: Space Separator
                // Strictly consumes only a single unquoted word to prevent over-masking 
                // subsequent commands (e.g., protects 'run' in "--password secret run")
                @"(\s+)(?![\-/]+[a-zA-Z])" +
                @"(?:""[^""]*""|'[^']*'|[^\s""']+)" +
            @")",
            RegexOptions.Compiled,
            AppConfig.InputRegexTimeout);

        #endregion

        #region Constants

        /// <summary>
        /// The maximum time, in milliseconds, the restarter executable will wait for the 
        /// main service process to terminate.
        /// </summary>
        /// <remarks>
        /// Defaults to 240,000ms (4 minutes). This provides a significant buffer for 
        /// the service to perform a graceful shutdown, flush logs, and release 
        /// file handles before the restarter attempts to perform maintenance or a restart.
        /// </remarks>
        public const int RestarterExeMaxWaitMs = 240_000;

        #endregion

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
        public void LogStartupArguments(IServyLogger logger, string[] args, StartOptions options)
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
                  $"- stopTimeoutInSeconds: {options.StopTimeout}\n" +
                  $"- enableConsoleUI: {options.EnableConsoleUI}\n\n" +


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
                    $"- realArgs: {MaskRawArguments(options.ExecutableArgs)}\n\n" +

                    "--------Recovery (Sensitive)---\n" +
                    $"- failureProgramArgs: {MaskRawArguments(options.FailureProgramArgs)}\n\n" +

                    "--------Advanced (Sensitive)---\n" +
                    $"- environmentVariables: {envVarsFormatted}\n\n" +

                    "--------Pre-Launch (Sensitive)-\n" +
                    $"- preLaunchExecutableArgs: {MaskRawArguments(options.PreLaunchExecutableArgs)}\n" +
                    $"- preLaunchEnvironmentVariables: {preLaunchEnvVarsFormatted}\n\n" +

                    "--------Post-Launch (Sensitive)\n" +
                    $"- postLaunchExecutableArgs: {MaskRawArguments(options.PostLaunchExecutableArgs)}\n\n" +

                    "--------Pre-Stop (Sensitive)---\n" +
                    $"- preStopExecutableArgs: {MaskRawArguments(options.PreStopExecutableArgs)}\n\n" +

                    "--------Post-Stop (Sensitive)--\n" +
                    $"- postStopExecutableArgs: {MaskRawArguments(options.PostStopExecutableArgs)}\n"
                );
            }
        }

        /// <inheritdoc />
        public void EnsureValidWorkingDirectory(StartOptions options, IServyLogger logger)
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
        public StartOptions InitializeStartup(IServiceRepository serviceRepository, IProcessHelper processHelper, IServyLogger logger)
        {
            var fullArgs = _commandLineProvider.GetArgs();
            var options = StartOptionsParser.Parse(serviceRepository, processHelper, fullArgs);

            LogStartupArguments(logger, fullArgs, options);

            if (!ValidateStartupOptions(logger, processHelper, options))
            {
                return null;
            }

            return options;
        }

        /// <inheritdoc />
        public string[] GetArgs()
            => _commandLineProvider.GetArgs();

        /// <inheritdoc />
        public StartOptions ParseOptions(IServiceRepository serviceRepository, IProcessHelper processHelper, string[] fullArgs)
            => StartOptionsParser.Parse(serviceRepository, processHelper, fullArgs);

        /// <inheritdoc />
        public bool ValidateAndLog(StartOptions options, IServyLogger logger, IProcessHelper processHelper, string[] fullArgs)
        {
            LogStartupArguments(logger, fullArgs, options);

            if (!ValidateStartupOptions(logger, processHelper, options))
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
            IServyLogger logger,
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
                logger?.Error($"Failed to restart process.", ex);
            }
        }

        /// <inheritdoc />
        public void RestartService(IServyLogger logger, string serviceName)
        {
            try
            {
#if DEBUG
                var exePath = Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(exePath);
#else
                var dir = AppConfig.ProgramDataPath;
#endif

                if (string.IsNullOrWhiteSpace(dir))
                {
                    logger?.Error("Execution Aborted: The directory path for the restarter is invalid.");
                    return;
                }

                var restarter = Path.Combine(dir, "Servy.Restarter.Net48.exe");

                if (!File.Exists(restarter))
                {
                    logger?.Error("Servy.Restarter.Net48.exe not found.");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = restarter,
                    Arguments = Helper.Quote(serviceName),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        logger?.Error("Failed to start Servy.Restarter.Net48.exe.");
                        return;
                    }

                    // 1. Wait for the restarter to complete the Stop/Start cycle
                    if (!process.WaitForExit(RestarterExeMaxWaitMs))
                    {
                        logger?.Error("Servy.Restarter.Net48.exe timed out after 4 minutes. Forcing termination to prevent orphan conflicts.");

                        try
                        {
                            // 2. Kill the orphaned restarter
                            process.Kill();

                            // 3. Brief wait to ensure kernel cleanup is complete before we return control
                            if (!process.WaitForExit(3000))
                            {
                                logger?.Warn("Restarter killed, but kernel cleanup is taking longer than 3s.");
                            }
                        }
                        catch (Exception killEx)
                        {
                            logger?.Error($"Failed to kill orphaned restarter: {killEx.Message}");
                        }

                        return;
                    }

                    logger?.Info($"Servy.Restarter.Net48.exe exited with code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Failed to launch restarter.", ex);
            }
        }

        /// <inheritdoc />
        public void RestartComputer(IServyLogger logger)
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
                logger?.Error($"Failed to restart computer.", ex);
            }
        }

        /// <inheritdoc />
        public void RequestAdditionalTime(ServiceBase service, int milliseconds, IServyLogger logger)
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
                logger?.Error($"RequestAdditionalTime failed.", ex);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Converts a collection of environment variables into a single formatted string,
        /// automatically masking values for keys recognized as sensitive.
        /// </summary>
        /// <param name="vars">The collection of environment variables to process.</param>
        /// <returns>A semicolon-separated string of key-value pairs, or "None" if the collection is null.</returns>
        private string EnvironmentVariablesToString(IEnumerable<EnvironmentVariable> vars)
        {
            if (vars == null) return "None";

            return string.Join("; ", vars.Select(v =>
                $"{v.Name}={MaskSensitiveValue(v.Name, v.Value)}"));
        }

        /// <summary>
        /// Evaluates a key-value pair and returns a masked string if the key matches 
        /// known sensitive patterns.
        /// </summary>
        /// <param name="key">The name of the variable or setting.</param>
        /// <param name="value">The raw value to potentially mask.</param>
        /// <returns>The original value, or "********" if the key is deemed sensitive.</returns>
        private static string MaskSensitiveValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            // Use the strict key matcher to avoid greedy substring matches
            bool isSensitive = KeyMatcherRegex.IsMatch(key);

            return isSensitive ? "********" : value;
        }

        /// <summary>
        /// Uses a timed regular expression to identify and mask sensitive credentials 
        /// within a raw command-line argument string.
        /// </summary>
        /// <param name="args">The raw string of executable arguments.</param>
        /// <returns>A string with masked credentials, or the original string if no sensitive patterns are found.</returns>
        private string MaskRawArguments(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return args;

            try
            {
                return MaskingRegex.Replace(args, m =>
                {
                    // m.Groups[1] is the Keyword
                    // m.Groups[2] is the Explicit Separator (Branch A)
                    // m.Groups[3] is the Space Separator (Branch B)
                    string separator = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;

                    return $"{m.Groups[1].Value}{separator}********";
                });
            }
            catch (RegexMatchTimeoutException)
            {
                Logger.Warn("Regex timeout occurred while masking arguments. Output has been fully masked for security.");
                return "[MASKED DUE TO TIMEOUT]";
            }
        }

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
        private bool ValidateStartupOptions(IServyLogger logger, IProcessHelper processHelper, StartOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                logger?.Error("Executable path not provided.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.ServiceName))
            {
                logger?.Error("Service name empty");
                return false;
            }

            if (!processHelper.ValidatePath(options.ExecutablePath))
            {
                logger?.Error($"Process path {options.ExecutablePath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.FailureProgramPath) && !processHelper.ValidatePath(options.FailureProgramPath))
            {
                logger?.Error($"Failure program path {options.FailureProgramPath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PreLaunchExecutablePath) && !processHelper.ValidatePath(options.PreLaunchExecutablePath))
            {
                logger?.Error($"Pre-launch process path {options.PreLaunchExecutablePath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PostLaunchExecutablePath) && !processHelper.ValidatePath(options.PostLaunchExecutablePath))
            {
                logger?.Error($"Post-launch process path {options.PostLaunchExecutablePath} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.WorkingDirectory) && !processHelper.ValidatePath(options.WorkingDirectory, false))
            {
                logger?.Error($"Process working directory {options.WorkingDirectory} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.FailureProgramWorkingDirectory) && !processHelper.ValidatePath(options.FailureProgramWorkingDirectory, false))
            {
                logger?.Error($"Failure program working directory {options.FailureProgramWorkingDirectory} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PreLaunchWorkingDirectory) && !processHelper.ValidatePath(options.PreLaunchWorkingDirectory, false))
            {
                logger?.Error($"Pre-launch process working directory {options.PreLaunchWorkingDirectory} is invalid.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.PostLaunchWorkingDirectory) && !processHelper.ValidatePath(options.PostLaunchWorkingDirectory, false))
            {
                logger?.Error($"Post-launch process working directory {options.PostLaunchWorkingDirectory} is invalid.");
                return false;
            }

            return true;
        }

        #endregion
    }
}
