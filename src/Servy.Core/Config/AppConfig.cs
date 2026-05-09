using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Reflection;

namespace Servy.Core.Config
{
    /// <summary>
    /// Provides application-wide configuration.
    /// </summary>
    public static class AppConfig
    {
        #region Static Constructor

        /// <summary>
        /// Initializes the <see cref="AppConfig"/> static members and performs 
        /// cross-field validation for configuration consistency.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="UpdateCheckTimeoutSeconds"/> exceeds <see cref="UpdateCheckHttpTimeoutSeconds"/>.
        /// This ensures the total task timeout provides enough buffer for the underlying HTTP request.
        /// </exception>
        static AppConfig()
        {
            // Invariant: The overall task timeout must be less than or equal to the HTTP client timeout 
            // to prevent orphaned requests or race conditions during update checks.
            if (UpdateCheckTimeoutSeconds > UpdateCheckHttpTimeoutSeconds)
            {
                throw new InvalidOperationException(
                    "UpdateCheckTimeoutSeconds must be <= UpdateCheckHttpTimeoutSeconds.");
            }
        }

        #endregion

        #region Application Info & Links

        /// <summary>
        /// Servy's current version.
        /// </summary>
        public static readonly string Version = "8.4";

        /// <summary>
        /// Gets the name of the Windows Event Log channel used for logging and querying.
        /// Default is "Application".
        /// </summary>
        public static readonly string EventLogName = "Application";

        /// <summary>
        /// The name of the Windows service and the associated Event Log source.
        /// Used for service registration and writing logs to the Windows Event Viewer.
        /// </summary>
        public static readonly string EventSource = "Servy";

        /// <summary>
        /// Servy's official documentation link.
        /// </summary>
        public static readonly string DocumentationLink = "https://github.com/aelassas/servy/wiki";

        /// <summary>
        /// Latest GitHub release link.
        /// </summary>
        public static readonly string LatestReleaseLink = "https://github.com/aelassas/servy/releases/latest";

        /// <summary>
        /// Command-line argument used to bypass hardware acceleration and force 
        /// the application to use software-based rendering.
        /// </summary>
        /// <remarks>
        /// This is primarily used to resolve "blank UI" issues in remote management 
        /// environments (like MeshCentral or specialized RDP configurations) where 
        /// the hardware DirectX pipeline cannot be correctly captured.
        /// </remarks>
        public const string ForceSoftwareRenderingArg = "--force-sr";

        /// <summary>
        /// The minimum required SQLite version to mitigate CVE-2025-6965.
        /// Version 3.50.2 introduced critical fixes for memory corruption.
        /// </summary>
        public static readonly Version MinRequiredSqliteVersion = new Version(3, 50, 2);

        #endregion

        #region File Paths & Directories

#if DEBUG
        /// <summary>
        /// The cached full filesystem path to the root of the repository. 
        /// This property is only available in <b>DEBUG</b> builds.
        /// </summary>
        /// <remarks>
        /// This path is resolved at runtime by traversing upward from the current 
        /// application base directory until the solution-level anchor is found. 
        /// It serves as the primary reference point for locating development-time assets 
        /// and solution-relative configurations.
        /// </remarks>
        private static readonly string RepoRoot = FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory);
#endif

        /// <summary>
        /// Retrieves the Target Framework Moniker (TFM) used during the build process.
        /// This value is injected via assembly metadata and is critical for locating
        /// binaries in DEBUG environments.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the 'BuiltWithFramework' metadata is missing, preventing reliable path resolution.
        /// </exception>
        private static readonly string TargetFramework =
            typeof(AppConfig).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuiltWithFramework")?.Value
            ?? throw new InvalidOperationException(
                "Assembly metadata 'BuiltWithFramework' is missing. " +
                "This attribute is required to resolve application binary paths. " +
                "Ensure the project file includes: <AssemblyMetadata Include=\"BuiltWithFramework\" Value=\"$(TargetFramework)\" />.");

        /// <summary>
        /// The default file name of the Sysinternals Handle executable used to detect
        /// processes holding handles to files. Typically <c>handle64.exe</c> on 64-bit systems.
        /// </summary>
        public static readonly string HandleExeFileName = "handle64";

        /// <summary>
        /// Gets the full file name of the Sysinternals Handle executable, including the ".exe" extension.
        /// </summary>
        public static readonly string HandleExe = $"{HandleExeFileName}.exe";

        /// <summary>
        /// The file name of the Servy.Core assembly (without extension).
        /// Used when copying or loading the core library dynamically.
        /// </summary>
        public static readonly string ServyCoreDllName = "Servy.Core";

        /// <summary>
        /// The base file name (without extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIFileName = "Servy.Service";

        /// <summary>
        /// The full file name (with extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIExe = $"{ServyServiceUIFileName}.exe";

#if DEBUG
        /// <summary>
        /// Servy Desktop App Release Folder.
        /// </summary>
        public static readonly string ServyDesktopAppReleaseFolder = Path.Combine(RepoRoot, "src", "Servy", "bin", "Release", TargetFramework, "win-x64");

        /// <summary>
        /// Servy Service Debug Folder (Manager).
        /// </summary>
        public static readonly string ServyServiceManagerDebugFolder = Path.Combine(RepoRoot, "src", "Servy.Manager", "bin", "Debug", TargetFramework, "win-x64");

        /// <summary>
        /// Servy Desktop App Publish Path.
        /// </summary>
        public static readonly string DesktopAppPublishReleasePath = Path.Combine(ServyDesktopAppReleaseFolder, "publish", "Servy.exe");

        /// <summary>
        /// Servy Manager Release Folder.
        /// </summary>
        public static readonly string ServyManagerReleaseFolder = Path.Combine(RepoRoot, "src", "Servy.Manager", "bin", "Release", TargetFramework, "win-x64");

        /// <summary>
        /// Servy Manager App Publish Path.
        /// </summary>
        public static readonly string ManagerAppPublishReleasePath = Path.Combine(ServyManagerReleaseFolder, "publish", "Servy.Manager.exe");
#endif

        /// <summary>
        /// Default Servy Desktop App Publish Path (Release).
        /// </summary>
        public static readonly string DefaultDesktopAppPublishPath = Path.Combine(AppFoldersHelper.GetAppDirectory(), "Servy.exe");

        /// <summary>
        /// Default Servy Manager App Publish Path (Release).
        /// </summary>
        public static readonly string DefaultManagerAppPublishPath = Path.Combine(AppFoldersHelper.GetAppDirectory(), "Servy.Manager.exe");

        /// <summary>
        /// The base file name (without extension) of the Servy Service CLI executable.
        /// </summary>
        public static readonly string ServyServiceCLIFileName = "Servy.Service.CLI";

        /// <summary>
        /// The full file name (with extension) of the Servy Service CLI executable.
        /// </summary>
        public static readonly string ServyServiceCLIExe = $"{ServyServiceCLIFileName}.exe";

        /// <summary>
        /// The root folder name under ProgramData.
        /// </summary>
        public static readonly string AppFolderName = "Servy";

        /// <summary>
        /// The full root path under ProgramData where Servy stores its data.
        /// </summary>
        public static readonly string ProgramDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName);

        /// <summary>
        /// Path to the database folder.
        /// </summary>
        public static readonly string DbFolderPath = Path.Combine(ProgramDataPath, "db");

        /// <summary>
        /// Path to the security folder containing AES key/IV files.
        /// </summary>
        public static readonly string SecurityFolderPath = Path.Combine(ProgramDataPath, "security");

        /// <summary>
        /// The default SQLite connection string for the Servy application.
        /// </summary>
        /// <remarks>
        /// This string configures the connection to <c>Servy.db</c> within the <see cref="DbFolderPath"/>. 
        /// It includes performance and resilience settings such as <c>Journal Mode=WAL</c> and a 5000ms busy timeout. 
        /// This value serves as the hardcoded fallback if no override is provided in the <c>ConnectionStrings:DefaultConnection</c> configuration of the application settings.
        /// </remarks>
        public static readonly string DefaultConnectionString = $"Data Source={Path.Combine(DbFolderPath, "Servy.db")};Busy Timeout=5000;Journal Mode=WAL;Pooling=True;";

        /// <summary>
        /// The default file path for the AES encryption key.
        /// </summary>
        /// <remarks>
        /// Points to <c>aes_key.dat</c> within the <see cref="SecurityFolderPath"/>. 
        /// This path is used by all Servy components (UI, CLI, and Service) to locate the master encryption key.
        /// This value serves as the hardcoded fallback if no override is provided in the <c>Security:AESKeyFilePath</c> configuration of the application settings.
        /// </remarks>
        public static readonly string DefaultAESKeyPath = Path.Combine(SecurityFolderPath, "aes_key.dat");

        /// <summary>
        /// The default file path for the AES initialization vector (IV).
        /// </summary>
        /// <remarks>
        /// Points to <c>aes_iv.dat</c> within the <see cref="SecurityFolderPath"/>. 
        /// Maintaining a single source of truth for this path prevents "split-brain" encryption issues between the configuration UI and the Windows Service.
        /// This value serves as the hardcoded fallback if no override is provided in the <c>Security:AESIVFilePath</c> configuration of the application settings.
        /// </remarks>
        public static readonly string DefaultAESIVPath = Path.Combine(SecurityFolderPath, "aes_iv.dat");

        #endregion

        #region Default Options & UI Tunables

        /// <summary>
        /// Default services refresh interval when not set in appsettings. Default is 4 seconds.
        /// </summary>
        public static readonly int DefaultRefreshIntervalInSeconds = 4;

        /// <summary>
        /// Default performance (CPU/RAM graphs) refresh interval when not set in appsettings. Default is 800 ms.
        /// </summary>
        public static readonly int DefaultPerformanceRefreshIntervalInMs = 800;

        /// <summary>
        /// Default console refresh interval when not set in appsettings. Default is 800 ms.
        /// </summary>
        public static readonly int DefaultConsoleRefreshIntervalInMs = 800;

        /// <summary>
        /// The default maximum number of log lines to retain in the console tab.
        /// </summary>
        /// <remarks>
        /// This value balances visibility of historical logs with application performance. 
        /// Increasing this beyond the default may lead to increased memory usage and UI virtualization lag 
        /// in the <see cref="Servy.Manager"/> views.
        /// </remarks>
        public static readonly int DefaultConsoleMaxLines = 20_000;

        /// <summary>
        /// The absolute hard limit for the console tab.
        /// </summary>
        /// <remarks>
        /// Set to twice the <see cref="DefaultConsoleMaxLines"/>, this constant prevents 
        /// exceptionally large log retention from causing OutOfMemory exceptions or 
        /// unresponsiveness in the WPF rendering thread.
        /// </remarks>
        public static readonly int MaxConsoleMaxLines = 2 * DefaultConsoleMaxLines;

        /// <summary>
        /// Default dependencies tab refresh interval when not set in appsettings. Default is 800 ms.
        /// </summary>
        public static readonly int DefaultDependenciesRefreshIntervalInMs = 800;

        /// <summary>
        /// Default Wait chunk in milliseconds. Used in pre-launch and pre-stop hooks.
        /// </summary>
        public const int DefaultWaitChunkMs = 5000;

        /// <summary>
        /// Specifies the default additional time, in milliseconds, used for Service Control Manager (SCM) operations.
        /// </summary>
        /// <remarks>This constant can be used to extend timeouts or delays when interacting with the
        /// Windows Service Control Manager to account for potential processing overhead.</remarks>
        public const int DefaultScmAdditionalTimeMs = 15_000;

        /// <summary>
        /// Extra buffer to ensure SCM doesn't kill the service before cleanup finishes.
        /// </summary>
        public static readonly int ScmTimeoutBufferSeconds = 15;

        /// <summary>
        /// The default maximum time (in seconds) to wait for a service to reach the 'Running' state.
        /// </summary>
        public const int DefaultServiceStartTimeoutSeconds = 30;

        /// <summary>
        /// The interval (in milliseconds) at which the Service Manager polls the SCM for status updates.
        /// </summary>
        public const int ScmPollIntervalMs = 500;

        /// <summary>
        /// The maximum number of concurrent threads used when querying the SCM for bulk service details.
        /// </summary>
        public const int MaxParallelScmQueries = 8;

        /// <summary>
        /// Default timeout in seconds to wait for a Windows Service to stop. Default is 60 seconds.
        /// </summary>
        public const int DefaultServiceStopTimeoutSeconds = 60;

        /// <summary>
        /// Populate native service details timeout in milliseconds. This is the maximum time allowed for retrieving native service details.
        /// </summary>
        public const int PopulateNativeDetailsTimeoutMs = 5000;

        /// <summary>
        /// The default startup type for a new service.
        /// Default is <see cref="ServiceStartType.Automatic"/>.
        /// </summary>
        public const ServiceStartType DefaultStartupType = ServiceStartType.Automatic;

        /// <summary>
        /// The default CPU priority class for the managed process.
        /// Default is <see cref="ProcessPriority.Normal"/>.
        /// </summary>
        public const ProcessPriority DefaultProcessPriority = ProcessPriority.Normal;

        /// <summary>
        /// The default value for <c>EnableConsoleUI</c>. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableConsoleUI = false;

        /// <summary>
        /// Gets a value indicating whether the service should run under the 
        /// LocalSystem account by default. Default is <c>true</c>.
        /// </summary>
        public const bool DefaultRunAsLocalSystem = true;

        /// <summary>
        /// Gets a value indicating whether verbose debug-level logging is enabled by default.
        /// Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableDebugLogs = false;

        /// <summary>
        /// Gets a value indicating whether process health monitoring (heartbeat checks) 
        /// is enabled by default. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableHealthMonitoring = false;

        /// <summary>
        /// Default heartbeat interval in seconds. Default is 30 seconds.
        /// </summary>
        public const int DefaultHeartbeatInterval = 30;

        /// <summary>
        /// Default maximum number of failed health checks before triggering an action. Default is 3 attempts.
        /// </summary>
        public const int DefaultMaxFailedChecks = 3;

        /// <summary>
        /// Default maximum number of restart attempts after a service failure. Default is 3 attempts.
        /// </summary>
        public const int DefaultMaxRestartAttempts = 3;

        /// <summary>
        /// Default recovery action.
        /// </summary>
        public const RecoveryAction DefaultRecoveryAction = RecoveryAction.RestartService;

        /// <summary>
        /// Default flag for running recovery action even if the process exits successfully.
        /// </summary>
        public const bool DefaultRecoveryOnCleanExit = false;

        /// <summary>
        /// Default start timeout in seconds to wait for the process to start successfully before considering the startup as failed. Default is 10 seconds.
        /// </summary>
        public const int DefaultStartTimeout = 10;

        /// <summary>
        /// Default stop timeout in seconds to wait for exit. Default is 5 seconds.
        /// </summary>
        /// <remarks>
        /// <see cref="DefaultServiceStopTimeoutSeconds"/> is a separate
        /// constant used by ServiceManager as a wall-clock floor when the
        /// configured per-service value would be unreasonably short.
        /// </remarks>
        public const int DefaultStopTimeout = 5;

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

        /// <summary>
        /// Minimum StartTimeout (in seconds) before requesting additional SCM time.
        /// Below this threshold the default SCM timeout is sufficient.
        /// </summary>
        public const int ScmStartupRequestThresholdSeconds = 20;

        /// <summary>
        /// The wait hint in milliseconds sent to the Service Control Manager (SCM) during a Pre-Shutdown event.
        /// </summary>
        /// <remarks>
        /// This value informs Windows how long the service expects to take to finish its cleanup. 
        /// Servy defaults to 30,000ms (30 seconds) to allow for graceful shutdown of child processes 
        /// and flushing of pending log buffers.
        /// </remarks>
        public const int PreShutdownWaitHintMs = 30_000;

        /// <summary>
        /// Default timeout in seconds before considering a pre-launch task as failed. Default is 30 seconds.
        /// </summary>
        public const int DefaultPreLaunchTimeoutSeconds = 30;

        /// <summary>
        /// Default number of retry attempts for pre-launch tasks. Default is 0 attempts.
        /// </summary>
        public const int DefaultPreLaunchRetryAttempts = 0;

        /// <summary>
        /// Gets a value indicating whether the service should continue starting if the 
        /// pre-launch executable fails. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultPreLaunchIgnoreFailure = false;

        /// <summary>
        /// Represents the default timeout, in seconds, to wait before stopping a process or service.
        /// </summary>
        public const int DefaultPreStopTimeoutSeconds = 5;

        /// <summary>
        /// Gets a value indicating whether failures in the pre-stop executable should 
        /// be logged as Errors. Default is <c>false</c> (logged as Warnings).
        /// </summary>
        public const bool DefaultPreStopLogAsError = false;

        /// <summary>
        /// Gets a value indicating whether size-based log rotation is enabled by default.
        /// Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableSizeRotation = false;

        /// <summary>
        /// Gets a value indicating whether date-based log rotation is enabled by default.
        /// Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableDateRotation = false;

        /// <summary>
        /// Default log rotation size in Megabytes (MB). Default is 10 MB.
        /// </summary>
        public const int DefaultRotationSizeMB = 10;

        /// <summary>
        /// Default maximum number of rotated log files to keep by default. 
        /// A value of 0 indicates no limit (unlimited retention).
        /// </summary>
        public const int DefaultMaxRotations = 0;

        /// <summary>
        /// The default interval used when date-based log rotation is enabled.
        /// Default is <see cref="DateRotationType.Daily"/>.
        /// </summary>
        public const DateRotationType DefaultDateRotationType = DateRotationType.Daily;

        /// <summary>
        /// Multiplier applied to <see cref="EventLogMaxResults"/> when prefetching raw events,
        /// to leave headroom for post-read provider/keyword filtering.
        /// </summary>
        public const int EventLogPrefetchCushion = 5;

        /// <summary>
        /// Default Log Level.
        /// </summary>
        public const LogLevel DefaultLogLevel = LogLevel.Info;

        /// <summary>
        /// The default value for <c>EnableEventLog</c>. Default is <c>true</c>.
        /// </summary>
        public const bool DefaultEnableEventLog = true;

        /// <summary>
        /// The default value for <c>UseLocalTimeForRotation</c>.
        /// </summary>
        /// <remarks>
        /// <para>Default is <c>false</c> (UTC).</para>
        /// <para>When set to <c>false</c>, log rotation intervals are calculated using Coordinated Universal Time (UTC). 
        /// This ensures a consistent, monotonic rotation schedule that is unaffected by Daylight Saving Time transitions.</para>
        /// </remarks>
        public const bool DefaultUseLocalTimeForRotation = false;

        /// <summary>
        /// The hard timeout for the HttpClient used in update checks. 
        /// This acts as a global safety net for the request.
        /// </summary>
        public const int UpdateCheckHttpTimeoutSeconds = 20;

        /// <summary>
        /// The cooperative cancellation timeout for update checks.
        /// NOTE: This value must be ≤ UpdateCheckHttpTimeoutSeconds.
        /// If this is greater than the HTTP timeout, the user will see a generic 
        /// connection error instead of the friendly 'Update check timed out' message.
        /// </summary>
        public const int UpdateCheckTimeoutSeconds = 10;

        /// <summary>
        /// The default log search window duration (3 days) used when no specific configuration value is found.
        /// </summary>
        public const int DefaultLogsWindowDays = 3;

        /// <summary>
        /// The interval in milliseconds used to poll for the existence of a target application's 
        /// directory (e.g., when waiting for a sidecar app to be installed or become available).
        /// </summary>
        /// <remarks>
        /// A value of 3000ms (3 seconds) provides a balance between UI responsiveness and 
        /// minimizing background I/O overhead.
        /// </remarks>
        public const int AppAvailabilityPollIntervalMs = 3000;

        /// <summary>
        /// The GitHub API endpoint used to retrieve the latest release metadata.
        /// </summary>
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/aelassas/servy/releases/latest";

        /// <summary>
        /// Default delay in milliseconds to debounce search keystrokes before filtering in console tab.
        /// </summary>
        public const int DefaultSearchDebounceDelayMs = 300;

        /// <summary>
        /// The default maximum number of concurrent Service Control Manager (SCM) operations 
        /// permitted during bulk lifecycle tasks (start, stop, or restart).
        /// </summary>
        /// <remarks>
        /// To prevent thread starvation and SCM contention, the actual degree of parallelism is 
        /// throttled by a hardware-aware ceiling: <c>Math.Min(Environment.ProcessorCount * 2, MaxBulkOperationParallelism)</c>.
        /// </remarks>
        public const int DefaultMaxBulkOperationParallelism = 8;

        /// <summary>
        /// The maximum number of log lines allowed to be loaded into Console tab at once during history loading.
        /// Prevents "Out of Memory" exceptions when opening consoles for services with massive log files.
        /// </summary>
        public const int LogTailerMaxSafeLines = 10_000;

        /// <summary>
        /// Number of lines to accumulate in the background tailer before flushing a batch to the UI dispatcher.
        /// Tuning this balances UI responsiveness against Garbage Collection (GC) pressure.
        /// </summary>
        public const int LogTailerBatchFlushThreshold = 500;

        /// <summary>
        /// The sleep duration in milliseconds before the tailing loop restarts after encountering 
        /// an unexpected, unhandled exception.
        /// </summary>
        public const int LogTailerUnhandledErrorRecoveryDelayMs = 1000;

        #endregion

        #region Limits, Thresholds & Constraints

        /// <summary>
        /// The maximum allowed length for a Windows service name.
        /// </summary>
        /// <remarks>
        /// According to Windows API specifications, the maximum length for a service name 
        /// is 256 characters. This limit applies to the service name itself, though 
        /// display names may have different constraints.
        /// </remarks>
        public const int MaxServiceNameLength = 256;

        /// <summary>
        /// Gets the maximum number of event records returned by an event log query.
        /// This prevents excessive memory consumption when querying large logs.
        /// </summary>
        public const int EventLogMaxResults = 10_000;

        /// <summary>
        /// The default timeout in milliseconds for regular expression matching operations.
        /// </summary>
        /// <remarks>
        /// A 200ms timeout is used as a security measure to prevent Regular Expression Denial of Service (ReDoS) 
        /// attacks while providing enough headroom for complex service name or log filtering patterns.
        /// </remarks>
        public const int InputRegexTimeoutMs = 200;

        /// <summary>
        /// Gets a <see cref="TimeSpan"/> representation of the <see cref="InputRegexTimeoutMs"/>.
        /// </summary>
        /// <remarks>
        /// This static readonly field is used to provide a pre-allocated <see cref="TimeSpan"/> object 
        /// for high-performance reuse in regex engine calls across the application.
        /// </remarks>
        public static readonly TimeSpan InputRegexTimeout = TimeSpan.FromMilliseconds(InputRegexTimeoutMs);

        /// <summary>
        /// Gets the timeout duration for Regex operations used to parse output from handle.exe.
        /// </summary>
        /// <remarks>
        /// A longer budget is intentional to accommodate cases where handle.exe produces voluminous output, 
        /// such as when a single large file is associated with thousands of owners or handles.
        /// </remarks>
        public static readonly TimeSpan HandleExeRegexTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Timeout in milliseconds to wait for an individual child process to exit after a termination signal.
        /// </summary>
        /// <remarks>
        /// A shorter timeout (2,000ms) is used here to prevent the "KillChildren" loop from hanging 
        /// if one specific sub-process is non-responsive.
        /// </remarks>
        public const int KillChildWaitMs = 2_000;

        /// <summary>
        /// The absolute minimum time (in milliseconds) the system will wait for a process to 
        /// exit after a kill command. This prevents race conditions in high-latency environments.
        /// </summary>
        public const int MinKillWaitMs = 1_000;

        /// <summary>
        /// Timeout in milliseconds to wait for an entire process tree to terminate.
        /// </summary>
        /// <remarks>
        /// A longer timeout (10_000ms) is allocated to allow Windows to clean up nested 
        /// process hierarchies and release associated kernel handles.
        /// </remarks>
        public const int KillTreeWaitMs = 10_000;

        /// <summary>
        /// Timeout in milliseconds to wait for a parent process to exit.
        /// </summary>
        public const int KillParentWaitMs = 5_000;

        /// <summary>
        /// Timeout in milliseconds for external handle-utility execution (e.g., handle.exe).
        /// </summary>
        public const int HandleExeTimeoutMs = 10_000;

        /// <summary>
        /// The maximum character length for a Windows Service display name.
        /// </summary>
        /// <remarks>
        /// This matches the SCM constraint for display strings in the Services console (services.msc).
        /// </remarks>
        public const int MaxDisplayNameLength = 256;

        /// <summary>
        /// The maximum permitted length for a service description.
        /// </summary>
        /// <remarks>
        /// While the registry can technically store larger strings, this limit prevents 
        /// unnecessary bloat in the Windows Registry and the local SQLite database.
        /// </remarks>
        public const int MaxDescriptionLength = 8192;

        /// <summary>
        /// The maximum permitted length for command-line arguments.
        /// </summary>
        /// <remarks>
        /// The theoretical Win32 limit for the CreateProcess argument string is 32,767 characters. 
        /// This value is set slightly lower to provide a safety margin for internal path canonicalization.
        /// </remarks>
        public const int MaxArgumentLength = 32000;

        /// <summary>
        /// Safety threshold that defines the maximum number of recursive expansion passes 
        /// allowed when resolving nested environment variables. This prevents infinite 
        /// loops caused by circular references.
        /// </summary>
        public const int MaxEnvVarExpansionPasses = 5;

        /// <summary>
        /// The maximum allowed length for a fully expanded environment variable string. 
        /// Set to 32,768 characters to align with the maximum environment variable 
        /// size limit on modern Windows systems.
        /// </summary>
        public const int MaxEnvVarExpandedLength = 32768;

        /// <summary>
        /// The maximum allowed size for imported configuration files (XML or JSON) in Megabytes.
        /// </summary>
        /// <remarks>
        /// This constant acts as a safety threshold to prevent "Out of Memory" exceptions or 
        /// Large Object Heap (LOH) fragmentation when parsing malformed or excessively large files.
        /// Default is 10 MB.
        /// </remarks>
        public const int MaxConfigFileSizeMB = 10;

        /// <summary>
        /// Authoritative byte count for configuration limits (Binary MiB: 1024 * 1024).
        /// </summary>
        public const long MaxConfigFileSizeBytes = (long)MaxConfigFileSizeMB * 1024 * 1024;

        /// <summary>
        /// The maximum age in minutes an extracted resource can be before it is considered stale.
        /// Time delta in minutes to consider an embedded resource as "newer" than an existing file
        /// </summary>
        public const int ResourceStalenessThresholdMinutes = 20;

        /// <summary>
        /// Bytes in a Megabyte (MB). Used for converting rotation size from MB to bytes.
        /// </summary>
        public const long BytesInMegabyte = 1024 * 1024;

        /// <summary>
        /// The maximum time in milliseconds to wait for the standard output and error streams 
        /// to drain after a process has exited. This prevents hanging the SCM thread if pipes 
        /// are stuck or asynchronous reads do not complete.
        /// </summary>
        public const int OutputDrainTimeoutMs = 5_000;

        /// <summary>
        /// The minimum duration to wait after a non-critical rotation failure (e.g., transient IO contention) 
        /// before attempting another rotation.
        /// </summary>
        public const int LogRotationCooldownMs = 1000;

        /// <summary>
        /// The duration the rotation circuit breaker remains tripped after a critical failure. 
        /// Defaults to 10 minutes to prevent log-storming and excessive CPU usage during persistent 
        /// infrastructure issues (e.g., unmounted drives or roaming profile sync locks).
        /// </summary>
        public const int LogRotationCriticalFailureCooldownMs = 600000; // 10 Minutes

        /// <summary>
        /// The maximum number of synchronous retries for the low-level <c>File.Move</c> operation 
        /// during a rotation event.
        /// </summary>
        public const int LogRotationMaxSyncRetries = 3;

        /// <summary>
        /// The delay between synchronous <c>File.Move</c> retry attempts.
        /// </summary>
        public const int LogRotationSyncRetryDelayMs = 50;

        #endregion

        #region Minimum Constraints

        /// <summary>
        /// Default minimum timeout in seconds to wait for the process to start successfully before considering the startup as failed. Default is 1 second.
        /// </summary>
        public const int MinStartTimeout = 1;

        /// <summary>
        /// Default minimum timeout in seconds to wait for exit. Default is 1 second.
        /// </summary>
        public const int MinStopTimeout = 1;

        /// <summary>
        /// Specifies the minimum allowed value, in seconds, for the pre-stop timeout setting.
        /// </summary>
        public const int MinPreStopTimeoutSeconds = 0;

        /// <summary>
        /// Minimum rotation size in MB.
        /// </summary>
        public const int MinRotationSize = 1;

        /// <summary>
        /// Minimum number of rotated log files to keep. 0 means unlimited.
        /// </summary>
        public const int MinMaxRotations = 0;

        /// <summary>
        /// Minimum heartbeat interval in seconds.
        /// </summary>
        public const int MinHeartbeatInterval = 5;

        /// <summary>
        /// Minimum max failed checks.
        /// </summary>
        public const int MinMaxFailedChecks = 1;

        /// <summary>
        /// Minimum max restart attempts. Set to 0 for unlimited.
        /// </summary>
        public const int MinMaxRestartAttempts = 0;

        /// <summary>
        /// Minimum pre-launch timeout in seconds.
        /// Set to 0 to run the pre-launch hook in fire-and-forget mode.
        /// </summary>
        public const int MinPreLaunchTimeoutSeconds = 0;

        /// <summary>
        /// Minimum pre-launch retry attempts.
        /// </summary>
        public const int MinPreLaunchRetryAttempts = 0;

        #endregion

        #region Maximum Constraints

        /// <summary>
        /// Maximum timeout in seconds to wait for the process to start (24 hours).
        /// </summary>
        public const int MaxStartTimeout = 86_400;

        /// <summary>
        /// Maximum timeout in seconds to wait for exit (24 hours).
        /// </summary>
        public const int MaxStopTimeout = 86_400;

        /// <summary>
        /// Maximum allowed pre-stop timeout in seconds (24 hours).
        /// </summary>
        public const int MaxPreStopTimeoutSeconds = 86_400;

        /// <summary>
        /// Maximum rotation size in MB (10 GB).
        /// </summary>
        public const int MaxRotationSize = 10_240;

        /// <summary>
        /// Maximum number of rotated log files to keep.
        /// </summary>
        public const int MaxMaxRotations = 10_000;

        /// <summary>
        /// Maximum heartbeat interval in seconds (24 hours).
        /// </summary>
        public const int MaxHeartbeatInterval = 86_400;

        /// <summary>
        /// Maximum allowed failed health checks.
        /// </summary>
        public const int MaxMaxFailedChecks = 100_000;

        /// <summary>
        /// Maximum allowed restart attempts.
        /// </summary>
        public const int MaxMaxRestartAttempts = 100_000;

        /// <summary>
        /// Maximum pre-launch timeout in seconds (1,000 seconds).
        /// </summary>
        public const int MaxPreLaunchTimeoutSeconds = 1_000;

        /// <summary>
        /// Maximum pre-launch retry attempts.
        /// </summary>
        public const int MaxPreLaunchRetryAttempts = int.MaxValue;

        #endregion

        #region Timing & Retry Policies

        /// <summary>
        /// Delay before retrying when the log file is not found (usually during initial startup).
        /// </summary>
        public const int LogTailerFileNotFoundRetryDelayMs = 1000;

        /// <summary>
        /// Delay before retrying after a file I/O error (sharing violation).
        /// </summary>
        public const int LogTailerIoErrorRetryDelayMs = 200;

        /// <summary>
        /// Polling interval to check for new lines when at the End Of File (EOF).
        /// </summary>
        public const int LogTailerEofPollIntervalMs = 150;

        /// <summary>
        /// Grace period in milliseconds to wait for a process to fully terminate and release kernel handles 
        /// before the restarter attempts to launch the new instance.
        /// </summary>
        public const int RestarterKillGracePeriodMs = 3000;

        /// <summary>
        /// Max retries for asynchronous SQLite operations encountering "Database is locked".
        /// </summary>
        public const int DbAsyncMaxRetries = 3;

        /// <summary>
        /// Initial exponential backoff delay for async database retries.
        /// </summary>
        public const int DbAsyncInitialDelayMs = 100;

        /// <summary>
        /// Maximum random jitter applied to database backoff to prevent thundering herd issues.
        /// </summary>
        public const int DbAsyncMaxJitterMs = 50;

        /// <summary>
        /// Max retries for synchronous SQLite operations (intentionally lower to avoid UI thread hangs).
        /// </summary>
        public const int DbSyncMaxRetries = 3;

        /// <summary>
        /// Initial delay for synchronous database retries.
        /// </summary>
        public const int DbSyncInitialDelayMs = 25;

        /// <summary>
        /// Max jitter for synchronous database retries.
        /// </summary>
        public const int DbSyncMaxJitterMs = 10;

        /// <summary>
        /// Interval at which the CPU metrics cache is pruned of dead/recycled process entries.
        /// </summary>
        public static readonly TimeSpan ProcessHelperPruneInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Safety cap for filename collision retries when creating rotated or unique log files.
        /// </summary>
        public const int RotatingStreamWriterMaxUniqueFilenameRetries = 10_000;

        #endregion

        #region Security & Encryption

        /// <summary>
        /// Controls whether the system will process legacy v1 (unauthenticated) ciphertexts.
        /// This should be enabled only during active migration from older Servy versions.
        /// Default is false to prevent ciphertext downgrade attacks.
        /// </summary>
        public static readonly bool AllowLegacyV1Decryption = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the full path to the Sysinternals Handle executable (<c>handle64.exe</c> or <c>handle.exe</c>)
        /// depending on the build configuration. In DEBUG mode, it looks in the application's base directory;
        /// in RELEASE mode, it looks in the ProgramData folder.
        /// </summary>
        /// <returns>The full path to the Handle executable.</returns>
        public static string GetHandleExePath() => ResolveExe(HandleExeFileName);

        /// <summary>
        /// Gets the absolute path to the Servy CLI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>ServyServiceCLI.exe</c>.</returns>
        public static string GetServyCLIServicePath() => ResolveExe(ServyServiceCLIFileName);

        /// <summary>
        /// Gets the absolute path to the Servy UI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>ServyServiceUI.exe</c>.</returns>
        public static string GetServyUIServicePath() => ResolveExe(ServyServiceUIFileName);

        /// <summary>
        /// Converts a size in megabytes (MB) to bytes.
        /// </summary>
        /// <param name="megabytes">The size in megabytes.</param>
        /// <returns>The size in bytes.</returns>
        public static long ToBytes(int megabytes) => (long)megabytes * BytesInMegabyte;

        #endregion

        #region Private Helpers

        /// <summary>
        /// Resolves the absolute path for an executable based on the current environment and build configuration.
        /// </summary>
        /// <param name="fileName">The base name of the executable (without the .exe extension).</param>
        /// <returns>The fully qualified path to the executable.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown in RELEASE mode if the executable cannot be found in the local directory or the ProgramData vault.
        /// </exception>
        /// <remarks>
        /// In <c>DEBUG</c> mode, this resolves to the resource directory within the repository root to facilitate 
        /// development without requiring manual file copying. In <c>RELEASE</c> mode, it checks the application's 
        /// base directory (supporting unit tests/portable use) before falling back to the hardened <c>ProgramData</c> vault.
        /// </remarks>
        private static string ResolveExe(string fileName)
        {
            string exeName = $"{fileName}.exe";

#if DEBUG
            // Development
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
#else
            // 1. Check local directory first (Critical for Unit Tests in Release mode and Portable execution)
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // 2. Fallback to the hardened vault (Production Service mode)
            return Path.Combine(ProgramDataPath, exeName);
#endif
        }


#if DEBUG
        /// <summary>
        /// Traverses up the directory hierarchy from the specified starting point to locate the 
        /// repository root, identified by the presence of the <c>Servy.sln</c> file.
        /// This method is only available in <b>DEBUG</b> builds.
        /// </summary>
        /// <param name="startDir">The directory path where the upward search begins.</param>
        /// <returns>The full filesystem path to the directory containing the Servy solution file.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the root of the drive is reached without finding <c>Servy.sln</c> in any ancestor directory.
        /// </exception>
        /// <remarks>
        /// This utility is primarily used by the bootstrapper and test harnesses to resolve 
        /// relative paths to assets or configuration files within the monorepo structure.
        /// </remarks>
        private static string FindRepoRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Servy.sln")))
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new InvalidOperationException("Servy.sln not found in any ancestor directory.");
        }
#endif

        #endregion
    }
}