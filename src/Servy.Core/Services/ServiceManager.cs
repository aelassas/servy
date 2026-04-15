using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using static Servy.Core.Native.NativeMethods;

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

        private const int ServiceStopTimeoutSeconds = 60;
        private const int ServiceStartTimeoutSeconds = 30;
        private const int ScmPollIntervalMs = 500;
        private const int MaxParallelScmQueries = 8;
        public const string LocalSystemAccount = "LocalSystem";

        #endregion

        #region SCM Access Rights

        public const uint SC_MANAGER_CONNECT = 0x0001;
        public const uint SC_MANAGER_CREATE_SERVICE = 0x0002;

        #endregion

        #region Service Access Rights

        public const uint SERVICE_CHANGE_CONFIG = 0x0002;
        public const uint SERVICE_QUERY_STATUS = 0x0004;
        public const uint SERVICE_START = 0x0010;
        public const uint SERVICE_STOP = 0x0020;
        public const uint SERVICE_DELETE = 0x00010000; // Standardized to 8-digit hex for clarity

        #endregion

        #region Service Configuration & Type Flags

        public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        public const uint SERVICE_ERROR_NORMAL = 0x00000001;
        public const int SERVICE_CONFIG_PRESHUTDOWN_INFO = 7;

        #endregion

        #region Private Fields

        private readonly Func<string, IServiceControllerWrapper> _controllerFactory;
        private readonly IServiceControllerProvider _serviceControllerProvider;
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly IWin32ErrorProvider _win32ErrorProvider;
        private readonly IServiceRepository _serviceRepository;

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
        /// <param name="serviceRepository">
        /// Service repository.
        /// </param>
        public ServiceManager(
            Func<string, IServiceControllerWrapper> controllerFactory,
            IServiceControllerProvider serviceControllerProvider,
            IWindowsServiceApi windowsServiceApi,
            IWin32ErrorProvider win32ErrorProvider,
            IServiceRepository serviceRepository
            )
        {
            _controllerFactory = controllerFactory;
            _serviceControllerProvider = serviceControllerProvider;
            _windowsServiceApi = windowsServiceApi;
            _win32ErrorProvider = win32ErrorProvider;
            _serviceRepository = serviceRepository;
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
            string? username,
            string? password,
            string? lpDependencies,
            string? displayName
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
                        lpLoadOrderGroup: null!,
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
        internal void SetServiceDescription(IntPtr serviceHandle, string description)
        {
            if (string.IsNullOrEmpty(description))
                return;

            IntPtr pDescription = IntPtr.Zero;
            try
            {
                // Allocate unmanaged memory
                pDescription = Marshal.StringToHGlobalUni(description);

                var desc = new ServiceDescription
                {
                    lpDescription = pDescription
                };

                // Attempt to update the service configuration
                if (!_windowsServiceApi.ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_DESCRIPTION, ref desc))
                {
                    int err = _win32ErrorProvider.GetLastWin32Error();
                    throw new Win32Exception(err, "Failed to set service description.");
                }
            }
            finally
            {
                // GUARANTEE: Free the unmanaged memory even if an exception was thrown above.
                if (pDescription != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pDescription);
                }
            }
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

        /// <summary>
        /// Configures the service to accept pre-shutdown notifications and sets the maximum timeout 
        /// the Service Control Manager (SCM) will wait for this service to stop during a system shutdown.
        /// </summary>
        /// <param name="serviceHandle">
        /// A handle to the service. This handle must have the <c>SERVICE_CHANGE_CONFIG</c> (0x0002) access right.
        /// </param>
        /// <param name="timeoutMs">
        /// The duration, in milliseconds, that the SCM should wait for the service to finish its cleanup.
        /// </param>
        /// <returns>
        /// <c>true</c> if the configuration was successfully updated; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method uses <c>Marshal.AllocHGlobal</c> to provide a raw pointer to the native 
        /// <c>ChangeServiceConfig2</c> function. This configuration change is persistent in the 
        /// Windows Service database.
        /// </remarks>
        private bool EnablePreShutdown(IntPtr serviceHandle, uint timeoutMs)
        {
            var info = new ServicePreShutdownInfo
            {
                dwPreshutdownTimeout = timeoutMs
            };

            IntPtr ptr = IntPtr.Zero;
            try
            {
                // Allocate unmanaged memory for the structure to satisfy the void* requirement of the API
                ptr = Marshal.AllocHGlobal(Marshal.SizeOf(info));
                Marshal.StructureToPtr(info, ptr, false);

                return _windowsServiceApi.ChangeServiceConfig2(
                    serviceHandle,
                    SERVICE_CONFIG_PRESHUTDOWN_INFO,
                    ptr
                );
            }
            finally
            {
                // Always free the unmanaged memory to prevent memory leaks
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        #endregion

        #region IServiceManager Implementation

        /// <inheritdoc />
        public async Task<OperationResult> InstallServiceAsync(InstallServiceOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ServiceName))
                throw new ArgumentException(nameof(options.ServiceName));
            if (string.IsNullOrWhiteSpace(options.WrapperExePath))
                throw new ArgumentException(nameof(options.WrapperExePath));
            if (string.IsNullOrWhiteSpace(options.RealExePath))
                throw new ArgumentException(nameof(options.RealExePath));

            // Compose binary path with wrapper and parameters
            string binPath = string.Join(" ",
                Helper.Quote(options.WrapperExePath),
                Helper.Quote(options.ServiceName)
            );

            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null!, null!, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
            if (scmHandle == IntPtr.Zero)
                throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");

            string displayName = string.IsNullOrWhiteSpace(options.DisplayName) ? options.ServiceName : options.DisplayName;

            IntPtr serviceHandle = IntPtr.Zero;
            try
            {
                string? lpDependencies = ServiceDependenciesParser.Parse(options.ServiceDependencies);
                string lpServiceStartName = string.IsNullOrWhiteSpace(options.Username) ? LocalSystemAccount : options.Username;
                string? lpPassword = string.IsNullOrEmpty(options.Password) ? null : options.Password;

                // Grant "Log on as a service" only for regular user accounts (local or Active Directory).
                // Skip LocalSystem (already has rights) and gMSA accounts (managed by Active Directory policies).
                bool isLocalSystem = lpServiceStartName.Equals(LocalSystemAccount, StringComparison.OrdinalIgnoreCase);
                bool isGmsa = lpServiceStartName.EndsWith('$');

                // For normal user accounts (local or AD) that are not gMSA or LocalSystem,
                // explicitly ensure they have the "Log on as a service" right locally.
                if (!isLocalSystem && !isGmsa)
                {
                    _windowsServiceApi.EnsureLogOnAsServiceRight(lpServiceStartName);
                    Logger.Info($"Ensured 'Log on as a service' right for account '{lpServiceStartName}' for service '{options.ServiceName}'.");
                }

                // Create the service if it does not exist
                serviceHandle = _windowsServiceApi.CreateService(
                    hSCManager: scmHandle,
                    lpServiceName: options.ServiceName,
                    lpDisplayName: displayName,
                    dwDesiredAccess: SERVICE_START | SERVICE_STOP | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_DELETE,
                    dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                    dwStartType: (uint)(options.StartType == ServiceStartType.AutomaticDelayedStart ? ServiceStartType.Automatic : options.StartType),
                    dwErrorControl: SERVICE_ERROR_NORMAL,
                    lpBinaryPathName: binPath,
                    lpLoadOrderGroup: null!,
                    lpdwTagId: IntPtr.Zero,
                    lpDependencies: lpDependencies,
                    lpServiceStartName: lpServiceStartName,
                    lpPassword: lpPassword
                );

                int createServiceError = _win32ErrorProvider.GetLastWin32Error();

                // Persist service in database
                var dto = new ServiceDto
                {
                    Name = options.ServiceName,
                    DisplayName = displayName,
                    Description = options.Description,
                    ExecutablePath = options.RealExePath,
                    StartupDirectory = options.WorkingDirectory,
                    Parameters = options.RealArgs,
                    StartupType = (int)options.StartType,
                    Priority = (int)options.ProcessPriority,
                    StdoutPath = options.StdoutPath,
                    StderrPath = options.StderrPath,
                    EnableRotation = options.EnableSizeRotation,
                    RotationSize = (int)(options.RotationSizeInBytes / (1024 * 1024)),
                    EnableDateRotation = options.EnableDateRotation,
                    DateRotationType = (int)options.DateRotationType,
                    MaxRotations = options.MaxRotations,
                    UseLocalTimeForRotation = options.UseLocalTimeForRotation,
                    EnableHealthMonitoring = options.EnableHealthMonitoring,
                    HeartbeatInterval = options.HeartbeatInterval,
                    MaxFailedChecks = options.MaxFailedChecks,
                    RecoveryAction = (int)options.RecoveryAction,
                    MaxRestartAttempts = options.MaxRestartAttempts,
                    FailureProgramPath = options.FailureProgramPath,
                    FailureProgramStartupDirectory = options.FailureProgramWorkingDirectory,
                    FailureProgramParameters = options.FailureProgramArgs,
                    EnvironmentVariables = options.EnvironmentVariables,
                    ServiceDependencies = options.ServiceDependencies,
                    RunAsLocalSystem = string.IsNullOrWhiteSpace(options.Username),
                    UserAccount = options.Username,
                    Password = options.Password,
                    PreLaunchExecutablePath = options.PreLaunchExePath,
                    PreLaunchStartupDirectory = options.PreLaunchWorkingDirectory,
                    PreLaunchParameters = options.PreLaunchArgs,
                    PreLaunchEnvironmentVariables = options.PreLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = options.PreLaunchStdoutPath,
                    PreLaunchStderrPath = options.PreLaunchStderrPath,
                    PreLaunchTimeoutSeconds = options.PreLaunchTimeout,
                    PreLaunchRetryAttempts = options.PreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = options.PreLaunchIgnoreFailure,

                    PostLaunchExecutablePath = options.PostLaunchExePath,
                    PostLaunchStartupDirectory = options.PostLaunchWorkingDirectory,
                    PostLaunchParameters = options.PostLaunchArgs,

                    EnableDebugLogs = options.EnableDebugLogs,

                    StartTimeout = options.StartTimeout,
                    StopTimeout = options.StopTimeout,

                    PreStopExecutablePath = options.PreStopExePath,
                    PreStopStartupDirectory = options.PreStopWorkingDirectory,
                    PreStopParameters = options.PreStopArgs,
                    PreStopTimeoutSeconds = options.PreStopTimeout,
                    PreStopLogAsError = options.PreStopLogAsError,

                    PostStopExecutablePath = options.PostStopExePath,
                    PostStopStartupDirectory = options.PostStopWorkingDirectory,
                    PostStopParameters = options.PostStopArgs,
                };

                // Set PID
                var serviceDto = await _serviceRepository.GetByNameAsync(options.ServiceName);
                dto.Pid = serviceDto?.Pid;

                // Request PreShutdown timeout
                var totalWaitTime = (options.StopTimeout ?? ServiceStopTimeoutSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                var previousWaitTime = (serviceDto?.PreviousStopTimeout ?? ServiceStopTimeoutSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                totalWaitTime = Math.Max(Math.Max(totalWaitTime, previousWaitTime), ServiceStopTimeoutSeconds);
                if (!string.IsNullOrEmpty(options.PreStopExePath))
                {
                    totalWaitTime += options.PreStopTimeout ?? AppConfig.DefaultPreStopTimeoutSeconds;
                }
                uint finalTimeoutMs = (uint)totalWaitTime * 1000;

                if (serviceHandle != IntPtr.Zero)
                {
                    var enablePreShutdownConfigSuccess = EnablePreShutdown(serviceHandle, finalTimeoutMs);

                    if (enablePreShutdownConfigSuccess)
                    {
                        Logger.Info($"Pre-shutdown enabled with timeout of {totalWaitTime} seconds for service '{options.ServiceName}' during installation.");
                    }
                    else
                    {
                        string errorMsg = $"Failed to enable pre-shutdown for service '{options.ServiceName}' during installation. Rolling back creation.";
                        Logger.Error(errorMsg);

                        // Roll back the partial installation
                        _windowsServiceApi.DeleteService(serviceHandle);

                        return OperationResult.Failure(errorMsg);
                    }

                    // Set delayed auto-start if necessary
                    if (options.StartType == ServiceStartType.AutomaticDelayedStart)
                    {
                        var delayedAutoStartConfigSuccess = ChangeServiceConfig2(serviceHandle, true);

                        if (!delayedAutoStartConfigSuccess)
                        {
                            string errorMsg = $"Failed to set delayed auto-start for service '{options.ServiceName}' during installation. Rolling back creation.";
                            Logger.Error(errorMsg);

                            // Roll back the partial installation
                            _windowsServiceApi.DeleteService(serviceHandle);

                            return OperationResult.Failure(errorMsg);
                        }
                        else
                        {
                            Logger.Info($"Delayed auto-start enabled for service '{options.ServiceName}' during installation.");
                        }
                    }
                }

                if (serviceHandle == IntPtr.Zero)
                {
                    var isInstalled = IsServiceInstalled(options.ServiceName);
                    if (isInstalled)
                    {
                        // Service exists - update its configuration
                        var updated = UpdateServiceConfig(
                            scmHandle: scmHandle,
                            serviceName: options.ServiceName,
                            description: options.Description,
                            binPath: binPath,
                            startType: options.StartType,
                            username: lpServiceStartName,
                            password: lpPassword,
                            lpDependencies: lpDependencies,
                            displayName: displayName
                        );

                        if (!updated)
                        {
                            Logger.Warn($"Failed to update existing service configuration for service '{options.ServiceName}'.");
                        }

                        // Set delayed auto-start if necessary
                        if (options.StartType == ServiceStartType.AutomaticDelayedStart || options.StartType == ServiceStartType.Automatic)
                        {
                            IntPtr existingServiceHandle = _windowsServiceApi.OpenService(
                                scmHandle,
                                options.ServiceName,
                                SERVICE_CHANGE_CONFIG
                            );

                            if (existingServiceHandle == IntPtr.Zero)
                            {
                                var err = _win32ErrorProvider.GetLastWin32Error();
                                Logger.Error($"Failed to open service '{options.ServiceName}' for config update. Win32 error: {err}");
                                return OperationResult.Failure($"Failed to open service '{options.ServiceName}' for configuration update. Error code: {err}");
                            }

                            try
                            {
                                var delayedAutostart = options.StartType == ServiceStartType.AutomaticDelayedStart;
                                var success = ChangeServiceConfig2(existingServiceHandle, delayedAutostart);

                                if (success)
                                {
                                    Logger.Info($"Delayed auto-start {(delayedAutostart ? "enabled" : "disabled")} for existing service '{options.ServiceName}'.");
                                }
                                else
                                {
                                    string errorMsg = $"Failed to set delayed auto-start for existing service '{options.ServiceName}'.";
                                    Logger.Error(errorMsg);
                                    return OperationResult.Failure(errorMsg);
                                }
                            }
                            finally
                            {
                                _windowsServiceApi.CloseServiceHandle(existingServiceHandle);
                            }
                        }

                        await _serviceRepository.UpsertAsync(dto);
                        Logger.Info($"Service '{options.ServiceName}' already exists. Updated its configuration.");

                        return OperationResult.Success();
                    }


                    string creationErrorMsg = $"Failed to create service '{options.ServiceName}'. Win32 error: {createServiceError}";
                    Logger.Error(creationErrorMsg);
                    return OperationResult.Failure(creationErrorMsg);
                }

                // Set description
                SetServiceDescription(serviceHandle, options.Description);

                await _serviceRepository.UpsertAsync(dto);
                Logger.Info($"Service '{options.ServiceName}' installed successfully.");

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error installing service '{options.ServiceName}'.", ex);
                throw;
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
        public async Task<OperationResult> UninstallServiceAsync(string serviceName)
        {
            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null!, null!, SC_MANAGER_CONNECT);
            if (scmHandle == IntPtr.Zero)
                return OperationResult.Failure("Failed to open Service Control Manager.");

            try
            {
                uint uninstallRights = SERVICE_STOP | SERVICE_QUERY_STATUS | SERVICE_DELETE;
                IntPtr serviceHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, uninstallRights);
                if (serviceHandle == IntPtr.Zero)
                    return OperationResult.Failure($"Failed to open service '{serviceName}' for uninstallation. It may not exist.");

                try
                {
                    // Change start type to demand start (if it's disabled)
                    _windowsServiceApi.ChangeServiceConfig(
                        serviceHandle,
                        SERVICE_NO_CHANGE,
                        SERVICE_DEMAND_START,
                        SERVICE_NO_CHANGE,
                        null!,
                        null!,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null!);

                    // Try to stop service
                    var status = new NativeMethods.ServiceStatus();
                    _windowsServiceApi.ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status);

                    // Wait for service to actually stop (up to 60 seconds)
                    using (var sc = _controllerFactory(serviceName))
                    {
                        sc.Refresh();
                        var sw = Stopwatch.StartNew();

                        while (sc.Status != ServiceControllerStatus.Stopped && sw.Elapsed.TotalSeconds < ServiceStopTimeoutSeconds)
                        {
                            await Task.Delay(ScmPollIntervalMs); // Poll every half-second
                            sc.Refresh();
                        }
                    }

                    // Delete the service
                    var res = _windowsServiceApi.DeleteService(serviceHandle);

                    if (res)
                    {
                        await _serviceRepository.DeleteAsync(serviceName);
                        Logger.Info($"Service '{serviceName}' uninstalled successfully.");
                        return OperationResult.Success();
                    }
                    else
                    {
                        string errorMsg = $"Failed to uninstall service '{serviceName}'.";
                        Logger.Error(errorMsg);
                        return OperationResult.Failure(errorMsg);
                    }
                }
                finally
                {
                    _windowsServiceApi.CloseServiceHandle(serviceHandle);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error uninstalling service '{serviceName}'.", ex);
                throw;
            }
            finally
            {
                _windowsServiceApi.CloseServiceHandle(scmHandle);
            }
        }

        ///<inheritdoc/>
        public async Task<OperationResult> StartServiceAsync(string serviceName, bool logSuccessfulStart = true)
        {
            int timeout = 0;
            try
            {
                var service = await _serviceRepository.GetByNameAsync(serviceName);

                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return OperationResult.Success();

                    timeout = (service.StartTimeout ?? ServiceStartTimeoutSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                    timeout = Math.Max(timeout, ServiceStartTimeoutSeconds);
                    if (!string.IsNullOrEmpty(service.PreLaunchExecutablePath))
                    {
                        timeout += service.PreLaunchTimeoutSeconds ?? AppConfig.DefaultPreLaunchTimeoutSeconds;
                    }

                    Logger.Info($"Attempting to start service '{serviceName}' with a timeout of {timeout} seconds.");
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(timeout));

                    if (logSuccessfulStart)
                    {
                        Logger.Info($"Service '{serviceName}' started successfully.");
                    }

                    return OperationResult.Success();
                }
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                // LOG AS WARN: The service might still be starting, just taking longer than the configured window.
                string msg = $"Service '{serviceName}' did not reach 'Running' status within the {timeout}s timeout. It may still be initializing.";
                Logger.Warn(msg);
                return OperationResult.Failure(msg);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start service '{serviceName}'.", ex);
                return OperationResult.Failure($"Failed to start service '{serviceName}'. Reason: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> StopServiceAsync(string serviceName, bool logSuccessfulStop = true)
        {
            int timeout = 0;
            try
            {
                var service = await _serviceRepository.GetByNameAsync(serviceName);

                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return OperationResult.Success();

                     timeout = ServiceHelper.CalculateStopTimeout(service.StopTimeout, service.PreviousStopTimeout, service.PreStopTimeoutSeconds ?? 0);

                    Logger.Info($"Attempting to stop service '{serviceName}' with a timeout of {timeout} seconds.");
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeout));

                    if (logSuccessfulStop)
                    {
                        Logger.Info($"Service '{serviceName}' stopped successfully.");
                    }

                    return OperationResult.Success();
                }
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                // LOG AS WARN: Common during graceful shutdowns that exceed the SCM window.
                string msg = $"Service '{serviceName}' did not stop within {timeout} seconds. A forceful termination may be required.";
                Logger.Warn(msg);
                return OperationResult.Failure(msg);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to stop service '{serviceName}'.", ex);
                return OperationResult.Failure($"Failed to stop service '{serviceName}'. Reason: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> RestartServiceAsync(string serviceName)
        {
            if (!(await StopServiceAsync(serviceName, logSuccessfulStop: false)).IsSuccess)
                return OperationResult.Failure($"Failed to restart service '{serviceName}'.");

            var res = await StartServiceAsync(serviceName, logSuccessfulStart: false);

            if (res.IsSuccess)
            {
                Logger.Info($"Service '{serviceName}' restarted successfully.");
            }
            else
            {
                Logger.Error($"Failed to restart service '{serviceName}'.");
            }

            return res;
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

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Use ServiceController to grab the base StartType natively
                using (var sc = _controllerFactory(serviceName))
                {
                    ServiceStartType startupType;

                    switch (sc.StartType)
                    {
                        case ServiceStartMode.Automatic: startupType = ServiceStartType.Automatic; break;
                        case ServiceStartMode.Manual: startupType = ServiceStartType.Manual; break;
                        case ServiceStartMode.Disabled: startupType = ServiceStartType.Disabled; break;
                        default: return null;
                    }


                    // If automatic, drill down with P/Invoke to check for Delayed Auto-Start
                    if (startupType == ServiceStartType.Automatic)
                    {
                        IntPtr scmHandle = IntPtr.Zero;
                        IntPtr svcHandle = IntPtr.Zero;

                        try
                        {
                            scmHandle = _windowsServiceApi.OpenSCManager(null!, null!, SC_MANAGER_CONNECT);
                            if (scmHandle != IntPtr.Zero)
                            {
                                svcHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, SERVICE_QUERY_CONFIG);
                                if (svcHandle != IntPtr.Zero)
                                {
                                    var info = new ServiceDelayedAutoStartInfo();
                                    int bytesNeeded = 0;

                                    bool ok = _windowsServiceApi.QueryServiceConfig2(
                                        svcHandle,
                                        SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                                        ref info,
                                        Marshal.SizeOf(typeof(ServiceDelayedAutoStartInfo)),
                                        ref bytesNeeded);

                                    if (ok && info.fDelayedAutostart)
                                    {
                                        startupType = ServiceStartType.AutomaticDelayedStart;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (svcHandle != IntPtr.Zero) _windowsServiceApi.CloseServiceHandle(svcHandle);
                            if (scmHandle != IntPtr.Zero) _windowsServiceApi.CloseServiceHandle(scmHandle);
                        }
                    }

                    return startupType;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting service startup type for '{serviceName}'.", ex);
                return ServiceStartType.Unknown;
            }
        }

        /// <inheritdoc/>
        public List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default(CancellationToken))
        {
            var results = new ConcurrentBag<ServiceInfo>();
            var services = _serviceControllerProvider.GetServices();

            IntPtr scmHandle = _windowsServiceApi.OpenSCManager(null!, null!, SC_MANAGER_ENUMERATE_SERVICE);
            if (scmHandle == IntPtr.Zero)
                throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");

            try
            {
                Parallel.ForEach(services, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaxParallelScmQueries),
                },
                service =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        ServiceInfo info = new ServiceInfo
                        {
                            Name = service.ServiceName,
                            Status = MapStatus(service.Status),
                            StartupType = MapStartupType(service), // Accessing service.StartType directly can throw Win32Exception on protected services
                            LogOnAs = "LocalSystem",
                            Description = string.Empty,
                        };

                        // Fetch deep details natively
                        PopulateNativeDetails(scmHandle, info);

                        results.Add(info);
                    }
                    finally
                    {
                        // CRITICAL for .NET Framework to prevent handle exhaustion
                        service.Dispose();
                    }
                });
            }
            finally
            {
                if (scmHandle != IntPtr.Zero)
                    _windowsServiceApi.CloseServiceHandle(scmHandle);
            }

            return results.OrderBy(s => s.Name).ToList();
        }

        /// <inheritdoc/>
        public ServiceDependencyNode? GetDependencies(string serviceName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serviceName))
                    throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

                if (!IsServiceInstalled(serviceName))
                {
                    return null;
                }

                using (var sc = _controllerFactory(serviceName))
                {
                    return sc.GetDependencies();
                }

            }
            catch (Exception ex)
            {
                // Error is intentionally swallowed to keep the API safe
                // for UI and monitoring scenarios.
                Logger.Error($"Error getting service dependencies for '{serviceName}'.", ex);
            }

            return null;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Populates additional service details using native Windows APIs.
        /// </summary>
        /// <param name="scmHandle">A handle to the Service Control Manager database.</param>
        /// <param name="info">The <see cref="ServiceInfo"/> object to be enriched with native data.</param>
        /// <exception cref="ArgumentException">Thrown when the service name is null or whitespace.</exception>
        private void PopulateNativeDetails(IntPtr scmHandle, ServiceInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Name)) throw new ArgumentException("Service name is empty!");
            IntPtr svcHandle = _windowsServiceApi.OpenService(scmHandle, info.Name, SERVICE_QUERY_CONFIG);
            if (svcHandle == IntPtr.Zero) return;

            try
            {
                info.LogOnAs = GetServiceUser(svcHandle) ?? info.LogOnAs;
                info.Description = GetServiceDescription(svcHandle) ?? string.Empty;

                if (info.StartupType == ServiceStartType.Automatic && IsDelayedStart(svcHandle))
                {
                    info.StartupType = ServiceStartType.AutomaticDelayedStart;
                }
            }
            finally
            {
                _windowsServiceApi.CloseServiceHandle(svcHandle);
            }
        }

        /// <summary>
        /// Retrieves the account name under which the service runs.
        /// </summary>
        /// <param name="svcHandle">A handle to the service.</param>
        /// <returns>The account name string (e.g., "LocalSystem" or "DOMAIN\User"), or null if retrieval fails.</returns>
        private string? GetServiceUser(IntPtr svcHandle)
        {
            int bytesNeeded = 0;
            _windowsServiceApi.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out bytesNeeded);
            if (bytesNeeded <= 0) return null;

            IntPtr ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (_windowsServiceApi.QueryServiceConfig(svcHandle, ptr, bytesNeeded, out _))
                {
                    var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(ptr);
                    return Marshal.PtrToStringAuto(config.lpServiceStartName);
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Maps a native <see cref="ServiceControllerStatus"/> to the internal <see cref="Enums.ServiceStatus"/>.
        /// </summary>
        /// <param name="nativeStatus">The status returned by the ServiceController.</param>
        /// <returns>The corresponding internal status enum value.</returns>
        private Enums.ServiceStatus MapStatus(ServiceControllerStatus nativeStatus)
        {
            switch (nativeStatus)
            {
                case ServiceControllerStatus.Running: return Enums.ServiceStatus.Running;
                case ServiceControllerStatus.Stopped: return Enums.ServiceStatus.Stopped;
                case ServiceControllerStatus.Paused: return Enums.ServiceStatus.Paused;
                case ServiceControllerStatus.StartPending: return Enums.ServiceStatus.StartPending;
                case ServiceControllerStatus.StopPending: return Enums.ServiceStatus.StopPending;
                case ServiceControllerStatus.PausePending: return Enums.ServiceStatus.PausePending;
                case ServiceControllerStatus.ContinuePending: return Enums.ServiceStatus.ContinuePending;
                default: return Enums.ServiceStatus.None;
            }
        }

        /// <summary>
        /// Maps the native start mode to the internal <see cref="ServiceStartType"/>.
        /// </summary>
        /// <param name="service">The service controller wrapper providing the start mode.</param>
        /// <returns>The mapped start type. Defaults to <see cref="ServiceStartType.Manual"/> on failure.</returns>
        /// <remarks>
        /// Accessing the StartType property can throw a <see cref="System.ComponentModel.Win32Exception"/> 
        /// (Access Denied) for certain protected system services.
        /// </remarks>
        private static ServiceStartType MapStartupType(IServiceControllerWrapper service)
        {
            try
            {
                switch (service.StartType)
                {
                    case ServiceStartMode.Automatic: return ServiceStartType.Automatic;
                    case ServiceStartMode.Manual: return ServiceStartType.Manual;
                    case ServiceStartMode.Disabled: return ServiceStartType.Disabled;
                    default: return ServiceStartType.Manual;
                }
            }
            catch (Win32Exception ex)
            {
                // Log the specific error and provide a diagnostic trail.
                // We use Debug level to avoid bloating logs with expected protected service errors.
                Logger.Debug($"Access denied or Win32 error reading StartType for '{service.ServiceName}'. Falling back to Manual.", ex);

                return ServiceStartType.Manual;
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected failures (e.g. ObjectDisposedException)
                Logger.Error($"Unexpected error mapping startup type for '{service.ServiceName}'.", ex);

                return ServiceStartType.Manual;
            }
        }

        /// <summary>
        /// Retrieves the optional description associated with the service.
        /// </summary>
        /// <param name="svcHandle">A handle to the service.</param>
        /// <returns>The description string, or null if retrieval fails or no description is set.</returns>
        private string? GetServiceDescription(IntPtr svcHandle)
        {
            int bytesNeeded = 0;
            _windowsServiceApi.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, IntPtr.Zero, 0, ref bytesNeeded);
            if (bytesNeeded <= 0) return null;

            IntPtr ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (_windowsServiceApi.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, ptr, bytesNeeded, ref bytesNeeded))
                {
                    var descStruct = Marshal.PtrToStructure<ServiceDescription>(ptr);
                    return Marshal.PtrToStringAuto(descStruct.lpDescription);
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Checks if the service is configured for a delayed automatic start.
        /// </summary>
        /// <param name="svcHandle">A handle to the service.</param>
        /// <returns>True if the service is set to start with a delay; otherwise, false.</returns>
        private bool IsDelayedStart(IntPtr svcHandle)
        {
            var info = new ServiceDelayedAutoStartInfo();
            int bytesNeeded = 0;
            int structSize = Marshal.SizeOf(typeof(ServiceDelayedAutoStartInfo));

            return _windowsServiceApi.QueryServiceConfig2(
                svcHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref info,
                structSize,
                ref bytesNeeded) && info.fDelayedAutostart;
        }

        #endregion
    }
}
