using Servy.Core.Common;

namespace Servy.Core.Services
{
    /// <summary>
    /// Installs, configures, starts and stops the Servy host service (<see cref="Config.AppConfig.ServyHostServiceName"/>),
    /// the Local System service that serves every Servy service its configuration over a named pipe.
    /// </summary>
    public interface IServyHostInstaller
    {
        /// <summary>
        /// Determines whether the Servy host service is installed and running (or starting).
        /// </summary>
        /// <returns><see langword="true"/> when it is running or start-pending.</returns>
        bool IsRunning();

        /// <summary>
        /// Stops the Servy host service when it is running and waits for it to stop. The Servy services depend on it,
        /// so the caller stops them first.
        /// </summary>
        /// <param name="cancellationToken">A token that stops the wait.</param>
        /// <returns>A task that completes when the service is stopped, or was not running.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service did not stop in time.</exception>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the Servy host service when it is not running and waits for it to run.
        /// </summary>
        /// <param name="cancellationToken">A token that stops the wait.</param>
        /// <returns>A task that completes when the service runs.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service did not start in time.</exception>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs the Servy host service when it is not installed, makes sure its startup type is Automatic and that it
        /// runs <paramref name="hostExePath"/> as Local System, and starts it when it is not running.
        /// </summary>
        /// <param name="hostExePath">The full path of <c>Servy.Host.exe</c>.</param>
        /// <param name="cancellationToken">A token that stops the operation.</param>
        /// <returns>The outcome; never throws except on cancellation.</returns>
        Task<OperationResult> EnsureInstalledAndRunningAsync(string hostExePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds the Servy host service to the dependencies of every given installed service that does not depend on it
        /// yet, keeping the dependencies it has. Services installed by an earlier version of Servy lack it.
        /// </summary>
        /// <param name="serviceNames">The names of the Servy services.</param>
        /// <param name="cancellationToken">A token checked between two services.</param>
        /// <returns>The number of services that were updated. A service that cannot be updated is logged and skipped.</returns>
        Task<int> EnsureServicesDependOnHostAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default);
    }
}
