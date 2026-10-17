using Servy.Core.Data;
using Servy.Core.Services;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// The machine-touching steps <see cref="Program"/> runs while it bootstraps a command: the SQLite version
    /// check, the elevation check, the database and vault folders, the extraction of the embedded binaries, and
    /// the console probes.
    /// </summary>
    /// <remarks>
    /// Production uses <see cref="CliRuntime"/>. Tests pass a fake to <see cref="Program.RunAsync"/> so the
    /// branches that depend on these steps can run without touching ProgramData, the registry, the Service
    /// Control Manager or a real console window.
    /// </remarks>
    internal interface ICliRuntime
    {
        /// <summary>
        /// Checks that the loaded SQLite library is not a version known to be vulnerable.
        /// </summary>
        /// <param name="detectedVersion">The SQLite version that was detected, or <see langword="null"/> when it could not be read.</param>
        /// <returns><see langword="true"/> when the version is safe to use; otherwise <see langword="false"/>.</returns>
        bool IsSqliteVersionSafe(out string? detectedVersion);

        /// <summary>
        /// Determines whether the current process runs with administrative privileges.
        /// </summary>
        /// <returns><see langword="true"/> when the process is elevated; otherwise <see langword="false"/>.</returns>
        bool IsAdministrator();

        /// <summary>
        /// Creates the database and security folders, registers the event source, and initializes the database.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string.</param>
        /// <param name="aesKeyFilePath">The path of the AES key file.</param>
        /// <param name="aesIVFilePath">The path of the AES IV file.</param>
        /// <param name="dbContext">The database context to initialize.</param>
        void EnsureDatabase(string connectionString, string aesKeyFilePath, string aesIVFilePath, IAppDbContext dbContext);

        /// <summary>
        /// Extracts the embedded service wrapper, restarter, host and Sysinternals binaries, then installs and starts
        /// the Servy host service.
        /// </summary>
        /// <param name="serviceRepository">The repository holding the installed services.</param>
        /// <param name="controllerFactory">Creates a service controller wrapper for a service name.</param>
        /// <param name="cancellationToken">A token that cancels the extraction and the waits.</param>
        /// <returns>A task that completes when the binaries are in place and the host was handled.</returns>
        Task EnsureServiceBinariesAsync(IServiceRepository serviceRepository, Func<string, IServiceControllerWrapper> controllerFactory, CancellationToken cancellationToken);

        /// <summary>
        /// Determines whether the process runs in a real, interactive console window.
        /// </summary>
        /// <returns><see langword="true"/> when a console that supports the loading animation is attached; otherwise <see langword="false"/>.</returns>
        bool IsRealConsole();

        /// <summary>
        /// Runs an action while a loading animation is shown on the console.
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <returns>A task that completes when the action completes.</returns>
        Task RunWithLoadingAnimationAsync(Func<Task> action);
    }
}
