using Servy.Core.Logging;
using Servy.Core.Resources;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace Servy.Core.Services
{
    /// <inheritdoc cref="IServiceControllerWrapper"/>
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
        [ExcludeFromCodeCoverage]
        public ServiceControllerStatus Status
        {
            get
            {
                ThrowIfDisposed();
                return _controller.Status;
            }
        }

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public ServiceStartMode StartType
        {
            get
            {
                ThrowIfDisposed();
                return _controller.StartType;
            }
        }

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public void Start()
        {
            ThrowIfDisposed();
            _controller.Start();
        }

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public void Stop()
        {
            ThrowIfDisposed();
            _controller.Stop();
        }

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public void Refresh()
        {
            ThrowIfDisposed();
            _controller.Refresh();
        }

        /// <inheritdoc/>
        [ExcludeFromCodeCoverage]
        public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout)
        {
            ThrowIfDisposed();
            _controller.WaitForStatus(desiredStatus, timeout);
        }

        /// <inheritdoc/>
        public ServiceDependencyNode GetDependencies(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            // Tracks the current branch from root to leaf to detect deep cycles
            var currentPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Tracks services that have already been fully resolved across ANY branch
            var fullyExpanded = new Dictionary<string, ServiceDependencyNode>(StringComparer.OrdinalIgnoreCase);

            return BuildDependencyTree(_serviceName, currentPath, fullyExpanded, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Recursively builds a dependency tree for the specified service
        /// sorted alphabetically by Display Name.
        /// </summary>
        /// <param name="serviceName">
        /// The internal name of the service whose dependencies are resolved.
        /// </param>
        /// <param name="currentPath">
        /// A hash set tracking the specific path from root to leaf to detect and prevent cyclic dependencies.
        /// </param>
        /// <param name="fullyExpanded">
        /// A dictionary of service names to already fully expanded nodes, used to prevent 
        /// redundant SCM queries and properly populate shared/diamond dependency paths.
        /// </param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>
        /// A <see cref="ServiceDependencyNode"/> representing the service
        /// and its dependencies. If a cycle is detected, a placeholder
        /// node is returned.
        /// </returns>
        private static ServiceDependencyNode BuildDependencyTree(
            string serviceName,
            HashSet<string> currentPath,
            Dictionary<string, ServiceDependencyNode> fullyExpanded,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Detect Cycle in the CURRENT branch
            var isCyclic = currentPath.Contains(serviceName);

            // 2. Check if we've already built the subtree for this service elsewhere
            // We only return the cached subtree if this isn't a cycle in the current path.
            if (!isCyclic && fullyExpanded.TryGetValue(serviceName, out var cachedNode))
            {
                // Re-use the existing populated node reference for diamond dependencies.
                return cachedNode;
            }

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    var isRunning = service.Status == ServiceControllerStatus.Running;

                    var node = new ServiceDependencyNode(
                        service.ServiceName,
                        service.DisplayName,
                        isRunning,
                        isCyclic
                    );

                    // Stop recursing if it's a cycle
                    if (isCyclic) return node;

                    // 3. Add to path before diving deeper
                    currentPath.Add(serviceName);
                    ServiceController[]? deps = null;

                    try
                    {
                        // Collect children in a temporary list so they can be sorted before being added to the node
                        var childNodes = new List<ServiceDependencyNode>();

                        // Accessing this property can throw Win32Exception (Access Denied)
                        deps = service.ServicesDependedOn;

                        try
                        {
                            foreach (var dep in deps)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                childNodes.Add(BuildDependencyTree(dep.ServiceName, currentPath, fullyExpanded, cancellationToken));
                            }
                        }
                        finally
                        {
                            // ROBUSTNESS: Dispose all remaining handles if the foreach exited early due to an exception.
                            // ServiceController.Dispose() is idempotent, making this safe for previously disposed items.
                            if (deps != null)
                            {
                                foreach (var dep in deps)
                                {
                                    try { dep.Dispose(); } catch { }
                                }
                            }
                        }

                        // 4. SORT and ADD: Order alphabetically by DisplayName
                        var sortedChildren = childNodes.OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase);
                        foreach (var child in sortedChildren)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            node.Dependencies.Add(child);
                        }

                        // MEMOIZATION REUSE: Evaluate if any descendant down this newly constructed 
                        // subtree flagged a cycle tracking placeholder. If an explicit cycle placeholder exists, 
                        // this subtree structure is path-dependent and cannot safely be stored globally.
                        if (!HasCyclicDescendant(node))
                        {
                            // Mark globally as fully expanded by caching the completed node only if completely cycle-free
                            fullyExpanded[serviceName] = node;
                        }
                    }
                    finally
                    {
                        // 5. BACKTRACK: Ensure the service is always removed from the current path,
                        // preventing path corruption for sibling nodes if an exception occurred.
                        currentPath.Remove(serviceName);
                    }

                    return node;
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.Debug($"Dependency '{serviceName}' unavailable: {ex.Message}");
                return new ServiceDependencyNode(serviceName, string.Format(Strings.Msg_DependencyUnavailable, serviceName), false, false);
            }
            catch (Win32Exception ex)
            {
                Logger.Warn($"Win32 error resolving dependency '{serviceName}'.", ex);

                // Discrimination filter targeting Win32 error code 5 (ERROR_ACCESS_DENIED)
                string localizedMessage = ex.NativeErrorCode == 5
                    ? string.Format(Strings.Msg_DependencyAccessDenied, serviceName)
                    : string.Format(Strings.Msg_DependencyUnavailable, serviceName);

                return new ServiceDependencyNode(serviceName, localizedMessage, false, false);
            }
        }

        /// <summary>
        /// Traverses the built tree reference recursively to inspect for nested cyclic path placeholders.
        /// </summary>
        private static bool HasCyclicDescendant(ServiceDependencyNode node)
        {
            if (node == null) return false;
            if (node.IsCyclic) return true;

            foreach (var child in node.Dependencies)
            {
                if (HasCyclicDescendant(child)) return true;
            }

            return false;
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
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
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