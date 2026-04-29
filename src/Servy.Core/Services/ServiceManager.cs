using Microsoft.Win32;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
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

        private const int ServiceStartTimeoutSeconds = 30;
        private const int ScmPollIntervalMs = 500;
        private const int MaxParallelScmQueries = 8;

        /// <summary>
        /// The standard localized string identifier for the LocalSystem account.
        /// </summary>
        public const string LocalSystemAccount = "LocalSystem";

        #endregion

        #region SCM Access Rights

        /// <summary>Access right to connect to the service control manager.</summary>
        public const uint SC_MANAGER_CONNECT = 0x0001;

        /// <summary>Access right to create a service object and add it to the database.</summary>
        public const uint SC_MANAGER_CREATE_SERVICE = 0x0002;

        #endregion

        #region Service Access Rights

        /// <summary>Access right to change the configuration of a service.</summary>
        public const uint SERVICE_CHANGE_CONFIG = 0x0002;

        /// <summary>Access right to query the status of a service.</summary>
        public const uint SERVICE_QUERY_STATUS = 0x0004;

        /// <summary>Access right to start the service.</summary>
        public const uint SERVICE_START = 0x0010;

        /// <summary>Access right to stop the service.</summary>
        public const uint SERVICE_STOP = 0x0020;

        /// <summary>Access right to delete the service.</summary>
        public const uint SERVICE_DELETE = 0x00010000;

        #endregion

        #region Service Configuration & Type Flags

        /// <summary>Specifies a service that runs in its own process.</summary>
        public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        /// <summary>Logs the error and continues the startup operation if the service fails to start.</summary>
        public const uint SERVICE_ERROR_NORMAL = 0x00000001;

        /// <summary>Information level to retrieve or set pre-shutdown information.</summary>
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
        /// <param name="controllerFactory">A factory function for creating service controller wrappers.</param>
        /// <param name="serviceControllerProvider">A provider to retrieve system service controllers.</param>
        /// <param name="windowsServiceApi">An abstraction over the native Windows Service APIs.</param>
        /// <param name="win32ErrorProvider">A provider to retrieve the last Win32 error code.</param>
        /// <param name="serviceRepository">The repository to store and read service configuration entities.</param>
        public ServiceManager(
            Func<string, IServiceControllerWrapper> controllerFactory,
            IServiceControllerProvider serviceControllerProvider,
            IWindowsServiceApi windowsServiceApi,
            IWin32ErrorProvider win32ErrorProvider,
            IServiceRepository serviceRepository
            )
        {
            _controllerFactory = controllerFactory ?? throw new ArgumentNullException(nameof(controllerFactory));
            _serviceControllerProvider = serviceControllerProvider ?? throw new ArgumentNullException(nameof(serviceControllerProvider));
            _windowsServiceApi = windowsServiceApi ?? throw new ArgumentNullException(nameof(windowsServiceApi));
            _win32ErrorProvider = win32ErrorProvider ?? throw new ArgumentNullException(nameof(win32ErrorProvider));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Updates the core configuration of an existing Windows service.
        /// </summary>
        /// <param name="scmHandle">A handle to the Service Control Manager database.</param>
        /// <param name="serviceName">The name of the service to configure.</param>
        /// <param name="description">The text description for the service.</param>
        /// <param name="binPath">The fully qualified path to the service binary executable.</param>
        /// <param name="startType">The startup type for the service.</param>
        /// <param name="username">The account under which the service will run.</param>
        /// <param name="password">The password for the account.</param>
        /// <param name="lpDependencies">A double null-terminated string of dependencies.</param>
        /// <param name="displayName">The display name to show in the Services console.</param>
        /// <returns><c>true</c> if the configuration was successfully applied; otherwise, <c>false</c>.</returns>
        /// <exception cref="Win32Exception">Thrown when a native API call fails to open or change the service.</exception>
        public bool UpdateServiceConfig(
            SafeScmHandle scmHandle,
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
            using (var serviceHandle = _windowsServiceApi.OpenService(
                scmHandle,
                serviceName,
                SERVICE_CHANGE_CONFIG | SERVICE_QUERY_CONFIG))
            {
                if (serviceHandle.IsInvalid)
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open existing service.");
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = serviceName;
                }

                bool result = _windowsServiceApi.ChangeServiceConfig(
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
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to update service config.");
                }

                SetServiceDescription(serviceHandle, description);

                return true;
            }
        }

        /// <summary>
        /// Sets the description text for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="description">The description string to assign.</param>
        /// <exception cref="Win32Exception">Thrown if the native configuration change fails.</exception>
        internal void SetServiceDescription(SafeServiceHandle serviceHandle, string description)
        {
            IntPtr pDescription = IntPtr.Zero;
            try
            {
                pDescription = Marshal.StringToHGlobalUni(description?.Trim());

                var desc = new SERVICE_DESCRIPTION
                {
                    lpDescription = pDescription
                };

                if (!_windowsServiceApi.ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_DESCRIPTION, ref desc))
                {
                    int err = _win32ErrorProvider.GetLastWin32Error();
                    throw new Win32Exception(err, "Failed to set service description.");
                }
            }
            finally
            {
                if (pDescription != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pDescription);
                }
            }
        }

        /// <summary>
        /// Enables or disables the delayed auto-start setting for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="delayedAutostart"><c>true</c> to delay the automatic startup; otherwise <c>false</c>.</param>
        /// <returns><c>true</c> if the change was successful; otherwise, <c>false</c>.</returns>
        private bool ChangeServiceConfig2(SafeServiceHandle serviceHandle, bool delayedAutostart)
        {
            var delayedInfo = new SERVICE_DELAYED_AUTO_START_INFO
            {
                fDelayedAutostart = delayedAutostart,
            };

            return _windowsServiceApi.ChangeServiceConfig2(
                serviceHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref delayedInfo
            );
        }

        /// <summary>
        /// Configures the service to accept pre-shutdown notifications and sets the maximum timeout 
        /// the Service Control Manager (SCM) will wait for this service to stop during a system shutdown.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="timeoutMs">The duration in milliseconds the SCM should wait.</param>
        /// <returns><c>true</c> if the pre-shutdown setting was successfully applied; otherwise, <c>false</c>.</returns>
        private bool EnablePreShutdown(SafeServiceHandle serviceHandle, uint timeoutMs)
        {
            var info = new SERVICE_PRE_SHUTDOWN_INFO
            {
                dwPreshutdownTimeout = timeoutMs
            };

            IntPtr ptr = IntPtr.Zero;
            try
            {
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
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        #endregion

        #region IServiceManager Implementation

        /// <inheritdoc />
        public async Task<OperationResult> InstallServiceAsync(InstallServiceOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options == null) throw new ArgumentNullException(nameof(options));
            if (_serviceRepository == null) throw new InvalidOperationException("Service repository is not initialized. Cannot install service without a repository.");
            if (string.IsNullOrWhiteSpace(options.ServiceName)) throw new ArgumentException("Value is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.WrapperExePath)) throw new ArgumentException("Value is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.RealExePath)) throw new ArgumentException("Value is required.", nameof(options));

            string binPath = string.Join(" ",
                Helper.Quote(options.WrapperExePath),
                Helper.Quote(options.ServiceName)
            );

            SafeScmHandle scmHandle = null;
            try
            {
                scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
                if (scmHandle == null || scmHandle.IsInvalid)
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");
                }

                string displayName = string.IsNullOrWhiteSpace(options.DisplayName) ? options.ServiceName : options.DisplayName;

                SafeServiceHandle serviceHandle = null;
                try
                {
                    string lpDependencies = ServiceDependenciesParser.Parse(options.ServiceDependencies);
                    string lpServiceStartName = string.IsNullOrWhiteSpace(options.Username) ? LocalSystemAccount : options.Username;
                    string lpPassword = string.IsNullOrEmpty(options.Password) ? null : options.Password;

                    bool isLocalSystem = lpServiceStartName.Equals(LocalSystemAccount, StringComparison.OrdinalIgnoreCase);
                    var isGmsa = string.IsNullOrEmpty(lpPassword) && lpServiceStartName.EndsWith("$");

                    if (!isLocalSystem && !isGmsa)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _windowsServiceApi.EnsureLogOnAsServiceRight(lpServiceStartName);
                        Logger.Info($"Ensured 'Log on as a service' right for account '{lpServiceStartName}' for service '{options.ServiceName}'.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

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
                        lpLoadOrderGroup: null,
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
                        EnableConsoleUI = options.EnableConsoleUI,
                        StdoutPath = options.StdoutPath,
                        StderrPath = options.StderrPath,
                        EnableSizeRotation = options.EnableSizeRotation,
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

                    cancellationToken.ThrowIfCancellationRequested();
                    var serviceDto = await _serviceRepository.GetByNameAsync(options.ServiceName);
                    dto.Pid = serviceDto?.Pid;

                    var totalWaitTime = (options.StopTimeout ?? AppConfig.DefaultServiceStopTimeoutSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                    var previousWaitTime = (serviceDto?.PreviousStopTimeout ?? AppConfig.DefaultServiceStopTimeoutSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                    totalWaitTime = Math.Max(Math.Max(totalWaitTime, previousWaitTime), AppConfig.DefaultServiceStopTimeoutSeconds);

                    if (!string.IsNullOrEmpty(options.PreStopExePath))
                    {
                        totalWaitTime += options.PreStopTimeout ?? AppConfig.DefaultPreStopTimeoutSeconds;
                    }
                    uint finalTimeoutMs = (uint)totalWaitTime * 1000;

                    if (serviceHandle != null && !serviceHandle.IsInvalid)
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
                            _windowsServiceApi.DeleteService(serviceHandle);
                            return OperationResult.Failure(errorMsg);
                        }

                        if (options.StartType == ServiceStartType.AutomaticDelayedStart)
                        {
                            var delayedAutoStartConfigSuccess = ChangeServiceConfig2(serviceHandle, true);

                            if (!delayedAutoStartConfigSuccess)
                            {
                                string errorMsg = $"Failed to set delayed auto-start for service '{options.ServiceName}' during installation. Rolling back creation.";
                                Logger.Error(errorMsg);
                                _windowsServiceApi.DeleteService(serviceHandle);
                                return OperationResult.Failure(errorMsg);
                            }
                            else
                            {
                                Logger.Info($"Delayed auto-start enabled for service '{options.ServiceName}' during installation.");
                            }
                        }
                    }

                    if (serviceHandle == null || serviceHandle.IsInvalid)
                    {
                        var isInstalled = IsServiceInstalled(options.ServiceName);
                        if (isInstalled)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
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

                            if (options.StartType == ServiceStartType.AutomaticDelayedStart || options.StartType == ServiceStartType.Automatic)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                using (var existingServiceHandle = _windowsServiceApi.OpenService(
                                    scmHandle,
                                    options.ServiceName,
                                    SERVICE_CHANGE_CONFIG))
                                {
                                    if (existingServiceHandle.IsInvalid)
                                    {
                                        var err = _win32ErrorProvider.GetLastWin32Error();
                                        Logger.Error($"Failed to open service '{options.ServiceName}' for config update. Win32 error: {err}");
                                        return OperationResult.Failure($"Failed to open service '{options.ServiceName}' for configuration update. Error code: {err}");
                                    }

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
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            await _serviceRepository.UpsertAsync(dto);
                            Logger.Info($"Service '{options.ServiceName}' already exists. Updated its configuration.");
                            return OperationResult.Success();
                        }

                        string creationErrorMsg = $"Failed to create service '{options.ServiceName}'. Win32 error: {createServiceError}";
                        Logger.Error(creationErrorMsg);
                        return OperationResult.Failure(creationErrorMsg);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    SetServiceDescription(serviceHandle, options.Description);
                    await _serviceRepository.UpsertAsync(dto);
                    Logger.Info($"Service '{options.ServiceName}' installed successfully.");

                    return OperationResult.Success();
                }
                finally
                {
                    if (serviceHandle != null)
                    {
                        serviceHandle.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error installing service '{options.ServiceName}'.", ex);
                throw;
            }
            finally
            {
                if (scmHandle != null)
                {
                    scmHandle.Dispose();
                }
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> UninstallServiceAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (_serviceRepository == null) throw new InvalidOperationException("Service repository is not initialized. Cannot uninstall service without a repository.");
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException("serviceName is required.", nameof(serviceName));

            SafeScmHandle scmHandle = null;
            try
            {
                // 1. Initial responsiveness check
                cancellationToken.ThrowIfCancellationRequested();

                scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT);
                if (scmHandle == null || scmHandle.IsInvalid)
                {
                    return OperationResult.Failure("Failed to open Service Control Manager.");
                }

                // Added SERVICE_CHANGE_CONFIG to permit the start-type modification.
                uint uninstallRights = SERVICE_STOP | SERVICE_QUERY_STATUS | SERVICE_DELETE | SERVICE_CHANGE_CONFIG;

                using (var serviceHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, uninstallRights))
                {
                    if (serviceHandle.IsInvalid)
                    {
                        return OperationResult.Failure($"Failed to open service '{serviceName}' for uninstallation. It may not exist.");
                    }

                    // Standardize start type before stopping to prevent auto-restart attempts during deletion.
                    // We now capture and check the result of this call.
                    bool configSuccess = _windowsServiceApi.ChangeServiceConfig(
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

                    if (!configSuccess)
                    {
                        // We log this as a warning rather than a failure, as we can still attempt 
                        // the stop and delete commands, but it's important for diagnostic visibility.
                        Logger.Warn($"Failed to standardize start type before uninstall for '{serviceName}': Win32 Error {Marshal.GetLastWin32Error()}");
                    }

                    // Trigger the stop command
                    var status = new NativeMethods.SERVICE_STATUS();
                    _windowsServiceApi.ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status);

                    // 2. The Wait Loop: Now fully cancellable
                    using (var sc = _controllerFactory(serviceName))
                    {
                        sc.Refresh();
                        var sw = Stopwatch.StartNew();

                        while (sc.Status != ServiceControllerStatus.Stopped && sw.Elapsed.TotalSeconds < AppConfig.DefaultServiceStopTimeoutSeconds)
                        {
                            // Passing the token to Task.Delay makes the loop immediately responsive to cancellation
                            await Task.Delay(ScmPollIntervalMs, cancellationToken);
                            sc.Refresh();
                        }
                    }

                    // 3. Final safety check before committing the permanent 'Delete'
                    cancellationToken.ThrowIfCancellationRequested();

                    var res = _windowsServiceApi.DeleteService(serviceHandle);

                    if (res)
                    {
                        // Ensure the repository deletion also honors the token
                        await _serviceRepository.DeleteAsync(serviceName, cancellationToken);

                        Logger.Info($"Service '{serviceName}' uninstalled successfully.");
                        return OperationResult.Success();
                    }
                    else
                    {
                        string errorMsg = $"Failed to uninstall service '{serviceName}'. Win32 Error {Marshal.GetLastWin32Error()}";
                        Logger.Error(errorMsg);
                        return OperationResult.Failure(errorMsg);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Uninstallation of '{serviceName}' was cancelled by the user.");
                throw; // Re-throw to let the ViewModel handle the cancellation UI state
            }
            catch (Exception ex)
            {
                Logger.Error($"Error uninstalling service '{serviceName}'.", ex);
                throw;
            }
            finally
            {
                scmHandle?.Dispose();
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult> StartServiceAsync(string serviceName, bool logSuccessfulStart = true, CancellationToken cancellationToken = default)
        {
            if (_serviceRepository == null) throw new InvalidOperationException("Service repository is not initialized. Cannot install service without a repository.");
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException("serviceName is required.", nameof(serviceName));

            int timeout = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                    // Replace blocking WaitForStatus with an async polling loop to respect cancellation
                    var stopwatch = Stopwatch.StartNew();
                    var timeoutSpan = TimeSpan.FromSeconds(timeout);

                    while (sc.Status != ServiceControllerStatus.Running)
                    {
                        if (stopwatch.Elapsed > timeoutSpan)
                        {
                            throw new System.ServiceProcess.TimeoutException();
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(250, cancellationToken);
                        sc.Refresh();
                    }

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
        public async Task<OperationResult> StopServiceAsync(string serviceName, bool logSuccessfulStop = true, CancellationToken cancellationToken = default)
        {
            if (_serviceRepository == null) throw new InvalidOperationException("Service repository is not initialized. Cannot install service without a repository.");
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException("serviceName is required.", nameof(serviceName));

            int timeout = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var service = await _serviceRepository.GetByNameAsync(serviceName);

                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return OperationResult.Success();

                    timeout = ServiceHelper.CalculateStopTimeout(service.StopTimeout, service.PreviousStopTimeout, service.PreStopTimeoutSeconds ?? 0);

                    Logger.Info($"Attempting to stop service '{serviceName}' with a timeout of {timeout} seconds.");
                    sc.Stop();

                    // Replace blocking WaitForStatus with an async polling loop to respect cancellation
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var timeoutSpan = TimeSpan.FromSeconds(timeout);

                    while (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        if (stopwatch.Elapsed > timeoutSpan)
                        {
                            throw new System.ServiceProcess.TimeoutException();
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(250, cancellationToken);
                        sc.Refresh();
                    }

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
        public async Task<OperationResult> RestartServiceAsync(string serviceName, bool logSuccessfulRestart = true, CancellationToken cancellationToken = default)
        {
            if (!(await StopServiceAsync(serviceName, logSuccessfulStop: logSuccessfulRestart, cancellationToken)).IsSuccess)
                return OperationResult.Failure($"Failed to restart service '{serviceName}'.");

            var res = await StartServiceAsync(serviceName, logSuccessfulStart: logSuccessfulRestart, cancellationToken);

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
        public ServiceControllerStatus GetServiceStatus(string serviceName, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            using (var sc = _controllerFactory(serviceName))
            {
                return sc.Status;
            }
        }

        /// <inheritdoc />
        public bool IsServiceInstalled(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentNullException(nameof(serviceName));

            return _windowsServiceApi.GetServices()
                            .Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public ServiceStartType? GetServiceStartupType(string serviceName, CancellationToken cancellationToken = default(CancellationToken))
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
                        SafeScmHandle scmHandle = null;
                        try
                        {
                            scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT);
                            if (scmHandle != null && !scmHandle.IsInvalid)
                            {
                                using (var svcHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, SERVICE_QUERY_CONFIG))
                                {
                                    if (!svcHandle.IsInvalid)
                                    {
                                        var info = new SERVICE_DELAYED_AUTO_START_INFO();
                                        int bytesNeeded = 0;

                                        bool ok = _windowsServiceApi.QueryServiceConfig2(
                                            svcHandle,
                                            SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                                            ref info,
                                            Marshal.SizeOf(typeof(SERVICE_DELAYED_AUTO_START_INFO)),
                                            ref bytesNeeded);

                                        if (ok && info.fDelayedAutostart)
                                        {
                                            startupType = ServiceStartType.AutomaticDelayedStart;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (scmHandle != null)
                            {
                                scmHandle.Dispose();
                            }
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

            // Even in a sync-first approach, we track any underlying async tasks 
            // spawned by the provider or previous refresh attempts to protect the handle.
            var trackedTasks = new ConcurrentBag<Task>();

            SafeScmHandle scmHandle = null;
            try
            {
                scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_ENUMERATE_SERVICE);
                if (scmHandle == null || scmHandle.IsInvalid)
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");
                }

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
                            StartupType = MapStartupType(service),
                            LogOnAs = "LocalSystem",
                            Description = string.Empty,
                        };

                        // Synchronous Execution:
                        // We use a local CancellationTokenSource to enforce the per-call timeout
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            try
                            {
                                // 1. Set the timeout inside the guarded block
                                cts.CancelAfter(AppConfig.PopulateNativeDetailsTimeoutMs);

                                // 2. The logic now runs directly on the Parallel worker thread.
                                // PopulateNativeDetails MUST be updated to accept and check this token.
                                PopulateNativeDetails(scmHandle, info, cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                info.Description = "(details unavailable: native query timed out)";
                                Logger.Warn($"Native SCM query timed out for service: {info.Name}");
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"Native details collection faulted for {info.Name}: {ex.Message}");
                                info.Description = $"(details unavailable: {ex.GetType().Name})";
                            }
                        }

                        results.Add(info);
                    }
                    finally
                    {
                        service.Dispose();
                    }
                });

                return results.OrderBy(s => s.Name).ToList();
            }
            finally
            {
                // Safety Gate: Ensure any background tasks (from previous or forked logic) 
                // are observed before we drop the SCM handle.
                if (trackedTasks.Count > 0)
                {
                    try { Task.WaitAll(trackedTasks.ToArray(), 5000); } catch { /* Ignore */ }
                }

                scmHandle?.Dispose();
            }
        }

        /// <inheritdoc/>
        public ServiceDependencyNode GetDependencies(string serviceName)
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
        /// <param name="scmHandle">An active handle to the Service Control Manager.</param>
        /// <param name="info">The service information object to populate.</param>
        private void PopulateNativeDetails(SafeScmHandle scmHandle, ServiceInfo info, CancellationToken ct)
        {
            // 1. Pre-flight check
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(info.Name))
                throw new ArgumentException("Service name is empty!");

            // 2. Open the service handle
            using (var svcHandle = _windowsServiceApi.OpenService(scmHandle, info.Name, SERVICE_QUERY_CONFIG))
            {
                if (svcHandle.IsInvalid) return;

                // 3. Check token before each discrete native query.
                // If the 2000ms timeout or user cancellation hits during GetServiceUser, 
                // we skip the subsequent calls to keep the loop moving.

                ct.ThrowIfCancellationRequested();
                info.LogOnAs = GetServiceUser(svcHandle) ?? info.LogOnAs;

                ct.ThrowIfCancellationRequested();
                info.Description = GetServiceDescription(svcHandle) ?? string.Empty;

                ct.ThrowIfCancellationRequested();
                if (info.StartupType == ServiceStartType.Automatic && IsDelayedStart(svcHandle))
                {
                    info.StartupType = ServiceStartType.AutomaticDelayedStart;
                }
            }
        }

        /// <summary>
        /// Retrieves the account name under which the service runs.
        /// </summary>
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns>The account string or <c>null</c> if it couldn't be retrieved.</returns>
        private string GetServiceUser(SafeServiceHandle svcHandle)
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
        /// Maps the standard .NET Framework <see cref="ServiceControllerStatus"/> to internal enum format.
        /// </summary>
        /// <param name="nativeStatus">The system status to map.</param>
        /// <returns>An internal <see cref="Enums.ServiceStatus"/> representation.</returns>
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
        /// Gets the startup type mapping while accounting for protected service API limitations.
        /// </summary>
        /// <param name="service">The service controller wrapper to query.</param>
        /// <returns>The identified <see cref="ServiceStartType"/>.</returns>
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
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns>The service description string or <c>null</c> if none exists.</returns>
        private string GetServiceDescription(SafeServiceHandle svcHandle)
        {
            int bytesNeeded = 0;
            _windowsServiceApi.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, IntPtr.Zero, 0, ref bytesNeeded);
            if (bytesNeeded <= 0) return null;

            IntPtr ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (_windowsServiceApi.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, ptr, bytesNeeded, ref bytesNeeded))
                {
                    var descStruct = Marshal.PtrToStructure<SERVICE_DESCRIPTION>(ptr);
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
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns><c>true</c> if it has delayed start configured; otherwise, <c>false</c>.</returns>
        private bool IsDelayedStart(SafeServiceHandle svcHandle)
        {
            var info = new SERVICE_DELAYED_AUTO_START_INFO();
            int bytesNeeded = 0;
            int structSize = Marshal.SizeOf(typeof(SERVICE_DELAYED_AUTO_START_INFO));

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
