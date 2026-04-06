using Microsoft.Win32;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods to query, start, and stop Servy services via ServiceController.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ServiceHelper
    {
        private readonly IServiceRepository _serviceRepository;

        /// <summary>
        /// Initializes a new instance of the ServiceHelper class using the specified service repository.
        /// </summary>
        /// <param name="serviceRepository">The service repository used to access and manage service-related resources. Cannot be null.</param>
        public ServiceHelper(IServiceRepository serviceRepository)
        {
            _serviceRepository = serviceRepository;
        }

        #region Public Methods

        /// <summary>
        /// Gets the names of all currently running Servy UI services.
        /// </summary>
        /// <returns>A list of service names.</returns>
        public List<string> GetRunningServyUIServices()
        {
            var wrapperExe = AppConfig.ServyServiceUIExe;
            var services = GetRunningServices(wrapperExe);
            return services;
        }

        /// <summary>
        /// Gets the names of all currently running Servy CLI services.
        /// </summary>
        /// <returns>A list of service names.</returns>
        public List<string> GetRunningServyCLIServices()
        {
            var wrapperExe = AppConfig.ServyServiceCLIExe;
            var services = GetRunningServices(wrapperExe);
            return services;
        }

        /// <summary>
        /// Gets the names of all currently running Servy services (GUI and CLI).
        /// </summary>
        /// <returns>A list of service names.</returns>
        public List<string> GetRunningServyServices() => GetRunningServyUIServices().Concat(GetRunningServyCLIServices()).ToList();

        /// <summary>
        /// Starts the specified services if they are not already running or pending start,
        /// and waits until each service is fully running.
        /// </summary>
        /// <param name="services">A collection of service names to start.</param>
        public async Task StartServices(IEnumerable<string> services)
        {
            const int defaultTimeoutInSeconds = 30;
            const int bufferTimeInSeconds = 15;

            // Create a bucket to collect any errors that occur
            var exceptions = new List<Exception>();

            foreach (var serviceName in services)
            {
                try
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        sc.Refresh();

                        if (sc.Status == ServiceControllerStatus.Running)
                            continue;

                        try
                        {
                            if (sc.Status == ServiceControllerStatus.Stopped)
                            {
                                sc.Start();
                            }
                            else if (sc.Status == ServiceControllerStatus.Paused)
                            {
                                sc.Continue();
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            sc.Refresh();
                            if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.Paused)
                            {
                                throw;
                            }
                        }

                        var service = await _serviceRepository.GetByNameAsync(serviceName);
                        if (service == null)
                        {
                            throw new InvalidOperationException($"Service '{serviceName}' not found in database.");
                        }

                        var startTimeout = (service.StartTimeout ?? defaultTimeoutInSeconds) + bufferTimeInSeconds;
                        var waitTime = TimeSpan.FromSeconds(Math.Max(startTimeout, defaultTimeoutInSeconds));

                        // This blocks until the service is Started or the waitTime expires
                        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, waitTime));
                    }
                }
                catch (Exception ex)
                {
                    // Instead of throwing, we log the error and store it
                    Logger.Error($"Failed to start {serviceName}.", ex);

                    // Wrap in a descriptive exception so the caller knows which one failed
                    exceptions.Add(new InvalidOperationException($"Service '{serviceName}' failed: {ex.Message}", ex));

                    // The loop continues to the next service
                }
            }

            // After attempting all services, check if we hit any snags
            if (exceptions.Any())
            {
                throw new AggregateException("One or more services failed to start.", exceptions);
            }

        }

        /// <summary>
        /// Stops the specified services if they are running or pending stop,
        /// and waits until each service is fully stopped.
        /// </summary>
        /// <param name="services">A collection of service names to stop.</param>
        public async Task StopServices(IEnumerable<string> services)
        {
            var defaultTimeoutInSeconds = 30;
            var bufferTimeInSeconds = 15;

            foreach (var serviceName in services)
            {
                try
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        // IMPORTANT: Always refresh to get the latest status from SCM
                        sc.Refresh();

                        // Check if it's already stopped first
                        if (sc.Status == ServiceControllerStatus.Stopped)
                            continue;

                        // Only call Stop() if it's not already trying to stop
                        if (sc.Status != ServiceControllerStatus.StopPending)
                        {
                            sc.Stop();
                        }

                        var service = await _serviceRepository.GetByNameAsync(serviceName);
                        if (service == null)
                        {
                            throw new InvalidOperationException($"Service '{serviceName}' not found in database.");
                        }
                        var stopTimeout = (service.StopTimeout ?? defaultTimeoutInSeconds) + bufferTimeInSeconds;
                        var previousStopTimeout = (service.PreviousStopTimeout ?? defaultTimeoutInSeconds) + bufferTimeInSeconds;
                        stopTimeout = Math.Max(Math.Max(stopTimeout, previousStopTimeout), defaultTimeoutInSeconds);
                        var waitTime = TimeSpan.FromSeconds(stopTimeout);

                        // This blocks until the service is Stopped or the waitTime expires
                        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, waitTime));
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // Providing the actual timeout value in the error helps with debugging
                    throw new InvalidOperationException(
                        $"Timed out waiting for service '{serviceName}' to stop.");
                }
                catch (Exception ex)
                {
                    // Catching general exceptions (like Service Not Found)
                    throw new InvalidOperationException(
                        $"An error occurred while stopping service '{serviceName}': {ex.Message}");
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Queries the Service Control Manager (SCM) and Windows Registry to find all active services 
        /// associated with a specific executable.
        /// </summary>
        /// <param name="wrapperExe">The filename of the executable to search for (e.g., "Servy.Service.exe").</param>
        /// <returns>A list containing the names of all currently running services that match the specified executable.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="wrapperExe"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an unexpected error occurs while querying the SCM or Registry.</exception>
        /// <remarks>
        /// This method bypasses prevents COM timeout issues in large-scale deployments. 
        /// It reads the <c>ImagePath</c> directly from the Registry, expands environment variables, 
        /// and safely parses out executable paths that contain quotes or command-line arguments.
        /// </remarks>
        private List<string> GetRunningServices(string wrapperExe)
        {
            if (string.IsNullOrWhiteSpace(wrapperExe))
                throw new ArgumentException("Wrapper executable name must be provided.", nameof(wrapperExe));

            var result = new List<string>();

            try
            {
                // 1. Get all services via SCM
                ServiceController[] services = ServiceController.GetServices();
                try
                {
                    foreach (var sc in services)
                    {
                        // Only care about running services
                        if (sc.Status != ServiceControllerStatus.Running)
                            continue;

                        // 2. Query Registry for the ImagePath (Binary Path)
                        // SCM stores this in HKLM\SYSTEM\CurrentControlSet\Services\[ServiceName]
                        string registryKeyPath = $@"SYSTEM\CurrentControlSet\Services\{sc.ServiceName}";
                        using (var key = Registry.LocalMachine.OpenSubKey(registryKeyPath))
                        {
                            if (key == null) continue;

                            var pathName = key.GetValue("ImagePath")?.ToString();
                            if (string.IsNullOrWhiteSpace(pathName)) continue;

                            // 1. Expand variables first (e.g., %SystemRoot% -> C:\Windows)
                            string expandedPath = Environment.ExpandEnvironmentVariables(pathName);

                            // 2. Extract the actual exe path (Handling quotes and arguments)
                            string exePath;
                            int firstQuote = expandedPath.IndexOf('"');
                            if (firstQuote >= 0)
                            {
                                int secondQuote = expandedPath.IndexOf('"', firstQuote + 1);
                                if (secondQuote > firstQuote)
                                    exePath = expandedPath.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                                else
                                    exePath = expandedPath; // Fallback
                            }
                            else
                            {
                                // If no quotes, take until first space (arguments)
                                int firstSpace = expandedPath.IndexOf(' ');
                                exePath = firstSpace > 0 ? expandedPath.Substring(0, firstSpace) : expandedPath;
                            }

                            // 4. Resolve the filename and compare
                            try
                            {
                                var exeName = Path.GetFileName(exePath);
                                if (string.Equals(exeName, wrapperExe, StringComparison.OrdinalIgnoreCase))
                                {
                                    result.Add(sc.ServiceName);
                                }
                            }
                            catch (ArgumentException) { /* Handle invalid paths gracefully */ }
                        }
                    }
                }
                finally
                {
                    foreach (var sc in services)
                        sc.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Fail-safe: log locally or return empty list. 
                // Do not let a query failure block deployment.
                throw new InvalidOperationException(
                    $"Failed to query services for {wrapperExe} via SCM/Registry: {ex.Message}", ex);
            }

            return result;
        }

        #endregion

    }
}
