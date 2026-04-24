using Servy.Core.Enums;
using System;
using System.IO;

namespace Servy.Core.Config
{
    /// <summary>
    /// Provides application-wide configuration.
    /// </summary>
    public static class AppConfig
    {
        #region Constants

        /// <summary>
        /// Servy's current version.
        /// </summary>
        public static readonly string Version = "8.3";

        /// <summary>
        /// The minimum required SQLite version to mitigate CVE-2025-6965.
        /// Version 3.50.2 introduced critical fixes for memory corruption.
        /// </summary>
        public static readonly Version MinRequiredSqliteVersion = new Version(3, 50, 2);

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
        /// Default timeout in seconds to wait for a Windows Service to stop. Default is 60 seconds.
        /// </summary>
        public const int DefaultServiceStopTimeoutSeconds = 60;

        /// <summary>
        /// The maximum character length for a Windows Service name.
        /// </summary>
        /// <remarks>
        /// This is a hard limit imposed by the Windows Service Control Manager (SCM).
        /// </remarks>
        public const int MaxServiceNameLength = 256;

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
        /// The name of the Windows service and the associated Event Log source.
        /// Used for service registration and writing logs to the Windows Event Viewer.
        /// </summary>
        public static readonly string EventSource = "Servy";

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
        /// The base file name (without extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIFileName = "Servy.Service.Net48";

        /// <summary>
        /// The full file name (with extension) of the Servy Service UI executable.
        /// </summary>
        public static readonly string ServyServiceUIExe = $"{ServyServiceUIFileName}.exe";

        /// <summary>
        /// Servy Desktop App Release Folder.
        /// </summary>
        public static readonly string ServyDesktopAppReleaseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Servy\bin\x64\Release\");

        /// <summary>
        /// Servy Service Debug Folder (Manager).
        /// </summary>
        public static readonly string ServyServiceManagerDebugFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Servy.Manager\bin\x64\Debug\");

        /// <summary>
        /// Servy Desktop App Publish Path.
        /// </summary>
        public static readonly string DesktopAppPublishReleasePath = Path.Combine(ServyDesktopAppReleaseFolder, "Servy.exe");

        /// <summary>
        /// Default Servy Desktop App Publish Path (Release).
        /// </summary>
        public static readonly string DefaultDesktopAppPublishPath = @".\Servy.exe";

        /// <summary>
        /// Servy Manager Release Folder.
        /// </summary>
        public static readonly string ServyManagerReleaseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Servy.Manager\bin\x64\Release\");

        /// <summary>
        /// Servy Manager App Publish Path.
        /// </summary>
        public static readonly string ManagerAppPublishReleasePath = Path.Combine(ServyManagerReleaseFolder, "Servy.Manager.exe");

        /// <summary>
        /// Default Servy Manager App Publish Path (Release).
        /// </summary>
        public static readonly string DefaultManagerAppPublishPath = @".\Servy.Manager.exe";

        /// <summary>
        /// The base file name (without extension) of the Servy Service CLI executable.
        /// </summary>
        public static readonly string ServyServiceCLIFileName = "Servy.Service.Net48.CLI";

        /// <summary>
        /// The full file name (with extension) of the Servy Service CLI executable.
        /// </summary>
        public static readonly string ServyServiceCLIExe = $"{ServyServiceCLIFileName}.exe";

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
        /// Default maximum number of lines that can be displayed in the console output.
        /// </summary>
        public static readonly int DefaultConsoleMaxLines = 20_000;

        /// <summary>
        /// Default dependencies tab refresh interval when not set in appsettings. Default is 800 ms.
        /// </summary>
        public static readonly int DefaultDependenciesRefreshIntervalInMs = 800;

        /// <summary>
        /// Extra buffer to ensure SCM doesn't kill the service before cleanup finishes.
        /// </summary>
        public static readonly int ScmTimeoutBufferSeconds = 15;

        /// <summary>
        /// Servy's official documentation link.
        /// </summary>
        public static readonly string DocumentationLink = "https://github.com/aelassas/servy/wiki";

        /// <summary>
        /// Latest GitHub release link.
        /// </summary>
        public static readonly string LatestReleaseLink = "https://github.com/aelassas/servy/releases/latest";

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
        /// Default SQLite connection string pointing to Servy.db in the database folder.
        /// </summary>
        public static readonly string DefaultConnectionString = $"Data Source={Path.Combine(DbFolderPath, "Servy.db")};Busy Timeout=5000;Journal Mode=WAL;Pooling=True;";

        /// <summary>
        /// Default AES key file path.
        /// </summary>
        public static readonly string DefaultAESKeyPath = Path.Combine(SecurityFolderPath, "aes_key.dat");

        /// <summary>
        /// Default AES IV file path.
        /// </summary>
        public static readonly string DefaultAESIVPath = Path.Combine(SecurityFolderPath, "aes_iv.dat");

        /// <summary>
        /// Default log rotation size in Megabytes (MB). Default is 10 MB.
        /// </summary>
        public const int DefaultRotationSize = 10;

        /// <summary>
        /// Default maximum number of rotated log files to keep by default. 
        /// A value of 0 indicates no limit (unlimited retention).
        /// </summary>
        public const int DefaultMaxRotations = 0;

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
        /// Default timeout in seconds before considering a pre-launch task as failed. Default is 30 seconds.
        /// </summary>
        public const int DefaultPreLaunchTimeoutSeconds = 30;

        /// <summary>
        /// Default number of retry attempts for pre-launch tasks. Default is 0 attempts.
        /// </summary>
        public const int DefaultPreLaunchRetryAttempts = 0;

        /// <summary>
        /// Default start timeout in seconds to wait for the process to start successfully before considering the startup as failed. Default is 10 seconds.
        /// </summary>
        public const int DefaultStartTimeout = 10;

        /// <summary>
        /// The default startup type for a new service.
        /// Default is <see cref="ServiceStartType.Automatic"/>.
        /// </summary>
        public const ServiceStartType DefaultStartupType = ServiceStartType.Automatic;

        /// <summary>
        /// The default CPU priority class for the managed process.
        /// Default is <see cref="ProcessPriority.Normal"/>.
        /// </summary>
        public const ProcessPriority DefaultPriority = ProcessPriority.Normal;

        /// <summary>
        /// The default interval used when date-based log rotation is enabled.
        /// Default is <see cref="DateRotationType.Daily"/>.
        /// </summary>
        public const DateRotationType DefaultDateRotationType = DateRotationType.Daily;

        /// <summary>
        /// Gets a value indicating whether size-based log rotation is enabled by default.
        /// Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableRotation = false;

        /// <summary>
        /// Gets a value indicating whether date-based log rotation is enabled by default.
        /// Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableDateRotation = false;

        /// <summary>
        /// Gets a value indicating whether process health monitoring (heartbeat checks) 
        /// is enabled by default. Default is <c>false</c>.
        /// </summary>
        public const bool DefaultEnableHealthMonitoring = false;

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
        /// Default stop timeout in seconds to wait for exit. Default is 5 seconds.
        /// </summary>
        public const int DefaultStopTimeout = 5;

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
        /// Set to 0 to run the pre-launch  hook in fire-and-forget mode.
        /// </summary>
        public const int MinPreLaunchTimeoutSeconds = 0;

        /// <summary>
        /// Minimum pre-launch retry attempts.
        /// </summary>
        public const int MinPreLaunchRetryAttempts = 0;

        /// <summary>
        /// Maximum timeout in seconds to wait for the process to start (24 hours).
        /// </summary>
        public const int MaxStartTimeout = 86400;

        /// <summary>
        /// Maximum timeout in seconds to wait for exit (24 hours).
        /// </summary>
        public const int MaxStopTimeout = 86400;

        /// <summary>
        /// Maximum allowed pre-stop timeout in seconds (24 hours).
        /// </summary>
        public const int MaxPreStopTimeoutSeconds = 86400;

        /// <summary>
        /// Maximum rotation size in MB (10 GB).
        /// </summary>
        public const int MaxRotationSize = 10240;

        /// <summary>
        /// Maximum number of rotated log files to keep.
        /// </summary>
        public const int MaxMaxRotations = 10_000;

        /// <summary>
        /// Minimum number of rotated log files to keep. 0 means unlimited.
        /// </summary>
        public const int MinMaxRotations = 0;

        /// <summary>
        /// Maximum heartbeat interval in seconds (24 hours).
        /// </summary>
        public const int MaxHeartbeatInterval = 86400;

        /// <summary>
        /// Maximum allowed failed health checks.
        /// </summary>
        public const int MaxMaxFailedChecks = int.MaxValue;

        /// <summary>
        /// Maximum allowed restart attempts.
        /// </summary>
        public const int MaxMaxRestartAttempts = int.MaxValue;

        /// <summary>
        /// Maximum pre-launch timeout in seconds (24 hours).
        /// </summary>
        public const int MaxPreLaunchTimeoutSeconds = 86400;

        /// <summary>
        /// Maximum pre-launch retry attempts.
        /// </summary>
        public const int MaxPreLaunchRetryAttempts = int.MaxValue;

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
        /// The maximum allowed character count for imported configuration payloads (JSON or XML).
        /// </summary>
        /// <remarks>
        /// This limit is a security measure to prevent Denial of Service (DoS) attacks via 
        /// memory exhaustion. 1,024,000 characters represent approximately 2MB of memory 
        /// usage for the raw string in UTF-16.
        /// </remarks>
        public const int MaxImportPayloadSizeChars = 1_024_000;

        /// <summary>
        /// Timeout in milliseconds to wait for an individual child process to exit after a termination signal.
        /// </summary>
        /// <remarks>
        /// A shorter timeout (2,000ms) is used here to prevent the "KillChildren" loop from hanging 
        /// if one specific sub-process is non-responsive.
        /// </remarks>
        public const int KillChildWaitMs = 2_000;

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
        public const int HandleExeTimeoutMs = 5_000;

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the full path to the Sysinternals Handle executable (<c>handle64.exe</c> or <c>handle.exe</c>)
        /// depending on the build configuration. In DEBUG mode, it looks in the application's base directory;
        /// in RELEASE mode, it looks in the ProgramData folder.
        /// </summary>
        /// <returns>The full path to the Handle executable.</returns>
        public static string GetHandleExePath()
        {
#if DEBUG
            var handleExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppConfig.HandleExeFileName}.exe");
#else
            var handleExePath  = Path.Combine(AppConfig.ProgramDataPath, $"{AppConfig.HandleExeFileName}.exe");
#endif
            return handleExePath;
        }

        /// <summary>
        /// Gets the absolute path to the Servy CLI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>ServyServiceCLI.exe</c>.</returns>
        public static string GetServyCLIServicePath()
        {
#if DEBUG
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppConfig.ServyServiceCLIFileName}.exe");
#else
            var wrapperExePath = Path.Combine(AppConfig.ProgramDataPath, $"{AppConfig.ServyServiceCLIFileName}.exe");
#endif
            return wrapperExePath;
        }

        /// <summary>
        /// Gets the absolute path to the Servy UI service executable.
        /// </summary>
        /// <remarks>
        /// In <c>DEBUG</c> builds, the path points to the executable located in the application’s base directory.
        /// In <c>RELEASE</c> builds, the path points to the executable located in the ProgramData folder.
        /// </remarks>
        /// <returns>The full file path to <c>ServyServiceUI.exe</c>.</returns>
        public static string GetServyUIServicePath()
        {
#if DEBUG
            var wrapperExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppConfig.ServyServiceUIFileName}.exe");
#else
            var wrapperExePath = Path.Combine(AppConfig.ProgramDataPath, $"{AppConfig.ServyServiceUIFileName}.exe");
#endif
            return wrapperExePath;
        }

        #endregion

    }
}
