namespace Servy.Core
{
    /// <summary>
    /// Application-wide constants for Servy paths and identifiers.
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// Servy's current version.
        /// </summary>
        public const string Version = "1.0.0";

        /// <summary>
        /// Servy's official documentation link.
        /// </summary>
        public const string DocumentationLink = "https://github.com/aelassas/servy/wiki";

        /// <summary>
        /// Latest GitHub release link.
        /// </summary>
        public const string LatestReleaseLink = "https://github.com/aelassas/servy/releases/latest";

        /// <summary>
        /// The root folder name under ProgramData.
        /// </summary>
        public const string AppFolderName = "Servy";

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
        public static readonly string DefaultConnectionString = $@"Data Source={Path.Combine(DbFolderPath, "Servy.db")};";

        /// <summary>
        /// Default AES key file path.
        /// </summary>
        public static readonly string DefaultAESKeyPath = Path.Combine(SecurityFolderPath, "aes_key.dat");

        /// <summary>
        /// Default AES IV file path.
        /// </summary>
        public static readonly string DefaultAESIVPath = Path.Combine(SecurityFolderPath, "aes_iv.dat");

        /// <summary>
        /// Default log rotation size in bytes. Default is 10 MB.
        /// </summary>
        public static readonly int DefaultRotationSize = 10 * 1024 * 1024;

        /// <summary>
        /// Default heartbeat interval in seconds. Default is 30 seconds.
        /// </summary>
        public static readonly int DefaultHeartbeatInterval = 30;

        /// <summary>
        /// Default maximum number of failed health checks before triggering an action. Default is 3 attempts.
        /// </summary>
        public static readonly int DefaultMaxFailedChecks = 3;

        /// <summary>
        /// Default maximum number of restart attempts after a service failure. Default is 3 attempts.
        /// </summary>
        public static readonly int DefaultMaxRestartAttempts = 3;

        /// <summary>
        /// Default timeout in seconds before considering a pre-launch task as failed. Default is 30 seconds.
        /// </summary>
        public static readonly int DefaultPreLaunchTimeoutSeconds = 30;

        /// <summary>
        /// Default number of retry attempts for pre-launch tasks. Default is 0 attempts.
        /// </summary>
        public static readonly int DefaultPreLaunchRetryAttempts = 0;

    }
}
