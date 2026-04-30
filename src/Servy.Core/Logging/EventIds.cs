namespace Servy.Core.Logging
{
    /// <summary>
    /// Centralizes Windows Event Log IDs used across the Servy ecosystem.
    /// </summary>
    /// <remarks>
    /// This class ensures that both the core C# service and auxiliary PowerShell scripts 
    /// use a consistent taxonomy, enabling reliable filtering for SIEM and monitoring tools.
    /// </remarks>
    public static class EventIds
    {
        #region Core Service Ranges (1000 - 3099)

        /// <summary>
        /// Base ID for informational events emitted by the core service.
        /// Range: 1000 - 1099.
        /// </summary>
        public const int Info = 1000;

        /// <summary>
        /// Base ID for warning events emitted by the core service (e.g., transient failures).
        /// Range: 2000 - 2099.
        /// </summary>
        public const int Warning = 2000;

        /// <summary>
        /// Base ID for critical error events emitted by the core service.
        /// Range: 3000 - 3099.
        /// </summary>
        public const int Error = 3000;

        #endregion

        #region Specific Security & Migration Events

        /// <summary>Triggered when a protected key cannot be decrypted by the current provider.</summary>
        public const int KeyUnprotectFailed = 3001;

        /// <summary>Indicates a transient issue during an automated configuration migration.</summary>
        public const int TransientMigrationWarning = 3002;

        /// <summary>Logged when a configuration migration fails catastrophically and requires manual intervention.</summary>
        public const int PersistentMigrationFailure = 3003;

        #endregion

        #region Auxiliary & Scheduled Task Events

        /// <summary>Generic failure within a scheduled task automation script.</summary>
        public const int ScheduledTaskScriptError = 3103;

        /// <summary>Fatal failure due to a missing script dependency or module import error.</summary>
        public const int ScheduledTaskScriptDependencyError = 3104;

        #endregion

    }
}