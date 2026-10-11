using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// Puts the binaries the Servy services run in place before a command that needs them: Sysinternals Handle, the
    /// service wrapper, the restarter, the DLLs they load (Release builds) and the Servy host, then installs and starts
    /// the Servy host service.
    /// </summary>
    /// <remarks>
    /// Replacing the service wrapper, a DLL it loads or the host needs every running Servy service and the host
    /// stopped. They are stopped once for all the files and the host reinstall, and started once at the end.
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
        /// Grants the service accounts their access to newly extracted files.
        /// </summary>
        private readonly IServyExePermissionsHardener _hardener;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBinariesBootstrapper"/> class.
        /// </summary>
        /// <param name="extractor">Extracts the embedded binaries.</param>
        /// <param name="serviceHelper">Stops and starts the Servy services around the update.</param>
        /// <param name="hostInstaller">Installs, stops and starts the Servy host service.</param>
        /// <param name="serviceRepository">The repository holding the installed services.</param>
        /// <param name="hardener">Grants the service accounts their access to newly extracted files.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        public ServiceBinariesBootstrapper(
            IEmbeddedResourceExtractor extractor,
            IServiceHelper serviceHelper,
            IServyHostInstaller hostInstaller,
            IServiceRepository serviceRepository,
            IServyExePermissionsHardener hardener)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _hostInstaller = hostInstaller ?? throw new ArgumentNullException(nameof(hostInstaller));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _hardener = hardener ?? throw new ArgumentNullException(nameof(hardener));
        }

        /// <summary>
        /// Extracts the embedded binaries, then installs and starts the Servy host service and makes the installed
        /// services depend on it.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the extraction and the waits.</param>
        /// <returns>A task that completes when the binaries are in place and the host was handled.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the service wrapper, the restarter, a DLL they load or the host could not be extracted.
        /// </exception>
        public async Task EnsureAsync(CancellationToken cancellationToken)
        {
            // Copy Sysinternals from embedded resources
            if (!await _extractor.CopyAsync(AppConfig.HandleExeFileName, "exe", cancellationToken))
            {
                Logger.Warn($"Failed copying embedded resource: {AppConfig.HandleExeFileName}; process-tree handle features may be degraded.");
            }

            var pause = new ServyServicesPause(_serviceHelper, _hostInstaller);
            try
            {
                // Copy service executable from embedded resources
                var resourceItems = new List<ResourceItem>
                {
                    new ResourceItem{ FileNameWithoutExtension = AppConfig.ServyServiceCLIFileName, Extension= "exe"},
                    // The service wrapper launches the restarter from its own folder, so it is extracted next to it
                    new ResourceItem{ FileNameWithoutExtension = AppConfig.ServyRestarterFileName, Extension= "exe"},
                };

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
                // Copy *.dll from embedded resources
                resourceItems.AddRange(new List<ResourceItem>
                {
                    new ResourceItem{ FileNameWithoutExtension = "Dapper", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "e_sqlite3", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "Microsoft.Bcl.AsyncInterfaces", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "Newtonsoft.Json", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "Servy.Core", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "Servy.Infrastructure", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Buffers", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Collections.Immutable", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Data.SQLite", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Memory", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Numerics.Vectors", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Runtime.CompilerServices.Unsafe", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Security.AccessControl", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Security.Principal.Windows", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Threading.AccessControl", Extension= "dll" },
                    new ResourceItem{ FileNameWithoutExtension = "System.Threading.Tasks.Extensions", Extension= "dll" },
                });
#endif

                // Replacing the service wrapper, a DLL it loads or the host needs every running Servy service and
                // the host stopped. They are stopped once for all the files and the host reinstall, and started once
                // at the end.
                if (resourceItems.Any(item => _extractor.IsExtractionNeeded(item.FileNameWithoutExtension, item.Extension))
                    || _extractor.IsExtractionNeeded(AppConfig.ServyHostFileName, "exe"))
                {
                    await pause.PauseAsync(cancellationToken);
                }

                // Copy embedded resources
                if (!await _extractor.CopyAllAsync(resourceItems, cancellationToken))
                {
                    throw new InvalidOperationException($"Failed to extract embedded resources. " +
                        "CLI cannot start safely - see file log for details.");
                }

                // Copy the Servy host service
                if (!await _extractor.CopyAsync(AppConfig.ServyHostFileName, "exe", cancellationToken))
                {
                    throw new InvalidOperationException($"Failed to extract embedded resource '{AppConfig.ServyHostExe}'. " +
                        "CLI cannot start safely - see file log for details.");
                }

#if !DEBUG
                // A file newly extracted into the vault carries no grant for the service accounts, so grant them
                // their access to it again. Debug builds extract next to the executable instead.
                if (_extractor.HasCopiedResources)
                {
                    await _hardener.HardenServiceAccountsAsync(_serviceRepository, cancellationToken);
                }
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
