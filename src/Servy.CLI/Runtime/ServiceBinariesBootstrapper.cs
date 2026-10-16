using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System.Runtime.InteropServices;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// Puts the binaries the Servy services run in place before a command that needs them: the service wrapper, the
    /// restarter, the Servy host and Sysinternals Handle, then installs and starts the Servy host service.
    /// </summary>
    /// <remarks>
    /// Replacing the service wrapper or the host needs every running Servy service and the host stopped. They are
    /// stopped once for all the files and the host reinstall, and started once at the end.
    /// </remarks>
    internal sealed class ServiceBinariesBootstrapper
    {
        /// <summary>
        /// Extracts the embedded binaries.
        /// </summary>
        private readonly IEmbeddedResourceExtractor _extractor;

        /// <summary>
        /// Stops and starts the Servy services around the update.
        /// </summary>
        private readonly IServiceHelper _serviceHelper;

        /// <summary>
        /// Installs, stops and starts the Servy host service.
        /// </summary>
        private readonly IServyHostInstaller _hostInstaller;

        /// <summary>
        /// The repository holding the installed services.
        /// </summary>
        private readonly IServiceRepository _serviceRepository;

        /// <summary>
        /// Purges the vault of non-administrative entries and grants the service accounts their access again.
        /// </summary>
        private readonly IServyExePermissionsHardener _hardener;

        /// <summary>
        /// The architecture of the operating system, which selects the Sysinternals Handle build to extract.
        /// </summary>
        private readonly Architecture _osArchitecture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBinariesBootstrapper"/> class.
        /// </summary>
        /// <param name="extractor">Extracts the embedded binaries.</param>
        /// <param name="serviceHelper">Stops and starts the Servy services around the update.</param>
        /// <param name="hostInstaller">Installs, stops and starts the Servy host service.</param>
        /// <param name="serviceRepository">The repository holding the installed services.</param>
        /// <param name="hardener">Purges the vault of non-administrative entries and grants the service accounts their access again.</param>
        /// <param name="osArchitecture">The architecture of the operating system.</param>
        /// <exception cref="ArgumentNullException">Thrown when a reference argument is <see langword="null"/>.</exception>
        public ServiceBinariesBootstrapper(
            IEmbeddedResourceExtractor extractor,
            IServiceHelper serviceHelper,
            IServyHostInstaller hostInstaller,
            IServiceRepository serviceRepository,
            IServyExePermissionsHardener hardener,
            Architecture osArchitecture)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _hostInstaller = hostInstaller ?? throw new ArgumentNullException(nameof(hostInstaller));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _hardener = hardener ?? throw new ArgumentNullException(nameof(hardener));
            _osArchitecture = osArchitecture;
        }

        /// <summary>
        /// Extracts the embedded binaries, then installs and starts the Servy host service and makes the installed
        /// services depend on it.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the extraction and the waits.</param>
        /// <returns>A task that completes when the binaries are in place and the host was handled.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the service wrapper, the restarter or the host could not be extracted.
        /// </exception>
        public async Task EnsureAsync(CancellationToken cancellationToken)
        {
            var pause = new ServyServicesPause(_serviceHelper, _hostInstaller);
            try
            {
                if (_extractor.IsExtractionNeeded(AppConfig.ServyServiceCLIFileName, "exe")
                    || _extractor.IsExtractionNeeded(AppConfig.ServyHostFileName, "exe"))
                {
                    await pause.PauseAsync(cancellationToken);
                }

                // Copy service executable from embedded resources
                if (!await _extractor.CopyAsync(AppConfig.ServyServiceCLIFileName, "exe", cancellationToken))
                {
                    throw new InvalidOperationException($"Failed to extract embedded resource '{AppConfig.ServyServiceCLIExe}'. " +
                        "CLI cannot start safely - see file log for details.");
                }

                // Copy the restarter next to the service wrapper, which launches it from its own folder.
                // No running service holds it open, so there is nothing to stop before overwriting it.
                if (!await _extractor.CopyAsync(AppConfig.ServyRestarterFileName, "exe", cancellationToken))
                {
                    throw new InvalidOperationException($"Failed to extract embedded resource '{AppConfig.ServyRestarterExe}'. " +
                        "CLI cannot start safely - see file log for details.");
                }

                // Copy the Servy host service
                if (!await _extractor.CopyAsync(AppConfig.ServyHostFileName, "exe", cancellationToken))
                {
                    throw new InvalidOperationException($"Failed to extract embedded resource '{AppConfig.ServyHostExe}'. " +
                        "CLI cannot start safely - see file log for details.");
                }

                // Copy Sysinternals from embedded resources
                var handleExeFileName = _osArchitecture == Architecture.Arm64
                    ? AppConfig.HandleExeARM64FileName
                    : AppConfig.HandleExeX64FileName;
                if (!await _extractor.CopyAsync(handleExeFileName, "exe", cancellationToken))
                {
                    Logger.Warn($"Failed copying embedded resource: {handleExeFileName}; process-tree handle features may be degraded.");
                }

#if DEBUG
                // Copy debug symbols from embedded resources (only in debug builds)
                if (!await _extractor.CopyAsync(AppConfig.ServyServiceCLIFileName, "pdb", cancellationToken))
                {
                    Logger.Warn($"Failed copying embedded resource: {AppConfig.ServyServiceCLIFileName}.pdb");
                }

                if (!await _extractor.CopyAsync(AppConfig.ServyRestarterFileName, "pdb", cancellationToken))
                {
                    Logger.Warn($"Failed copying embedded resource: {AppConfig.ServyRestarterFileName}.pdb");
                }

                if (!await _extractor.CopyAsync(AppConfig.ServyHostFileName, "pdb", cancellationToken))
                {
                    Logger.Warn($"Failed copying embedded resource: {AppConfig.ServyHostFileName}.pdb");
                }
#else
                // Purge every non-administrative entry in the vault, then grant the service accounts their access
                // again, newly extracted files included (#7526). Debug builds extract next to the executable instead.
                await _hardener.SecureVaultAsync(_serviceRepository, cancellationToken);
#endif
                // Install the Servy host service when it is missing, keep its startup type Automatic, and start it
                var hostResult = await _hostInstaller.EnsureInstalledAndRunningAsync(Path.Combine(_extractor.BaseExtractionDirectory, AppConfig.ServyHostExe), _serviceHelper, cancellationToken);
                if (hostResult.IsSuccess)
                {
                    // Services installed by an earlier version do not depend on the host yet
                    var installed = await _serviceRepository.GetAllAsync(decrypt: false, cancellationToken);
                    await _hostInstaller.EnsureServicesDependOnHostAsync(installed.Select(s => s.Name), cancellationToken);
                }
                else
                {
                    Logger.Warn($"{hostResult.ErrorMessage} Servy services cannot start until the '{AppConfig.ServyHostServiceName}' service runs.");
                }
            }
            finally
            {
                await pause.ResumeAsync();
            }
        }
    }
}
