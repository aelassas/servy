using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.ServiceDependencies;
using System;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using static Servy.Core.Native.NativeMethods;

#pragma warning disable CS8625
namespace Servy.Core.Services
{
    /// <summary>
    /// Provides methods to install, uninstall, start, stop, restart, and update Windows services.
    /// Handles low-level Service Control Manager operations, configuration updates,
    /// process monitoring, logging, and recovery options.
    /// </summary>
    public class ServiceManager : IServiceManager
    {
        #region Constants

        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_QUERY_CONFIG = 0x0001;
        private const uint SERVICE_CHANGE_CONFIG = 0x0002;
        private const uint SERVICE_START = 0x0010;
        private const uint SERVICE_STOP = 0x0020;
        private const uint SERVICE_DELETE = 0x00010000;
        private const int SERVICE_CONFIG_DESCRIPTION = 1;
        private const int ServiceStopTimeoutSeconds = 60;

        #endregion

        #region Private Fields

        private readonly Func<string, IServiceControllerWrapper> _controllerFactory;
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly IWin32ErrorProvider _win32ErrorProvider;
        private readonly IServiceRepository _serviceRepository;
        private readonly IWmiSearcher _searcher;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceManager"/> class.
        /// </summary>
        /// <param name="controllerFactory">
        /// Factory function that creates a wrapper for controlling a Windows service.
        /// </param>
        /// <param name="windowsServiceApi">
        /// Abstraction for low-level Win32 service API calls.
        /// </param>
        /// <param name="win32ErrorProvider">
        /// Provider for retrieving the last Win32 error codes.
        /// </param>
        /// <param name="_serviceRepository">
        /// Service repository.
        /// </param>
        /// <param name="serviceRepository">Service Repository.</param>
        /// <param name="searcher">WMI searcher.</param>
        public ServiceManager(
            Func<string, IServiceControllerWrapper> controllerFactory,
            IWindowsServiceApi windowsServiceApi,
            IWin32ErrorProvider win32ErrorProvider,
            IServiceRepository serviceRepository,
            IWmiSearcher searcher
            )
        {
            _controllerFactory = controllerFactory;
            _windowsServiceApi = windowsServiceApi;
            _win32ErrorProvider = win32ErrorProvider;
            _serviceRepository = serviceRepository;
            _searcher = searcher;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Updates the configuration of an existing Windows service.
        /// </summary>
        /// <param name="scmHandle">Handle to the Service Control Manager.</param>
        /// <param name="serviceName">The service name.</param>
        /// <param name="description">The service description.</param>
        /// <param name="binPath">The path to the service executable.</param>
        /// <param name="startType">The service startup type.</param>
        /// <param name="username">Service account username: .\username  for local accounts, DOMAIN\username for domain accounts.</param>
        /// <param name="password">Service account password.</param>
        /// <returns>
        /// <see langword="true"/> if the configuration was updated successfully; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="Win32Exception">Thrown if updating the service configuration fails.</exception>
        public bool UpdateServiceConfig(
            IntPtr scmHandle,
            string serviceName,
            string description,
            string binPath,
            ServiceStartType startType,
            string username,
            string password
            )
        {
            IntPtr serviceHandle = _windowsServiceApi.OpenService(
                scmHandle,
                serviceName,
                SERVICE_CHANGE_CONFIG | SERVICE_QUERY_CONFIG);

            if (serviceHandle == IntPtr.Zero)
                throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open existing service.");

            try
            {
                var result = _windowsServiceApi.ChangeServiceConfig(
                        hService: serviceHandle,
                        dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                        dwStartType: (uint)startType,
                        dwErrorControl: SERVICE_ERROR_NORMAL,
                        lpBinaryPathName: binPath,
                        lpLoadOrderGroup: null,
                        lpdwTagId: IntPtr.Zero,
                        lpDependencies: null,
                        lpServiceStartName: username,
                        lpPassword: password,
                        lpDisplayName: null
                        );

                if (!result)
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to update service config.");

                SetServiceDescription(serviceHandle, description);

                return true;
            }
            finally
            {
                _windowsServiceApi.CloseServiceHandle(serviceHandle);
            }
        }

        /// <summary>
        /// Sets the description for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">Handle to the service.</param>
        /// <param name="description">The description text.</param>
        /// <exception cref="Win32Exception">Thrown if setting the description fails.</exception>
        public void SetServiceDescription(IntPtr serviceHandle, string description)
        {
            if (string.IsNullOrEmpty(description))
                return;

            var desc = new SERVICE_DESCRIPTION
            {
                lpDescription = Marshal.StringToHGlobalUni(description)
            };

            if (!_windowsServiceApi.ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_DESCRIPTION, ref desc))
            {
                int err = _win32ErrorProvider.GetLastWin32Error();
                throw new Win32Exception(err, "Failed to set service description.");
            }

            Marshal.FreeHGlobal(desc.lpDescription);
        }

        #endregion

        #region IServiceManager Implementation

        /// <inheritdoc />
        public async Task<bool> InstallService(
                string serviceName,
                string description,
                string wrapperExePath,
                string realExePath,
                string workingDirectory,
                string realArgs,
                ServiceStartType startType,
                ProcessPriority processPriority,
                string stdoutPath,
                string stderrPath,
                int rotationSizeInBytes,
                int heartbeatInterval,
                int maxFailedChecks,
                RecoveryAction recoveryAction,
                int maxRestartAttempts,
                string environmentVariables,
                string serviceDependencies,
                string username,
                string password,
                string preLaunchExePath,
                string preLaunchWorkingDirectory,
                string preLaunchArgs,
                string preLaunchEnvironmentVariables,
                string preLaunchStdoutPath,
                string preLaunchStderrPath,
                int preLaunchTimeout = 5,
                int preLaunchRetryAttempts = 0,
                bool preLaunchIgnoreFailure = false
            )
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentNullException(nameof(serviceName));
            if (string.IsNullOrWhiteSpace(wrapperExePath))
                throw new ArgumentNullException(nameof(wrapperExePath));
            if (string.IsNullOrWhiteSpace(realExePath))
                throw new ArgumentNullException(nameof(realExePath));

            // Compose binary path with wrapper and parameters
            string binPath = string.Join(" ",
                Helper.Quote(wrapperExePath),
                Helper.Quote(realExePath),
                Helper.Quote(realArgs ?? string.Empty),
                Helper.Quote(workingDirectory ?? string.Empty),
                Helper.Quote(processPriority.ToString()),
                Helper.Quote(stdoutPath ?? string.Empty),
                Helper.Quote(stderrPath ?? string.Empty),
                Helper.Quote(rotationSizeInBytes.ToString()),
                Helper.Quote(heartbeatInterval.ToString()),
                Helper.Quote(maxFailedChecks.ToString()),
                Helper.Quote(recoveryAction.ToString()),
                Helper.Quote(serviceName),
                Helper.Quote(maxRestartAttempts.ToString()),
                Helper.Quote(environmentVariables ?? string.Empty),

                // Pre-Launch
                Helper.Quote(preLaunchExePath ?? string.Empty),
                Helper.Quote(preLaunchWorkingDirectory ?? string.Empty),
                Helper.Quote(preLaunchArgs ?? string.Empty),
                Helper.Quote(preLaunchEnvironmentVariables ?? string.Empty),
                Helper.Quote(preLaunchStdoutPath ?? string.Empty),
                Helper.Quote(preLaunchStderrPath ?? string.Empty),
                Helper.Quote(preLaunchTimeout.ToString()),
                Helper.Quote(preLaunchRetryAttempts.ToString()),
                Helper.Quote(preLaunchIgnoreFailure.ToString())
            );

            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero)
                throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");

            IntPtr serviceHandle = IntPtr.Zero;
            try
            {
                string lpDependencies = ServiceDependenciesParser.Parse(serviceDependencies);
                string lpServiceStartName = string.IsNullOrWhiteSpace(username) ? null : username;
                string lpPassword = string.IsNullOrEmpty(password) ? null : password;

                serviceHandle = _windowsServiceApi.CreateService(
                    hSCManager: scmHandle,
                    lpServiceName: serviceName,
                    lpDisplayName: serviceName,
                    dwDesiredAccess: SERVICE_START | SERVICE_STOP | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_DELETE,
                    dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                    dwStartType: (uint)startType,
                    dwErrorControl: SERVICE_ERROR_NORMAL,
                    lpBinaryPathName: binPath,
                    lpLoadOrderGroup: null,
                    lpdwTagId: IntPtr.Zero,
                    lpDependencies: lpDependencies,
                    lpServiceStartName: lpServiceStartName,
                    lpPassword: lpPassword
                );


                // Persist service in database
                var dto = new DTOs.ServiceDto
                {
                    Name = serviceName,
                    Description = description,
                    ExecutablePath = realExePath,
                    StartupDirectory = workingDirectory,
                    Parameters = realArgs,
                    StartupType = (int)startType,
                    Priority = (int)processPriority,
                    StdoutPath = stdoutPath,
                    StderrPath = stderrPath,
                    EnableRotation = rotationSizeInBytes > 0,
                    RotationSize = rotationSizeInBytes,
                    EnableHealthMonitoring = heartbeatInterval > 0,
                    HeartbeatInterval = heartbeatInterval,
                    MaxFailedChecks = maxFailedChecks,
                    RecoveryAction = (int)recoveryAction,
                    MaxRestartAttempts = maxRestartAttempts,
                    EnvironmentVariables = environmentVariables,
                    ServiceDependencies = serviceDependencies,
                    RunAsLocalSystem = string.IsNullOrWhiteSpace(username),
                    UserAccount = username,
                    Password = password,
                    PreLaunchExecutablePath = preLaunchExePath,
                    PreLaunchStartupDirectory = preLaunchWorkingDirectory,
                    PreLaunchParameters = preLaunchArgs,
                    PreLaunchEnvironmentVariables = preLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = preLaunchStdoutPath,
                    PreLaunchStderrPath = preLaunchStderrPath,
                    PreLaunchTimeoutSeconds = preLaunchTimeout,
                    PreLaunchRetryAttempts = preLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = preLaunchIgnoreFailure
                };

                if (serviceHandle == IntPtr.Zero)
                {
                    int err = _win32ErrorProvider.GetLastWin32Error();
                    if (err == 1073) // ERROR_SERVICE_EXISTS
                    {
                        var res = UpdateServiceConfig(scmHandle, serviceName, description, binPath, startType, lpServiceStartName, lpPassword);
                        await _serviceRepository.UpsertAsync(dto);
                        return res;
                    }

                    throw new Win32Exception(err, "Failed to create service.");
                }

                // Set description
                SetServiceDescription(serviceHandle, description);

                await _serviceRepository.UpsertAsync(dto);

                return true;
            }
            finally
            {
                if (serviceHandle != IntPtr.Zero)
                    _windowsServiceApi.CloseServiceHandle(serviceHandle);
                if (scmHandle != IntPtr.Zero)
                    _windowsServiceApi.CloseServiceHandle(scmHandle);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UninstallService(string serviceName)
        {
            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero)
                return false;

            try
            {
                IntPtr serviceHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, SERVICE_ALL_ACCESS);
                if (serviceHandle == IntPtr.Zero)
                    return false;

                try
                {
                    // Change start type to demand start (if it's disabled)
                    _windowsServiceApi.ChangeServiceConfig(
                        serviceHandle,
                        SERVICE_NO_CHANGE,
                        SERVICE_DEMAND_START,
                        SERVICE_NO_CHANGE,
                        null,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null);

                    // Try to stop service
                    var status = new SERVICE_STATUS();
                    _windowsServiceApi.ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status);

                    // Wait for service to actually stop (up to 60 seconds)
                    using (var sc = _controllerFactory(serviceName))
                    {
                        sc.Refresh();
                        DateTime waitUntil = DateTime.Now.AddSeconds(ServiceStopTimeoutSeconds);

                        while (sc.Status != ServiceControllerStatus.Stopped && DateTime.Now < waitUntil)
                        {
                            Thread.Sleep(500); // Poll every half-second
                            sc.Refresh();
                        }
                    }

                    // Delete the service
                    var res = _windowsServiceApi.DeleteService(serviceHandle);

                    if (res)
                    {
                        await _serviceRepository.DeleteAsync(serviceName);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                finally
                {
                    _windowsServiceApi.CloseServiceHandle(serviceHandle);
                }
            }
            finally
            {
                _windowsServiceApi.CloseServiceHandle(scmHandle);
            }
        }

        /// <inheritdoc />
        public bool StartService(string serviceName)
        {
            try
            {
                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return true;

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool StopService(string serviceName)
        {
            try
            {
                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return true;

                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(ServiceStopTimeoutSeconds));

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool RestartService(string serviceName)
        {
            if (!StopService(serviceName))
                return false;

            return StartService(serviceName);
        }

        /// <inheritdoc />
        public ServiceControllerStatus GetServiceStatus(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            using (var sc = _controllerFactory(serviceName))
            {
                return sc.Status;
            }
        }

        ///<inheritdoc />
        public bool IsServiceInstalled(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentNullException(nameof(serviceName));

            return _windowsServiceApi.GetServices()
                            .Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public ServiceStartType? GetServiceStartupType(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentNullException(nameof(serviceName));

            string startMode = null;

            foreach (ManagementObject service in _searcher.Get($"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'", cancellationToken))
            {
                startMode = service["StartMode"]?.ToString() ?? string.Empty;
                break;
            }

            if (string.Equals(startMode, "Auto", StringComparison.OrdinalIgnoreCase))
                startMode = "Automatic";

            if (!string.IsNullOrEmpty(startMode))
            {
                try { return (ServiceStartType)Enum.Parse(typeof(ServiceStartType), startMode, true); }
                catch (ArgumentException) { return null; }
            }

            return null;
        }

        /// <inheritdoc />
        public string GetServiceDescription(string serviceName, CancellationToken cancellationToken = default)
        {
            try
            {
                foreach (ManagementObject service in _searcher.Get($"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'", cancellationToken))
                {
                    return service["Description"]?.ToString() ?? null;
                }
            }
            catch
            {
                // Log or handle error
            }
            return null;
        }

        /// <inheritdoc />
        public string GetServiceUser(string serviceName, CancellationToken cancellationToken = default)
        {
            string account = null;

            foreach (ManagementObject service in _searcher.Get($"SELECT StartName FROM Win32_Service WHERE Name = '{serviceName}'", cancellationToken))
            {
                account = service["StartName"]?.ToString();
                break;
            }


            return account;
        }
    }


    #endregion
}

#pragma warning restore CS8625
