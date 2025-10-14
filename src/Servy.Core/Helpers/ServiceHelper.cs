using Servy.Core.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Management;
using System.ServiceProcess;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods to query, start, and stop Servy services via WMI and ServiceController.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ServiceHelper
    {

        #region Public Methods

        /// <summary>
        /// Gets the names of all currently running Servy UI services.
        /// </summary>
        /// <returns>A list of service names.</returns>
        public static List<string> GetRunningServyUIServices()
        {
            var wrapperExe = AppConfig.ServyServiceUIExe;
            var services = GetRunningServices(wrapperExe);
            return services;
        }

        /// <summary>
        /// Gets the names of all currently running Servy CLI services.
        /// </summary>
        /// <returns>A list of service names.</returns>
        public static List<string> GetRunningServyCLIServices()
        {
            var wrapperExe = AppConfig.ServyServiceCLIExe;
            var services = GetRunningServices(wrapperExe);
            return services;
        }

        /// <summary>
        /// Gets the names of all currently running Servy services (GUI and CLI).
        /// </summary>
        /// <returns>A list of service names.</returns>
        public static List<string> GetRunningServyServices()
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
        /// <param name="timeout">The maximum time to wait for each service to start (default: 30 seconds).</param>
        public static void StartServices(IEnumerable<string> services, TimeSpan? timeout = null)
        {
            foreach (var service in services)
            {
                using (var sc = new ServiceController(service))
                {
                    if (sc.Status != ServiceControllerStatus.Running &&
                        sc.Status != ServiceControllerStatus.StartPending)
                    {
                        sc.Start();
                    }

                    // Default wait time = 30 seconds if not specified
                    var waitTime = timeout ?? TimeSpan.FromSeconds(30);

                    try
                    {
                        sc.WaitForStatus(ServiceControllerStatus.Running, waitTime);
                    }
                    catch (System.ServiceProcess.TimeoutException)
                    {
                        throw new InvalidOperationException(
                            $"Timed out waiting for service '{service}' to start.");
                    }
                }
            }
        }

        /// <summary>
        /// Stops the specified services if they are running or pending stop,
        /// and waits until each service is fully stopped.
        /// </summary>
        /// <param name="services">A collection of service names to stop.</param>
        /// <param name="timeout">The maximum time to wait for each service to stop.</param>
        public static void StopServices(IEnumerable<string> services, TimeSpan? timeout = null)
        {
            foreach (var service in services)
            {
                using (var sc = new ServiceController(service))
                {
                    if (sc.Status == ServiceControllerStatus.Running ||
                        sc.Status == ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();

                        // Default wait time = 30 seconds if not specified
                        var waitTime = timeout ?? TimeSpan.FromSeconds(30);

                        try
                        {
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, waitTime);
                        }
                        catch (System.ServiceProcess.TimeoutException)
                        {
                            throw new InvalidOperationException(
                                $"Timed out waiting for service '{service}' to stop.");
                        }
                    }
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
        private static List<string> GetRunningServices(string wrapperExe)
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
                        string exePath = null;
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
