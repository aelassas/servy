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
            // Use a list to track the specific path from root to leaf
            var currentPath = new List<string>();
            return BuildDependencyTree(_serviceName, currentPath);
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
        private static ServiceDependencyNode BuildDependencyTree(string serviceName, List<string> currentPath)
        {
            // 1. Detect Cycle in the CURRENT branch
            var isCycle = currentPath.Contains(serviceName, StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    var isRunning = service.Status == ServiceControllerStatus.Running;
                    var node = new ServiceDependencyNode(
                        service.ServiceName,
                        //isCycle ? $"{service.DisplayName} [Cycle]" : service.DisplayName,
                        service.DisplayName,
                        isRunning
                    );

                    // If it's a cycle, we stop recursing here but return the node
                    if (isCycle) return node;

                    // 2. Add to path before diving deeper
                    currentPath.Add(serviceName);

                    var deps = service.ServicesDependedOn;
                    foreach (var dep in deps)
                    {
                        try
                        {
                            node.Dependencies.Add(BuildDependencyTree(dep.ServiceName, currentPath));
                        }
                        finally
                        {
                            dep.Dispose();
                        }
                    }

                    // 3. BACKTRACK: Remove from path so other branches can see this service
                    currentPath.RemoveAt(currentPath.Count - 1);

                    return node;
                }
            }
            catch
            {
                return new ServiceDependencyNode(serviceName, $"{serviceName} (Unavailable)", false);
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
