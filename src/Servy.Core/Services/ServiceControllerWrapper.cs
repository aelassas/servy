using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        public string ServiceName
        {
            get
            {
                ThrowIfDisposed();
                return _serviceName;
            }
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
        public ServiceStartMode StartType
        {
            get
            {
                ThrowIfDisposed();
                return _controller.StartType;
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
            // Tracks the current branch from root to leaf to detect deep cycles
            var currentPath = new List<string>();

            // Tracks services that have already been fully resolved across ANY branch
            var fullyExpanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return BuildDependencyTree(_serviceName, currentPath, fullyExpanded);
        }

        /// <summary>
        /// Recursively builds a dependency tree for the specified service
        /// sorted alphabetically by Display Name.
        /// </summary>
        /// <param name="serviceName">
        /// The internal name of the service whose dependencies are resolved.
        /// </param>
        /// <param name="currentPath">
        /// A list tracking the specific path from root to leaf to detect and prevent cyclic dependencies.
        /// </param>
        /// <param name="fullyExpanded">
        /// A set of service names already fully expanded across all paths, used to prevent 
        /// redundant SCM queries and O(n²) overhead in diamond dependency patterns.
        /// </param>
        /// <returns>
        /// A <see cref="ServiceDependencyNode"/> representing the service
        /// and its dependencies. If a cycle is detected, a placeholder
        /// node is returned.
        /// </returns>
        private static ServiceDependencyNode BuildDependencyTree(string serviceName, List<string> currentPath, HashSet<string> fullyExpanded)
        {
            // 1. Detect Cycle in the CURRENT branch
            var isCycle = currentPath.Contains(serviceName, StringComparer.OrdinalIgnoreCase);

            // 2. Check if we've already built the subtree for this service elsewhere
            var isAlreadyExpanded = fullyExpanded.Contains(serviceName);

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    var isRunning = service.Status == ServiceControllerStatus.Running;

                    var node = new ServiceDependencyNode(
                        service.ServiceName,
                        service.DisplayName,
                        isRunning,
                        isCycle
                    );

                    // Stop recursing if it's a cycle OR if we've already done the heavy lifting for this node
                    if (isCycle || isAlreadyExpanded) return node;

                    // 3. Add to path before diving deeper
                    currentPath.Add(serviceName);

                    // Collect children in a temporary list to sort them before adding to the TreeView
                    var childNodes = new List<ServiceDependencyNode>();

                    var deps = service.ServicesDependedOn;
                    foreach (var dep in deps)
                    {
                        try
                        {
                            childNodes.Add(BuildDependencyTree(dep.ServiceName, currentPath, fullyExpanded));
                        }
                        finally
                        {
                            dep.Dispose();
                        }
                    }

                    // 4. SORT and ADD: Order alphabetically by DisplayName
                    var sortedChildren = childNodes.OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase);
                    foreach (var child in sortedChildren)
                    {
                        node.Dependencies.Add(child);
                    }

                    // 5. BACKTRACK: Remove from current path, but mark globally as fully expanded
                    currentPath.RemoveAt(currentPath.Count - 1);
                    fullyExpanded.Add(serviceName);

                    return node;
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.Debug($"Dependency '{serviceName}' unavailable: {ex.Message}");
                return new ServiceDependencyNode(serviceName, $"{serviceName} (Unavailable)", false, false);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.Warn($"Win32 error resolving dependency '{serviceName}': {ex.Message}", ex);
                return new ServiceDependencyNode(serviceName, $"{serviceName} (Access Denied)", false, false);
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
