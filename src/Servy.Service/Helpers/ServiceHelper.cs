using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Service.CommandLine;
using Servy.Service.ProcessManagement;
using System.Diagnostics;
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
        /// SYNC WITH: setup/taskschd/ServySecurity.ps1 ($sensitiveKeys)
        /// </summary>
        private static readonly HashSet<string> SensitiveKeyWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Core Credentials ---
            "PASSWORD", "PWD", "PASSPHRASE", "PIN", "USERPWD",

            // --- Web & Mobile Auth (JWT/OAuth) ---
            "TOKEN", "AUTH", "CREDENTIAL", "BEARER", "JWT",
            "SESSION", "COOKIE", "CLIENT_SECRET", "PAT",

            // --- Cloud & Infrastructure (AWS/Azure/GCP) ---
            "SECRET", "SAS", "ACCOUNTKEY", "ACCESSKEY", "SKEY",
            "SIGNATURE", "TENANT_ID", // Often sensitive when paired with secrets

            // --- Databases & Storage ---
            "CONNECTIONSTRING", "CONNSTR", "DSN", "DATABASE_URL",
            "PROVIDER_CONNECTION_STRING", "DATABASE_PASSWORD",

            // --- Cryptography & Identity ---
            // Explicitly replaced broad "KEY" with target variants
            "PRIVATE_KEY", "SSH_KEY", "SECRET_KEY", "API_KEY",
            "CERTIFICATE", "CERT", "THUMBPRINT", "PFX", "PEM", "SALT", "PEPPER",

            // --- API Service Identifiers ---
            "API",          // Catches API_KEY, API_SECRET, etc.
            "APP_SECRET",
            "BROWSER_KEY",
            "WEBHOOK_URL",   // These often contain embedded tokens
            "KUBE_CONFIG",
            "TELEGRAM_TOKEN",
            "DISCORD_TOKEN"
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
        /// The value pattern handles both quoted strings and standard tokens, including
        /// multi-word values that do not look like subsequent CLI flags.
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
                // Consumes unquoted strings, supporting multi-word values (e.g., "my secret pass")
                // but stops consuming if it detects a subsequent CLI flag.
                @"(\s+)(?![\-/]+[a-zA-Z])" +
                @"(?:""[^""]*""|'[^']*'|(?:[^\s""']+(?:\s+(?![\-/]+[a-zA-Z])[^\s""']+)*))" +
            @")",
            RegexOptions.Compiled,
            AppConfig.InputRegexTimeout);

        #endregion

        #region Private Fields

        private readonly ICommandLineProvider _commandLineProvider;
        private readonly IProcessHelper _processHelper;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceHelper"/> class.
        /// </summary>
        /// <param name="commandLineProvider">The provider used to access system command line arguments.</param>
        /// <param name="processHelper">The process helper used for any necessary process-related operations during parsing.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="commandLineProvider"/> is null.</exception>
        public ServiceHelper(
            ICommandLineProvider commandLineProvider,
            IProcessHelper processHelper
            )
        {
            _commandLineProvider = commandLineProvider ?? throw new ArgumentNullException(nameof(commandLineProvider));
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
        }

        #endregion

        #region IServiceHelper implementation

        /// <inheritdoc />
        public void LogStartupArguments(IServyLogger? logger, StartOptions options)
        {
            if (options == null)
            {
                logger?.Error("StartOptions is null.");
                return;
            }

            // 1. PUBLIC DATA: Logged to both Local Log and Windows Event Log (logger?.Info)
            logger?.Info(
                  $"[Startup Parameters]\n" +
                  "--------Main-------------------\n" +
                  $"- serviceName: {options.ServiceName}\n" +
                  $"- realExePath: {options.ExecutablePath}\n" +
                  $"- workingDir: {options.WorkingDirectory}\n" +
                  $"- priority: {options.Priority}\n" +
                  $"- startTimeoutInSeconds: {options.StartTimeoutInSeconds}\n" +
                  $"- stopTimeoutInSeconds: {options.StopTimeoutInSeconds}\n" +
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
                  $"- recoveryOnCleanExit: {options.RecoveryOnCleanExit}\n" +
                  $"- maxRestartAttempts: {options.MaxRestartAttempts}\n" +
                  $"- failureProgramPath: {options.FailureProgramPath}\n" +
                  $"- failureProgramWorkingDirectory: {options.FailureProgramWorkingDirectory}\n\n" +

                  "--------Pre-Launch-------------\n" +
                  $"- preLaunchExecutablePath: {options.PreLaunchExecutablePath}\n" +
                  $"- preLaunchWorkingDirectory: {options.PreLaunchWorkingDirectory}\n" +
                  $"- preLaunchStdOutPath: {options.PreLaunchStdoutPath}\n" +
                  $"- preLaunchStdErrPath: {options.PreLaunchStderrPath}\n" +
                  $"- preLaunchTimeout: {options.PreLaunchTimeoutInSeconds}\n" +
                  $"- preLaunchRetryAttempts: {options.PreLaunchRetryAttempts}\n" +
                  $"- preLaunchIgnoreFailure: {options.PreLaunchIgnoreFailure}\n\n" +

                  "--------Post-Launch------------\n" +
                  $"- postLaunchExecutablePath: {options.PostLaunchExecutablePath}\n" +
                  $"- postLaunchWorkingDirectory: {options.PostLaunchWorkingDirectory}\n\n" +

                  "--------Pre-Stop-------------\n" +
                  $"- preStopExecutablePath: {options.PreStopExecutablePath}\n" +
                  $"- preStopWorkingDirectory: {options.PreStopWorkingDirectory}\n" +
                  $"- preStopTimeout: {options.PreStopTimeoutInSeconds}\n" +
                  $"- preStopLogAsError: {options.PreStopLogAsError}\n\n" +

                  "--------Post-Stop-------------\n" +
                  $"- postStopExecutablePath: {options.PostStopExecutablePath}\n" +
                  $"- postStopWorkingDirectory: {options.PostStopWorkingDirectory}\n"
            );

            // 2. SENSITIVE DATA: Logged to Local Text Logs ONLY (Servy.Service.log)
            // This is only triggered if EnableDebugLogs is true.
            if (options.EnableDebugLogs)
            {
                string envVarsFormatted = EnvironmentVariablesToString(options.EnvironmentVariables);
                string preLaunchEnvVarsFormatted = EnvironmentVariablesToString(options.PreLaunchEnvironmentVariables);

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
            // Check if the current directory is missing, malformed, or physically non-existent
            if (string.IsNullOrWhiteSpace(options.WorkingDirectory) ||
                !Helper.IsValidPath(options.WorkingDirectory) ||
                !Directory.Exists(options.WorkingDirectory))
            {
                // 1. Capture original value
                string originalValue = string.IsNullOrWhiteSpace(options.WorkingDirectory)
                    ? "[Empty]"
                    : options.WorkingDirectory;

                // 2. Establish the absolute floor (System32)
                string system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");

                // 3. derive fallback from ExecutablePath with explicit guards
                // This avoids ArgumentNullException on older runtimes and handles malformed roots
                string? exeDir = string.IsNullOrEmpty(options.ExecutablePath)
                    ? null
                    : Path.GetDirectoryName(options.ExecutablePath);

                // 4. Final safety check: if GetDirectoryName returned null or empty, use System32
                options.WorkingDirectory = string.IsNullOrEmpty(exeDir) ? system32 : exeDir!;

                // 5. Diagnostic logging with full context
                logger?.Warn($"Working directory '{originalValue}' is invalid or inaccessible. Falling back to '{options.WorkingDirectory}'.");
            }
        }

        /// <inheritdoc />
        public string[] GetArgs()
            => _commandLineProvider.GetArgs();

        /// <inheritdoc />
        public StartOptions? ParseOptions(IServiceRepository serviceRepository, string[] fullArgs)
            => StartOptionsParser.Parse(serviceRepository, _processHelper, fullArgs);

        /// <inheritdoc />
        public bool ValidateAndLog(StartOptions options, IServyLogger? logger, string[] fullArgs)
        {
            LogStartupArguments(logger, options);

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
                    IServyLogger logger,
                    int stopTimeoutMs)
        {
            if (startProcess == null) throw new ArgumentNullException(nameof(startProcess));

            try
            {
                logger?.Info("Restarting child process...");

                if (process != null)
                {
                    try
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

                        if (!process.HasExited)
                        {
                            process.Stop(stopTimeoutMs);
                        }

                        // Always sweep descendants -- orphans persist even after the parent exits.
                        try
                        {
                            process.StopDescendants(parentPid, parentStartTime, stopTimeoutMs);
                        }
                        catch (Exception ex)
                        {
                            logger?.Warn($"RestartProcess descendant cleanup failed: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Error("Failed to stop old child process; proceeding with launch anyway to avoid restart loop.", ex);
                    }
                }

                startProcess?.Invoke(realExePath, realArgs, workingDir, environmentVariables);

                logger?.Info("Process restarted.");
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to restart process.", ex);
            }
            finally
            {
                // Ensure the old process wrapper is disposed to prevent 
                // handle leaks during repeated recovery cycles.
                process?.Dispose();
            }
        }

        /// <inheritdoc />
        public void RestartService(IServyLogger logger, string serviceName)
        {
            try
            {
#if DEBUG
                var dir = AppFoldersHelper.GetAppDirectory();
#else
                var dir = AppConfig.ProgramDataPath;
#endif

                if (string.IsNullOrWhiteSpace(dir))
                {
                    logger?.Error("Execution Aborted: The directory path for the restarter is invalid.");
                    return;
                }

                var restarter = Path.Combine(dir, "Servy.Restarter.exe");

                if (!File.Exists(restarter))
                {
                    logger?.Error("Servy.Restarter.exe not found.");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = restarter,
                    Arguments = Helper.Quote(serviceName),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var process = _processHelper.Start(psi))
                {
                    if (process == null)
                    {
                        logger?.Error("Failed to start Servy.Restarter.exe.");
                        return;
                    }

                    // 1. Wait for the restarter to complete the Stop/Start cycle
                    if (!process.WaitForExit(AppConfig.RestarterExeMaxWaitMs))
                    {
                        logger?.Error($"Servy.Restarter.exe timed out after {AppConfig.RestarterExeMaxWaitMs / AppConfig.MillisecondsPerMinute} minutes. Forcing termination to prevent orphan conflicts.");

                        try
                        {
                            // 2. Kill the orphaned restarter
                            process.Kill();

                            // 3. Brief wait to ensure kernel cleanup is complete before we return control
                            if (!process.WaitForExit(AppConfig.RestarterKillGracePeriodMs))
                            {
                                logger?.Warn($"Restarter killed, but kernel cleanup is taking longer than {AppConfig.RestarterKillGracePeriodMs / AppConfig.MillisecondsPerSecond} seconds.");
                            }
                        }
                        catch (Exception killEx)
                        {
                            logger?.Error($"Failed to kill orphaned restarter: {killEx.Message}");
                        }

                        return;
                    }

                    logger?.Info($"Servy.Restarter.exe exited with code {process.ExitCode}.");
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
                var shutdownExe = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "shutdown.exe");

                using (var process = _processHelper.Start(new ProcessStartInfo
                {
                    FileName = shutdownExe,
                    Arguments = "/r /t 0 /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    // The using block ensures the native process handle is closed 
                    // immediately after the process is launched, preventing a 
                    // handle leak in the calling application.
                    if (process == null)
                    {
                        logger?.Error("Failed to launch shutdown.exe for RestartComputer; no process was started.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to restart computer.", ex);
            }
        }

        /// <inheritdoc />
        public void RequestAdditionalTime(ServiceBase service, int milliseconds, IServyLogger? logger)
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
            if (string.IsNullOrWhiteSpace(key)) return value;

            bool isSensitive;
            try
            {
                // Use the strict key matcher to avoid greedy substring matches
                isSensitive = KeyMatcherRegex.IsMatch(key);
            }
            catch (RegexMatchTimeoutException)
            {
                Logger.Warn($"Regex timeout while classifying key '{key}'. Defaulting to masked.");
                return "********";
            }

            return isSensitive ? "********" : value;
        }

        /// <summary>
        /// Uses a timed regular expression to identify and mask sensitive credentials 
        /// within a raw command-line argument string.
        /// </summary>
        /// <param name="args">The raw string of executable arguments.</param>
        /// <returns>A string with masked credentials, or the original string if no sensitive patterns are found.</returns>
        private string? MaskRawArguments(string? args)
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
        /// Uses reflection to automatically validate any property decorated with ServicePathAttribute.
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
        private bool ValidateStartupOptions(IServyLogger? logger, StartOptions options)
        {
            // 1. Explicit check for ServiceName (not a path field)
            if (string.IsNullOrWhiteSpace(options.ServiceName))
            {
                logger?.Error("Service name empty");
                return false;
            }

            // 2. Reflective check for all path-based fields
            var pathFields = typeof(StartOptions).GetProperties()
                .Select(p => new
                {
                    Property = p,
                    Attr = p.GetCustomAttribute<ServicePathAttribute>()
                })
                .Where(x => x.Attr != null);

            foreach (var field in pathFields)
            {
                var property = field.Property;
                var attr = field.Attr;

                // Get the current path value from options
                var pathValue = property.GetValue(options) as string;
                bool isPathEmpty = string.IsNullOrWhiteSpace(pathValue);

                // Required check: Logic specifically matches the original error "not provided"
                if (attr!.Required && isPathEmpty)
                {
                    logger?.Error($"{attr.Label} not provided.");
                    return false;
                }

                // Validity check: Logic matches original error "{label} {path} is invalid."
                if (!isPathEmpty && !_processHelper.ValidatePath(pathValue, attr.IsFile))
                {
                    logger?.Error($"{attr.Label} {pathValue} is invalid.");
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
