using Servy.Core.Config;
using Servy.Core.Data;
using System.Diagnostics.CodeAnalysis;
using System.Management;
using System.ServiceProcess;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods to query, start, and stop Servy services via WMI and ServiceController.
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
        public List<string> GetRunningServyServices()
        {
            var guiServices = GetRunningServyUIServices();
            var cliServices = GetRunningServyCLIServices();
            var services = new List<string>();
            services.AddRange(guiServices);
            services.AddRange(cliServices);
            return services;
        }

        /// <summary>
        /// Starts the specified services if they are not already running or pending start,
        /// and waits until each service is fully running.
        /// </summary>
        /// <param name="services">A collection of service names to start.</param>
        public async Task StartServices(IEnumerable<string> services)
        {
            var defaultTimeoutInSeconds = 30;
            var bufferTimeInSeconds = 15;

            foreach (var serviceName in services)
            {
                try
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        sc.Refresh(); // Sync with SCM

                        // If already running, move to the next service
                        if (sc.Status == ServiceControllerStatus.Running)
                            continue;

                        // Only trigger Start if it is currently Stopped
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                        }

                        // If paused, continue it
                        if (sc.Status == ServiceControllerStatus.Paused)
                        {
                            sc.Continue();
                        }

                        var service = await _serviceRepository.GetByNameAsync(serviceName);
                        if (service == null)
                        {
                            throw new InvalidOperationException($"Service '{serviceName}' not found in database.");
                        }
                        var startTimeout = (service.StartTimeout ?? defaultTimeoutInSeconds) + bufferTimeInSeconds;
                        startTimeout = Math.Max(startTimeout, defaultTimeoutInSeconds);
                        var waitTime = TimeSpan.FromSeconds(startTimeout);

                        // This blocks until the service is Started or the waitTime expires
                        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, waitTime));
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    throw new InvalidOperationException(
                        $"Timed out waiting for service '{serviceName}' to start.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Could not start service '{serviceName}': {ex.Message}");
                }
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
        /// Queries WMI to get all running services whose executable matches the given wrapper filename.
        /// </summary>
        /// <param name="wrapperExe">The filename of the service executable (e.g., "Servy.Service.exe").</param>
        /// <returns>A list of service names that are currently running and match the executable.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="wrapperExe"/> is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown if WMI query fails.</exception>
        private List<string> GetRunningServices(string wrapperExe)
        {
            if (string.IsNullOrWhiteSpace(wrapperExe))
                throw new ArgumentException("Wrapper executable name must be provided.", nameof(wrapperExe));

            var result = new List<string>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Name, PathName FROM Win32_Service WHERE State = 'Running'"))
                using (var services = searcher.Get())
                {
                    foreach (var service in services)
                    {
                        var pathName = service["PathName"]?.ToString();
                        if (string.IsNullOrWhiteSpace(pathName))
                            continue;

                        // Extract the first quoted string (the actual exe path)
                        string? exePath = null;
                        int firstQuote = pathName.IndexOf('"');
                        if (firstQuote >= 0)
                        {
                            int secondQuote = pathName.IndexOf('"', firstQuote + 1);
                            if (secondQuote > firstQuote)
                                exePath = pathName.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                        }
                        else
                        {
                            // Fallback if no quotes: take substring until first space
                            int firstSpace = pathName.IndexOf(' ');
                            exePath = firstSpace > 0 ? pathName.Substring(0, firstSpace) : pathName;
                        }

                        if (string.IsNullOrEmpty(exePath))
                            continue;

                        // Get just the executable filename
                        var exeName = System.IO.Path.GetFileName(exePath);

                        // Compare with the requested wrapperExe
                        if (string.Equals(exeName, wrapperExe, StringComparison.OrdinalIgnoreCase))
                        {
                            var name = service["Name"]?.ToString();
                            if (!string.IsNullOrEmpty(name))
                                result.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to query services for {wrapperExe}: {ex.Message}", ex);
            }

            return result;
        }

        #endregion

    }
}
