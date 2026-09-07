using Microsoft.Extensions.Configuration;

namespace Servy.Core.Config
{
    /// <summary>
    /// Loads the core data-layer settings (connection string and AES key/IV file paths) that every
    /// Servy process (Desktop, Manager, Service, Restarter, CLI) needs before it can open its
    /// <see cref="Servy.Core.Data.IAppDbContext"/> or construct its <see cref="Servy.Core.Security.ProtectedKeyProvider"/>.
    /// </summary>
    public static class CoreSettingsLoader
    {
        /// <summary>
        /// The core data-layer settings required by every Servy process.
        /// </summary>
        /// <param name="ConnectionString">The SQLite connection string used to open <see cref="Servy.Core.Data.IAppDbContext"/>.</param>
        /// <param name="AESKeyFilePath">The file path of the AES key used by <see cref="Servy.Core.Security.ProtectedKeyProvider"/>.</param>
        /// <param name="AESIVFilePath">The file path of the AES IV used by <see cref="Servy.Core.Security.ProtectedKeyProvider"/>.</param>
        public sealed record CoreSettings(string ConnectionString, string AESKeyFilePath, string AESIVFilePath);

        /// <summary>
        /// Reads the core data-layer settings from <paramref name="config"/>, falling back to the
        /// <see cref="AppConfig"/> defaults for any value that is missing or empty. Does not validate
        /// the result; call <see cref="Validate"/> or use <see cref="LoadAndValidate"/> to do so.
        /// </summary>
        /// <param name="config">The application configuration to read from.</param>
        /// <returns>The resolved <see cref="CoreSettings"/>.</returns>
        public static CoreSettings Load(IConfiguration config)
        {
            var connectionString = Coalesce(config.GetConnectionString("DefaultConnection"), AppConfig.DefaultConnectionString);
            var aesKeyFilePath = Coalesce(config["Security:AESKeyFilePath"], AppConfig.DefaultAESKeyPath);
            var aesIVFilePath = Coalesce(config["Security:AESIVFilePath"], AppConfig.DefaultAESIVPath);

            return new CoreSettings(connectionString, aesKeyFilePath, aesIVFilePath);
        }

        /// <summary>
        /// Validates that none of <paramref name="settings"/>' values are missing or empty.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <param name="settingsFileName">The settings file name to mention in the exception message if validation fails.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the connection string or either AES file path is missing or empty even after
        /// applying the <see cref="AppConfig"/> defaults.
        /// </exception>
        public static void Validate(CoreSettings settings, string settingsFileName)
        {
            if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.AESKeyFilePath) || string.IsNullOrWhiteSpace(settings.AESIVFilePath))
            {
                throw new InvalidOperationException(
                    $"Critical configuration values are missing. Ensure that the {settingsFileName} file is present and correctly configured.");
            }
        }

        /// <summary>
        /// Reads and validates the core data-layer settings from <paramref name="config"/> in one call.
        /// </summary>
        /// <param name="config">The application configuration to read from.</param>
        /// <param name="settingsFileName">The settings file name to mention in the exception message if validation fails.</param>
        /// <returns>The resolved <see cref="CoreSettings"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the connection string or either AES file path is missing or empty even after
        /// applying the <see cref="AppConfig"/> defaults.
        /// </exception>
        public static CoreSettings LoadAndValidate(IConfiguration config, string settingsFileName)
        {
            var settings = Load(config);
            Validate(settings, settingsFileName);
            return settings;
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
        private static string Coalesce(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
