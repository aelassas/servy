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

        #region PowerShell / Helper Script Ranges (1100 - 3199)

        /// <summary>
        /// Base ID for informational events emitted by setup or notification scripts.
        /// Range: 1100 - 1199.
        /// </summary>
        public const int ScriptInfo = 1100;

        /// <summary>
        /// Base ID for warning events emitted by setup or notification scripts.
        /// Range: 2100 - 2199.
        /// </summary>
        public const int ScriptWarning = 2100;

        /// <summary>
        /// Base ID for error events emitted by setup or notification scripts (e.g., SMTP failures).
        /// Range: 3100 - 3199.
        /// </summary>
        public const int ScriptError = 3100;

        #endregion
    }
}