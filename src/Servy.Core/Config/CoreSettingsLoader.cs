namespace Servy.Core.Config
{
    /// <summary>
    /// Immutable result of <see cref="CoreSettingsLoader.Load"/>: the connection
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
    /// Single source of truth for the connection string and AES key/IV file paths shared by every
    /// Servy composition root (UI, Manager, Service, Restarter, CLI).
    /// </summary>
    /// <remarks>
    /// These settings are read-only: they are always the <see cref="AppConfig"/> defaults under
    /// <see cref="AppConfig.ProgramDataPath"/>, and the <c>DefaultConnection</c>,
    /// <c>Security:AESKeyFilePath</c> and <c>Security:AESIVFilePath</c> keys of an application
    /// <c>.exe.config</c> file are ignored. The database and the key must live in the vault that
    /// <see cref="Servy.Core.Security.ServyExePermissionsHardener"/> hardens; a relocated database or key
    /// would sit outside those ACLs, and every Servy process must agree on the same location.
    /// </remarks>
    public static class CoreSettingsLoader
    {
        /// <summary>
        /// Test-only replacement for the settings <see cref="Load"/> returns, so tests can point a
        /// Servy process at a temporary database and key instead of the machine's vault.
        /// <see langword="null"/> (the default) means the <see cref="AppConfig"/> defaults.
        /// Not reachable from any configuration file.
        /// </summary>
        internal static CoreSettings TestOverride { get; set; }

        /// <summary>
        /// Returns the core data-layer settings: always the <see cref="AppConfig"/> defaults
        /// (<see cref="AppConfig.DefaultConnectionString"/>, <see cref="AppConfig.DefaultAESKeyPath"/> and
        /// <see cref="AppConfig.DefaultAESIVPath"/>).
        /// </summary>
        /// <returns>The resolved <see cref="CoreSettings"/>.</returns>
        public static CoreSettings Load()
            => TestOverride ?? new CoreSettings(AppConfig.DefaultConnectionString, AppConfig.DefaultAESKeyPath, AppConfig.DefaultAESIVPath);
    }
}
