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
        /// <exception cref="Win32Exception">Thrown when a native API call fails to open or change the service.</exception>
        internal void UpdateServiceConfig(
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
                    dwStartType: ToScmStartType(startType),
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
            }
        }

        /// <summary>
        /// Sets the description text for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="description">The description string to assign. A null value removes the description.</param>
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
        private bool SetDelayedAutoStart(SafeServiceHandle serviceHandle, bool delayedAutostart)
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

            // HARDENING: Check database via UNICODE_NOCASE to intercept if this service or a linguistic 
            // variation of it already exists before running native SCM queries.
            var existingDbService = await _serviceRepository.GetByNameAsync(options.ServiceName, decrypt: true, cancellationToken);
            bool isUpdateMode = existingDbService != null;

            // Track if we successfully executed an aggressive purge of a casing layout duplicate
            bool legacyDroppedFromDb = false;
            ServiceDto legacyBackupDto = null;

            // If the database has a record under a different casing layout (e.g. 'serviceä' vs 'serviceÄ'),
            // the Windows SCM will treat them as two entirely different entities. We must aggressively drop the old 
            // casing layout registration from the OS before proceeding to avoid split-brain or orphaned processes.
            if (isUpdateMode && !string.Equals(existingDbService.Name, options.ServiceName, StringComparison.Ordinal))
            {
                Logger.Info($"Unicode name variance detected during update sequence ('{existingDbService.Name}' -> '{options.ServiceName}'). Executing full uninstallation sequence for the legacy casing variant from SCM and Database.");

                if (IsServiceInstalled(existingDbService.Name))
                {
                    try
                    {
                        // To prevent permanent data loss if the subsequent installation steps fail,
                        // we hold onto a deep copy or reference of the DTO before calling Uninstall.
                        legacyBackupDto = existingDbService;

                        var uninstallRes = await UninstallServiceAsync(existingDbService.Name, cancellationToken);
                        if (!uninstallRes.IsSuccess)
                        {
                            string uninstError = $"Failed to unregister legacy casing variant '{legacyBackupDto.Name}' from SCM: {uninstallRes.ErrorMessage}";
                            Logger.Error(uninstError);
                            return OperationResult.Failure(uninstError);
                        }

                        // UninstallServiceAsync succeeded, meaning the DB record for legacyBackupDto.Name is now deleted!
                        legacyDroppedFromDb = true;
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info($"Installation cancelled while dropping legacy casing variant '{existingDbService.Name}'.");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        string criticalError = $"Unexpected error occurred while trying to drop legacy service casing layout '{existingDbService.Name}'.";
                        Logger.Error(criticalError, ex);
                        return OperationResult.Failure($"{criticalError} Details: {ex.Message}");
                    }
                }
            }

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
                    string lpServiceStartName = string.IsNullOrWhiteSpace(options.Username) ? ServiceAccounts.LocalSystem : options.Username;
                    string lpPassword = string.IsNullOrEmpty(options.Password) ? null : options.Password;

                    bool isLocalSystem = lpServiceStartName.Equals(ServiceAccounts.LocalSystem, StringComparison.OrdinalIgnoreCase);
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
                        dwStartType: ToScmStartType(options.StartType),
                        dwErrorControl: SERVICE_ERROR_NORMAL,
                        lpBinaryPathName: binPath,
                        lpLoadOrderGroup: null,
                        lpdwTagId: IntPtr.Zero,
                        lpDependencies: lpDependencies,
                        lpServiceStartName: lpServiceStartName,
                        lpPassword: lpPassword
                    );

                    int createServiceError = _win32ErrorProvider.GetLastWin32Error();
                    bool serviceCreated = serviceHandle != null && !serviceHandle.IsInvalid;

                    // ROBUSTNESS: Establish a comprehensive try/catch/finally sequence immediately 
                    // following native creation. This guarantees cleanup upon cancellation or async IO exceptions.
                    bool needsRollback = false;

                    try
                    {
                        // Persist service in database
                        var dto = new ServiceDto
                        {
                            Name = options.ServiceName,
                            DisplayName = !string.IsNullOrWhiteSpace(options.DisplayName) ? displayName : null,
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
                            RotationSize = (int)(options.RotationSizeInBytes / AppConfig.BytesInMegabyte),
                            EnableDateRotation = options.EnableDateRotation,
                            DateRotationType = (int)options.DateRotationType,
                            MaxRotations = options.MaxRotations,
                            UseLocalTimeForRotation = options.UseLocalTimeForRotation,
                            EnableHealthMonitoring = options.EnableHealthMonitoring,
                            HeartbeatInterval = options.HeartbeatInterval,
                            MaxFailedChecks = options.MaxFailedChecks,
                            RecoveryAction = (int)options.RecoveryAction,
                            RecoveryOnCleanExit = options.RecoveryOnCleanExit,
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
                        var serviceDto = await _serviceRepository.GetByNameAsync(options.ServiceName, decrypt: false, cancellationToken);
                        dto.Pid = serviceDto?.Pid;

                        var totalWaitTime = (options.StopTimeout > AppConfig.ScmStopTimeoutFloorSeconds
                            ? options.StopTimeout : AppConfig.ScmStopTimeoutFloorSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                        var previousWaitTime = (serviceDto?.PreviousStopTimeout != null && serviceDto.PreviousStopTimeout.Value > AppConfig.ScmStopTimeoutFloorSeconds
                            ? serviceDto.PreviousStopTimeout.Value : AppConfig.ScmStopTimeoutFloorSeconds) + AppConfig.ScmTimeoutBufferSeconds;
                        totalWaitTime = Math.Max(totalWaitTime, previousWaitTime);

                        if (!string.IsNullOrEmpty(options.PreStopExePath))
                        {
                            totalWaitTime += options.PreStopTimeout;
                        }
                        uint finalTimeoutMs = (uint)totalWaitTime * 1000;

                        if (serviceCreated)
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
                                needsRollback = true;
                                throw new InvalidOperationException(errorMsg);
                            }

                            if (options.StartType == ServiceStartType.AutomaticDelayedStart)
                            {
                                var delayedAutoStartConfigSuccess = SetDelayedAutoStart(serviceHandle, true);

                                if (!delayedAutoStartConfigSuccess)
                                {
                                    string errorMsg = $"Failed to set delayed auto-start for service '{options.ServiceName}' during installation. Rolling back creation.";
                                    Logger.Error(errorMsg);
                                    needsRollback = true;
                                    throw new InvalidOperationException(errorMsg);
                                }
                                else
                                {
                                    Logger.Info($"Delayed auto-start enabled for service '{options.ServiceName}' during installation.");
                                }
                            }
                        }

                        if (!serviceCreated)
                        {
                            var isInstalled = IsServiceInstalled(options.ServiceName);
                            if (isInstalled)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                UpdateServiceConfig(
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

                                // Open the existing service for configuration updates (delayed start and pre-shutdown)
                                using (var existingServiceHandle = _windowsServiceApi.OpenService(
                                    scmHandle,
                                    options.ServiceName,
                                    SERVICE_CHANGE_CONFIG))
                                {
                                    if (existingServiceHandle.IsInvalid)
                                    {
                                        var err = _win32ErrorProvider.GetLastWin32Error();
                                        Logger.Error($"Failed to open service '{options.ServiceName}' for config update. Win32 error: {err}");
                                        throw new Win32Exception(err, $"Failed to open service '{options.ServiceName}' for configuration update. Error code: {err}");
                                    }

                                    // 1. Update Pre-shutdown Timeout for existing service
                                    // This ensures that updates to StopTimeout or PreStopTimeout are reflected in the OS SCM.
                                    var preShutdownSuccess = EnablePreShutdown(existingServiceHandle, finalTimeoutMs);
                                    if (preShutdownSuccess)
                                    {
                                        Logger.Info($"Pre-shutdown timeout updated to {totalWaitTime} seconds for existing service '{options.ServiceName}'.");
                                    }
                                    else
                                    {
                                        // UpdateServiceConfig and ChangeServiceConfig2 have already mutated OS state.
                                        // We explicitly reject the state run and notify that manual structural reconciliation is required.
                                        string errorMsg = $"CRITICAL STATE DRIFT: Failed to update pre-shutdown timeout for existing service '{options.ServiceName}'. Core properties were modified but advanced configurations failed. The Servy database remains un-updated. Run re-installation immediately to prevent shutdown data corruption.";
                                        Logger.Error(errorMsg);
                                        throw new InvalidOperationException(errorMsg);
                                    }

                                    // 2. Update Delayed Auto-start
                                    var delayedAutostart = options.StartType == ServiceStartType.AutomaticDelayedStart;
                                    var success = SetDelayedAutoStart(existingServiceHandle, delayedAutostart);

                                    if (success)
                                    {
                                        Logger.Info($"Delayed auto-start {(delayedAutostart ? "enabled" : "disabled")} for existing service '{options.ServiceName}'.");
                                    }
                                    else
                                    {
                                        // UpdateServiceConfig has already committed baseline mutations to the SCM.
                                        // We escalate the diagnostic error to alert operators that the system is now out of sync.
                                        string errorMsg = $"CRITICAL STATE DRIFT: Failed to set delayed auto-start for existing service '{options.ServiceName}'. SCM configuration is now in an inconsistent state and database synchronization was aborted. Please re-run the full installer context to repair.";
                                        Logger.Error(errorMsg);
                                        throw new InvalidOperationException(errorMsg);
                                    }
                                }

                                cancellationToken.ThrowIfCancellationRequested();
                                await _serviceRepository.UpsertAsync(
                                    dto,
                                    preserveExistingRuntimeState: true,
                                    preserveExistingCredentials: false,
                                    cancellationToken);
                                Logger.Info($"Service '{options.ServiceName}' already exists. Updated its configuration.");
                                return OperationResult.Success();
                            }

                            string creationErrorMsg = $"Failed to create service '{options.ServiceName}'. Win32 error: {createServiceError}";
                            Logger.Error(creationErrorMsg);
                            throw new Win32Exception(createServiceError, creationErrorMsg);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        SetServiceDescription(serviceHandle, options.Description);
                        await _serviceRepository.UpsertAsync(
                                             dto,
                                             preserveExistingRuntimeState: false,
                                             preserveExistingCredentials: false,
                                             cancellationToken); // New service: update runtime state in db (PID, ActiveStdoutPath, ActiveStderrPath)

                        Logger.Info($"Service '{options.ServiceName}' installed successfully.");
                        return OperationResult.Success();
                    }
                    catch
                    {
                        needsRollback = true;
                        throw;
                    }
                    finally
                    {
                        // Collapsed rollback handler: cleans up upon explicit Failure returns OR unhandled exceptions
                        if (needsRollback && serviceCreated && serviceHandle != null && !serviceHandle.IsInvalid)
                        {
                            try
                            {
                                if (!_windowsServiceApi.DeleteService(serviceHandle))
                                {
                                    int rollbackErr = _win32ErrorProvider.GetLastWin32Error();
                                    Logger.Error($"Rollback failed: DeleteService returned false for '{options.ServiceName}'. Win32 error: {rollbackErr}. Manual cleanup may be required.");
                                }
                            }
                            catch (Exception delEx)
                            {
                                Logger.Error($"Rollback raised an exception for '{options.ServiceName}'.", delEx);
                            }
                        }
                    }
                }
                finally
                {
                    if (serviceHandle != null)
                    {
                        serviceHandle.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Installation of '{options.ServiceName}' was cancelled by the user.");
                await ExecuteDatabaseRecoveryAsync(legacyDroppedFromDb, legacyBackupDto);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error installing service '{options.ServiceName}'.", ex);
                await ExecuteDatabaseRecoveryAsync(legacyDroppedFromDb, legacyBackupDto);
                return OperationResult.Failure($"Error installing service '{options.ServiceName}': {ex.Message}");
            }
            finally
            {
                scmHandle?.Dispose();
            }
        }

        /// <summary>
        /// Orchestrates an isolated database rollback sequence to restore state tracking for a legacy 
        /// service variant if the subsequent installation pipeline fails or is canceled.
        /// </summary>
        /// <param name="legacyDroppedFromDb">A flag indicating whether the legacy service record was successfully removed during validation hardening.</param>
        /// <param name="legacyBackupDto">The original service data tracking context captured before structural execution began.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous database recovery operations.</returns>
        /// <remarks>
        /// This routine enforces data tracking atomicity across case-renaming scenarios. It executes under 
        /// <see cref="CancellationToken.None"/> to ensure that recovery routines finish processing even 
        /// if the primary installation process was canceled via user interaction.
        /// </remarks>
        private async Task ExecuteDatabaseRecoveryAsync(bool legacyDroppedFromDb, ServiceDto legacyBackupDto)
        {
            if (legacyDroppedFromDb && legacyBackupDto != null)
            {
                try
                {
                    Logger.Warn($"Installation pipeline failed after dropping legacy Unicode layout. Restoring database state tracking for '{legacyBackupDto.Name}'.");

                    // Re-hydrate the original DTO back into the system repository
                    await _serviceRepository.UpsertAsync(
                        legacyBackupDto,
                        preserveExistingRuntimeState: false,
                        preserveExistingCredentials: false,
                        CancellationToken.None); // Use None to ensure recovery runs even during user cancellations
                }
                catch (Exception dbRecoveryEx)
                {
                    Logger.Error($"CRITICAL: DB Recovery failed while restoring state tracking for '{legacyBackupDto.Name}'. Database is now out of sync.", dbRecoveryEx);
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

                    // Trigger the stop command
                    var status = new SERVICE_STATUS();
                    if (!_windowsServiceApi.ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status))
                    {
                        int controlErr = _win32ErrorProvider.GetLastWin32Error();
                        Logger.Warn($"ControlService(STOP) for '{serviceName}' returned false. Win32 error: {controlErr}. Proceeding to wait loop.");
                    }

                    // 2. The Wait Loop: Now fully cancellable
                    using (var sc = _controllerFactory(serviceName))
                    {
                        sc.Refresh();
                        var sw = Stopwatch.StartNew();

                        var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken: cancellationToken);
                        int waitTimeout = ServiceHelper.CalculateStopTimeout(
                            service?.StopTimeout,
                            service?.PreviousStopTimeout,
                            service?.PreStopTimeoutSeconds ?? 0);

                        while (sc.Status != ServiceControllerStatus.Stopped && sw.Elapsed.TotalSeconds < waitTimeout)
                        {
                            // Passing the token to Task.Delay makes the loop immediately responsive to cancellation
                            await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                            sc.Refresh();
                        }

                        sc.Refresh();
                        if (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            var msg = $"Service '{serviceName}' did not reach 'Stopped' within the {waitTimeout}s timeout. Aborting uninstall to avoid SCM 'marked for delete' state.";
                            Logger.Warn(msg);
                            return OperationResult.Failure(msg);
                        }
                    }

                    // ROBUSTNESS: Standardize start type *only after* confirming the service is completely stopped.
                    // This guarantees that if the wait loop times out or throws a cancellation exception above, 
                    // the original SCM startup configuration remains unaltered, eliminating the manual-start downgrade trap.
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
                        // the delete command, but it's important for diagnostic visibility.
                        Logger.Warn($"Failed to standardize start type before uninstall for '{serviceName}': Win32 Error {_win32ErrorProvider.GetLastWin32Error()}");
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
                        string errorMsg = $"Failed to uninstall service '{serviceName}'. Win32 Error {_win32ErrorProvider.GetLastWin32Error()}";
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
            if (_serviceRepository == null)
                throw new InvalidOperationException("Service repository is not initialized. Cannot start service without a repository.");
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("service name cannot be null or whitespace.", nameof(serviceName));

            int timeout = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken);
                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return OperationResult.Success();

                    timeout = ((service.StartTimeout.HasValue && service.StartTimeout.Value > AppConfig.DefaultServiceStartTimeoutSeconds)
                        ? service.StartTimeout.Value : AppConfig.DefaultServiceStartTimeoutSeconds)
                        + AppConfig.ScmTimeoutBufferSeconds;

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
                        await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                        sc.Refresh();
                    }

                    if (logSuccessfulStart)
                    {
                        Logger.Info($"Service '{serviceName}' started successfully.");
                    }

                    return OperationResult.Success();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Start of '{serviceName}' was cancelled by the user.");
                throw;
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
            if (_serviceRepository == null)
                throw new InvalidOperationException("Service repository is not initialized. Cannot stop service without a repository.");
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("service name cannot be null or whitespace.", nameof(serviceName));

            int timeout = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken);

                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return OperationResult.Success();

                    timeout = ServiceHelper.CalculateStopTimeout(service.StopTimeout, service.PreviousStopTimeout, service.PreStopTimeoutSeconds ?? 0);

                    Logger.Info($"Attempting to stop service '{serviceName}' with a timeout of {timeout} seconds.");
                    sc.Stop();

                    // Replace blocking WaitForStatus with an async polling loop to respect cancellation
                    var stopwatch = Stopwatch.StartNew();
                    var timeoutSpan = TimeSpan.FromSeconds(timeout);

                    while (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        if (stopwatch.Elapsed > timeoutSpan)
                        {
                            throw new System.ServiceProcess.TimeoutException();
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                        sc.Refresh();
                    }

                    if (logSuccessfulStop)
                    {
                        Logger.Info($"Service '{serviceName}' stopped successfully.");
                    }

                    return OperationResult.Success();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Stop of '{serviceName}' was cancelled by the user.");
                throw;
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
            var stopResult = await StopServiceAsync(serviceName, logSuccessfulStop: logSuccessfulRestart, cancellationToken);
            if (!stopResult.IsSuccess)
            {
                return OperationResult.Failure($"Failed to restart service '{serviceName}': {stopResult.ErrorMessage}");
            }

            var startResult = await StartServiceAsync(serviceName, logSuccessfulStart: logSuccessfulRestart, cancellationToken);

            if (startResult.IsSuccess)
            {
                if (logSuccessfulRestart)
                    Logger.Info($"Service '{serviceName}' restarted successfully.");
            }
            else
            {
                Logger.Error($"Failed to restart service '{serviceName}': {startResult.ErrorMessage}");
                return OperationResult.Failure($"Failed to restart service '{serviceName}': {startResult.ErrorMessage}");
            }

            return startResult;
        }

        /// <inheritdoc />
        public ServiceControllerStatus? GetServiceStatus(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var sc = _controllerFactory(serviceName))
                {
                    return sc.Status;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Catching InvalidOperationException handles cases where the service does not exist 
                // or was uninstalled mid-flight, safely satisfying the nullable fallback contract.
                Logger.Debug($"Service '{serviceName}' was not found or was removed during status retrieval: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error retrieving status for service '{serviceName}'.", ex);
                return null;
            }
        }

        /// <inheritdoc />
        public bool IsServiceInstalled(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            return _windowsServiceApi.GetServices()
                            .Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public ServiceStartType GetServiceStartupType(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

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
                        default: return ServiceStartType.Unknown;
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
                                    if (!svcHandle.IsInvalid && IsDelayedStart(svcHandle))
                                    {
                                        startupType = ServiceStartType.AutomaticDelayedStart;
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
        public List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default)
        {
            var results = new ConcurrentBag<ServiceInfo>();

            // Materialize the list so we can guarantee deterministic disposal of all items
            var services = _serviceControllerProvider.GetServices().ToList();

            try
            {
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
                        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, AppConfig.MaxParallelScmQueries),
                    },
                    service =>
                    {
                        // Wrapper disposal is owned by the outer finally so cancelled iterations are still cleaned up.

                        // Check before any work so cancellation surfaces as OperationCanceledException, not a partial result set.
                        cancellationToken.ThrowIfCancellationRequested();

                        ServiceInfo info = new ServiceInfo
                        {
                            Name = service.ServiceName,
                            Status = MapStatus(service.Status),
                            StartupType = MapStartupType(service),
                            LogOnAs = string.Empty,
                            Description = string.Empty,
                        };

                        // Per-service timeout enforcement
                        // We use a local CancellationTokenSource to enforce the per-call timeout
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            try
                            {
                                // Set the timeout inside the guarded block
                                cts.CancelAfter(AppConfig.PopulateNativeDetailsTimeoutMs);

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
                    });

                    return results.OrderBy(s => s.Name).ToList();
                }
                finally
                {
                    // Native SCM queries are synchronous; nothing outlives the loop, so the handle can be disposed here.
                    scmHandle?.Dispose();
                }
            }
            finally
            {
                // Guarantee disposal of all service controller wrappers, 
                // including those left unprocessed due to Parallel loop cancellation.
                foreach (var service in services)
                {
                    service?.Dispose();
                }
            }
        }

        /// <inheritdoc/>
        public ServiceDependencyNode GetDependencies(string serviceName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            if (!IsServiceInstalled(serviceName, cancellationToken))
            {
                return null; // legitimate: not installed
            }

            using (var sc = _controllerFactory(serviceName))
            {
                return sc.GetDependencies(cancellationToken); // let exceptions propagate; UI can show "Failed to query: ..."
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Populates additional service details using native Windows APIs.
        /// </summary>
        /// <param name="scmHandle">An active handle to the Service Control Manager.</param>
        /// <param name="info">The service information object to populate.</param>
        /// <param name="cancellationToken">A cancellation token (carries the per-service native-query timeout); checked before each discrete native call.</param>
        private void PopulateNativeDetails(SafeScmHandle scmHandle, ServiceInfo info, CancellationToken cancellationToken)
        {
            // 1. Pre-flight check
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(info.Name))
                throw new ArgumentException("Service name is empty!");

            // 2. Open the service handle
            using (var svcHandle = _windowsServiceApi.OpenService(scmHandle, info.Name, SERVICE_QUERY_CONFIG))
            {
                if (svcHandle.IsInvalid) return;

                // 3. Check token before each discrete native query.
                // If the 2000ms timeout or user cancellation hits during GetServiceUser, 
                // we skip the subsequent calls to keep the loop moving.

                cancellationToken.ThrowIfCancellationRequested();
                info.LogOnAs = GetServiceUser(svcHandle) ?? ServiceAccounts.LocalSystem;  // confirmed null = LocalSystem (Win32 default)

                cancellationToken.ThrowIfCancellationRequested();
                info.Description = GetServiceDescription(svcHandle) ?? string.Empty;

                cancellationToken.ThrowIfCancellationRequested();
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
        /// <exception cref="Win32Exception">Thrown when the Win32 subsystem encounters an infrastructural or security impediment.</exception>
        private string GetServiceUser(SafeServiceHandle svcHandle)
        {
            int bytesNeeded = 0;

            // Invoke Pass 1: Size-Probe using an intentional null destination pointer
            _windowsServiceApi.QueryServiceConfig(svcHandle, IntPtr.Zero, 0, out bytesNeeded);

            // Intercept the native error state immediately before any subsequent C# evaluations occur
            int errorCode = _win32ErrorProvider.GetLastWin32Error();

            if (bytesNeeded <= 0)
            {
                if (errorCode != ERROR_INSUFFICIENT_BUFFER)
                {
                    Logger.Warn($"QueryServiceConfig size probe failed for account configuration. Win32 Error Code: {errorCode}");
                    throw new Win32Exception(errorCode);
                }
                return null;
            }

            IntPtr ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (_windowsServiceApi.QueryServiceConfig(svcHandle, ptr, bytesNeeded, out _))
                {
                    var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(ptr);
                    return Marshal.PtrToStringAuto(config.lpServiceStartName);
                }

                int callErrorCode = _win32ErrorProvider.GetLastWin32Error();
                throw new Win32Exception(callErrorCode);
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
        private static ServiceStatus MapStatus(ServiceControllerStatus nativeStatus)
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
                    default: return ServiceStartType.Unknown;
                }
            }
            catch (Win32Exception ex)
            {
                // Log the specific error and provide a diagnostic trail.
                // We use Debug level to avoid bloating logs with expected protected service errors.
                Logger.Debug($"Access denied or Win32 error reading StartType for '{service.ServiceName}'. Falling back to Unknown.", ex);

                return ServiceStartType.Unknown;
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected failures (e.g. ObjectDisposedException)
                Logger.Error($"Unexpected error mapping startup type for '{service.ServiceName}'.", ex);

                return ServiceStartType.Unknown;
            }
        }

        /// <summary>
        /// Retrieves the optional description associated with the service.
        /// </summary>
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns>The service description string or <c>null</c> if none exists.</returns>
        /// <exception cref="Win32Exception">Thrown when the Win32 subsystem encounters an infrastructural or security impediment.</exception>
        private string GetServiceDescription(SafeServiceHandle svcHandle)
        {
            // Invoke Pass 1: Size-Probe using an intentional null destination pointer
            _windowsServiceApi.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, IntPtr.Zero, 0, out int bytesNeeded);

            // Intercept the native error state immediately before any subsequent C# evaluations occur
            int errorCode = _win32ErrorProvider.GetLastWin32Error();

            if (bytesNeeded <= 0)
            {
                // If it failed because there's truly no data, or if it's an expected condition, return null.
                // Otherwise, treat non-buffer-size errors as structural failures.
                if (errorCode != ERROR_INSUFFICIENT_BUFFER)
                {
                    Logger.Warn($"QueryServiceConfig2 size probe failed for description. Win32 Error Code: {errorCode}");
                    throw new Win32Exception(errorCode);
                }
                return null;
            }

            IntPtr ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (_windowsServiceApi.QueryServiceConfig2(svcHandle, SERVICE_CONFIG_DESCRIPTION, ptr, bytesNeeded, out int pcbBytesNeeded))
                {
                    var descStruct = Marshal.PtrToStructure<SERVICE_DESCRIPTION>(ptr);
                    return Marshal.PtrToStringAuto(descStruct.lpDescription);
                }

                int callErrorCode = _win32ErrorProvider.GetLastWin32Error();
                throw new Win32Exception(callErrorCode);
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
            int structSize = Marshal.SizeOf(typeof(SERVICE_DELAYED_AUTO_START_INFO));

            return _windowsServiceApi.QueryServiceConfig2(
                svcHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref info,
                structSize,
                out int bytesNeeded) && info.fDelayedAutostart;
        }

        /// <summary>
        /// Maps a <see cref="ServiceStartType"/> to its corresponding Windows Service Control Manager (SCM) 
        /// constant value for the <c>CreateService</c> API.
        /// </summary>
        /// <remarks>
        /// The SCM does not have a native "Delayed Start" type; instead, the delayed property is 
        /// managed as a separate configuration change after service creation. This helper 
        /// coerces <see cref="ServiceStartType.AutomaticDelayedStart"/> to <see cref="ServiceStartType.Automatic"/> 
        /// to ensure the service is created with the correct base start mode.
        /// </remarks>
        /// <param name="t">The <see cref="ServiceStartType"/> requested by the configuration.</param>
        /// <returns>The unsigned integer representation compatible with the Windows API.</returns>
        private static uint ToScmStartType(ServiceStartType t) =>
            (uint)(t == ServiceStartType.AutomaticDelayedStart ? ServiceStartType.Automatic : t);

        #endregion
    }
}
