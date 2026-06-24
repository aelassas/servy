using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods to query, start, and stop Servy services via ServiceController.
    /// </summary>
    public class ServiceHelper : IServiceHelper
    {
        private readonly IServiceRepository _serviceRepository;

        /// <summary>
        /// Initializes a new instance of the ServiceHelper class using the specified service repository.
        /// </summary>
        /// <param name="serviceRepository">The service repository used to access and manage service-related resources. Cannot be null.</param>
        [ExcludeFromCodeCoverage]
        public ServiceHelper(IServiceRepository serviceRepository)
        {
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        }

        #region Public Methods

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public List<string> GetRunningServyUIServices()
        {
            var wrapperExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppConfig.ServyServiceUIExe
            };
            return GetRunningServices(wrapperExes);
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public List<string> GetRunningServyCLIServices()
        {
            var wrapperExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppConfig.ServyServiceCLIExe
            };
            return GetRunningServices(wrapperExes);
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public List<string> GetRunningServyServices()
        {
            var wrapperExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppConfig.ServyServiceUIExe,
                AppConfig.ServyServiceCLIExe
            };
            return GetRunningServices(wrapperExes);
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public async Task StartServices(IEnumerable<string> services, CancellationToken cancellationToken = default)
        {
            // Create a bucket to collect any errors that occur
            var exceptions = new List<Exception>();

            foreach (var serviceName in services)
            {
                // Abort the batch operation immediately if cancellation is requested
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        sc.Refresh();

                        if (sc.Status == ServiceControllerStatus.Running)
                            continue;

                        var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken: cancellationToken);
                        if (service == null)
                        {
                            throw new InvalidOperationException($"Service '{serviceName}' not found in database.");
                        }

                        // ROBUSTNESS: Leverage the symmetric helper method instead of nested ternary operations
                        int preLaunchTimeout = string.IsNullOrEmpty(service.PreLaunchExecutablePath)
                            ? 0
                            : (service.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds);

                        int timeout = CalculateStartTimeout(service.StartTimeout, preLaunchTimeout, service.PreLaunchRetryAttempts ?? 0);
                        var waitTime = TimeSpan.FromSeconds(timeout);

                        // --- ROBUSTNESS: Settle In-Flight Transitional Pending States ---
                        // If a service is transitioning (e.g., StopPending from a prior failure/command), 
                        // wait for it to land on a terminal state before deciding whether to issue Start or Continue.
                        var stopwatch = Stopwatch.StartNew();
                        while (sc.Status == ServiceControllerStatus.StopPending ||
                               sc.Status == ServiceControllerStatus.PausePending ||
                               sc.Status == ServiceControllerStatus.StartPending ||
                               sc.Status == ServiceControllerStatus.ContinuePending)
                        {
                            if (stopwatch.Elapsed > waitTime)
                                throw new System.ServiceProcess.TimeoutException();

                            cancellationToken.ThrowIfCancellationRequested();
                            await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                            sc.Refresh();
                        }

                        // If the settled state turned out to be Running, we are good to go
                        if (sc.Status == ServiceControllerStatus.Running)
                            continue;

                        // Now safely evaluate stable states and issue control commands
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

                        // Instantiate a dedicated start-wait stopwatch to decouple the fast-fail interval validation
                        // from the preceding transitional state settle loop timespan.
                        var startWaitTimer = Stopwatch.StartNew();

                        // This blocks until the service is Started or the waitTime expires
                        while (true)
                        {
                            // Force a fresh Scm status block reload at the very top of the loop.
                            // This ensures the first iteration immediately acknowledges the requested Start/Continue commands
                            // instead of evaluating stale pre-execution cached layout state variables.
                            sc.Refresh();

                            if (sc.Status == ServiceControllerStatus.Running)
                                break;

                            if (stopwatch.Elapsed > waitTime)
                                throw new System.ServiceProcess.TimeoutException();

                            // FAST FAIL: A service that successfully started would never re-enter Stopped.
                            // Seeing Stopped here means the wrapped process crashed during OnStart.
                            // First-iteration grace avoids false-positives before SCM applies StartPending status.
                            // Evaluated tracking properties using the dedicated startWaitTimer context.
                            if (sc.Status == ServiceControllerStatus.Stopped && startWaitTimer.ElapsedMilliseconds > AppConfig.ScmPollIntervalMs)
                            {
                                throw new InvalidOperationException(
                                    $"Service '{serviceName}' entered Stopped state during start. " +
                                    "The supervised process likely failed immediately (check the Windows Event Log and the service's stderr log).");
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                        }
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // Log the warning and add to the collection instead of throwing immediately
                    // This aligns severity and log shape with the StopServices behavior.
                    var msg = $"Timed out waiting for service '{serviceName}' to start.";
                    Logger.Warn(msg);
                    exceptions.Add(new InvalidOperationException(msg));
                }
                catch (Exception ex)
                {
                    // Instead of throwing, we log the error and store it
                    Logger.Error($"Failed to start '{serviceName}'.", ex);

                    // Wrap in a descriptive exception so the caller knows which one failed
                    exceptions.Add(new InvalidOperationException($"Service '{serviceName}' failed.", ex));

                    // The loop continues to the next service
                }
            }

            // After attempting all services, check if we hit any snags
            if (exceptions.Any())
            {
                throw new AggregateException("One or more services failed to start.", exceptions);
            }
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public async Task StopServices(IEnumerable<string> services, CancellationToken cancellationToken = default)
        {
            // Create a bucket to collect any errors that occur during the batch operation
            var exceptions = new List<Exception>();

            foreach (var serviceName in services)
            {
                // Abort the batch operation immediately if cancellation is requested
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        // IMPORTANT: Always refresh to get the latest status from SCM
                        sc.Refresh();

                        // Check if it's already stopped first
                        if (sc.Status == ServiceControllerStatus.Stopped)
                            continue;

                        var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken: cancellationToken);
                        if (service == null)
                        {
                            throw new InvalidOperationException($"Service '{serviceName}' not found in database.");
                        }

                        try
                        {
                            // Only call Stop() if it's not already trying to stop
                            if (sc.Status != ServiceControllerStatus.StopPending)
                            {
                                sc.Stop();
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            sc.Refresh();
                            if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                            {
                                throw;
                            }
                            // else: service is already stopped or stopping - no-op
                        }

                        int timeout = CalculateStopTimeout(
                            service.StopTimeout,
                            service.PreviousStopTimeout,
                            service.PreStopTimeoutSeconds ?? 0);
                        var waitTime = TimeSpan.FromSeconds(timeout);

                        // This blocks until the service is Stopped or the waitTime expires
                        var stopwatch = Stopwatch.StartNew();
                        while (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            if (stopwatch.Elapsed > waitTime)
                                throw new System.ServiceProcess.TimeoutException();

                            cancellationToken.ThrowIfCancellationRequested();
                            await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                            sc.Refresh();
                        }
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // Log the warning and add to the collection instead of throwing immediately
                    var msg = $"Timed out waiting for service '{serviceName}' to stop.";
                    Logger.Warn(msg);
                    exceptions.Add(new InvalidOperationException(msg));
                }
                catch (Exception ex)
                {
                    // Capture general exceptions (Access Denied, Service Not Found, etc.)
                    Logger.Error($"An error occurred while stopping service '{serviceName}'.", ex);
                    exceptions.Add(new InvalidOperationException(
                        $"An error occurred while stopping service '{serviceName}'.", ex));
                }
            }

            // If any services failed to stop, notify the caller of all failures at once
            if (exceptions.Any())
            {
                throw new AggregateException("One or more services failed to stop.", exceptions);
            }
        }

        /// <summary>
        /// Calculates the total start timeout by evaluating configured limits,
        /// mandatory floors, and pre-launch executable hooks.
        /// </summary>
        /// <param name="configuredTimeout">The timeout value from the service configuration.</param>
        /// <param name="preLaunchTimeoutSeconds">The timeout for the pre-launch executable hook in seconds, if any.</param>
        /// <param name="preLaunchRetryAttempts">The number of pre-launch retry attempts; used to scale the total pre-launch timeout and backoff allowance.</param>
        /// <returns>The calculated timeout in seconds, including SCM safety buffers.</returns>
        public static int CalculateStartTimeout(
            int? configuredTimeout,
            int preLaunchTimeoutSeconds = 0,
            int preLaunchRetryAttempts = 0)
        {
            int floor = AppConfig.DefaultServiceStartTimeoutSeconds;
            int baseline = configuredTimeout.HasValue && configuredTimeout.Value > floor
                           ? configuredTimeout.Value
                           : floor;

            int attempts = Math.Max(0, preLaunchRetryAttempts) + 1;
            int safePreLaunch = Math.Max(0, preLaunchTimeoutSeconds);
            int totalPreLaunch = checked(attempts * safePreLaunch);
            int totalBackoff = 0;
            for (int i = 1; i < attempts; i++)
            {
                totalBackoff += Math.Min(
                    (i * AppConfig.PreLaunchRetryInitialDelayMs) / 1000,
                    AppConfig.PreLaunchRetryMaxDelayMs / 1000);
            }

            return baseline + AppConfig.ScmTimeoutBufferSeconds + totalPreLaunch + totalBackoff;
        }

        /// <summary>
        /// Calculates the total stop timeout by evaluating configured limits, 
        /// historical stop times, and mandatory safety buffers.
        /// </summary>
        /// <param name="configuredTimeout">The timeout value from the service configuration.</param>
        /// <param name="previousStopTimeout">The last recorded successful stop duration.</param>
        /// <param name="preStopTimeout">The timeout for the pre-stop executable hook, if any.</param>
        /// <returns>The calculated timeout in seconds, including safety buffers.</returns>
        public static int CalculateStopTimeout(int? configuredTimeout, int? previousStopTimeout, int preStopTimeout = 0)
        {
            // Standardize the floor using the default stop timeout
            int floor = AppConfig.DefaultStopTimeout;

            // Determine the baseline: highest of configured or historical duration (respecting the floor)
            int previousCapped = previousStopTimeout.HasValue
                ? Math.Min(previousStopTimeout.Value, AppConfig.MaxStopTimeout)
                : floor;

            int baseline = Math.Max(
                configuredTimeout.HasValue && configuredTimeout.Value > floor ? configuredTimeout.Value : floor,
                previousCapped);

            // Add the configurable OS/SCM buffer and the pre-stop hook duration
            int total = baseline + AppConfig.ScmTimeoutBufferSeconds + Math.Max(0, preStopTimeout);

            return total;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Queries the Service Control Manager (SCM) via native Win32 APIs to safely find all active services 
        /// associated with a specific set of executables without relying on registry subkey read capabilities.
        /// </summary>
        /// <param name="wrapperExes">The set of executable filenames to search for (e.g., "Servy.Service.exe").</param>
        /// <returns>A list containing the names of all currently running services that match any of the specified executables.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="wrapperExes"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an unexpected error occurs while querying the SCM or Registry.</exception>
        /// <remarks>
        /// This method bypasses <see cref="ServiceController.ServicesDependedOn"/> (and related
        /// SCM round-trips) to prevent COM timeout issues in large-scale deployments.
        /// It reads the <c>ImagePath</c> directly from the Registry, expands environment variables, 
        /// and safely parses out executable paths that contain quotes or command-line arguments.
        /// </remarks>
        [ExcludeFromCodeCoverage]
        private List<string> GetRunningServices(HashSet<string> wrapperExes)
        {
            if (wrapperExes == null || wrapperExes.Count == 0)
                throw new ArgumentException("At least one wrapper executable name must be provided.", nameof(wrapperExes));

            var result = new List<string>();

            // Open a safe context handle wrapper to the Service Control Manager via NativeMethods
            using (SafeScmHandle scmHandle = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT))
            {
                if (scmHandle.IsInvalid)
                {
                    throw new InvalidOperationException("Failed to open Service Control Manager connection handle.", new Win32Exception(Marshal.GetLastWin32Error()));
                }

                try
                {
                    // 1. Get all services via SCM
                    ServiceController[] services = ServiceController.GetServices();
                    try
                    {
                        foreach (var sc in services)
                        {
                            // ROBUSTNESS: Wrap the inner enumeration iteration body entirely inside a local try-catch boundary.
                            // This ensures that any single unqueryable service (deleted mid-loop via a TOCTOU race or locked down via explicit permissions)
                            // will be cleanly bypassed instead of crashing discovery for all the other active services.
                            try
                            {
                                // Only care about running services
                                if (sc.Status != ServiceControllerStatus.Running)
                                    continue;

                                // Query ImagePath using native QueryServiceConfig to bypass the HKLM Registry permission wall completely
                                using (SafeServiceHandle serviceHandle = NativeMethods.OpenService(scmHandle, sc.ServiceName, NativeMethods.SERVICE_QUERY_CONFIG))
                                {
                                    if (serviceHandle.IsInvalid)
                                    {
                                        // If an individual service has been locked down from observation, log and fail-safe over it
                                        Logger.Info($"Bypassing service metadata mapping context for '{sc.ServiceName}': Access Denied querying configuration descriptor.");
                                        continue;
                                    }

                                    // Determine the buffer length constraint allocation requirements dynamically
                                    NativeMethods.QueryServiceConfig(serviceHandle, IntPtr.Zero, 0, out int bytesNeeded);
                                    int lastError = Marshal.GetLastWin32Error();

                                    if (lastError != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                                        continue;

                                    IntPtr bufferPtr = Marshal.AllocHGlobal(bytesNeeded);
                                    try
                                    {
                                        if (NativeMethods.QueryServiceConfig(serviceHandle, bufferPtr, bytesNeeded, out _))
                                        {
                                            var config = Marshal.PtrToStructure<NativeMethods.QUERY_SERVICE_CONFIG>(bufferPtr);

                                            // Marshal out the lpBinaryPathName pointer address fields safely since it's defined as IntPtr inside NativeMethods
                                            string pathName = Marshal.PtrToStringAuto(config.lpBinaryPathName);

                                            if (string.IsNullOrWhiteSpace(pathName)) continue;

                                            // 2a. Expand variables first (e.g., %SystemRoot% -> C:\Windows)
                                            string expandedPath = Environment.ExpandEnvironmentVariables(pathName);
                                            string exePath = null;

                                            // 2b. Extract the actual exe path (Handling quotes and arguments)
                                            IntPtr argsPtr = NativeMethods.CommandLineToArgvW(expandedPath, out int argc);
                                            if (argsPtr != IntPtr.Zero)
                                            {
                                                try
                                                {
                                                    if (argc >= 1)
                                                    {
                                                        // Read the first pointer (index 0) from the array of pointers.
                                                        IntPtr firstArgPtr = Marshal.ReadIntPtr(argsPtr);

                                                        // Convert the native Unicode string at that address into a C# string.
                                                        exePath = Marshal.PtrToStringUni(firstArgPtr);
                                                    }
                                                }
                                                finally
                                                {
                                                    // CRITICAL: We must free the memory allocated by shell32.dll
                                                    NativeMethods.LocalFree(argsPtr);
                                                }
                                            }

                                            // 2c. Fallback and Comparison Logic
                                            if (exePath == null)
                                            {
                                                exePath = expandedPath; // best-effort; Path.GetFileName below handles unquoted paths
                                            }

                                            try
                                            {
                                                var exeName = Path.GetFileName(exePath);
                                                if (wrapperExes.Contains(exeName))
                                                {
                                                    result.Add(sc.ServiceName);
                                                }
                                            }
                                            catch (ArgumentException) { /* Handle invalid paths gracefully */ }
                                        }
                                    }
                                    finally
                                    {
                                        Marshal.FreeHGlobal(bufferPtr);
                                    }
                                }
                            }
                            catch (Exception iterationException)
                            {
                                // One problematic or transiently uninstalled service must not break resource checking loops.
                                Logger.Warn($"GetRunningServices: Skipping unqueryable service iteration context for '{sc.ServiceName}'. Details: {iterationException.Message}");
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
                    // Wrap any wholesale SCM communication layer failures in a deterministic exception type so callers
                    // can distinguish an operational failure from a successfully completed loop resulting in an empty tracking dataset.
                    var exeNames = string.Join(", ", wrapperExes);
                    throw new InvalidOperationException($"Failed to query services for [{exeNames}] via SCM Native APIs.", ex);
                }
            }

            return result;
        }

        #endregion
    }
}