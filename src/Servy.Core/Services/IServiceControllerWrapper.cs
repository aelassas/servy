using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;

namespace Servy.Core.Services
{
    /// <summary>
    /// Defines a contract for interacting with Windows Service Controller instances.
    /// </summary>
    public interface IServiceControllerWrapper : IDisposable
    {
        /// <summary>
        /// Gets the internal service name.
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets the human-readable display name of the service.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the status of the service as of the last <see cref="Refresh"/>.
        /// </summary>
        /// <remarks>
        /// The value is cached on first read and does not change until <see cref="Refresh"/> is
        /// called. <see cref="Start"/> and <see cref="Stop"/> do not invalidate it, so call
        /// <see cref="Refresh"/> before testing for a state transition.
        /// </remarks>
        ServiceControllerStatus Status { get; }

        /// <summary>
        /// Gets the startup type of the service.
        /// </summary>
        ServiceStartMode StartType { get; }

        /// <summary>
        /// Starts the service.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the service.
        /// </summary>
        void Stop();

        /// <summary>
        /// Discards the cached <see cref="Status"/>, <see cref="DisplayName"/> and
        /// <see cref="StartType"/> values so the next read queries the Service Control Manager again.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Waits for the service to reach the specified status within the given timeout.
        /// </summary>
        /// <param name="desiredStatus">The status to wait for.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);

        /// <summary>
        /// Gets the names of the services depended on by this service.
        /// </summary>
        /// <returns>An enumeration of service names depended on.</returns>
        IEnumerable<string> GetDependencyNames();

        /// <summary>
        /// Resolves the dependency hierarchy for this service.
        /// </summary>
        /// <param name="cancellationToken">A token to observe while resolving dependencies.</param>
        /// <returns>
        /// A <see cref="ServiceDependencyNode"/> representing the root of the resolved dependency
        /// hierarchy. Services that cannot be fully resolved (e.g. access denied) are represented by
        /// placeholder nodes within the returned tree, carrying
        /// <see cref="ServiceDependencyNode.IsUnavailable"/> and an error sentence in place of a
        /// display name; a branch that revisits a service already on the current path is terminated
        /// by a childless node carrying <see cref="ServiceDependencyNode.IsCyclic"/>. Unexpected
        /// failures propagate as exceptions rather than returning <c>null</c>.
        /// </returns>
        ServiceDependencyNode GetDependencies(CancellationToken cancellationToken = default);
    }
}
