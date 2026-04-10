using Servy.Core.Config;

namespace Servy.Infrastructure.Helpers
{
    /// <summary>
    /// Provides methods to validate the integrity and security of the SQLite database engine.
    /// </summary>
    public static class DatabaseValidator
    {
        /// <summary>
        /// Validates the version of the SQLite engine currently loaded in the application environment.
        /// </summary>
        /// <param name="currentVersion">When this method returns, contains the version string of the loaded SQLite engine.</param>
        /// <returns>
        /// <see langword="true"/> if the detected version is greater than or equal to 
        /// <see cref="AppConfig.MinRequiredSqliteVersion"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This check is critical for mitigating memory corruption vulnerabilities (CVE-2025-6965) 
        /// found in SQLite versions prior to 3.50.2.
        /// </remarks>
        public static bool IsSqliteVersionSafe(out string? currentVersion)
        {
            return ValidateVersion(System.Data.SQLite.SQLiteConnection.SQLiteVersion, out currentVersion);
        }

        /// <summary>
        /// Validates a specific version string against the minimum security requirements.
        /// </summary>
        /// <param name="versionText">The raw version string to validate (e.g., "3.50.4").</param>
        /// <param name="currentVersion">When this method returns, contains the original <paramref name="versionText"/>.</param>
        /// <returns>
        /// <see langword="true"/> if the string is a valid version and meets security thresholds; 
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <example>
        /// <code>
        /// if (DatabaseValidator.ValidateVersion("3.50.4", out _)) { /* Safe */ }
        /// </code>
        /// </example>
        public static bool ValidateVersion(string? versionText, out string? currentVersion)
        {
            currentVersion = versionText;

            return Version.TryParse(versionText, out var sqlVersion) &&
                   sqlVersion >= AppConfig.MinRequiredSqliteVersion;
        }
    }
}