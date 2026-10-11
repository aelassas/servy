using Servy.CLI.Helpers;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Infrastructure.Helpers;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// The production <see cref="ICliRuntime"/>: runs each bootstrap step against the real machine.
    /// </summary>
    internal sealed class CliRuntime : ICliRuntime
    {
        /// <summary>
        /// The base namespace where embedded resource files are located.
        /// Used for locating and extracting files such as the service executable.
        /// </summary>
        private const string ResourcesNamespace = "Servy.CLI.Resources";

        /// <summary>
        /// Checks that the loaded SQLite library is not a version known to be vulnerable.
        /// </summary>
        /// <param name="detectedVersion">The SQLite version that was detected, or <see langword="null"/> when it could not be read.</param>
        /// <returns><see langword="true"/> when the version is safe to use; otherwise <see langword="false"/>.</returns>
        public bool IsSqliteVersionSafe(out string? detectedVersion)
            => DatabaseValidator.IsSqliteVersionSafe(out detectedVersion);

        /// <summary>
        /// Creates the database and security folders, registers the event source, and initializes the database.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string.</param>
        /// <param name="aesKeyFilePath">The path of the AES key file.</param>
        /// <param name="aesIVFilePath">The path of the AES IV file.</param>
        /// <param name="dbContext">The database context to initialize.</param>
        public void EnsureDatabase(string connectionString, string aesKeyFilePath, string aesIVFilePath, IAppDbContext dbContext)
        {
            // Ensure db and security folders exist
            AppFoldersHelper.EnsureFolders(connectionString, aesKeyFilePath, aesIVFilePath);

            // Ensure event source exists
            Core.Helpers.Helper.EnsureEventSourceExists();

            // Initialize the database
            DatabaseInitializer.InitializeDatabase(dbContext, SQLiteDbInitializer.Initialize);
        }

        /// <summary>
        /// Extracts the embedded service wrapper, restarter, host and Sysinternals binaries, then installs and starts
        /// the Servy host service.
        /// </summary>
        /// <param name="serviceRepository">The repository holding the installed services.</param>
        /// <param name="controllerFactory">Creates a service controller wrapper for a service name.</param>
        /// <param name="cancellationToken">A token that cancels the extraction and the waits.</param>
        /// <returns>A task that completes when the binaries are in place and the host was handled.</returns>
        public Task EnsureServiceBinariesAsync(IServiceRepository serviceRepository, Func<string, IServiceControllerWrapper> controllerFactory, CancellationToken cancellationToken)
        {
            var bootstrapper = new ServiceBinariesBootstrapper(
                new EmbeddedResourceExtractor(new ResourceHelper(new ProcessKiller()), Assembly.GetExecutingAssembly(), ResourcesNamespace),
                new ServiceHelper(serviceRepository),
                new ServyHostInstaller(new WindowsServiceApi(), new Win32ErrorProvider(), new ServiceControllerProvider(controllerFactory)),
                serviceRepository,
                new ServyExePermissionsHardener(),
                RuntimeInformation.OSArchitecture);

            return bootstrapper.EnsureAsync(cancellationToken);
        }

        /// <summary>
        /// Determines whether the process runs in a real, interactive console window.
        /// </summary>
        /// <returns><see langword="true"/> when a console that supports the loading animation is attached; otherwise <see langword="false"/>.</returns>
        public bool IsRealConsole() => Program.IsRealConsole();

        /// <summary>
        /// Runs an action while a loading animation is shown on the console.
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <returns>A task that completes when the action completes.</returns>
        public Task RunWithLoadingAnimationAsync(Func<Task> action) => ConsoleHelper.RunWithLoadingAnimation(action);
    }
}
