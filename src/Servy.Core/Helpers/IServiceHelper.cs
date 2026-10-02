using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides abstractions to query, start, and stop Servy services.
    /// </summary>
    public interface IServiceHelper
    {
        /// <summary>
        /// Gets the names of all currently running Servy services (GUI and CLI).
        /// </summary>
        /// <returns>A list of service names.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the Service Control Manager cannot be opened or queried.</exception>
        List<string> GetRunningServyServices();

        /// <summary>
        /// Starts the specified services if they are not already running or pending start,
        /// and waits until each service is fully running.
        /// </summary>
        /// <param name="services">A collection of service names to start.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous start operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="AggregateException">
        /// Thrown after every service has been attempted, when one or more of them failed to start or timed out;
        /// each inner exception is an <see cref="InvalidOperationException"/> naming the failed service.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled; the remaining services are not attempted.</exception>
        Task StartServicesAsync(IEnumerable<string> services, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the specified services if they are running or pending stop,
        /// and waits until each service is fully stopped.
        /// </summary>
        /// <param name="services">A collection of service names to stop.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous stop operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        /// <exception cref="AggregateException">
        /// Thrown after every service has been attempted, when one or more of them failed to stop or timed out;
        /// each inner exception is an <see cref="InvalidOperationException"/> naming the failed service.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled; the remaining services are not attempted.</exception>
        Task StopServicesAsync(IEnumerable<string> services, CancellationToken cancellationToken = default);
    }
}
