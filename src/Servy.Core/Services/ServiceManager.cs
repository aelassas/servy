using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        public const string LocalSystemAccount = "LocalSystem";

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
        /// <param name="lpDependencies">Service dependencies.</param>
        /// <param name="displayName">Service display name.</param>
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
            string password,
            string lpDependencies,
            string displayName
            )
        {
            IntPtr serviceHandle = _windowsServiceApi.OpenService(
                scmHandle,
                serviceName,
                SERVICE_CHANGE_CONFIG | SERVICE_QUERY_CONFIG);

            if (serviceHandle == IntPtr.Zero)
                throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open existing service.");

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = serviceName;
            }

            try
            {
                var result = _windowsServiceApi.ChangeServiceConfig(
                        hService: serviceHandle,
                        dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                        dwStartType: (uint)(startType == ServiceStartType.AutomaticDelayedStart ? ServiceStartType.Automatic : startType),
                        dwErrorControl: SERVICE_ERROR_NORMAL,
                        lpBinaryPathName: binPath,
                        lpLoadOrderGroup: null,
                        lpdwTagId: IntPtr.Zero,
                        lpDependencies: lpDependencies,
                        lpServiceStartName: username,
                        lpPassword: password,
                        lpDisplayName: displayName
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

            var desc = new ServiceDescription
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

        /// <summary>
        /// Enables or disables the delayed auto-start setting for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">
        /// A handle to the service whose configuration is to be changed.  
        /// The handle must have the <c>SERVICE_CHANGE_CONFIG</c> access right.
        /// </param>
        /// <param name="delayedAutostart">
        /// <see langword="true"/> to enable delayed auto-start;  
        /// <see langword="false"/> to disable it.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the configuration change succeeds; otherwise, <see langword="false"/>.  
        /// Call <see cref="Marshal.GetLastWin32Error"/> to retrieve extended error information.
        /// </returns>
        /// <remarks>
        /// This method wraps the native <c>ChangeServiceConfig2</c> function with the 
        /// <c>SERVICE_CONFIG_DELAYED_AUTO_START_INFO</c> information level.  
        /// It can be used only for services whose start type is set to <c>Automatic</c>.
        /// </remarks>
        private bool ChangeServiceConfig2(
            IntPtr serviceHandle,
            bool delayedAutostart
            )
        {
            var delayedInfo = new ServiceDelayedAutoStartInfo
            {
                fDelayedAutostart = delayedAutostart,
            };

            var success = _windowsServiceApi.ChangeServiceConfig2(
                serviceHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref delayedInfo
            );
            return success;
        }

        #endregion

        #region IServiceManager Implementation

        /// <inheritdoc />
        public async Task<bool> InstallService(
                string serviceName,
                string description,
                string wrapperExePath,
                string realExePath,
                string workingDirectory = null,
                string realArgs = null,
                ServiceStartType startType = ServiceStartType.Automatic,
                ProcessPriority processPriority = ProcessPriority.Normal,
                string stdoutPath = null,
                string stderrPath = null,
                bool enableSizeRotation = false,
                ulong rotationSizeInBytes = AppConfig.DefaultRotationSize * 1024 * 1024,
                bool enableHealthMonitoring = false,
                int heartbeatInterval = AppConfig.DefaultHeartbeatInterval,
                int maxFailedChecks = AppConfig.DefaultMaxFailedChecks,
                RecoveryAction recoveryAction = RecoveryAction.None,
                int maxRestartAttempts = AppConfig.DefaultMaxRestartAttempts,
                string environmentVariables = null,
                string serviceDependencies = null,
                string username = null,
                string password = null,
                string preLaunchExePath = null,
                string preLaunchWorkingDirectory = null,
                string preLaunchArgs = null,
                string preLaunchEnvironmentVariables = null,
                string preLaunchStdoutPath = null,
                string preLaunchStderrPath = null,
                int preLaunchTimeout = AppConfig.DefaultPreLaunchTimeoutSeconds,
                int preLaunchRetryAttempts = AppConfig.DefaultPreLaunchRetryAttempts,
                bool preLaunchIgnoreFailure = false,
                string failureProgramPath = null,
                string failureProgramWorkingDirectory = null,
                string failureProgramArgs = null,
                string postLaunchExePath = null,
                string postLaunchWorkingDirectory = null,
                string postLaunchArgs = null,
                bool enableDebugLogs = false,
                string displayName = null,
                int? maxRotations = AppConfig.DefaultMaxRotations,
                bool enableDateRotation = false,
                DateRotationType dateRotationType = DateRotationType.Daily,
                int? startTimeout = AppConfig.DefaultStartTimeout,
                int? stopTimeout = AppConfig.DefaultStopTimeout
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
                //Helper.Quote(realArgs ?? string.Empty),
                Helper.Quote(string.Empty), // Process parameters are no longer passed from binary path and are retrived from DB instead
                Helper.Quote(workingDirectory ?? string.Empty),
                Helper.Quote(processPriority.ToString()),
                Helper.Quote(stdoutPath ?? string.Empty),
                Helper.Quote(stderrPath ?? string.Empty),
                Helper.Quote(enableSizeRotation ? rotationSizeInBytes.ToString() : "0"),
                Helper.Quote(enableHealthMonitoring ? heartbeatInterval.ToString() : "0"),
                Helper.Quote(enableHealthMonitoring ? maxFailedChecks.ToString() : "0"),
                Helper.Quote(recoveryAction.ToString()),
                Helper.Quote(serviceName),
                Helper.Quote(enableHealthMonitoring ? maxRestartAttempts.ToString() : "0"),
                //Helper.Quote(environmentVariables ?? string.Empty),
                Helper.Quote(string.Empty), // Environment variables are no longer passed from binary path and are retrived from DB instead

                // Pre-Launch
                Helper.Quote(preLaunchExePath ?? string.Empty),
                Helper.Quote(preLaunchWorkingDirectory ?? string.Empty),
                //Helper.Quote(preLaunchArgs ?? string.Empty),
                Helper.Quote(string.Empty), // Process parameters are no longer passed from binary path and are retrived from DB instead
                //Helper.Quote(preLaunchEnvironmentVariables ?? string.Empty),
                Helper.Quote(string.Empty), // Environment variables are no longer passed from binary path and are retrived from DB instead
                Helper.Quote(preLaunchStdoutPath ?? string.Empty),
                Helper.Quote(preLaunchStderrPath ?? string.Empty),
                Helper.Quote(preLaunchTimeout.ToString()),
                Helper.Quote(preLaunchRetryAttempts.ToString()),
                Helper.Quote(preLaunchIgnoreFailure.ToString()),

                // Failure program
                Helper.Quote(failureProgramPath ?? string.Empty),
                Helper.Quote(failureProgramWorkingDirectory ?? string.Empty),
                //Helper.Quote(failureProgramArgs ?? string.Empty),
                Helper.Quote(string.Empty), // Process parameters are no longer passed from binary path and are retrived from DB instead

                // Post-Launch
                Helper.Quote(postLaunchExePath ?? string.Empty),
                Helper.Quote(postLaunchWorkingDirectory ?? string.Empty),
                //Helper.Quote(postLaunchArgs ?? string.Empty),
                Helper.Quote(string.Empty), // Process parameters are no longer passed from binary path and are retrived from DB instead

                // Debug Logs
                Helper.Quote(enableDebugLogs.ToString()),

                // Max rotations
                Helper.Quote(maxRotations.ToString()),

                // Date rotation
                Helper.Quote(enableSizeRotation.ToString()), // size rotation
                Helper.Quote(enableDateRotation.ToString()),
                Helper.Quote(dateRotationType.ToString()),

                // Start/Stop timeouts
                Helper.Quote((startTimeout ?? AppConfig.DefaultStartTimeout).ToString()),
                Helper.Quote((stopTimeout ?? AppConfig.DefaultStopTimeout).ToString())

            );

            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero)
                throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = serviceName;
            }

            IntPtr serviceHandle = IntPtr.Zero;
            try
            {
                string lpDependencies = ServiceDependenciesParser.Parse(serviceDependencies);
                string lpServiceStartName = string.IsNullOrWhiteSpace(username) ? LocalSystemAccount : username;
                string lpPassword = string.IsNullOrEmpty(password) ? null : password;

                // Grant "Log on as a service" only for regular user accounts (local or Active Directory).
                // Skip LocalSystem (already has rights) and gMSA accounts (managed by Active Directory policies).
                bool isLocalSystem = lpServiceStartName.Equals(LocalSystemAccount, StringComparison.OrdinalIgnoreCase);
                bool isGmsa = lpServiceStartName.EndsWith("$");

                // For normal user accounts (local or AD) that are not gMSA or LocalSystem,
                // explicitly ensure they have the "Log on as a service" right locally.
                if (!isLocalSystem && !isGmsa)
                {
                    _windowsServiceApi.EnsureLogOnAsServiceRight(lpServiceStartName);
                }

                // Create the service if it does not exist
                serviceHandle = _windowsServiceApi.CreateService(
                    hSCManager: scmHandle,
                    lpServiceName: serviceName,
                    lpDisplayName: displayName,
                    dwDesiredAccess: SERVICE_START | SERVICE_STOP | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_DELETE,
                    dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                    dwStartType: (uint)(startType == ServiceStartType.AutomaticDelayedStart ? ServiceStartType.Automatic : startType),
                    dwErrorControl: SERVICE_ERROR_NORMAL,
                    lpBinaryPathName: binPath,
                    lpLoadOrderGroup: null,
                    lpdwTagId: IntPtr.Zero,
                    lpDependencies: lpDependencies,
                    lpServiceStartName: lpServiceStartName,
                    lpPassword: lpPassword
                );

                // Persist service in database
                var dto = new ServiceDto
                {
                    Name = serviceName,
                    DisplayName = displayName,
                    Description = description,
                    ExecutablePath = realExePath,
                    StartupDirectory = workingDirectory,
                    Parameters = realArgs,
                    StartupType = (int)startType,
                    Priority = (int)processPriority,
                    StdoutPath = stdoutPath,
                    StderrPath = stderrPath,
                    EnableRotation = enableSizeRotation,
                    RotationSize = (int)(rotationSizeInBytes / (1024 * 1024)),
                    EnableDateRotation = enableDateRotation,
                    DateRotationType = (int)dateRotationType,
                    MaxRotations = maxRotations,
                    EnableHealthMonitoring = enableHealthMonitoring,
                    HeartbeatInterval = heartbeatInterval,
                    MaxFailedChecks = maxFailedChecks,
                    RecoveryAction = (int)recoveryAction,
                    MaxRestartAttempts = maxRestartAttempts,
                    FailureProgramPath = failureProgramPath,
                    FailureProgramStartupDirectory = failureProgramWorkingDirectory,
                    FailureProgramParameters = failureProgramArgs,
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
                    PreLaunchIgnoreFailure = preLaunchIgnoreFailure,

                    PostLaunchExecutablePath = postLaunchExePath,
                    PostLaunchStartupDirectory = postLaunchWorkingDirectory,
                    PostLaunchParameters = postLaunchArgs,

                    EnableDebugLogs = enableDebugLogs,

                    StartTimeout = startTimeout,
                    StopTimeout = stopTimeout
                };

                // Set PID
                var serviceDto = await _serviceRepository.GetByNameAsync(serviceName);
                dto.Pid = serviceDto?.Pid;

                // Set delayed auto-start if necessary
                if (serviceHandle != IntPtr.Zero && startType == ServiceStartType.AutomaticDelayedStart)
                {
                    var success = ChangeServiceConfig2(serviceHandle, true);

                    if (!success)
                    {
                        return false;
                    }
                }

                if (serviceHandle == IntPtr.Zero)
                {
                    var isInstalled = IsServiceInstalled(serviceName);
                    if (isInstalled)
                    {
                        // Service exists - update its configuration
                        _ = UpdateServiceConfig(
                            scmHandle: scmHandle,
                            serviceName: serviceName,
                            description: description,
                            binPath: binPath,
                            startType: startType,
                            username: lpServiceStartName,
                            password: lpPassword,
                            lpDependencies: lpDependencies,
                            displayName: displayName
                        );

                        // Set delayed auto-start if necessary
                        if (startType == ServiceStartType.AutomaticDelayedStart || startType == ServiceStartType.Automatic)
                        {
                            IntPtr existingServiceHandle = _windowsServiceApi.OpenService(
                                scmHandle,
                                serviceName,
                                SERVICE_CHANGE_CONFIG
                            );

                            try
                            {
                                var delayedAutostart = startType == ServiceStartType.AutomaticDelayedStart;
                                var success = ChangeServiceConfig2(existingServiceHandle, delayedAutostart);

                                if (!success)
                                {
                                    return false;
                                }
                            }
                            finally
                            {
                                _windowsServiceApi.CloseServiceHandle(existingServiceHandle);
                            }
                        }

                        await _serviceRepository.UpsertAsync(dto);
                        return true;
                    }
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
                    var status = new NativeMethods.ServiceStatus();
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

            var services = _searcher.Get($"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'", cancellationToken);
            var service = services.FirstOrDefault();
            var startMode = service?["StartMode"]?.ToString() ?? string.Empty;

            if (string.Equals(startMode, "Auto", StringComparison.OrdinalIgnoreCase))
                startMode = "Automatic";

            if (!string.IsNullOrEmpty(startMode))
            {
                try
                {
                    return (ServiceStartType)Enum.Parse(typeof(ServiceStartType), startMode, true);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public string GetServiceDescription(string serviceName, CancellationToken cancellationToken = default)
        {
            try
            {
                var services = _searcher.Get($"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'", cancellationToken);
                var service = services.FirstOrDefault();
                return service?["Description"]?.ToString();
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

            var services = _searcher.Get($"SELECT StartName FROM Win32_Service WHERE Name = '{serviceName}'", cancellationToken);
            var service = services.FirstOrDefault();
            account = service?["StartName"]?.ToString();

            return account;
        }

        /// <inheritdoc/>
        public List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default)
        {
            var results = new ConcurrentBag<ServiceInfo>();
            const string query = "SELECT Name, State, StartMode, StartName, Description FROM Win32_Service";

            var wmiResults = _searcher.Get(query).ToList();

            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_ENUMERATE_SERVICE);
            if (scmHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open Service Control Manager.");

            int delayedStructSize = Marshal.SizeOf(typeof(ServiceDelayedAutoStartInfo));

            try
            {
                Parallel.ForEach(wmiResults, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                service =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string name = (service["Name"] as string) ?? string.Empty;
                    string description = (service["Description"] as string) ?? string.Empty;
                    string user = (service["StartName"] as string) ?? "LocalSystem";
                    string startMode = ((service["StartMode"] as string) ?? string.Empty).ToLowerInvariant();
                    string stateStr = ((service["State"] as string) ?? string.Empty).ToLowerInvariant();

                    // Map service state
                    Enums.ServiceStatus status;
                    switch (stateStr)
                    {
                        case "running": status = Enums.ServiceStatus.Running; break;
                        case "stopped": status = Enums.ServiceStatus.Stopped; break;
                        case "paused": status = Enums.ServiceStatus.Paused; break;
                        case "start pending": status = Enums.ServiceStatus.StartPending; break;
                        case "stop pending": status = Enums.ServiceStatus.StopPending; break;
                        case "pause pending": status = Enums.ServiceStatus.PausePending; break;
                        case "continue pending": status = Enums.ServiceStatus.ContinuePending; break;
                        default: status = Enums.ServiceStatus.None; break;
                    }

                    // Map service start mode
                    ServiceStartType startupType;
                    switch (startMode)
                    {
                        case "auto":
                        case "automatic": startupType = ServiceStartType.Automatic; break;
                        case "manual": startupType = ServiceStartType.Manual; break;
                        case "disabled": startupType = ServiceStartType.Disabled; break;
                        default: startupType = ServiceStartType.Automatic; break;
                    }

                    // Check delayed auto-start if Automatic
                    if (startupType == ServiceStartType.Automatic)
                    {
                        IntPtr svcHandle = _windowsServiceApi.OpenService(scmHandle, name, SERVICE_QUERY_CONFIG);
                        if (svcHandle != IntPtr.Zero)
                        {
                            try
                            {
                                var info = new ServiceDelayedAutoStartInfo();
                                int bytesNeeded = 0;

                                var ok = _windowsServiceApi.QueryServiceConfig2(
                                    svcHandle,
                                    SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                                    ref info,
                                    delayedStructSize,
                                    ref bytesNeeded);

                                if (ok && info.fDelayedAutostart)
                                {
                                    startupType = ServiceStartType.AutomaticDelayedStart;
                                }
                            }
                            finally
                            {
                                _windowsServiceApi.CloseServiceHandle(svcHandle);
                            }
                        }
                    }

                    results.Add(new ServiceInfo
                    {
                        Name = name,
                        Status = status,
                        StartupType = startupType,
                        UserSession = user,
                        Description = description,
                    });
                });
            }
            finally
            {
                if (scmHandle != IntPtr.Zero)
                    _windowsServiceApi.CloseServiceHandle(scmHandle);
            }

            return results.OrderBy(s => s.Name).ToList();
        }


    }


    #endregion
}

#pragma warning restore CS8625
