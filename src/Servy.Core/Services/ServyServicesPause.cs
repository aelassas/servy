using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;

namespace Servy.Core.Services
{
    /// <summary>
    /// Stops every running Servy service and the Servy host service once, so that several binaries
    /// (<c>Servy.Service.exe</c>, <c>Servy.Service.CLI.exe</c>, <c>Servy.Host.exe</c>) can be replaced and the host reinstalled
    /// in one go, and starts them again once at the end.
    /// </summary>
    /// <remarks>
    /// Stopping and starting the services around each file would restart every service several times, which makes the
    /// desktop app, Servy Manager and <c>servy-cli</c> slow to start after an upgrade on a machine with many services.
    /// </remarks>
    public sealed class ServyServicesPause
    {
        private readonly IServiceHelper _serviceHelper;
        private readonly IServyHostInstaller _hostInstaller;
        private List<string> _stoppedServices = new List<string>();
        private bool _hostStopped;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyServicesPause"/> class.
        /// </summary>
        /// <param name="serviceHelper">Lists, stops and starts the Servy services.</param>
        /// <param name="hostInstaller">Lists the Servy services and stops and starts the Servy host service.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        public ServyServicesPause(IServiceHelper serviceHelper, IServyHostInstaller hostInstaller)
        {
            _serviceHelper = serviceHelper ?? throw new ArgumentNullException(nameof(serviceHelper));
            _hostInstaller = hostInstaller ?? throw new ArgumentNullException(nameof(hostInstaller));
        }

        /// <summary>
        /// Gets a value indicating whether the services are stopped by this instance.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Gets the services that were running and are started again by <see cref="ResumeAsync"/>.
        /// </summary>
        public IReadOnlyList<string> StoppedServices => _stoppedServices;

        /// <summary>
        /// Stops every running Servy service, then the Servy host service. Does nothing the second time.
        /// </summary>
        /// <param name="cancellationToken">A token that stops the waits.</param>
        /// <returns>A task that completes when the services and the host are stopped.</returns>
        /// <remarks>
        /// A service that is registered under the host's name but does not run the Servy host is never stopped (#7294).
        /// </remarks>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            if (IsPaused)
                return;

            IsPaused = true;
            _stoppedServices = _hostInstaller.GetRunningServyServices(_serviceHelper);
            if (_stoppedServices.Count > 0)
            {
                Logger.Info($"Stopping the running Servy services once for the update: {string.Join(", ", _stoppedServices)}");
                await _serviceHelper.StopServicesAsync(_stoppedServices, cancellationToken);
            }

            // The Servy services depend on the host, so it is stopped after them
            if (_hostInstaller.GetState() == ServyHostServiceState.ServyHost)
            {
                await _hostInstaller.StopAsync(cancellationToken);
                _hostStopped = true;
            }
        }

        /// <summary>
        /// Starts the Servy host service, when this instance stopped it, and then the services it stopped. Never throws:
        /// a failure is logged.
        /// </summary>
        /// <returns>A task that completes when the services were started.</returns>
        public async Task ResumeAsync()
        {
            if (!IsPaused)
                return;

            IsPaused = false;

            // Normally already running again: the caller reinstalls and starts it before resuming
            if (_hostStopped)
            {
                _hostStopped = false;
                try
                {
                    if (_hostInstaller.GetState() == ServyHostServiceState.ServyHost)
                        await _hostInstaller.StartAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.Error($"The '{AppConfig.ServyHostServiceName}' service failed to start after the update.", ex);
                }
            }

            if (_stoppedServices.Count == 0)
                return;

            try
            {
                Logger.Info($"Starting the Servy services stopped for the update: {string.Join(", ", _stoppedServices)}");

                // CancellationToken.None: a cancelled start-up must not leave the services stopped
                await _serviceHelper.StartServicesAsync(_stoppedServices, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Error($"{_stoppedServices.Count} Servy service(s) stopped for the update failed to start again.", ex);
            }
        }
    }
}
