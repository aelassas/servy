using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace Servy.Core.Services
{
    /// <inheritdoc cref="IServiceControllerWrapper"/>
    [ExcludeFromCodeCoverage]
    public class ServiceControllerWrapper : IServiceControllerWrapper
    {
        private readonly string _serviceName;
        private readonly ServiceController _controller;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceControllerWrapper"/> class with the specified service name.
        /// </summary>
        /// <param name="serviceName">The name of the Windows service to control.</param>
        public ServiceControllerWrapper(string serviceName)
        {
            _serviceName = serviceName;
            _controller = new ServiceController(serviceName);
        }

        /// <inheritdoc/>
        public ServiceControllerStatus Status
        {
            get
            {
                ThrowIfDisposed();
                return _controller.Status;
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            ThrowIfDisposed();
            _controller.Start();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            ThrowIfDisposed();
            _controller.Stop();
        }

        /// <inheritdoc/>
        public void Refresh()
        {
            ThrowIfDisposed();
            _controller.Refresh();
        }

        /// <inheritdoc/>
        public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout)
        {
            ThrowIfDisposed();
            _controller.WaitForStatus(desiredStatus, timeout);
        }

        /// <inheritdoc/>
        public ServiceDependencyNode GetDependencies()
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return BuildDependencyTree(_serviceName, visited);
        }

        /// <summary>
        /// Recursively builds a dependency tree for the specified service.
        /// </summary>
        /// <param name="serviceName">
        /// The internal name of the service whose dependencies are resolved.
        /// </param>
        /// <param name="visited">
        /// A set of service names already visited during traversal, used
        /// to detect and prevent cyclic dependencies.
        /// </param>
        /// <returns>
        /// A <see cref="ServiceDependencyNode"/> representing the service
        /// and its dependencies. If a cycle is detected, a placeholder
        /// node is returned.
        /// </returns>
        private static ServiceDependencyNode BuildDependencyTree(string serviceName, HashSet<string> visited)
        {
            if (!visited.Add(serviceName))
            {
                // Cycle detected
                return new ServiceDependencyNode(serviceName, "[cycle]");
            }

            using (var service = new ServiceController(serviceName))
            {
                var node = new ServiceDependencyNode(
                    service.ServiceName,
                    service.DisplayName
                );

                foreach (var dependency in service.ServicesDependedOn)
                {
                    node.Dependencies.Add(
                        BuildDependencyTree(dependency.ServiceName, visited)
                    );
                }

                return node;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True if called from <see cref="Dispose()"/>, false if called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                _controller.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this instance has already been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceControllerWrapper));
        }
    }
}
