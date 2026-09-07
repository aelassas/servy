using System;
using System.Collections.Specialized;

namespace Servy.Core.Config
{
    /// <summary>
    /// Immutable result of <see cref="CoreSettingsLoader.Load(NameValueCollection)"/>: the connection
    /// string and AES key/IV file paths shared by every Servy composition root (UI, Manager, Service,
    /// Restarter, CLI).
    /// </summary>
    public sealed class CoreSettings
    {
        /// <summary>
        /// The SQLite connection string used to open the application database.
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// The file path where the AES encryption key is stored.
        /// </summary>
        public string AESKeyFilePath { get; }

        /// <summary>
        /// The file path where the AES initialization vector (IV) is stored.
        /// </summary>
        public string AESIVFilePath { get; }

        public CoreSettings(string connectionString, string aesKeyFilePath, string aesIVFilePath)
        {
            ConnectionString = connectionString;
            AESKeyFilePath = aesKeyFilePath;
            AESIVFilePath = aesIVFilePath;
        }
    }

    /// <summary>
    /// Single source of truth for reading the connection string and AES key/IV file paths from
    /// application settings. Every Servy composition root (UI, Manager, Service, Restarter, CLI)
    /// resolves these same three values and must use this loader instead of re-implementing the read.
    /// </summary>
    public static class CoreSettingsLoader
    {
        /// <summary>
        /// Reads <c>DefaultConnection</c>, <c>Security:AESKeyFilePath</c> and <c>Security:AESIVFilePath</c>
        /// from <paramref name="config"/>, falling back to the hardcoded <see cref="AppConfig"/> defaults
        /// when a key is absent, null, empty, or whitespace-only.
        /// </summary>
        /// <param name="config">
        /// The application settings collection (e.g. <see cref="System.Configuration.ConfigurationManager.AppSettings"/>).
        /// </param>
        /// <remarks>
        /// Unlike a plain <c>??</c> fallback, an empty string configured for a key (e.g. <c>DefaultConnection=""</c>)
        /// is treated as absent and falls back to the default, rather than being used as-is.
        /// </remarks>
        public static CoreSettings Load(NameValueCollection config)
        {
            var connectionString = Coalesce(config?["DefaultConnection"], AppConfig.DefaultConnectionString);
            var aesKeyFilePath = Coalesce(config?["Security:AESKeyFilePath"], AppConfig.DefaultAESKeyPath);
            var aesIVFilePath = Coalesce(config?["Security:AESIVFilePath"], AppConfig.DefaultAESIVPath);

            return new CoreSettings(connectionString, aesKeyFilePath, aesIVFilePath);
        }

        /// <summary>
        /// Throws if any value in <paramref name="settings"/> is null, empty, or whitespace-only.
        /// </summary>
        /// <param name="settings">The settings previously produced by <see cref="Load(NameValueCollection)"/>.</param>
        /// <param name="settingsFileName">The settings file name to mention in the exception message.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the connection string or AES key/IV paths are missing or empty.
        /// </exception>
        public static void Validate(CoreSettings settings, string settingsFileName)
        {
            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.ConnectionString) ||
                string.IsNullOrWhiteSpace(settings.AESKeyFilePath) ||
                string.IsNullOrWhiteSpace(settings.AESIVFilePath))
            {
                throw new InvalidOperationException(
                    $"Critical configuration values are missing. Ensure that the {settingsFileName} file is present and correctly configured.");
            }
        }

        /// <summary>
        /// Returns the specified string value if it is not <see langword="null"/>, empty, or consists only of white-space characters;
        /// otherwise, returns the provided fallback value.
        /// </summary>
        /// <param name="value">The string value to evaluate.</param>
        /// <param name="fallback">The default fallback string to return when <paramref name="value"/> is absent or whitespace.</param>
        /// <returns>
        /// <paramref name="value"/> if it contains non-whitespace content; otherwise, <paramref name="fallback"/>.
        /// </returns>
        /// <remarks>
        /// Unlike the standard null-coalescing operator (<c>??</c>), this method treats empty (<c>""</c>)
        /// and whitespace-only strings as absent values.
        /// </remarks>
        private static string Coalesce(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
