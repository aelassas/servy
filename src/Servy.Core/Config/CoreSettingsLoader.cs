namespace Servy.Core.Config
{
    /// <summary>
    /// Provides the core data-layer settings (connection string and AES key/IV file paths) that every
    /// Servy process (Desktop, Manager, Service, Restarter, CLI) needs before it can open its
    /// <see cref="Servy.Core.Data.IAppDbContext"/> or construct its <see cref="Servy.Core.Security.ProtectedKeyProvider"/>.
    /// </summary>
    /// <remarks>
    /// These settings are read-only: they are always the <see cref="AppConfig"/> defaults under
    /// <see cref="AppConfig.ProgramDataPath"/>, and the <c>ConnectionStrings:DefaultConnection</c>,
    /// <c>Security:AESKeyFilePath</c> and <c>Security:AESIVFilePath</c> keys of an application settings
    /// file are ignored. The database and the key must live in the vault that
    /// <see cref="Servy.Core.Security.ServyExePermissionsHardener"/> hardens; a relocated database or key
    /// would sit outside those ACLs, and every Servy process must agree on the same location.
    /// </remarks>
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
        /// Test-only replacement for the settings <see cref="Load"/> returns, so tests can point a
        /// Servy process at a temporary database and key instead of the machine's vault.
        /// <see langword="null"/> (the default) means the <see cref="AppConfig"/> defaults.
        /// Not reachable from any settings file.
        /// </summary>
        internal static CoreSettings? TestOverride { get; set; }

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
