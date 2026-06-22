using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Servy.Core.Config
{
    /// <summary>
    /// Provides application-wide configuration.
    /// </summary>
    public static class AppConfig
    {
        #region Application Info & Links

        /// <summary>
        /// Servy's current version.
        /// </summary>
        public static readonly string Version = typeof(AppConfig).Assembly.GetName().Version?.ToString(2) ?? "unknown";

        /// <summary>
        /// Gets the name of the Windows Event Log channel used for logging and querying.
        /// Default is "Application".
        /// </summary>
        public const string EventLogName = "Application";

        /// <summary>
        /// The name of the Windows service and the associated Event Log source.
        /// Used for service registration and writing logs to the Windows Event Viewer.
        /// </summary>
        public const string EventSource = "Servy";

        /// <summary>
        /// Servy's official documentation link.
        /// </summary>
        public const string DocumentationLink = "https://github.com/aelassas/servy/wiki";

        /// <summary>
        /// Latest GitHub release link.
        /// </summary>
        public const string LatestReleaseLink = "https://github.com/aelassas/servy/releases/latest";

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
#endif

        /// <summary>
        /// The default file name of the Sysinternals Handle executable used to detect
        /// processes holding handles to files. Typically <c>handle64.exe</c> on 64-bit systems.
        /// </summary>
        public const string HandleExeX64FileName = "handle64";

        /// <summary>
        /// Gets the full file name of the Sysinternals Handle executable (x64), including the ".exe" extension.
        /// </summary>
        public static readonly string HandleExeX64 = $"{HandleExeX64FileName}.exe";

        /// <summary>
        /// The default file name of the Sysinternals Handle executable used to detect
        /// processes holding handles to files. Typically <c>handle64a.exe</c> on ARM64 systems.
        /// </summary>
        public const string HandleExeARM64FileName = "handle64a";

        /// <summary>
        /// Gets the full file name of the Sysinternals Handle executable (ARM64), including the ".exe" extension.
        /// </summary>
        public static readonly string HandleExeARM64 = $"{HandleExeARM64FileName}.exe";

        /// <summary>
        /// The file name of the Servy.Core assembly (without extension).
        /// Used when copying or loading the core library dynamically.
        /// </summary>
        public const string ServyCoreDllName = "Servy.Core";

        /// <summary>
        /// The base file name (without extension) of the Servy Service UI executable.
        /// </summary>
        public const string ServyServiceUIFileName = "Servy.Service";

        /// <summary>
        /// The full file name (with extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIExe = $"{ServyServiceUIFileName}.exe";

#if DEBUG
        /// <summary>
        /// Servy Desktop App Release Folder.
        /// </summary>
        public static readonly string ServyDesktopAppReleaseFolder = Path.Combine(RepoRoot, "src", "Servy", "bin", "Release", TargetFramework, RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64");

        /// <summary>
        /// Servy Service Debug Folder (Manager).
        /// </summary>
        public static readonly string ServyServiceManagerDebugFolder = Path.Combine(RepoRoot, "src", "Servy.Manager", "bin", "Debug", TargetFramework);

        /// <summary>
        /// Servy Desktop App Publish Path.
        /// </summary>
        public static readonly string DesktopAppPublishReleasePath = Path.Combine(ServyDesktopAppReleaseFolder, "publish", "Servy.exe");

        /// <summary>
        /// Servy Manager Release Folder.
        /// </summary>
        public static readonly string ServyManagerReleaseFolder = Path.Combine(RepoRoot, "src", "Servy.Manager", "bin", "Release", TargetFramework, RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64");

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
        /// Path to the recovery folder containing service restart attempts files.
        /// </summary>
        public static readonly string RecoveryFolderPath = Path.Combine(ProgramDataPath, "recovery");

        /// <summary>
        /// Path to the logs folder containing log files.
        /// </summary>
        public static readonly string LogsFolderPath = Path.Combine(ProgramDataPath, "logs");

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
        /// The wall-clock minimum (floor), in seconds, applied by ServiceManager when a
        /// configured per-service stop timeout would otherwise be unreasonably short. Default is 60 seconds.
        /// </summary>
        public const int ScmStopTimeoutFloorSeconds = 60;

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
        /// The default value for <c>RunAsLocalSystem</c>. Default is <c>true</c>.
        /// </summary>
        public const bool DefaultRunAsLocalSystem = true;

        /// <summary>
        /// The default value for <c>EnableDebugLogs</c>. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableDebugLogs = false;

        /// <summary>The default rolling interval for Servy's own internal log when not set in appsettings. Default is None (no date-based rotation).</summary>
        public const DateRotationType DefaultLogRollingInterval = DateRotationType.None;

        /// <summary>
        /// The default value for <c>EnableHealthMonitoring</c>. Default is <c>false</c>.
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
        /// Default stop timeout in seconds to wait for the process to exit. Default is 5 seconds.
        /// </summary>
        /// <remarks>
        /// <see cref="ScmStopTimeoutFloorSeconds"/> is a separate
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
        /// Defines the safety buffer, in seconds, added to the configured service start 
        /// timeout when requesting additional time from the Service Control Manager (SCM). 
        /// This ensures the service has sufficient overhead to initialize without 
        /// being prematurely terminated by the SCM.
        /// </summary>
        public const int ScmStartupRequestBufferSeconds = 10;

        /// <summary>
        /// The wait hint in milliseconds sent to the Service Control Manager (SCM) during a Pre-Shutdown event.
        /// </summary>
        /// <remarks>
        /// This value informs Windows how long the service expects to take to finish its cleanup. 
        /// Servy defaults to 30,000ms (30 seconds) to allow for graceful shutdown of child processes 
        /// and flushing of pending log buffers.
        /// </remarks>
        public const int PreShutdownWaitHintMs = 30_000;

        /// <summary>Checkpoint pulse interval (ms) used while waiting for pre-shutdown teardown to complete.</summary>
        public const int PreShutdownPulseIntervalMs = 2_000;

        /// <summary>
        /// Default timeout in seconds before considering a pre-launch task as failed. Default is 30 seconds.
        /// </summary>
        public const int DefaultPreLaunchTimeoutSeconds = 30;

        /// <summary>
        /// Default number of retry attempts for pre-launch tasks. Default is 0 attempts.
        /// </summary>
        public const int DefaultPreLaunchRetryAttempts = 0;

        /// <summary>
        /// The default value for <c>PreLaunchIgnoreFailure</c>. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultPreLaunchIgnoreFailure = false;

        /// <summary>
        /// Represents the default timeout, in seconds, to wait before stopping a process or service.
        /// </summary>
        public const int DefaultPreStopTimeoutSeconds = 5;

        /// <summary>
        /// The default value for <c>PreStopLogAsError</c>. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultPreStopLogAsError = false;

        /// <summary>
        /// The default value for <c>EnableSizeRotation</c>. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableSizeRotation = false;

        /// <summary>
        /// The default value for <c>EnableDateRotation</c>. Default is <c>false</c>.
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
        /// <para>
        /// <b>CRITICAL SIDE EFFECT:</b> Changing this value introduces a global side effect 
        /// that extends beyond log file splitting boundaries. It directly dictates whether log entry line 
        /// headers are written using the host system's localized time zone context (<see cref="DateTime.Now"/>) 
        /// or Coordinated Universal Time (<see cref="DateTime.UtcNow"/>).
        /// </para>
        /// <para>
        /// Flipping this configuration will cause subsequent log lines within a single file to straddle 
        /// two distinct time-base formats (e.g., transitioning from a 'Z' suffix to a '+02:00' suffix), 
        /// which may complicate historical time-series aggregation, text parsing, and cross-environment 
        /// log stream diffing.
        /// </para>
        /// </remarks>
        public const bool DefaultUseLocalTimeForRotation = false;

        /// <summary>
        /// The hard timeout for the HttpClient used in update checks. 
        /// This acts as a global safety net for the request.
        /// </summary>
        /// <remarks>
        /// This value defines the absolute ceiling for network operations. If the server does not respond
        /// within this duration, the underlying <see cref="System.Net.Http.HttpClient"/> will forcibly 
        /// terminate the connection.
        /// </remarks>
        public const int UpdateCheckHttpTimeoutSeconds = 20;

        /// <summary>
        /// The cooperative cancellation timeout for update checks.
        /// NOTE: This value must be ≤ UpdateCheckHttpTimeoutSeconds.
        /// If this is greater than the HTTP timeout, the user will see a generic 
        /// connection error instead of the friendly 'Update check timed out' message.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value governs the application-level logic for cancelling pending tasks. By ensuring this
        /// is strictly less than <see cref="UpdateCheckHttpTimeoutSeconds"/>, we guarantee that the 
        /// system provides a deterministic, high-level timeout notification before the lower-level 
        /// transport layer reaches its hard limit.
        /// </para>
        /// <para>
        /// To ensure the relationship between <see cref="UpdateCheckHttpTimeoutSeconds"/> 
        /// and <see cref="UpdateCheckTimeoutSeconds"/> remains valid, there is a unit test
        /// that validates the constraint.
        /// </para>
        /// </remarks>
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
        /// throttled by a hardware-aware ceiling: <c>Math.Min(Environment.ProcessorCount * 2, DefaultMaxBulkOperationParallelism)</c>.
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

        /// <summary>
        /// Defines the minimum execution duration threshold (in milliseconds) required to keep the splash screen visible.
        /// </summary>
        /// <remarks>
        /// This boundary constant prevents visual stutter or jarring UI flashes on high-performance environments. 
        /// If the core application subsystem initialization sequence completes faster than this designated time window, 
        /// the layout engine introduces an artificial padding delay before transitioning to the primary application workspace.
        /// </remarks>
        public const int SplashMinDisplayThresholdMs = 1000;

        /// <summary>
        /// Specifies the artificial cooling padding duration (in milliseconds) enforced when sub-system initializations complete prematurely.
        /// </summary>
        /// <remarks>
        /// This constant is injected into the sleep pipeline when a quick application boot bypasses the minimum display 
        /// requirement threshold. It dampens the window transition, providing an intentional, smooth visual pause for the user.
        /// </remarks>
        public const int SplashMinDisplayPaddingMs = 500;

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
        /// A 2000ms timeout is used as a security measure to prevent Regular Expression Denial of Service (ReDoS) 
        /// attacks while providing enough headroom for complex service name or log filtering patterns.
        /// </remarks>
        public const int InputRegexTimeoutMs = 2_000;

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
        /// Specifies the maximum duration, in milliseconds, allowed for standard output and error streams 
        /// of <c>handle.exe</c> to completely flush and drain after a termination event.
        /// </summary>
        /// <remarks>
        /// This timeout provides a best-effort structural buffer to ensure that diagnostic traces are fully 
        /// written to logs before resources are cleaned up, preventing deadlocks if the underlying stream is blocked 
        /// during a forced <see cref="TimeoutException"/> escalation.
        /// </remarks>
        public const int HandleExeKillDrainTimeoutMs = 2_000;

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
        public const long MaxConfigFileSizeBytes = (long)MaxConfigFileSizeMB * BytesInMegabyte;

        /// <summary>
        /// Tolerance, in minutes, when comparing the embedded resource timestamp against an
        /// existing extracted file: the resource is treated as "newer" (and re-extracted)
        /// only if it is at least this much younger than the file on disk.
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

        /// <summary>
        /// The maximum recursion depth for processing nested <see cref="Exception.InnerException"/> chains.
        /// This prevents a <see cref="StackOverflowException"/> or infinite loops when formatting pathological exception structures.
        /// </summary>
        public const int LoggerMaxInnerExceptionDepth = 16;

        /// <summary>
        /// The maximum character length for a formatted exception string. 
        /// Excessively large stack traces or messages are truncated to prevent application memory pressure and excessive disk usage in log files.
        /// </summary>
        public const int LoggerMaxFormattedExceptionLength = 16384; // 16 KB cap to prevent log bloat

        /// <summary>
        /// The maximum character length permitted for a single Windows Event Log entry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The underlying Windows Event Log API has a strict per-entry size limit of approximately 31,839 characters.
        /// This constant enforces a truncation limit of 31,000 characters to provide a safety margin for multi-byte Unicode 
        /// characters and header metadata, ensuring that log writes do not fail due to message bloat.
        /// </para>
        /// <para>
        /// When a log message exceeds this limit, the <c>SafeWriteToEventLog</c> method in <see cref="EventLogLogger"/> will truncate the string 
        /// and append a "[truncated]" suffix to maintain forensic visibility while ensuring the write operation succeeds.
        /// </para>
        /// </remarks>
        public const int EventLogMessageMaxChars = 31_000;

        /// <summary>
        /// Specifies the default duration in milliseconds to wait for the operating system 
        /// to finalize the cleanup of a process and its descendant tree after a forced kill is issued.
        /// </summary>
        /// <remarks>
        /// This delay helps prevent race conditions where subsequent file system operations 
        /// (like re-extracting resources) fail because the OS hasn't fully released file 
        /// handles held by the terminated process tree.
        /// </remarks>
        public const int DefaultDescendantPostKillWaitMs = 3000;

        /// <summary>
        /// Specifies the delay in milliseconds to wait after registering the PRESHUTDOWN notification 
        /// with the Service Control Manager (SCM) to ensure the registration is fully processed.
        /// </summary>
        public const int PreShutdownRegistrationDelayMs = 500;

        /// <summary>
        /// Specifies the maximum duration in milliseconds to wait for the internal logger 
        /// to flush its final buffers to disk during service shutdown.
        /// </summary>
        public const int LoggerFlushTimeoutMs = 1500;

        /// <summary>
        /// Specifies the minimum safety buffer in seconds used to determine if a service 
        /// has reached a stable "Running" state after a recovery reset.
        /// </summary>
        public const int ConditionalResetStabilityBufferSeconds = 30;

        /// <summary>
        /// Specifies the fixed delay in milliseconds applied when scheduling service recovery 
        /// actions to prevent rapid restart loops.
        /// </summary>
        public const int RecoverySchedulingDelayMs = 5000;

        /// <summary>
        /// Specifies the absolute maximum duration in milliseconds added to the cumulative 
        /// process-tree timeout to protect the SCM from kernel-level hangs during termination.
        /// </summary>
        public const int SafeKillProcessSafetyBufferMs = 10_000;

        /// <summary>
        /// Specifies the interval in milliseconds between pulses during a safe-kill operation 
        /// where the service requests additional "Wait Hint" time from the SCM.
        /// </summary>
        public const int SafeKillProcessPulseIntervalMs = 5_000;

        /// <summary>
        /// Specifies the polling interval in milliseconds when asynchronously waiting for 
        /// a process to exit or reach a timeout threshold.
        /// </summary>
        public const int WaitForExitOrTimeoutDelayMs = 500;

        /// <summary>
        /// Specifies the thread yield duration in milliseconds required for the operating system 
        /// to finalize console attachment or control-handler registration.
        /// </summary>
        public const int ConsoleAttachYieldMs = 50;

        /// <summary>
        /// Specifies the polling interval in milliseconds used by the service restarter 
        /// when waiting for a service to transition out of a pending or transitional state.
        /// </summary>
        public const int ServiceRestarterPollIntervalMs = 500;

        /// <summary>
        /// Specifies the polling interval in milliseconds used to check if a managed process 
        /// has successfully exited during a teardown sequence.
        /// </summary>
        public const int ProcessExitPollIntervalMs = 500;

        /// <summary>
        /// Specifies the refresh interval in milliseconds for the CLI progress spinner 
        /// to maintain a smooth visual frame rate during long-running commands.
        /// </summary>
        public const int ConsoleSpinnerDelayMs = 100;

        /// <summary>
        /// Specifies the retry delay in milliseconds applied when a clipboard operation fails 
        /// due to a temporary COM locking exception.
        /// </summary>
        public const int ClipboardComRetryDelayMs = 50;

        /// <summary>
        /// The maximum time, in milliseconds, the service will wait for a tracked hook process tree 
        /// (pre-launch, post-launch, or pre-stop) to exit after a kill signal has been issued.
        /// </summary>
        /// <remarks>
        /// This timeout prevents the service teardown sequence from hanging indefinitely if an auxiliary 
        /// hook is blocked by uninterruptible I/O or a kernel-mode lock. When reached, the service 
        /// will log a warning and proceed with its own shutdown to remain responsive to the 
        /// Windows Service Control Manager (SCM).
        /// </remarks>
        public const int HookCleanupTimeoutMs = 5_000;

        /// <summary>
        /// The default timeout, in seconds, for service restart operations within the wrapper service.
        /// </summary>
        /// <remarks>
        /// Set to 120 seconds to ensure maximum resiliency for background operations. 
        /// This extended duration allows the restarter to wait out long 'Pending' transitions 
        /// (e.g., heavy I/O cleanup or database flushes) without triggering a timeout 
        /// exception in the host service.
        /// </remarks>
        public const int DefaultRestarterTimeoutSeconds = 120;

        /// <summary>
        /// The number of bytes read from the beginning of a file to generate a prefix digest. 
        /// 4096 bytes is the default to clear common application log headers and prologues.
        /// </summary>
        public const int FileIdentityPrefixBytes = 4096;

        /// <summary>
        /// The safety window, in seconds, used to detect Windows PID reuse during recursive process tree traversals.
        /// </summary>
        /// <remarks>
        /// Windows recycles Process IDs (PIDs) aggressively. Without an identity check, a recursive 
        /// operation might inadvertently target a new, unrelated process that has claimed the PID 
        /// of a recently terminated child or parent. 
        /// 
        /// This constant defines the maximum allowed drift between the creation time of a parent 
        /// and its detected children. If a "child" process has a <see cref="System.Diagnostics.Process.StartTime"/> 
        /// that is significantly older or younger than its parent beyond this threshold (accounting for 
        /// kernel-scheduling jitter), it is flagged as a recycled PID and excluded from the operation 
        /// to prevent "process suicide" or the accidental termination of system-critical infrastructure.
        /// </remarks>
        public const int PidReuseToleranceSeconds = 2;

        /// <summary>
        /// Milliseconds per second.
        /// </summary>
        public const int MillisecondsPerSecond = 1_000;

        /// <summary>
        /// Milliseconds per minute.
        /// </summary>
        public const int MillisecondsPerMinute = 60_000;

        /// <summary>
        /// Defines the consecutive failure limit before a file system access error is escalated from a 
        /// warning to a high-severity error message.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Telemetry Dampening Circuit:</b> In long-running background worker environments, transient filesystem 
        /// friction (such as temporary file locks from antivirus sweeps, backup software indexing, or active 
        /// remote management tailing tools) can cause intermittent deletion failures. Logging these as critical 
        /// immediately would pollute event streams with non-actionable noise.
        /// </para>
        /// <para>
        /// This constant establishes a threshold window. If the internal failure counter intercepts 
        /// <value>10</value> consecutive exceptions without a successful pass, the retention lifecycle agent 
        /// escalates the threat vector to an operational <c>Logger.Error</c> notification, warning administrators 
        /// that disk space allocation boundaries are no longer actively contained.
        /// </para>
        /// </remarks>
        public const int LogRotationDeletionFailureEscalationThreshold = 10;

        /// <summary>
        /// Defines the maximum number of structural replacement attempts allowed when performing an atomic file swap operation.
        /// </summary>
        /// <remarks>
        /// This boundary value is a reliability safeguard against transient I/O conflicts during the final file replacement phase. 
        /// Even though the file content is initially dumped into an isolated temporary file, the final transaction-which relies on 
        /// an NTFS transactional metadata swap via <see cref="File.Move(string, string, bool)"/>-can trigger race conditions with background 
        /// operating system filters. If a filter interceptor is actively evaluating the destination, this configuration permits up to 
        /// <value>3</value> consecutive execution passes before propagating a failure exception up the stack.
        /// </remarks>
        public const int WriteFileAtomicMaxRetries = 3;

        /// <summary>
        /// Specifies the defensive cooling or backoff duration (in milliseconds) introduced between subsequent transactional file move attempts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Win32 File Locking and Interception Dynamics:</b> When a new binary artifact or configuration profile is written to a storage volume, 
        /// the Windows Kernel file system filter manager alerts registered background engines. Aggressive real-time security scanners, 
        /// corporate endpoint detection agents (EDR), and local search indexing pipelines (such as Windows Search) immediately acquire short-lived, 
        /// shared read locks on the temporary payload to verify integrity.
        /// </para>
        /// <para>
        /// If these hooks are running concurrently when the application executes its atomic metadata swap, the operation is blocked with a 
        /// transient Win32 Error 5 (<see cref="UnauthorizedAccessException"/>) or Error 32 (<see cref="IOException"/> sharing violation). This 
        /// value establishes a <value>100</value> millisecond pause, providing an adequate operational window for automated system hooks 
        /// to conclude their evaluation and release their handles.
        /// </para>
        /// </remarks>
        public const int WriteFileAtomicRetryDelayMs = 100;

        /// <summary>
        /// The minimum interval, in seconds, before the same UI exception message 
        /// will trigger another modal error dialog.
        /// </summary>
        /// <remarks>
        /// This debounce window prevents a flood of repetitive error messages from 
        /// modal-locking the UI thread during recurring background task failures.
        /// </remarks>
        public const int UnexpectedErrorDialogDebounceSeconds = 15;

        /// <summary>
        /// The maximum allowed length for a full Windows file path, typically defined as 260 characters including the null terminator. 
        /// A limit of 259 is used to safely accommodate paths without overflowing the buffer.
        /// </summary>
        public const int WriteFileAtomicMaxPathLength = 259;

        /// <summary>
        /// Absolute cap (in seconds) on the stability window used by ConditionalResetRestartAttemptsAsync
        /// before the persistent restart-attempts counter is reset. Excludes the pre-launch budget.
        /// </summary>
        public const int ConditionalResetMaxThresholdSeconds = 3600;

        /// <summary>
        /// The maximum time, in milliseconds, that a logging thread will wait for an 
        /// in-flight log file rotation to complete before timing out and dropping the entry.
        /// </summary>
        public const int LogRotationWaitTimeoutMs = 15_000;

        /// <summary>
        /// Defines the maximum Process ID (PID) value reserved for critical Windows system processes 
        /// (e.g., Idle at 0, System at 4). These IDs must never be targeted for termination 
        /// to avoid system-level instability or accidental kernel-space interference.
        /// </summary>
        public const int MaxReservedSystemPid = 4;

        #endregion

        #region Manager Configuration Bounds

        /// <summary>Minimum allowed interval for the main service list refresh.</summary>
        public const int MinRefreshIntervalInSeconds = 1;
        /// <summary>Maximum allowed interval for the main service list refresh (1 hour).</summary>
        public const int MaxRefreshIntervalInSeconds = 3600;

        /// <summary>Minimum allowed interval for performance metric updates.</summary>
        public const int MinPerformanceRefreshIntervalInMs = 100;
        /// <summary>Maximum allowed interval for performance metric updates (5 minutes).</summary>
        public const int MaxPerformanceRefreshIntervalInMs = 300_000;

        /// <summary>Minimum allowed interval for log console tailing updates.</summary>
        public const int MinConsoleRefreshIntervalInMs = 100;
        /// <summary>Maximum allowed interval for log console tailing updates (5 minutes).</summary>
        public const int MaxConsoleRefreshIntervalInMs = 300_000;

        /// <summary>Minimum number of lines maintained in the log console buffer.</summary>
        public const int MinConsoleMaxLines = 100;

        /// <summary>Minimum allowed interval for refreshing service dependency trees.</summary>
        public const int MinDependenciesRefreshIntervalInMs = 100;
        /// <summary>Maximum allowed interval for refreshing service dependency trees (5 minutes).</summary>
        public const int MaxDependenciesRefreshIntervalInMs = 300_000;

        /// <summary>Minimum delay for search input debouncing to prevent UI flicker.</summary>
        public const int MinSearchDebounceDelayMs = 100;
        /// <summary>Maximum delay for search input debouncing.</summary>
        public const int MaxSearchDebounceDelayMs = 2000;

        /// <summary>Minimum number of parallel tasks allowed for bulk service operations.</summary>
        public const int MinMaxBulkOperationParallelism = 1;
        /// <summary>Maximum number of parallel tasks allowed for bulk service operations.</summary>
        public const int MaxMaxBulkOperationParallelism = 64;

        /// <summary>Minimum number of days to look back when fetching event logs.</summary>
        public const int MinLogsWindowDays = 1;
        /// <summary>Maximum number of days to look back when fetching event logs.</summary>
        public const int MaxLogsWindowDays = 30;

        /// <summary>
        /// Defines the maximum number of structural allocation attempts permitted when setting text into the system clipboard.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Win32 Shared Resource Architecture:</b> The Windows Clipboard is an unmanaged, session-wide shared resource. 
        /// When another process opens the clipboard data broker loop (e.g., an active remote desktop session, a password manager 
        /// clipboard clearing loop, or an intensive security scanning agent), subsequent allocation requests from other processes 
        /// are rejected with an explicit <see cref="System.Runtime.InteropServices.COMException"/> or <see cref="System.Runtime.InteropServices.ExternalException"/>.
        /// </para>
        /// <para>
        /// To mitigate these transient environment locks without triggering application crashes or freezing the UI dispatcher thread, 
        /// the copying pipeline employs an asynchronous spin-retry circuit. This constant caps the execution boundary to 
        /// <value>5</value> attempts before giving up and logging an operational warning.
        /// </para>
        /// </remarks>
        public const int ClipboardComMaxRetries = 5;

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
        /// Maximum pre-launch timeout in seconds (24 hours).
        /// </summary>
        public const int MaxPreLaunchTimeoutSeconds = 86_400;

        /// <summary>
        /// Maximum pre-launch retry attempts.
        /// </summary>
        public const int MaxPreLaunchRetryAttempts = 100_000;

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
        /// The absolute maximum delay allowed for database retry operations, in milliseconds.
        /// </summary>
        /// <remarks>
        /// This constant serves as the upper safety cap for the exponential backoff algorithm to 
        /// ensure that retries remain responsive and do not grow indefinitely during sustained 
        /// infrastructure failures.
        /// </remarks>
        public const int DbBackoffMaxMs = 5_000;

        /// <summary>
        /// Max DTOs per chunk when re-querying generated IDs after a batch upsert.
        /// Kept under SQLite's default SQLITE_MAX_VARIABLE_NUMBER (999) parameter limit.
        /// </summary>
        public const int DbBatchIdSyncChunkSize = 900;

        /// <summary>
        /// Interval at which the CPU metrics cache is pruned of dead/recycled process entries.
        /// </summary>
        public static readonly TimeSpan ProcessHelperPruneInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Safety cap for filename collision retries when creating rotated or unique log files.
        /// </summary>
        public const int RotatingStreamWriterMaxUniqueFilenameRetries = 10_000;

        /// <summary>
        /// The initial delay in milliseconds to wait before retrying a failed pre-launch process attempt.
        /// </summary>
        public const int PreLaunchRetryInitialDelayMs = 1_000;

        /// <summary>
        /// The maximum cumulative delay in milliseconds allowed between pre-launch process retry attempts.
        /// </summary>
        public const int PreLaunchRetryMaxDelayMs = 10_000;

        /// <summary>
        /// Maximum allowed recovery backoff delay in milliseconds for unhandled log tailer errors.
        /// </summary>
        public const int LogTailerMaxUnhandledErrorRecoveryDelayMs = 60_000;

        /// <summary>
        /// Throttles repeated unhandled tailer errors: only every Nth consecutive
        /// error is logged, to avoid flooding the log pipeline.
        /// </summary>
        public const int LogTailerErrorLogThrottlingInterval = 60;

        /// <summary>
        /// The frequency at which background monitoring tick errors are logged to the console to prevent log flooding.
        /// </summary>
        public const int MonitoringTickErrorLogThrottlingInterval = 10;

        #endregion

        #region Security & Encryption

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass the user password from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyPasswordEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string PasswordEnvVarName = "SERVY_PASSWORD";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass process parameters from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyProcessParametersEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string ProcessParametersEnvVarName = "SERVY_PROCESS_PARAMETERS";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass environment variables from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyEnvironmentVariablesEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string EnvironmentVariablesEnvVarName = "SERVY_ENVIRONMENT_VARIABLES";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass failure program parameters from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyFailureProgramParametersEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string FailureProgramParametersEnvVarName = "SERVY_FAILURE_PROGRAM_PARAMETERS";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass pre-launch parameters from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyPreLaunchParametersEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string PreLaunchParametersEnvVarName = "SERVY_PRE_LAUNCH_PARAMETERS";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass pre-launch environment variables from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyPreLaunchEnvironmentVariablesEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string PreLaunchEnvironmentVariablesEnvVarName = "SERVY_PRE_LAUNCH_ENVIRONMENT_VARIABLES";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass post-launch parameters from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyPostLaunchParametersEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string PostLaunchParametersEnvVarName = "SERVY_POST_LAUNCH_PARAMETERS";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass pre-stop parameters from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyPreStopParametersEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string PreStopParametersEnvVarName = "SERVY_PRE_STOP_PARAMETERS";

        /// <summary>
        /// Specifies the name of the environment variable used to securely pass post-stop parameters from the CLI.
        /// SYNC WITH: src/Servy.CLI/Servy.psm1 ($script:ServyPostStopParametersEnvVar)
        /// </summary>
        /// <remarks>
        /// Using an environment variable prevents sensitive credentials from being exposed in plain text 
        /// within command-line history, logs, or system process lists.
        /// </remarks>
        public const string PostStopParametersEnvVarName = "SERVY_POST_STOP_PARAMETERS";

        /// <summary>
        /// Controls whether the system will process legacy v1 (unauthenticated) ciphertexts.
        /// </summary>
        /// <remarks>
        /// This flag is maintained as a hardcoded compile-time constant <c>false</c> to mitigate ciphertext 
        /// downgrade attack vectors. Automated runtime migration windows are no longer supported. Older 
        /// configurations must be modernized by re-saving them through the Servy Manager UI or the CLI tool.
        /// </remarks>
        public const bool AllowLegacyV1Decryption = false;

        #endregion

        #region Native Error Codes

        /// <summary>Win32 ERROR_SERVICE_SPECIFIC_ERROR (1066); reported to the SCM so it reads dwServiceSpecificExitCode.</summary>
        public const int ServiceSpecificErrorCode = 1066;

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the full path to the Sysinternals Handle executable (<c>handle64.exe</c> on x64, or
        /// <c>handle64a.exe</c> on ARM64). In DEBUG mode, it looks in the application's base directory;
        /// in RELEASE mode, it looks in the ProgramData folder.
        /// </summary>
        /// <returns>The full path to the Handle executable.</returns>
        public static string GetHandleExePath() =>
            ResolveExe(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? HandleExeARM64FileName : HandleExeX64FileName);

        /// <summary>
        /// Gets the absolute path to the Servy CLI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>Servy.Service.CLI.exe</c>.</returns>
        public static string GetServyCLIServicePath() => ResolveExe(ServyServiceCLIFileName);

        /// <summary>
        /// Gets the absolute path to the Servy UI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>Servy.Service.exe</c>.</returns>
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
        /// <returns>The fully qualified path to the executable; callers must verify with File.Exists.</returns>
        /// <remarks>
        /// In <c>DEBUG</c> mode, this resolves to the application's base directory (the build output folder), 
        /// where the required executables are copied during the build. In <c>RELEASE</c> mode, it checks the application's 
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

        /// <summary>
        /// Traverses up the directory hierarchy from the specified starting point to locate the 
        /// repository root, identified by the presence of the <c>Servy.sln</c> file.
        /// </summary>
        /// <param name="startDir">The directory path where the upward search begins.</param>
        /// <returns>The full filesystem path to the directory containing the Servy solution file.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the root of the drive is reached without finding <c>Servy.sln</c> in any ancestor directory.
        /// </exception>
        /// <remarks>
        /// Primarily used by the DEBUG-only <c>RepoRoot</c> field and by unit tests
        /// (which run in all build configurations) to resolve solution-relative paths.
        /// </remarks>
        public static string FindRepoRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Servy.sln")))
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new InvalidOperationException("Servy.sln not found in any ancestor directory.");
        }

        #endregion
    }
}