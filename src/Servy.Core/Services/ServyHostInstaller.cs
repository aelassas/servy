using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using static Servy.Core.Native.Errors;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class ServyHostInstaller : IServyHostInstaller
    {
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly IWin32ErrorProvider _win32ErrorProvider;
        private readonly IServiceControllerProvider _serviceControllerProvider;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyHostInstaller"/> class.
        /// </summary>
        /// <param name="windowsServiceApi">The native Service Control Manager API.</param>
        /// <param name="win32ErrorProvider">Reads the last Win32 error.</param>
        /// <param name="serviceControllerProvider">Opens service controllers to read the status and wait for a state.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        public ServyHostInstaller(IWindowsServiceApi windowsServiceApi, IWin32ErrorProvider win32ErrorProvider, IServiceControllerProvider serviceControllerProvider)
            : this(windowsServiceApi, win32ErrorProvider, serviceControllerProvider, TimeSpan.FromSeconds(AppConfig.ServyHostServiceTimeoutSeconds))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyHostInstaller"/> class with a custom wait timeout.
        /// </summary>
        /// <param name="windowsServiceApi">The native Service Control Manager API.</param>
        /// <param name="win32ErrorProvider">Reads the last Win32 error.</param>
        /// <param name="serviceControllerProvider">Opens service controllers to read the status and wait for a state.</param>
        /// <param name="timeout">How long to wait for the service to start or stop.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        internal ServyHostInstaller(IWindowsServiceApi windowsServiceApi, IWin32ErrorProvider win32ErrorProvider, IServiceControllerProvider serviceControllerProvider, TimeSpan timeout)
        {
            _windowsServiceApi = windowsServiceApi ?? throw new ArgumentNullException(nameof(windowsServiceApi));
            _win32ErrorProvider = win32ErrorProvider ?? throw new ArgumentNullException(nameof(win32ErrorProvider));
            _serviceControllerProvider = serviceControllerProvider ?? throw new ArgumentNullException(nameof(serviceControllerProvider));
            _timeout = timeout;
        }

        /// <inheritdoc />
        public bool IsRunning()
        {
            try
            {
                using (var sc = _serviceControllerProvider.GetService(AppConfig.ServyHostServiceName))
                {
                    sc.Refresh();
                    return sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending;
                }
            }
            catch (InvalidOperationException)
            {
                // Not installed
                return false;
            }
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var sc = _serviceControllerProvider.GetService(AppConfig.ServyHostServiceName))
            {
                try
                {
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return;

                    if (sc.Status != ServiceControllerStatus.StopPending)
                        sc.Stop();
                }
                catch (InvalidOperationException ex) when (!IsInstalled(sc))
                {
                    Logger.Debug($"The '{AppConfig.ServyHostServiceName}' service is not installed; nothing to stop. {ex.Message}");
                    return;
                }

                await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, cancellationToken);
                Logger.Info($"Stopped the '{AppConfig.ServyHostServiceName}' service.");
            }
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var sc = _serviceControllerProvider.GetService(AppConfig.ServyHostServiceName))
            {
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Running)
                    return;

                if (sc.Status == ServiceControllerStatus.StopPending)
                    await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, cancellationToken);

                if (sc.Status != ServiceControllerStatus.StartPending)
                    sc.Start();

                await WaitForStatusAsync(sc, ServiceControllerStatus.Running, cancellationToken);
                Logger.Info($"Started the '{AppConfig.ServyHostServiceName}' service.");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> EnsureInstalledAndRunningAsync(string hostExePath, IServiceHelper serviceHelper, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostExePath))
                throw new ArgumentException("The path of the host executable is required.", nameof(hostExePath));
            if (serviceHelper == null) throw new ArgumentNullException(nameof(serviceHelper));

            cancellationToken.ThrowIfCancellationRequested();

            var stoppedServices = new List<string>();
            try
            {
                if (!File.Exists(hostExePath))
                    return Fail($"'{hostExePath}' does not exist, so the '{AppConfig.ServyHostServiceName}' service cannot be installed.");

                // Installed with another executable (a switch between the net10 and the net48 build, or between Debug and
                // Release): stop every Servy service and the host before the service is pointed at this build's executable
                var registeredPath = GetRegisteredExecutablePath();
                if (registeredPath != null && !IsSameExecutable(registeredPath, hostExePath))
                {
                    Logger.Info($"The '{AppConfig.ServyHostServiceName}' service runs '{registeredPath}' instead of '{hostExePath}'; restarting it from the expected path.");

                    stoppedServices = GetRunningServyServices(serviceHelper);
                    if (stoppedServices.Count > 0)
                    {
                        Logger.Info($"Stopping services before moving the '{AppConfig.ServyHostServiceName}' service: {string.Join(", ", stoppedServices)}");
                        await serviceHelper.StopServicesAsync(stoppedServices, cancellationToken);
                    }

                    await StopAsync(cancellationToken);
                }

                EnsureInstalledAndAutomatic(hostExePath);
                await StartAsync(cancellationToken);
                return OperationResult.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to install or start the '{AppConfig.ServyHostServiceName}' service.", ex);
                return OperationResult.Failure($"Failed to install or start the '{AppConfig.ServyHostServiceName}' service: {ex.Message}");
            }
            finally
            {
                if (stoppedServices.Count > 0)
                {
                    try
                    {
                        Logger.Info($"Starting the services stopped to move the '{AppConfig.ServyHostServiceName}' service: {string.Join(", ", stoppedServices)}");
                        await serviceHelper.StartServicesAsync(stoppedServices, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Some services failed to restart after moving the '{AppConfig.ServyHostServiceName}' service.", ex);
                    }
                }
            }
        }

        /// <inheritdoc />
        public Task<int> EnsureServicesDependOnHostAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
        {
            if (serviceNames == null) throw new ArgumentNullException(nameof(serviceNames));

            int updated = 0;
            using (var scm = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT))
            {
                if (scm == null || scm.IsInvalid)
                {
                    Logger.Error("Failed to open the Service Control Manager to add the Servy host dependency.", new Win32Exception(_win32ErrorProvider.GetLastWin32Error()));
                    return Task.FromResult(0);
                }

                foreach (var name in serviceNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.Equals(name.Trim(), AppConfig.ServyHostServiceName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        List<string> dependencies;
                        using (var sc = _serviceControllerProvider.GetService(name))
                        {
                            dependencies = sc.GetDependencyNames().ToList();
                        }

                        if (dependencies.Contains(AppConfig.ServyHostServiceName, StringComparer.OrdinalIgnoreCase))
                            continue;

                        using (var service = _windowsServiceApi.OpenService(scm, name, SERVICE_CHANGE_CONFIG))
                        {
                            if (service == null || service.IsInvalid)
                            {
                                var err = _win32ErrorProvider.GetLastWin32Error();
                                if (err != ERROR_SERVICE_DOES_NOT_EXIST)
                                    Logger.Warn($"Could not open service '{name}' to add the '{AppConfig.ServyHostServiceName}' dependency. Win32 error: {err}");
                                continue;
                            }

                            var lpDependencies = ServiceDependenciesParser.ParseWithRequired(string.Join(";", dependencies), AppConfig.ServyHostServiceName);
                            if (!_windowsServiceApi.ChangeServiceConfig(service, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, null, null, IntPtr.Zero, lpDependencies, null, null, null))
                            {
                                Logger.Warn($"Could not add the '{AppConfig.ServyHostServiceName}' dependency to service '{name}'. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                                continue;
                            }
                        }

                        updated++;
                        Logger.Info($"Service '{name}' now depends on the '{AppConfig.ServyHostServiceName}' service.");
                    }
                    catch (InvalidOperationException)
                    {
                        // Not installed in the SCM (a database-only record): nothing to update
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Could not add the '{AppConfig.ServyHostServiceName}' dependency to service '{name}'.", ex);
                    }
                }
            }

            return Task.FromResult(updated);
        }

        /// <summary>
        /// Reads the executable the Servy host service is registered with.
        /// </summary>
        /// <returns>The command line the Service Control Manager runs, or <see langword="null"/> when the service is not
        /// installed or its configuration cannot be read (a failure is logged).</returns>
        internal string GetRegisteredExecutablePath()
        {
            try
            {
                using (var scm = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT))
                {
                    if (scm == null || scm.IsInvalid)
                    {
                        Logger.Warn($"Could not open the Service Control Manager to read the '{AppConfig.ServyHostServiceName}' service's executable. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                        return null;
                    }

                    using (var service = _windowsServiceApi.OpenService(scm, AppConfig.ServyHostServiceName, SERVICE_QUERY_CONFIG))
                    {
                        if (service == null || service.IsInvalid)
                        {
                            var err = _win32ErrorProvider.GetLastWin32Error();
                            if (err != ERROR_SERVICE_DOES_NOT_EXIST)
                                Logger.Warn($"Could not open the '{AppConfig.ServyHostServiceName}' service to read its executable. Win32 error: {err}");
                            return null;
                        }

                        // Pass 1 reports the size; pass 2 fills the buffer
                        _windowsServiceApi.QueryServiceConfig(service, IntPtr.Zero, 0, out int bytesNeeded);
                        if (bytesNeeded <= 0)
                        {
                            Logger.Warn($"Could not read the '{AppConfig.ServyHostServiceName}' service's configuration. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                            return null;
                        }

                        IntPtr buffer = Marshal.AllocHGlobal(bytesNeeded);
                        try
                        {
                            if (!_windowsServiceApi.QueryServiceConfig(service, buffer, bytesNeeded, out _))
                            {
                                Logger.Warn($"Could not read the '{AppConfig.ServyHostServiceName}' service's configuration. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                                return null;
                            }

                            var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buffer);
                            return Marshal.PtrToStringUni(config.lpBinaryPathName);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not read the '{AppConfig.ServyHostServiceName}' service's executable: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines whether a service command line runs the given executable.
        /// </summary>
        /// <param name="commandLine">The command line the Service Control Manager runs, quoted or not.</param>
        /// <param name="exePath">The expected executable.</param>
        /// <returns><see langword="true"/> when the command line's executable is <paramref name="exePath"/>.</returns>
        internal static bool IsSameExecutable(string commandLine, string exePath)
        {
            var registered = commandLine.Trim();
            if (registered.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = registered.IndexOf('"', 1);
                registered = end > 0 ? registered.Substring(1, end - 1) : registered.Substring(1);
            }

            try
            {
                return string.Equals(Path.GetFullPath(registered), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return false;
            }
        }

        /// <summary>
        /// Lists the running Servy services: the ones this build's wrappers run, and every running service that depends on
        /// the Servy host service, which includes the services the other build (net10 or net48) installed.
        /// </summary>
        /// <param name="serviceHelper">Lists the services this build's wrappers run.</param>
        /// <returns>The service names, without duplicates.</returns>
        private List<string> GetRunningServyServices(IServiceHelper serviceHelper)
        {
            var names = new List<string>(serviceHelper.GetRunningServyServices() ?? new List<string>());
            try
            {
                foreach (var sc in _serviceControllerProvider.GetServices())
                {
                    using (sc)
                    {
                        try
                        {
                            if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                                continue;

                            if (sc.GetDependencyNames().Contains(AppConfig.ServyHostServiceName, StringComparer.OrdinalIgnoreCase))
                                names.Add(sc.ServiceName);
                        }
                        catch (InvalidOperationException)
                        {
                            // Removed while enumerating
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not list the services that depend on the '{AppConfig.ServyHostServiceName}' service: {ex.Message}");
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Creates the service, or reconfigures it: Automatic (not delayed) start, Local System, and the given executable.
        /// </summary>
        /// <param name="hostExePath">The full path of <c>Servy.Host.exe</c>.</param>
        /// <exception cref="Win32Exception">Thrown when the Service Control Manager refuses an operation.</exception>
        private void EnsureInstalledAndAutomatic(string hostExePath)
        {
            string binPath = Helper.Quote(hostExePath);

            using (var scm = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE))
            {
                if (scm == null || scm.IsInvalid)
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");

                const uint access = SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_START | (uint)SERVICE_QUERY_STATUS;
                using (var existing = _windowsServiceApi.OpenService(scm, AppConfig.ServyHostServiceName, access))
                {
                    if (existing != null && !existing.IsInvalid)
                    {
                        // Startup type Automatic and the executable this app extracted; the account stays Local System
                        if (!_windowsServiceApi.ChangeServiceConfig(existing, SERVICE_NO_CHANGE, SERVICE_AUTO_START, SERVICE_NO_CHANGE, binPath, null, IntPtr.Zero, null, ServiceAccounts.LocalSystem, null, null))
                            throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), $"Failed to configure the '{AppConfig.ServyHostServiceName}' service.");

                        SetNotDelayed(existing);
                        Logger.Debug($"The '{AppConfig.ServyHostServiceName}' service is installed; its startup type is Automatic.");
                        return;
                    }

                    var openError = _win32ErrorProvider.GetLastWin32Error();
                    if (openError != ERROR_SERVICE_DOES_NOT_EXIST)
                        throw new Win32Exception(openError, $"Failed to open the '{AppConfig.ServyHostServiceName}' service.");
                }

                using (var created = _windowsServiceApi.CreateService(
                    hSCManager: scm,
                    lpServiceName: AppConfig.ServyHostServiceName,
                    lpDisplayName: AppConfig.ServyHostDisplayName,
                    dwDesiredAccess: access,
                    dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                    dwStartType: SERVICE_AUTO_START,
                    dwErrorControl: SERVICE_ERROR_NORMAL,
                    lpBinaryPathName: binPath,
                    lpLoadOrderGroup: null,
                    lpdwTagId: IntPtr.Zero,
                    lpDependencies: null,
                    lpServiceStartName: null, // Local System
                    lpPassword: null))
                {
                    if (created == null || created.IsInvalid)
                        throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), $"Failed to create the '{AppConfig.ServyHostServiceName}' service.");

                    SetDescription(created);
                    Logger.Info($"Installed the '{AppConfig.ServyHostServiceName}' service ({binPath}).");
                }
            }
        }

        /// <summary>
        /// Sets the service's description; a failure is only logged.
        /// </summary>
        /// <param name="service">The service handle.</param>
        private void SetDescription(SafeServiceHandle service)
        {
            var pDescription = Marshal.StringToHGlobalUni(AppConfig.ServyHostDescription);
            try
            {
                var description = new SERVICE_DESCRIPTION { lpDescription = pDescription };
                if (!_windowsServiceApi.ChangeServiceConfig2(service, SERVICE_CONFIG_DESCRIPTION, ref description))
                    Logger.Warn($"Could not set the description of the '{AppConfig.ServyHostServiceName}' service. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
            }
            finally
            {
                Marshal.FreeHGlobal(pDescription);
            }
        }

        /// <summary>
        /// Clears the delayed flag, so the startup type is plain Automatic; a failure is only logged.
        /// </summary>
        /// <param name="service">The service handle.</param>
        private void SetNotDelayed(SafeServiceHandle service)
        {
            var info = new SERVICE_DELAYED_AUTO_START_INFO { fDelayedAutostart = false };
            if (!_windowsServiceApi.ChangeServiceConfig2(service, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, ref info))
                Logger.Warn($"Could not clear the delayed start of the '{AppConfig.ServyHostServiceName}' service. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
        }

        /// <summary>
        /// Waits for a service to reach a status, polling so the wait can be cancelled.
        /// </summary>
        /// <param name="sc">The service controller.</param>
        /// <param name="status">The status to wait for.</param>
        /// <param name="cancellationToken">A token that stops the wait.</param>
        /// <returns>A task that completes when the service reached the status.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service did not reach it within the timeout.</exception>
        private async Task WaitForStatusAsync(IServiceControllerWrapper sc, ServiceControllerStatus status, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + _timeout;
            sc.Refresh();
            while (sc.Status != status)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new InvalidOperationException($"The '{AppConfig.ServyHostServiceName}' service did not reach the {status} state within {_timeout.TotalSeconds:0} seconds.");

                await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                sc.Refresh();
            }
        }

        /// <summary>
        /// Determines whether a service controller refers to an installed service.
        /// </summary>
        /// <param name="sc">The controller.</param>
        /// <returns><see langword="false"/> when reading its status fails.</returns>
        private static bool IsInstalled(IServiceControllerWrapper sc)
        {
            try
            {
                _ = sc.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Logs and builds a failed result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The failed result.</returns>
        private static OperationResult Fail(string message)
        {
            Logger.Error(message);
            return OperationResult.Failure(message);
        }
    }
}
